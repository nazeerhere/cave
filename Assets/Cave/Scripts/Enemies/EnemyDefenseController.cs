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
        [SerializeField, Range(0f, 1f)] private float necromancerProjectileParryChance = 0.8f;

        [Header("Defense Timing")]
        [SerializeField, Min(0f)] private float defenseCooldown = 0.9f;
        [SerializeField, Min(0.01f)] private float activeDefenseDuration = 0.18f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.3f;

        [Header("Block Eligibility")]
        [SerializeField] private bool startsFacingRight = true;
        [SerializeField] private bool blockProjectiles;
        [SerializeField, Range(-1f, 1f)] private float minimumFrontDot = 0.05f;
        // [SerializeField] private float BlockHitVfxScale = 0.12f;
        [SerializeField] private Vector2 blockImpactVfxOffset = new Vector2(0.45f, 0.15f);
        [SerializeField] private float blockImpactVfxScale = 0.7f;
        [SerializeField] private GameObject BlockHitVfxPrefab;

        [Header("Skill Evolution")]
        [SerializeField, Range(0f, 0.25f)] private float evolutionOneBlockBonus = 0.05f;
        [SerializeField, Range(0f, 0.25f)] private float evolutionTwoBlockBonus = 0.05f;
        [SerializeField, Range(0f, 0.2f)] private float evolutionOneParryBonus = 0.03f;
        [SerializeField, Range(0f, 0.2f)] private float evolutionTwoParryBonus = 0.03f;
        [SerializeField, Range(0.1f, 1f)] private float evolutionOneRecoveryMultiplier = 0.9f;
        [SerializeField, Range(0.1f, 1f)] private float evolutionTwoRecoveryMultiplier = 0.8f;

        [Header("Hard Probability Caps")]
        [SerializeField, Range(0f, 0.95f)] private float bruteBlockCap = 0.5f;
        [SerializeField, Range(0f, 0.95f)] private float trollBlockCap = 0.7f;
        [SerializeField, Range(0f, 0.95f)] private float trollProjectileParryCap = 0.5f;
        [SerializeField, Range(0f, 0.95f)] private float necromancerProjectileParryCap = 0.3f;
        [SerializeField, Range(0f, 0.95f)] private float wizardProjectileParryCap = 0.35f;
        [SerializeField, Range(0f, 0.95f)] private float customBlockCap = 0.65f;
        [SerializeField, Range(0f, 0.95f)] private float customProjectileParryCap = 0.5f;

        [Header("Feedback Hooks")]
        [SerializeField] private Color blockColor = new Color(0.35f, 0.72f, 1f, 0.9f);
        [SerializeField] private Color projectileParryColor = new Color(0.85f, 0.4f, 1f, 0.9f);
        [SerializeField] private GameObject blockImpactVfxPrefab;
        [SerializeField, Range(0.1f, 1f)] private float trollFailedAttemptCooldownMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float guardBrokenRecovery = 0.8f;

        [Header("Successful Block Recoil")]
        [SerializeField, Min(0f)] private float normalAttackerRecoilDuration = 0.4f;
        [SerializeField, Min(0f)] private float heavyAttackerRecoilDuration = 0.55f;
        [SerializeField, Min(0f)] private float attackerRecoilSpeed = 5.5f;

        [Header("Optional Defensive Posture")]
        [SerializeField] private Transform weaponPresentation;
        [SerializeField] private float guardWeaponAngle = 48f;
        [SerializeField, Min(1f)] private float guardScale = 1.04f;

        [Header("Current State (Read Only)")]
        [SerializeField] private EnemyDefenseState currentState = EnemyDefenseState.Ready;
        [SerializeField] private float facingDirection = 1f;
        [SerializeField, Range(0f, 0.3f)] private float runtimeBlockChanceBonus;

        private EnemyStagger stagger;
        private float activeUntil;
        private float recoveryUntil;
        private float nextDecisionTime;
        private EnemyEvolutionStage evolutionStage;
        private SpriteRenderer[] renderers;
        private Color[] restingColors;
        private Vector3 restingScale;
        private Quaternion restingWeaponRotation;
        private bool defensePoseApplied;
        private EnemyMeleeCombat meleePresentation;

        public event Action<EnemyDefenseState, bool> DefenseResolved;
        public event Action GuardBroken;
        public EnemyDefenseState CurrentState => currentState;
        public float EffectiveBlockChance => ResolveBlockChance();
        public float EffectiveProjectileParryChance => ResolveProjectileParryChance();
        public bool IsActivelyBlocking
        {
            get
            {
                RefreshState();
                return currentState == EnemyDefenseState.Blocking
                    && Time.time < activeUntil;
            }
        }
        public bool CanStartAttack => Time.time >= activeUntil
            && Time.time >= recoveryUntil
            && (stagger == null || stagger.CanAct);
        public bool CanEnterDefensivePosture
        {
            get
            {
                RefreshState();
                return currentState == EnemyDefenseState.Ready
                    && Time.time >= nextDecisionTime
                    && (stagger == null || stagger.CanAct);
            }
        }

        private void Awake()
        {
            facingDirection = startsFacingRight ? 1f : -1f;
            stagger = GetComponent<EnemyStagger>();
            CachePresentation();
        }

        private void Start()
        {
            stagger = GetComponent<EnemyStagger>();
            CachePresentation();
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
                meleePresentation?.SetCombatFacing(horizontalDirection);
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
                bool postureBlockedHit = UnityEngine.Random.value < chance;
                if (postureBlockedHit)
                {
                    ShowDefenseImpact(blockColor);
                    ApplyAttackerRecoil(context);
                }

                return postureBlockedHit;
            }

            if (!CanAttemptDefense())
            {
                return false;
            }

            bool succeeded = UnityEngine.Random.value < chance;
            ResolveAttempt(EnemyDefenseState.Blocking, succeeded, blockColor);
            if (succeeded)
            {
                ApplyAttackerRecoil(context);
            }

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
                    ShowDefenseFeedback(EnemyDefenseState.Parrying, projectileParryColor);
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
                case EnemyDefensePreset.Skeleton:
                    baseChance = blockAttemptChance;
                    break;
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
                    evolutionTwoBlockBonus)
                    + runtimeBlockChanceBonus);
        }

        private float ResolveProjectileParryChance()
        {
            if (probabilityPreset == EnemyDefensePreset.Necromancer)
            {
                // Necromancer defense is deliberately a strong, imperfect
                // projectile response and is tuned independently of other roles.
                return Mathf.Clamp01(necromancerProjectileParryChance);
            }

            float baseChance;
            switch (probabilityPreset)
            {
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
                ApplyDefensePose(successfulState);
                ShowDefenseFeedback(successfulState, feedbackColor);
            }
            else
            {
                currentState = EnemyDefenseState.Cooldown;
                activeUntil = Time.time;
                recoveryUntil = Time.time;
                float failedCooldown = probabilityPreset == EnemyDefensePreset.Troll
                    ? defenseCooldown * trollFailedAttemptCooldownMultiplier
                    : defenseCooldown;
                nextDecisionTime = Time.time + failedCooldown;
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

        public void ConfigurePreset(EnemyDefensePreset preset)
        {
            probabilityPreset = preset;
        }

        /// <summary>
        /// Runtime fallback for a Brute that was authored without the shared
        /// defense capability. Existing configured components are intentionally
        /// left alone; this only gives dynamically installed Brutes a real Guard.
        /// </summary>
        public void ConfigureBruteDefaults()
        {
            probabilityPreset = EnemyDefensePreset.Brute;
            blockProjectiles = false;
            activeDefenseDuration = Mathf.Max(activeDefenseDuration, 0.32f);
            recoveryDuration = Mathf.Max(recoveryDuration, 0.25f);
            defenseCooldown = Mathf.Max(defenseCooldown, 0.9f);
        }

        public void ConfigureSkeletonBlock(
            float attemptChance,
            float activeDuration,
            float recovery,
            float cooldown)
        {
            probabilityPreset = EnemyDefensePreset.Skeleton;
            blockAttemptChance = Mathf.Clamp01(attemptChance);
            projectileParryChance = 0f;
            blockProjectiles = false;
            activeDefenseDuration = Mathf.Max(0.01f, activeDuration);
            recoveryDuration = Mathf.Max(0f, recovery);
            defenseCooldown = Mathf.Max(0f, cooldown);
            runtimeBlockChanceBonus = 0f;
        }

        public void SetRuntimeBlockChanceBonus(float bonus)
        {
            runtimeBlockChanceBonus = Mathf.Clamp(bonus, 0f, 0.3f);
        }

        public bool TryEnterDefensivePosture(float duration)
        {
            if (!CanEnterDefensivePosture || ResolveBlockChance() <= 0f)
            {
                return false;
            }

            currentState = EnemyDefenseState.Blocking;
            activeUntil = Time.time + Mathf.Max(activeDefenseDuration, duration);
            recoveryUntil = activeUntil + recoveryDuration * ResolveRecoveryMultiplier();
            nextDecisionTime = recoveryUntil + defenseCooldown;
            ApplyDefensePose(EnemyDefenseState.Blocking);
            ShowDefenseFeedback(EnemyDefenseState.Blocking, blockColor);
            DefenseResolved?.Invoke(EnemyDefenseState.Blocking, true);
            return true;
        }

        public bool TryReceiveGuardBreak(GameObject source)
        {
            RefreshState();
            if (source == null
                || currentState != EnemyDefenseState.Blocking
                || Time.time >= activeUntil)
            {
                return false;
            }

            currentState = EnemyDefenseState.Cooldown;
            activeUntil = Time.time;
            recoveryUntil = Time.time;
            nextDecisionTime = Time.time + guardBrokenRecovery;
            RestoreDefensePose();
            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Slash,
                0.78f,
                blockColor,
                0.24f,
                -25f * facingDirection);
            GuardBroken?.Invoke();
            return true;
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
                RestoreDefensePose();
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

        private void ShowDefenseFeedback(EnemyDefenseState state, Color color)
        {
            CombatShape shape = state == EnemyDefenseState.Parrying
                ? CombatShape.Diamond
                : CombatShape.Hexagon;
            CombatShapeEffect.Create(transform.position, shape, 0.72f, color, 0.18f);
        }

        private void ShowDefenseImpact(Color color)
        {
            GameObject impactPrefab = blockImpactVfxPrefab != null
                ? blockImpactVfxPrefab
                : BlockHitVfxPrefab;
            if (impactPrefab != null)
            {
                Vector3 spawnPosition =
                    transform.position +
                    new Vector3(blockImpactVfxOffset.x, blockImpactVfxOffset.y, 0f);

                GameObject instance = Instantiate(
                    impactPrefab,
                    spawnPosition,
                    Quaternion.identity);

                instance.transform.localScale *= blockImpactVfxScale;
                Destroy(instance, 1.5f);
            }

            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Hexagon,
                0.78f,
                color,
                0.16f);
        }

        private void ApplyAttackerRecoil(DamageContext context)
        {
            if (!context.HasTrait(DamageTrait.Melee)
                || context.HasTrait(DamageTrait.Projectile)
                || context.Source == null)
            {
                return;
            }

            Cave.Player.PlayerGuardBreak playerCombatLock =
                context.Source.GetComponentInParent<Cave.Player.PlayerGuardBreak>();
            if (playerCombatLock == null)
            {
                return;
            }

            float duration = context.HasTrait(DamageTrait.StaggerHeavy)
                ? heavyAttackerRecoilDuration
                : normalAttackerRecoilDuration;
            Vector2 away = context.Source.transform.position - transform.position;
            playerCombatLock.ApplyCombatRecoil(away, duration, attackerRecoilSpeed);
        }

        private void CachePresentation()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            restingColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                restingColors[index] = renderers[index].color;
            }

            restingScale = transform.localScale;
            meleePresentation = GetComponent<EnemyMeleeCombat>();
            if (weaponPresentation == null)
            {
                foreach (EnemyContactDamage contact in GetComponentsInChildren<EnemyContactDamage>(true))
                {
                    if (contact.transform != transform
                        && contact.GetComponent<SpriteRenderer>() != null)
                    {
                        weaponPresentation = contact.transform;
                        break;
                    }
                }
            }

            if (weaponPresentation != null)
            {
                restingWeaponRotation = weaponPresentation.localRotation;
            }
        }

        private void ApplyDefensePose(EnemyDefenseState state)
        {
            defensePoseApplied = true;
            transform.localScale = new Vector3(
                restingScale.x * guardScale,
                restingScale.y,
                restingScale.z);
            Color tint = state == EnemyDefenseState.Parrying
                ? projectileParryColor
                : blockColor;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].color = Color.Lerp(restingColors[index], tint, 0.35f);
                }
            }

            if (meleePresentation != null)
            {
                meleePresentation.SetDefensiveWeaponPose(guardWeaponAngle);
            }
            else if (weaponPresentation != null)
            {
                weaponPresentation.localRotation = restingWeaponRotation
                    * Quaternion.Euler(0f, 0f, guardWeaponAngle * facingDirection);
            }
        }

        private void RestoreDefensePose()
        {
            if (!defensePoseApplied)
            {
                return;
            }

            defensePoseApplied = false;
            transform.localScale = restingScale;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].color = restingColors[index];
                }
            }

            GetComponent<Damageable>()?.ReapplyPersistentTint();

            if (meleePresentation != null)
            {
                meleePresentation.RestoreCombatPresentation();
            }
            else if (weaponPresentation != null)
            {
                weaponPresentation.localRotation = restingWeaponRotation;
            }
        }

        public void Interrupt()
        {
            currentState = EnemyDefenseState.Cooldown;
            activeUntil = Time.time;
            recoveryUntil = Time.time;
            nextDecisionTime = Mathf.Max(nextDecisionTime, Time.time + defenseCooldown);
            RestoreDefensePose();
        }

        private void OnDisable()
        {
            currentState = EnemyDefenseState.Ready;
            activeUntil = 0f;
            recoveryUntil = 0f;
            nextDecisionTime = 0f;
            runtimeBlockChanceBonus = 0f;
            RestoreDefensePose();
        }
    }
}
