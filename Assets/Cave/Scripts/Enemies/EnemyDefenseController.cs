using System;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    public enum EnemyDefenseState
    {
        Ready,
        Blocking,
        Parrying,
        Recovering,
        Cooldown
    }

    public enum EnemyDefensePreset
    {
        Custom,
        Skeleton,
        Necromancer,
        Wizard,
        Brute,
        Detective,
        Troll
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyDefenseController : MonoBehaviour, IEnemyInterruptible
    {
        [Header("Defense Probabilities")]
        [SerializeField] private EnemyDefensePreset probabilityPreset = EnemyDefensePreset.Custom;
        [SerializeField, Range(0f, 1f)] private float blockAttemptChance;
        [SerializeField, Range(0f, 1f)] private float projectileParryChance;

        [Header("Defense Timing")]
        [SerializeField, Min(0f)] private float defenseCooldown = 0.9f;
        [SerializeField, Min(0.01f)] private float activeDefenseDuration = 0.18f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.3f;

        [Header("Block Eligibility")]
        [SerializeField] private bool startsFacingRight = true;
        [SerializeField] private bool blockProjectiles;
        [SerializeField, Range(-1f, 1f)] private float minimumFrontDot = 0.05f;

        [Header("Feedback Hooks")]
        [SerializeField] private Color blockColor = new Color(0.35f, 0.72f, 1f, 0.9f);
        [SerializeField] private Color projectileParryColor = new Color(0.85f, 0.4f, 1f, 0.9f);

        [Header("Current State (Read Only)")]
        [SerializeField] private EnemyDefenseState currentState = EnemyDefenseState.Ready;
        [SerializeField] private float facingDirection = 1f;

        private EnemyStagger stagger;
        private float activeUntil;
        private float recoveryUntil;
        private float nextDecisionTime;

        public event Action<EnemyDefenseState, bool> DefenseResolved;
        public EnemyDefenseState CurrentState => currentState;
        public float EffectiveBlockChance => ResolveBlockChance();
        public float EffectiveProjectileParryChance => ResolveProjectileParryChance();
        public bool CanStartAttack => Time.time >= activeUntil
            && Time.time >= recoveryUntil
            && (stagger == null || stagger.CanAct);

        private void Awake()
        {
            facingDirection = startsFacingRight ? 1f : -1f;
            stagger = GetComponent<EnemyStagger>();
        }

        private void Update()
        {
            RefreshState();
        }

        public void FaceDirection(float horizontalDirection)
        {
            if (!Mathf.Approximately(horizontalDirection, 0f))
            {
                facingDirection = Mathf.Sign(horizontalDirection);
            }
        }

        public bool TryBlockDamage(DamageContext context)
        {
            float chance = ResolveBlockChance();
            if (chance <= 0f
                || context.Source == null
                || context.Source == gameObject
                || context.HasTrait(DamageTrait.AreaOfEffect)
                || context.HasTrait(DamageTrait.Piercing)
                || context.HasTrait(DamageTrait.GuardBreak)
                || (context.HasTrait(DamageTrait.Projectile) && !blockProjectiles)
                || !IsThreatInFront(context.Source.transform.position))
            {
                return false;
            }

            if (currentState == EnemyDefenseState.Blocking && Time.time < activeUntil)
            {
                ShowDefenseFeedback(blockColor);
                return true;
            }

            if (!CanAttemptDefense())
            {
                return false;
            }

            bool succeeded = UnityEngine.Random.value < chance;
            ResolveAttempt(EnemyDefenseState.Blocking, succeeded, blockColor);
            return succeeded;
        }

        public bool TryParryProjectile(IEnemyParryableProjectile projectile)
        {
            float chance = ResolveProjectileParryChance();
            if (chance <= 0f
                || projectile == null
                || !projectile.CanBeEnemyParried)
            {
                return false;
            }

            if (currentState == EnemyDefenseState.Parrying && Time.time < activeUntil)
            {
                bool activeDeflection = projectile.TryEnemyParry(gameObject);
                if (activeDeflection)
                {
                    ShowDefenseFeedback(projectileParryColor);
                }

                return activeDeflection;
            }

            if (!CanAttemptDefense())
            {
                return false;
            }

            bool attemptedSuccessfully = UnityEngine.Random.value < chance;
            bool deflected = attemptedSuccessfully && projectile.TryEnemyParry(gameObject);
            ResolveAttempt(EnemyDefenseState.Parrying, deflected, projectileParryColor);
            return deflected;
        }

        private bool CanAttemptDefense()
        {
            RefreshState();
            return Time.time >= nextDecisionTime
                && currentState == EnemyDefenseState.Ready
                && (stagger == null || stagger.CanAct);
        }

        private float ResolveBlockChance()
        {
            switch (probabilityPreset)
            {
                case EnemyDefensePreset.Brute:
                    return 0.35f;
                case EnemyDefensePreset.Troll:
                    return 0.55f;
                case EnemyDefensePreset.Custom:
                    return blockAttemptChance;
                default:
                    return 0f;
            }
        }

        private float ResolveProjectileParryChance()
        {
            switch (probabilityPreset)
            {
                case EnemyDefensePreset.Necromancer:
                    return 0.15f;
                case EnemyDefensePreset.Wizard:
                    return 0.25f;
                case EnemyDefensePreset.Troll:
                    return 0.35f;
                case EnemyDefensePreset.Custom:
                    return projectileParryChance;
                default:
                    return 0f;
            }
        }

        private void ResolveAttempt(EnemyDefenseState successfulState, bool succeeded, Color feedbackColor)
        {
            if (succeeded)
            {
                currentState = successfulState;
                activeUntil = Time.time + activeDefenseDuration;
                recoveryUntil = activeUntil + recoveryDuration;
                nextDecisionTime = recoveryUntil + defenseCooldown;
                ShowDefenseFeedback(feedbackColor);
            }
            else
            {
                currentState = EnemyDefenseState.Cooldown;
                activeUntil = Time.time;
                recoveryUntil = Time.time;
                nextDecisionTime = Time.time + defenseCooldown;
            }

            DefenseResolved?.Invoke(successfulState, succeeded);
        }

        private void RefreshState()
        {
            if ((currentState == EnemyDefenseState.Blocking
                    || currentState == EnemyDefenseState.Parrying)
                && Time.time >= activeUntil)
            {
                currentState = Time.time < recoveryUntil
                    ? EnemyDefenseState.Recovering
                    : EnemyDefenseState.Cooldown;
            }

            if (currentState == EnemyDefenseState.Recovering && Time.time >= recoveryUntil)
            {
                currentState = EnemyDefenseState.Cooldown;
            }

            if (currentState == EnemyDefenseState.Cooldown && Time.time >= nextDecisionTime)
            {
                currentState = EnemyDefenseState.Ready;
            }
        }

        private bool IsThreatInFront(Vector3 sourcePosition)
        {
            float horizontalDelta = sourcePosition.x - transform.position.x;
            if (Mathf.Abs(horizontalDelta) <= 0.01f)
            {
                return true;
            }

            return Mathf.Sign(horizontalDelta) * facingDirection >= minimumFrontDot;
        }

        private void ShowDefenseFeedback(Color color)
        {
            Cave.Combat.AreaPulseEffect.Create(transform.position, 0.72f, color, 0.16f);
        }

        public void Interrupt()
        {
            currentState = EnemyDefenseState.Cooldown;
            activeUntil = Time.time;
            recoveryUntil = Time.time;
            nextDecisionTime = Mathf.Max(nextDecisionTime, Time.time + defenseCooldown);
        }

        private void OnDisable()
        {
            currentState = EnemyDefenseState.Ready;
            activeUntil = 0f;
            recoveryUntil = 0f;
            nextDecisionTime = 0f;
        }
    }
}
