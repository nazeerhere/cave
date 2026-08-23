using System;
using UnityEngine;

namespace Cave.Enemies
{
    public enum StaggerStrength
    {
        Minor,
        Normal,
        Heavy
    }

    public enum StaggerEligibilityOverride
    {
        Automatic,
        Eligible,
        Immune
    }

    public interface IEnemyInterruptible
    {
        void Interrupt();
    }

    [DisallowMultipleComponent]
    public sealed class EnemyStagger : MonoBehaviour
    {
        [Header("Eligibility")]
        [SerializeField] private StaggerEligibilityOverride eligibilityOverride;

        [Header("Strength Durations")]
        [SerializeField, Min(0f)] private float minorDuration = 0.18f;
        [SerializeField, Min(0f)] private float normalDuration = 0.35f;
        [SerializeField, Min(0f)] private float heavyDuration = 0.6f;

        [Header("Resistance")]
        [SerializeField, Range(0.1f, 1f)] private float staggerDurationMultiplier = 1f;
        [SerializeField, Min(0f)] private float staggerImmunityDuration = 0.5f;
        [SerializeField] private bool interruptWindups = true;

        private EnemyController enemyController;
        private FlyingSwarmController flyingController;
        private EnemyArchetypeProfile archetypeProfile;
        private EnemyStaggerVisuals visuals;
        private float staggeredUntil;
        private float nextAllowedStaggerTime;

        public event Action<StaggerStrength, float> Staggered;
        public bool IsStaggered => Time.time < staggeredUntil;
        public bool CanAct => !IsStaggered;
        public bool IsStaggerEligible => ResolveEligibility();

        private void Awake()
        {
            enemyController = GetComponent<EnemyController>();
            flyingController = GetComponent<FlyingSwarmController>();
            archetypeProfile = GetComponent<EnemyArchetypeProfile>();
            visuals = GetComponent<EnemyStaggerVisuals>();
            if (visuals == null)
            {
                visuals = gameObject.AddComponent<EnemyStaggerVisuals>();
            }
        }

        public bool TryStagger(float baseDuration)
        {
            return TryStagger(StaggerStrength.Normal, baseDuration);
        }

        public bool TryStagger(StaggerStrength strength)
        {
            return TryStagger(strength, ResolveBaseDuration(strength));
        }

        public bool TryStagger(StaggerStrength strength, float baseDuration)
        {
            if (!ResolveEligibility()
                || baseDuration <= 0f
                || Time.time < nextAllowedStaggerTime)
            {
                return false;
            }

            float duration = baseDuration * Mathf.Clamp(staggerDurationMultiplier, 0.1f, 1f);
            if (duration <= 0.01f)
            {
                return false;
            }

            staggeredUntil = Mathf.Max(staggeredUntil, Time.time + duration);
            nextAllowedStaggerTime = staggeredUntil + staggerImmunityDuration;
            enemyController?.SuspendMovement(duration);
            flyingController?.SuspendMovement(duration);
            if (interruptWindups)
            {
                foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
                {
                    if (behaviour != this && behaviour is IEnemyInterruptible interruptible)
                    {
                        interruptible.Interrupt();
                    }
                }
            }

            bool resisted = staggerDurationMultiplier < 0.99f;
            visuals?.Show(strength, duration, resisted);
            Staggered?.Invoke(strength, duration);
            return true;
        }

        public void SetStaggerResistance(float resistance)
        {
            staggerDurationMultiplier = Mathf.Clamp(1f - resistance, 0.4f, 1f);
        }

        public void SetDurationMultiplier(float multiplier)
        {
            staggerDurationMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        }

        private bool ResolveEligibility()
        {
            if (eligibilityOverride == StaggerEligibilityOverride.Eligible)
            {
                return true;
            }

            if (eligibilityOverride == StaggerEligibilityOverride.Immune)
            {
                return false;
            }

            if (archetypeProfile == null)
            {
                archetypeProfile = GetComponent<EnemyArchetypeProfile>();
            }

            if (archetypeProfile == null)
            {
                return true;
            }

            // The most restrictive meaningful role wins for multi-role enemies.
            return !archetypeProfile.Includes(EnemyArchetype.Support)
                && !archetypeProfile.Includes(EnemyArchetype.Ranged);
        }

        private float ResolveBaseDuration(StaggerStrength strength)
        {
            switch (strength)
            {
                case StaggerStrength.Heavy:
                    return heavyDuration;
                case StaggerStrength.Normal:
                    return normalDuration;
                default:
                    return minorDuration;
            }
        }

        private void OnDisable()
        {
            staggeredUntil = 0f;
            nextAllowedStaggerTime = 0f;
        }
    }
}
