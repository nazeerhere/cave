using System;
using Cave.Combat;
using Cave.Axioms.Elemental;
using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Axioms.Phase
{
    /// <summary>
    /// Actor-local latent Phase, player-owned Imaginary mode, and temporary exposure.
    /// Expiration uses timestamps queried by combat; it has no Update loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhaseCombatState : MonoBehaviour
    {
        [Header("Imaginary Mode")]
        [SerializeField, Min(0.01f)] private float imaginaryBaseDuration = 4.2f;
        [SerializeField, Min(0f)] private float imaginaryDurationBonusAtFullPhaseMastery = 0.8f;

        [Header("Collapse Exposure")]
        [SerializeField, Min(1f)] private float strongDamageMultiplier = 1.35f;
        [SerializeField, Min(1f)] private float weakDamageMultiplier = 1.18f;
        [SerializeField, Min(0.01f)] private float strongBaseDuration = 3.5f;
        [SerializeField, Min(0.01f)] private float weakBaseDuration = 5.6f;
        [SerializeField, Min(0f)] private float durationPerOddPair = 0.84f;

        [Header("Read Only")]
        [SerializeField, Min(0)] private int latentStacks;
        [SerializeField] private float imaginaryExpiresAt;
        [SerializeField] private bool resistanceBreakAvailable;
        [SerializeField] private int imaginaryActivationAttackSequence;
        [SerializeField] private PhaseEffectClass activeEffectClass;
        [SerializeField] private float activeDamageMultiplier = 1f;
        [SerializeField] private float activeExposureExpiresAt;

        private AxiomRuntimeState axiomRuntime;
        private AxiomMasteryState mastery;
        private ImaginaryCollisionPhase collisionPhase;

        /// <summary>Actual applied Phase stack; openings intentionally do not raise this event.</summary>
        public event Action<GameObject, int> LatentStackApplied;
        /// <summary>Resolved Phase collapse, including restrained even recoherence.</summary>
        public event Action<GameObject, int, PhaseExposureDefinition> Collapsed;
        /// <summary>Player-facing Imaginary state became active.</summary>
        public event Action<float> OpeningArmed;
        /// <summary>Player-facing Imaginary state ended by consumption or expiry.</summary>
        public event Action OpeningEnded;

        public int LatentStacks => latentStacks;
        public bool HasImaginaryActive => ResolveImaginaryActive(Time.time);
        public float ImaginaryExpiresAt => imaginaryExpiresAt;
        public float CurrentImaginaryDuration => ResolveImaginaryDuration();
        public bool ResistanceBreakAvailable => HasImaginaryActive && resistanceBreakAvailable;
        // Compatibility aliases for presentation consumers while their naming migrates.
        public bool HasOpening => HasImaginaryActive;
        public GameObject OpeningTarget => null;
        public float RemainingOpeningSeconds => HasImaginaryActive
            ? Mathf.Max(0f, imaginaryExpiresAt - Time.time)
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

            if (source == PhaseOpeningSource.ResonanceBreak)
            {
                EnsureOn(attacker).ActivateImaginary(Time.time);
            }
        }

        public static bool TryConsumeOpeningOnSuccessfulHit(
            GameObject attacker,
            GameObject target,
            int appliedDamage,
            DamageContext context,
            float timestamp)
        {
            // Imaginary is player-timed; ordinary hits no longer consume it.
            return false;
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
            if (source == PhaseOpeningSource.ResonanceBreak) ActivateImaginary(timestamp);
        }

        public bool TryConsumeOpening(GameObject target, GameObject source, float timestamp)
        {
            return false;
        }

        public void ActivateImaginary(float timestamp, int activatingAttackSequence = 0)
        {
            float duration = ResolveImaginaryDuration();
            imaginaryExpiresAt = timestamp + duration;
            resistanceBreakAvailable = true;
            imaginaryActivationAttackSequence = activatingAttackSequence;
            EnsureCollisionPhase().SetImaginaryActive(true);
            OpeningArmed?.Invoke(duration);
        }

        /// <summary>
        /// Reserves the one-per-activation heavy token without binding it to an
        /// invented resistance system. The activating heavy cannot consume it.
        /// </summary>
        public bool TryConsumeResistanceBreakForHeavy(int attackSequence, float timestamp)
        {
            if (!ResolveImaginaryActive(timestamp)
                || !resistanceBreakAvailable
                || (imaginaryActivationAttackSequence != 0 && attackSequence == imaginaryActivationAttackSequence))
            {
                return false;
            }

            resistanceBreakAvailable = false;
            return true;
        }

        public void AddLatentStack(GameObject source, float timestamp)
        {
            AxiomRuntimeState runtime = ResolveAxiomRuntime();
            AxiomPatternContextState context = AxiomPatternContextState.EnsureOn(gameObject);
            context.Establish(AxiomKind.Phase, "latent-phase");
            AxiomTrajectoryState trajectory;
            int currentStrength = runtime.TryGetTrajectory(AxiomKind.Phase, timestamp, out trajectory)
                ? Mathf.Max(0, Mathf.RoundToInt(trajectory.CurrentValue))
                : 0;
            int nextStrength = currentStrength + 1;
            if (!runtime.TrySetPatternStrength(AxiomKind.Phase, nextStrength, timestamp))
            {
                return;
            }
            latentStacks = nextStrength;
            AxiomPhenomenonApplicationBridge.RefreshAfterRuntimeMutation(
                runtime,
                AxiomDynamicsState.EnsureOn(gameObject),
                Cave.Axioms.Control.AxiomControlState.EnsureOn(gameObject),
                AxiomKind.Phase,
                1f,
                source,
                gameObject,
                timestamp);
            LatentStackApplied?.Invoke(source, latentStacks);
        }

        public bool TryCollapse(GameObject source, float timestamp)
        {
            if (latentStacks <= 0)
            {
                return false;
            }

            AxiomRuntimeState runtime = ResolveAxiomRuntime();
            AxiomTrajectoryState trajectory;
            int stackCount = runtime.TryGetTrajectory(AxiomKind.Phase, timestamp, out trajectory)
                ? Mathf.Max(0, Mathf.RoundToInt(trajectory.CurrentValue))
                : latentStacks;
            if (stackCount <= 0) return false;
            if (!runtime.TrySetPatternStrength(AxiomKind.Phase, 0f, timestamp)) return false;
            latentStacks = 0;
            AxiomPatternContextState.EnsureOn(gameObject).Clear(AxiomKind.Phase);
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

        private float ResolveImaginaryDuration()
        {
            if (mastery == null) mastery = GetComponent<AxiomMasteryState>();
            float masteryValue = mastery != null ? mastery.Get(MasteryDomain.Phase) : 0f;
            return Mathf.Max(4.2f, imaginaryBaseDuration)
                + Mathf.Clamp01(masteryValue) * Mathf.Max(0f, imaginaryDurationBonusAtFullPhaseMastery);
        }

        private bool ResolveImaginaryActive(float timestamp)
        {
            if (imaginaryExpiresAt <= 0f) return false;
            if (timestamp < imaginaryExpiresAt) return true;
            imaginaryExpiresAt = 0f;
            resistanceBreakAvailable = false;
            imaginaryActivationAttackSequence = 0;
            collisionPhase?.SetImaginaryActive(false);
            OpeningEnded?.Invoke();
            return false;
        }

        private ImaginaryCollisionPhase EnsureCollisionPhase()
        {
            if (collisionPhase == null)
            {
                collisionPhase = GetComponent<ImaginaryCollisionPhase>();
                if (collisionPhase == null) collisionPhase = gameObject.AddComponent<ImaginaryCollisionPhase>();
            }
            return collisionPhase;
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

        private void OnDisable()
        {
            imaginaryExpiresAt = 0f;
            resistanceBreakAvailable = false;
            imaginaryActivationAttackSequence = 0;
            collisionPhase?.SetImaginaryActive(false);
        }
    }
}
