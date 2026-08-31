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

    public interface IEnemyInterruptPolicy
    {
        bool CanBeInterruptedBy(StaggerStrength strength);
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

        [Header("Current Stagger (Read Only)")]
        [SerializeField] private StaggerStrength lastAppliedStrength;
        [SerializeField] private bool wasResisted;
        [SerializeField, Min(0f)] private float currentStaggerRemaining;

        private EnemyController enemyController;
        private FlyingSwarmController flyingController;
        private WizardFlightMotor wizardFlightMotor;
        private EnemyArchetypeProfile archetypeProfile;
        private EnemyStaggerVisuals visuals;
        private float staggeredUntil;
        private float nextAllowedStaggerTime;

        public event Action<StaggerStrength, float> Staggered;
        public bool IsStaggered => Time.time < staggeredUntil;
        public bool CanAct => !IsStaggered;
        public bool IsStaggerEligible => ResolveEligibility();
        public StaggerStrength LastAppliedStrength => lastAppliedStrength;
        public bool WasResisted => wasResisted;
        public float CurrentStaggerRemaining => Mathf.Max(0f, staggeredUntil - Time.time);

        private void Awake()
        {
            enemyController = GetComponent<EnemyController>();
            flyingController = GetComponent<FlyingSwarmController>();
            wizardFlightMotor = GetComponent<WizardFlightMotor>();
            archetypeProfile = GetComponent<EnemyArchetypeProfile>();
            visuals = GetComponent<EnemyStaggerVisuals>();
            if (visuals == null)
            {
                visuals = gameObject.AddComponent<EnemyStaggerVisuals>();
            }

            EnemyWorldStatusIndicators.EnsureOn(gameObject);
        }

        private void Update()
        {
            currentStaggerRemaining = CurrentStaggerRemaining;
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
            return TryStaggerInternal(strength, baseDuration, 0f);
        }

        public bool TryGuardBreakStagger(float baseDuration, float minimumAppliedDuration)
        {
            return TryStaggerInternal(
                StaggerStrength.Heavy,
                baseDuration,
                Mathf.Max(0f, minimumAppliedDuration));
        }

        private bool TryStaggerInternal(
            StaggerStrength strength,
            float baseDuration,
            float minimumAppliedDuration)
        {
            if (!ResolveEligibility()
                || baseDuration <= 0f
                || Time.time < nextAllowedStaggerTime)
            {
                return false;
            }

            float duration = Mathf.Max(
                minimumAppliedDuration,
                baseDuration * Mathf.Clamp(staggerDurationMultiplier, 0.1f, 1f));
            if (duration <= 0.01f)
            {
                return false;
            }

            lastAppliedStrength = strength;
            wasResisted = staggerDurationMultiplier < 0.99f;
            staggeredUntil = Mathf.Max(staggeredUntil, Time.time + duration);
            nextAllowedStaggerTime = staggeredUntil + staggerImmunityDuration;
            enemyController?.SuspendMovement(duration);
            flyingController?.SuspendMovement(duration);
            wizardFlightMotor?.SuspendMovement(duration);
            bool tankResistedMinor = strength == StaggerStrength.Minor
                && IsTankArchetype();
            if (interruptWindups && !tankResistedMinor)
            {
                // Capabilities such as projectile shooters and contact hitboxes can
                // live on authored child objects. Interrupt the whole enemy-owned
                // capability hierarchy, not only components on this root.
                foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != this && behaviour is IEnemyInterruptible interruptible)
                    {
                        if (behaviour is IEnemyInterruptPolicy policy
                            && !policy.CanBeInterruptedBy(strength))
                        {
                            continue;
                        }

                        interruptible.Interrupt();
                    }
                }
            }

            visuals?.Show(strength, duration, wasResisted);
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

        private bool IsTankArchetype()
        {
            if (archetypeProfile == null)
            {
                archetypeProfile = GetComponent<EnemyArchetypeProfile>();
            }

            return archetypeProfile != null
                && archetypeProfile.Includes(EnemyArchetype.Tank);
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
            currentStaggerRemaining = 0f;
            wasResisted = false;
        }
    }
}
