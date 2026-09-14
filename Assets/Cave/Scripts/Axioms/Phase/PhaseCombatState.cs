using System;
using Cave.Combat;
using UnityEngine;

namespace Cave.Axioms.Phase
{
    /// <summary>
    /// Actor-local latent Phase, target-bound opening, and temporary exposure.
    /// Expiration uses timestamps queried by combat; it has no Update loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhaseCombatState : MonoBehaviour
    {
        [Header("Opening")]
        [SerializeField, Min(0.01f)] private float openingDuration = 3f;

        [Header("Collapse Exposure")]
        [SerializeField, Min(1f)] private float strongDamageMultiplier = 1.35f;
        [SerializeField, Min(1f)] private float weakDamageMultiplier = 1.18f;
        [SerializeField, Min(0.01f)] private float strongBaseDuration = 2.5f;
        [SerializeField, Min(0.01f)] private float weakBaseDuration = 4f;
        [SerializeField, Min(0f)] private float durationPerOddPair = 0.6f;

        [Header("Read Only")]
        [SerializeField, Min(0)] private int latentStacks;
        [SerializeField] private GameObject openingTarget;
        [SerializeField] private float openingExpiresAt;
        [SerializeField] private PhaseEffectClass activeEffectClass;
        [SerializeField] private float activeDamageMultiplier = 1f;
        [SerializeField] private float activeExposureExpiresAt;

        private AxiomRuntimeState axiomRuntime;

        /// <summary>Actual applied Phase stack; openings intentionally do not raise this event.</summary>
        public event Action<GameObject, int> LatentStackApplied;
        /// <summary>Resolved Phase collapse, including restrained even recoherence.</summary>
        public event Action<GameObject, int, PhaseExposureDefinition> Collapsed;
        /// <summary>Player-facing Imaginary state became active; no gameplay rule is changed.</summary>
        public event Action<float> OpeningArmed;
        /// <summary>Player-facing Imaginary state ended by consumption or expiry.</summary>
        public event Action OpeningEnded;

        public int LatentStacks => latentStacks;
        public bool HasOpening => ResolveOpeningTarget(Time.time) != null;
        public GameObject OpeningTarget => ResolveOpeningTarget(Time.time);
        public float RemainingOpeningSeconds => HasOpening
            ? Mathf.Max(0f, openingExpiresAt - Time.time)
            : 0f;
        public bool HasActiveExposure => ResolveActiveExposure(Time.time);
        public PhaseEffectClass ActiveEffectClass => HasActiveExposure ? activeEffectClass : PhaseEffectClass.None;
        public float ActiveDamageMultiplier => HasActiveExposure ? activeDamageMultiplier : 1f;
        public float RemainingExposureSeconds => HasActiveExposure
            ? Mathf.Max(0f, activeExposureExpiresAt - Time.time)
            : 0f;

        public static PhaseCombatState EnsureOn(GameObject owner)
        {
            if (owner == null)
            {
                return null;
            }

            PhaseCombatState state = owner.GetComponent<PhaseCombatState>();
            if (state != null)
            {
                return state;
            }

            state = owner.AddComponent<PhaseCombatState>();
            owner.GetComponent<Cave.Axioms.Vfx.AxiomVfxPresenter>()?.RefreshBindings();
            return state;
        }

        public static void GrantOpening(GameObject attacker, GameObject target, PhaseOpeningSource source)
        {
            if (attacker == null || target == null || attacker == target)
            {
                return;
            }

            EnsureOn(attacker).ArmOpening(target, source, Time.time);
        }

        public static bool TryConsumeOpeningOnSuccessfulHit(
            GameObject attacker,
            GameObject target,
            int appliedDamage,
            DamageContext context,
            float timestamp)
        {
            if (attacker == null
                || target == null
                || appliedDamage <= 0
                || !context.HasTrait(DamageTrait.Melee)
                || context.HasTrait(DamageTrait.AreaOfEffect))
            {
                return false;
            }

            PhaseCombatState attackerState = attacker.GetComponentInParent<PhaseCombatState>();
            return attackerState != null && attackerState.TryConsumeOpening(target, attacker, timestamp);
        }

        public static bool TryCollapseFromChargedHit(
            GameObject target,
            int chargeTier,
            GameObject source,
            float timestamp)
        {
            if (target == null || chargeTier < 2)
            {
                return false;
            }

            PhaseCombatState targetState = target.GetComponent<PhaseCombatState>();
            return targetState != null && targetState.TryCollapse(source, timestamp);
        }

        public void ArmOpening(GameObject target, PhaseOpeningSource source, float timestamp)
        {
            if (target == null || target == gameObject)
            {
                return;
            }

            openingTarget = target;
            openingExpiresAt = timestamp + Mathf.Max(0.01f, openingDuration);
            OpeningArmed?.Invoke(Mathf.Max(0.01f, openingDuration));
        }

        public bool TryConsumeOpening(GameObject target, GameObject source, float timestamp)
        {
            if (ResolveOpeningTarget(timestamp) != target)
            {
                return false;
            }

            openingTarget = null;
            openingExpiresAt = 0f;
            OpeningEnded?.Invoke();
            PhaseCombatState receiver = EnsureOn(target);
            receiver.AddLatentStack(source, timestamp);
            return true;
        }

        public void AddLatentStack(GameObject source, float timestamp)
        {
            latentStacks++;
            AxiomRuntimeState runtime = ResolveAxiomRuntime();
            runtime.ApplyDelta(AxiomKind.Phase, 1f, source, gameObject, timestamp);
            LatentStackApplied?.Invoke(source, latentStacks);
        }

        public bool TryCollapse(GameObject source, float timestamp)
        {
            if (latentStacks <= 0)
            {
                return false;
            }

            int stackCount = latentStacks;
            latentStacks = 0;
            ResolveAxiomRuntime().ApplyDelta(AxiomKind.Phase, -stackCount, source, gameObject, timestamp);
            PhaseExposureDefinition exposure = PhaseCombatRules.ResolveExposure(
                stackCount,
                strongDamageMultiplier,
                weakDamageMultiplier,
                strongBaseDuration,
                weakBaseDuration,
                durationPerOddPair);
            if (exposure.IsActive)
            {
                ApplyExposure(exposure, timestamp);
                ResolveAxiomRuntime().RaiseFeedback(new AxiomFeedbackEvent(
                    AxiomKind.Phase,
                    AxiomFeedbackType.PhaseDebuffActive,
                    exposure.DamageMultiplier - 1f,
                    source,
                    gameObject,
                    timestamp));
            }

            Collapsed?.Invoke(source, stackCount, exposure);

            return true;
        }

        public int ResolveIncomingDamage(int amount)
        {
            if (amount <= 0 || !HasActiveExposure)
            {
                return amount;
            }

            return Mathf.Max(1, Mathf.CeilToInt(amount * activeDamageMultiplier));
        }

        private AxiomRuntimeState ResolveAxiomRuntime()
        {
            if (axiomRuntime == null)
            {
                axiomRuntime = GetComponent<AxiomRuntimeState>();
                if (axiomRuntime == null)
                {
                    axiomRuntime = gameObject.AddComponent<AxiomRuntimeState>();
                }
            }

            return axiomRuntime;
        }

        private void ApplyExposure(PhaseExposureDefinition incoming, float timestamp)
        {
            bool existing = ResolveActiveExposure(timestamp);
            if (!existing || incoming.DamageMultiplier >= activeDamageMultiplier)
            {
                activeEffectClass = incoming.EffectClass;
                activeDamageMultiplier = incoming.DamageMultiplier;
            }

            activeExposureExpiresAt = Mathf.Max(
                activeExposureExpiresAt,
                timestamp + incoming.Duration);
        }

        private GameObject ResolveOpeningTarget(float timestamp)
        {
            if (openingTarget == null)
            {
                return null;
            }

            if (timestamp >= openingExpiresAt)
            {
                openingTarget = null;
                openingExpiresAt = 0f;
                OpeningEnded?.Invoke();
                return null;
            }

            return openingTarget;
        }

        private bool ResolveActiveExposure(float timestamp)
        {
            if (activeEffectClass == PhaseEffectClass.None || timestamp < activeExposureExpiresAt)
            {
                return activeEffectClass != PhaseEffectClass.None;
            }

            activeEffectClass = PhaseEffectClass.None;
            activeDamageMultiplier = 1f;
            activeExposureExpiresAt = 0f;
            return false;
        }
    }
}
