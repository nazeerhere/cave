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
    public sealed class EnemyDefenseController : MonoBehaviour,
        IEnemyInterruptible,
        IEnemySkillEvolutionReceiver
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

        [Header("Skill Evolution")]
        [SerializeField, Range(0f, 0.25f)] private float evolutionOneBlockBonus = 0.05f;
        [SerializeField, Range(0f, 0.25f)] private float evolutionTwoBlockBonus = 0.05f;
        [SerializeField, Range(0f, 0.2f)] private float evolutionOneParryBonus = 0.03f;
        [SerializeField, Range(0f, 0.2f)] private float evolutionTwoParryBonus = 0.03f;
        [SerializeField, Range(0.1f, 1f)] private float evolutionOneRecoveryMultiplier = 0.9f;
        [SerializeField, Range(0.1f, 1f)] private float evolutionTwoRecoveryMultiplier = 0.8f;

        [Header("Hard Probability Caps")]
        [SerializeField, Range(0f, 0.95f)] private float bruteBlockCap = 0.5f;
        [SerializeField, Range(0f, 0.95f)] private float trollBlockCap = 0.65f;
        [SerializeField, Range(0f, 0.95f)] private float trollProjectileParryCap = 0.5f;
        [SerializeField, Range(0f, 0.95f)] private float necromancerProjectileParryCap = 0.3f;
        [SerializeField, Range(0f, 0.95f)] private float wizardProjectileParryCap = 0.35f;
        [SerializeField, Range(0f, 0.95f)] private float customBlockCap = 0.65f;
        [SerializeField, Range(0f, 0.95f)] private float customProjectileParryCap = 0.5f;

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
        private EnemyEvolutionStage evolutionStage;

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
            float baseChance;
            switch (probabilityPreset)
            {
                case EnemyDefensePreset.Brute:
                    baseChance = 0.35f;
                    break;
                case EnemyDefensePreset.Troll:
                    baseChance = 0.55f;
                    break;
                case EnemyDefensePreset.Custom:
                    baseChance = blockAttemptChance;
                    break;
                default:
                    return 0f;
            }

            return Mathf.Min(
                ResolveBlockCap(),
                baseChance + ResolveEvolutionBonus(
                    evolutionOneBlockBonus,
                    evolutionTwoBlockBonus));
        }

        private float ResolveProjectileParryChance()
        {
            float baseChance;
            switch (probabilityPreset)
            {
                case EnemyDefensePreset.Necromancer:
                    baseChance = 0.15f;
                    break;
                case EnemyDefensePreset.Wizard:
                    baseChance = 0.25f;
                    break;
                case EnemyDefensePreset.Troll:
                    baseChance = 0.35f;
                    break;
                case EnemyDefensePreset.Custom:
                    baseChance = projectileParryChance;
                    break;
                default:
                    return 0f;
            }

            return Mathf.Min(
                ResolveProjectileParryCap(),
                baseChance + ResolveEvolutionBonus(
                    evolutionOneParryBonus,
                    evolutionTwoParryBonus));
        }

        private void ResolveAttempt(EnemyDefenseState successfulState, bool succeeded, Color feedbackColor)
        {
            if (succeeded)
            {
                currentState = successfulState;
                activeUntil = Time.time + activeDefenseDuration;
                recoveryUntil = activeUntil + recoveryDuration * ResolveRecoveryMultiplier();
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

        private float ResolveEvolutionBonus(float evolutionOneBonus, float evolutionTwoBonus)
        {
            if (evolutionStage == EnemyEvolutionStage.EvolutionTwo)
            {
                return evolutionOneBonus + evolutionTwoBonus;
            }

            return evolutionStage == EnemyEvolutionStage.EvolutionOne
                ? evolutionOneBonus
                : 0f;
        }

        private float ResolveRecoveryMultiplier()
        {
            return evolutionStage == EnemyEvolutionStage.EvolutionTwo
                ? evolutionTwoRecoveryMultiplier
                : evolutionStage == EnemyEvolutionStage.EvolutionOne
                    ? evolutionOneRecoveryMultiplier
                    : 1f;
        }

        private float ResolveBlockCap()
        {
            switch (probabilityPreset)
            {
                case EnemyDefensePreset.Brute:
                    return bruteBlockCap;
                case EnemyDefensePreset.Troll:
                    return trollBlockCap;
                default:
                    return customBlockCap;
            }
        }

        private float ResolveProjectileParryCap()
        {
            switch (probabilityPreset)
            {
                case EnemyDefensePreset.Necromancer:
                    return necromancerProjectileParryCap;
                case EnemyDefensePreset.Wizard:
                    return wizardProjectileParryCap;
                case EnemyDefensePreset.Troll:
                    return trollProjectileParryCap;
                default:
                    return customProjectileParryCap;
            }
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
        }

        public void SuspendForMajorAbility(float duration)
        {
            currentState = EnemyDefenseState.Cooldown;
            activeUntil = Time.time;
            recoveryUntil = Time.time;
            nextDecisionTime = Mathf.Max(
                nextDecisionTime,
                Time.time + Mathf.Max(0f, duration));
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
