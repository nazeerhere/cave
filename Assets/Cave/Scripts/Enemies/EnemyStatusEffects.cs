using System.Collections;
using Cave.Axioms.Elemental;
using Cave.Combat;
using Cave.Progression;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Enemies
{
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyStatusEffects : MonoBehaviour
    {
        private Damageable damageable;
        private EnemyController enemyController;
        private FlyingSwarmController flyingController;
        private WizardFlightMotor wizardFlightMotor;
        private Coroutine slowRoutine;
        private Coroutine burnRoutine;
        private Coroutine immobilizeRoutine;
        private float slowEndsAt;
        private float immobilizedUntil;
        private float strongestSlowMultiplier = 1f;
        private EnemyStatusVisuals statusVisuals;
        private SpecialModeTier2Settings settings;
        private BurnDefinition activeBurn;
        private bool applyingBurnDamage;

        public bool IsBurning => burnRoutine != null;
        public bool IsSlowed => Time.time < slowEndsAt;
        public bool IsImmobilized => Time.time < immobilizedUntil;

        private struct BurnDefinition
        {
            public int DamagePerTick;
            public float TickInterval;
            public float Duration;
            public DamageContext DamageContext;
            public float DeathSpreadRadius;
            public int MaximumSpreadTargets;
            public LayerMask DamageableLayers;

            public bool CanSpread => DeathSpreadRadius > 0f && MaximumSpreadTargets > 0;
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            enemyController = GetComponent<EnemyController>();
            flyingController = GetComponent<FlyingSwarmController>();
            wizardFlightMotor = GetComponent<WizardFlightMotor>();
            statusVisuals = GetComponent<EnemyStatusVisuals>();
            if (statusVisuals == null)
            {
                statusVisuals = gameObject.AddComponent<EnemyStatusVisuals>();
            }

            EnemyWorldStatusIndicators.EnsureOn(gameObject);

            damageable.Died += HandleDied;
        }

        internal void Configure(SpecialModeTier2Settings specialModeSettings)
        {
            settings = specialModeSettings;
            if (statusVisuals == null)
            {
                statusVisuals = GetComponent<EnemyStatusVisuals>();
            }

            statusVisuals?.Configure(settings);
        }

        public void ApplySlow(float movementMultiplier, float duration)
        {
            if ((enemyController == null && flyingController == null && wizardFlightMotor == null)
                || duration <= 0f)
            {
                return;
            }

            strongestSlowMultiplier = Mathf.Min(
                strongestSlowMultiplier,
                Mathf.Clamp(movementMultiplier, 0.05f, 1f));
            slowEndsAt = Mathf.Max(slowEndsAt, Time.time + duration);
            SetMovementMultiplier(strongestSlowMultiplier);
            RefreshFrostVisual();
            if (slowRoutine == null)
            {
                slowRoutine = StartCoroutine(SlowUntilExpired());
            }
        }

        public bool ApplyImmobilize(float duration)
        {
            if ((enemyController == null && flyingController == null && wizardFlightMotor == null)
                || duration <= 0f
                || GetComponent<BossPhaseController>() != null)
            {
                return false;
            }

            immobilizedUntil = Mathf.Max(immobilizedUntil, Time.time + duration);
            SetMovementMultiplier(0f);
            RefreshFrostVisual();
            if (immobilizeRoutine == null)
            {
                immobilizeRoutine = StartCoroutine(ImmobilizeUntilExpired());
            }

            return true;
        }

        public void ApplyBurn(int damagePerTick, float tickInterval, float duration)
        {
            ApplyBurn(damagePerTick, tickInterval, duration, default);
        }

        public void ApplyBurn(
            int damagePerTick,
            float tickInterval,
            float duration,
            DamageContext damageContext)
        {
            ApplyBurn(damagePerTick, tickInterval, duration, damageContext, 0f, 0, default);
        }

        public void ApplyBurn(
            int damagePerTick,
            float tickInterval,
            float duration,
            DamageContext damageContext,
            float deathSpreadRadius,
            int maximumSpreadTargets,
            LayerMask damageableLayers)
        {
            if (damagePerTick <= 0 || tickInterval <= 0f || duration <= 0f)
            {
                return;
            }

            activeBurn = new BurnDefinition
            {
                DamagePerTick = damagePerTick,
                TickInterval = tickInterval,
                Duration = duration,
                DamageContext = damageContext,
                DeathSpreadRadius = deathSpreadRadius,
                MaximumSpreadTargets = maximumSpreadTargets,
                DamageableLayers = damageableLayers
            };

            if (burnRoutine != null)
            {
                StopCoroutine(burnRoutine);
            }

            statusVisuals?.SetBurnActive(true);
            burnRoutine = StartCoroutine(BurnForDuration(activeBurn));
        }

        /// <summary>
        /// Applies the normal Burn implementation with the current Heat response
        /// only when a caller has already confirmed a successful direct Fire hit.
        /// Generic Burn, DOT, and spread paths remain mechanically unchanged.
        /// </summary>
        public void ApplyBurnWithHeatPersistence(
            int damagePerTick,
            float tickInterval,
            float duration,
            DamageContext damageContext,
            float deathSpreadRadius = 0f,
            int maximumSpreadTargets = 0,
            LayerMask damageableLayers = default)
        {
            AxiomDynamicsState dynamics = GetComponent<AxiomDynamicsState>();
            float persistence = dynamics != null
                ? dynamics.GetHeatBurnPersistenceMultiplier(Time.time)
                : 1f;
            ApplyBurn(
                damagePerTick,
                tickInterval,
                duration * persistence,
                damageContext,
                deathSpreadRadius,
                maximumSpreadTargets,
                damageableLayers);
        }

        private IEnumerator SlowUntilExpired()
        {
            while (Time.time < slowEndsAt)
            {
                yield return null;
            }

            strongestSlowMultiplier = 1f;
            slowEndsAt = 0f;
            slowRoutine = null;
            ApplyCurrentMovementMultiplier();
            RefreshFrostVisual();
        }

        private IEnumerator ImmobilizeUntilExpired()
        {
            while (Time.time < immobilizedUntil)
            {
                yield return null;
            }

            immobilizedUntil = 0f;
            immobilizeRoutine = null;
            ApplyCurrentMovementMultiplier();
            RefreshFrostVisual();
        }

        private void SetMovementMultiplier(float multiplier)
        {
            enemyController?.SetMovementSpeedMultiplier(multiplier);
            flyingController?.SetMovementSpeedMultiplier(multiplier);
            wizardFlightMotor?.SetMovementSpeedMultiplier(multiplier);
        }

        public bool HasRemovableDebuff => Time.time < slowEndsAt || burnRoutine != null;

        public bool DispelRemovableDebuffs()
        {
            bool removed = false;
            if (slowRoutine != null || Time.time < slowEndsAt)
            {
                if (slowRoutine != null)
                {
                    StopCoroutine(slowRoutine);
                }

                slowRoutine = null;
                strongestSlowMultiplier = 1f;
                slowEndsAt = 0f;
                removed = true;
            }

            if (burnRoutine != null)
            {
                StopCoroutine(burnRoutine);
                burnRoutine = null;
                activeBurn = default;
                applyingBurnDamage = false;
                statusVisuals?.SetBurnActive(false);
                removed = true;
            }

            ApplyCurrentMovementMultiplier();
            RefreshFrostVisual();
            return removed;
        }

        private void ApplyCurrentMovementMultiplier()
        {
            SetMovementMultiplier(Time.time < immobilizedUntil ? 0f : strongestSlowMultiplier);
        }

        private void RefreshFrostVisual()
        {
            bool immobilized = Time.time < immobilizedUntil;
            bool slowed = Time.time < slowEndsAt;
            statusVisuals?.SetFrostActive(immobilized || slowed, immobilized);
        }

        private IEnumerator BurnForDuration(BurnDefinition burn)
        {
            float endsAt = Time.time + burn.Duration;
            WaitForSeconds tickDelay = new WaitForSeconds(burn.TickInterval);

            while (Time.time + burn.TickInterval <= endsAt + Mathf.Epsilon)
            {
                yield return tickDelay;
                applyingBurnDamage = true;
                damageable.TakeDamage(burn.DamagePerTick, burn.DamageContext);
                applyingBurnDamage = false;

                if (!gameObject.activeInHierarchy)
                {
                    yield break;
                }
            }

            burnRoutine = null;
            activeBurn = default;
            statusVisuals?.SetBurnActive(false);
        }

        private void HandleDied()
        {
            if (!applyingBurnDamage || !activeBurn.CanSpread)
            {
                return;
            }

            AreaPulseEffect.Create(
                transform.position,
                activeBurn.DeathSpreadRadius,
                settings != null ? settings.BurnStatusHighlightColor : new Color(1f, 0.45f, 0.05f, 0.8f),
                0.28f);
            BurnSpreadEffect.Spread(
                damageable,
                activeBurn.DeathSpreadRadius,
                activeBurn.MaximumSpreadTargets,
                activeBurn.DamagePerTick,
                activeBurn.TickInterval,
                activeBurn.Duration,
                activeBurn.DamageContext,
                activeBurn.DamageableLayers);
        }

        private void OnDisable()
        {
            if (enemyController != null)
            {
                enemyController.SetMovementSpeedMultiplier(1f);
            }

            flyingController?.SetMovementSpeedMultiplier(1f);
            wizardFlightMotor?.SetMovementSpeedMultiplier(1f);

            strongestSlowMultiplier = 1f;
            slowEndsAt = 0f;
            immobilizedUntil = 0f;
            slowRoutine = null;
            immobilizeRoutine = null;
            burnRoutine = null;
            activeBurn = default;
            applyingBurnDamage = false;
            statusVisuals?.SetBurnActive(false);
            statusVisuals?.SetFrostActive(false, false);
        }

        private void OnDestroy()
        {
            if (damageable != null)
            {
                damageable.Died -= HandleDied;
            }
        }
    }
}
