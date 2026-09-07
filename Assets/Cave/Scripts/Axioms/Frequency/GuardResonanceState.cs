using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Axioms.Frequency
{
    /// <summary>
    /// Actor-local sustained-Guard cadence state. Dictionaries are touched only
    /// when a qualifying guard contact occurs; this component has no Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuardResonanceState : MonoBehaviour
    {
        [Header("Natural Guard Period")]
        [SerializeField, Min(0.01f)] private float playerNaturalPeriodSeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float enemyBaseNaturalPeriodSeconds = 0.8f;
        [SerializeField, Range(0f, 0.5f)] private float enemyVariationFraction = 0.15f;

        [Header("Cadence Matching")]
        [SerializeField, Range(0f, 0.5f)] private float toleranceFraction = 0.15f;
        [SerializeField, Min(1f)] private float staleIntervalMultiplier = 2.5f;
        [SerializeField, Min(1)] private int resonanceBreakProgress = 4;
        [SerializeField, Min(0.01f)] private float destabilizedDuration = 2.5f;

        [Header("Read Only")]
        [SerializeField] private float naturalPeriodSeconds;
        [SerializeField] private float destabilizedUntil;

        private readonly Dictionary<GameObject, GuardResonanceTracker> trackers =
            new Dictionary<GameObject, GuardResonanceTracker>();
        private readonly List<GameObject> pruneBuffer = new List<GameObject>();
        private bool destabilized;

        public event Action<GameObject, GuardResonanceContactResult> ContactResolved;
        public event Action<GameObject> ResonanceBroken;
        public event Action DestabilizedStarted;
        public event Action DestabilizedEnded;

        public float NaturalPeriodSeconds => naturalPeriodSeconds;
        public bool IsDestabilized
        {
            get
            {
                RefreshDestabilization(Time.time);
                return destabilized;
            }
        }
        public bool CanSustainGuard => !IsDestabilized;

        public static GuardResonanceState EnsureOn(GameObject owner)
        {
            if (owner == null)
            {
                return null;
            }

            GuardResonanceState state = owner.GetComponent<GuardResonanceState>();
            return state != null ? state : owner.AddComponent<GuardResonanceState>();
        }

        private void Awake()
        {
            ResolveNaturalPeriod();
        }

        public GuardResonanceContactResult RegisterGuardContact(GameObject attacker, float timestamp)
        {
            RefreshDestabilization(timestamp);
            if (attacker == null || destabilized)
            {
                return new GuardResonanceContactResult(
                    GuardResonanceContactOutcome.Ignored,
                    0,
                    0f);
            }

            GuardResonanceTracker tracker;
            if (!trackers.TryGetValue(attacker, out tracker))
            {
                tracker = new GuardResonanceTracker();
                trackers.Add(attacker, tracker);
                PruneDestroyedTrackers();
            }

            GuardResonanceContactResult result = tracker.RegisterContact(timestamp, Settings);
            ContactResolved?.Invoke(attacker, result);
            PlayContactFeedback(result);
            if (result.Broke)
            {
                trackers.Clear();
                destabilized = true;
                destabilizedUntil = timestamp + Mathf.Max(0.01f, destabilizedDuration);
                ResonanceBroken?.Invoke(attacker);
                DestabilizedStarted?.Invoke();
            }

            return result;
        }

        public void EndSustainedGuardForDestabilization()
        {
            if (!IsDestabilized)
            {
                return;
            }

            SidewaysParryAttack playerGuard = GetComponent<SidewaysParryAttack>();
            if (playerGuard != null)
            {
                playerGuard.EndSustainedGuardForDestabilization();
            }

            EnemyDefenseController enemyGuard = GetComponent<EnemyDefenseController>();
            if (enemyGuard != null)
            {
                enemyGuard.EndBlockingForDestabilization();
            }
        }

        public void ClearCadenceSequences()
        {
            trackers.Clear();
        }

        /// <summary>Counterphase cancels only one attacker pressure sequence.</summary>
        public bool ClearCadenceSequence(GameObject attacker)
        {
            return attacker != null && trackers.Remove(attacker);
        }

        private GuardResonanceSettings Settings => new GuardResonanceSettings(
            naturalPeriodSeconds,
            toleranceFraction,
            staleIntervalMultiplier,
            resonanceBreakProgress);

        private void ResolveNaturalPeriod()
        {
            bool enemyGuard = GetComponent<EnemyDefenseController>() != null;
            if (!enemyGuard)
            {
                naturalPeriodSeconds = Mathf.Max(0.01f, playerNaturalPeriodSeconds);
                return;
            }

            uint hash = (uint)GetInstanceID();
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            float normalized = (hash & 0xffffu) / 65535f;
            float variation = (normalized * 2f - 1f) * enemyVariationFraction;
            naturalPeriodSeconds = Mathf.Max(0.01f, enemyBaseNaturalPeriodSeconds * (1f + variation));
        }

        private void RefreshDestabilization(float timestamp)
        {
            if (!destabilized || timestamp < destabilizedUntil)
            {
                return;
            }

            destabilized = false;
            destabilizedUntil = 0f;
            DestabilizedEnded?.Invoke();
        }

        private void PruneDestroyedTrackers()
        {
            if (trackers.Count < 16)
            {
                return;
            }

            pruneBuffer.Clear();
            foreach (KeyValuePair<GameObject, GuardResonanceTracker> pair in trackers)
            {
                if (pair.Key == null)
                {
                    pruneBuffer.Add(pair.Key);
                }
            }

            if (pruneBuffer.Count == 0)
            {
                return;
            }

            for (int index = 0; index < pruneBuffer.Count; index++)
            {
                trackers.Remove(pruneBuffer[index]);
            }
        }

        private void PlayContactFeedback(GuardResonanceContactResult result)
        {
            if (result.Outcome == GuardResonanceContactOutcome.Advanced)
            {
                float size = 0.42f + result.Progress * 0.1f;
                CombatShapeEffect.Create(
                    transform.position,
                    CombatShape.Diamond,
                    size,
                    new Color(0.35f, 0.85f, 1f, 0.8f),
                    0.12f);
            }
            else if (result.Outcome == GuardResonanceContactOutcome.Broken)
            {
                AreaPulseEffect.Create(
                    transform.position,
                    1f,
                    new Color(0.72f, 0.4f, 1f, 0.9f),
                    0.22f);
            }
        }
    }
}
