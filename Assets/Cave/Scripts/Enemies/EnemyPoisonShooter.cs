using Cave.Combat;
using Cave.Player;
using Cave.Projectiles;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyPoisonShooter : MonoBehaviour,
        IEnemyInterruptible,
        IEnemySkillEvolutionReceiver
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform firePoint;
        [SerializeField] private EnemyPoisonProjectile projectilePrefab;

        [Header("Poison Projectile")]
        [SerializeField, Min(1)] private int directDamage = 1;
        [FormerlySerializedAs("poisonDamagePercentPerTick")]
        [SerializeField, Range(0f, 0.25f)] private float poisonDamagePercentPerSecond = 0.01f;
        [SerializeField, Range(0f, 0.25f)] private float poisonZoneDamagePercentPerSecond = 0.03f;
        [SerializeField, Min(1)] private int minimumPoisonTickDamage = 1;
        [SerializeField, Min(1)] private int maximumPoisonTickDamage = 99;
        [SerializeField, Min(0.05f)] private float poisonInterval = 1f;
        [SerializeField, Min(0.1f)] private float poisonDuration = 6f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 6f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 7f;
        [SerializeField, Min(0f)] private float fireCooldown = 2.2f;
        [SerializeField, Min(0f)] private float fireRange = 9f;
        [SerializeField, Min(0f)] private float attackWindup = 0.3f;
        [SerializeField] private Color attackTelegraphColor = new Color(0.45f, 1f, 0.2f, 0.85f);

        [Header("Skill Evolution")]
        [SerializeField, Min(1f)] private float evolutionOneProjectileSpeedMultiplier = 1.15f;
        [SerializeField, Min(1f)] private float evolutionOnePoisonDurationMultiplier = 1.25f;
        [SerializeField] private bool evolutionTwoCreatesPoisonZone = true;
        [SerializeField, Min(0.1f)] private float poisonZoneRadius = 1.35f;
        [SerializeField, Min(0.1f)] private float poisonZoneDuration = 3f;

        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private float nextFireTime;
        private float fireCompletesAt;
        private float nextTargetResolveTime;
        private bool isWindingUp;
        private int runtimeDirectDamage;
        private float runtimeProjectileSpeed;
        private float runtimeFireCooldown;
        private EnemyEvolutionStage evolutionStage;
        private bool brainControlled;
        private Transform firingTarget;

        public int BaseDirectDamage => directDamage;
        public float PoisonDamagePercentPerSecond => poisonDamagePercentPerSecond;
        public float PoisonZoneDamagePercentPerSecond => poisonZoneDamagePercentPerSecond;
        public float BaseProjectileSpeed => projectileSpeed;
        public float BaseFireCooldown => fireCooldown;
        public bool IsBusy => isWindingUp;
        public bool IsReady => !isWindingUp
            && Time.time >= nextFireTime
            && (stagger == null || stagger.CanAct);

        private void Awake()
        {
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = gameObject.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(EnemyArchetype.Ranged);
            runtimeDirectDamage = directDamage;
            runtimeProjectileSpeed = projectileSpeed;
            runtimeFireCooldown = fireCooldown;
        }

        private void Update()
        {
            EnsureTarget();
            if (isWindingUp)
            {
                if (Time.time >= fireCompletesAt)
                {
                    Fire();
                }

                return;
            }

            if (brainControlled)
            {
                return;
            }

            TryUse(target);
        }

        public bool CanUse(Transform requestedTarget)
        {
            return IsReady
                && requestedTarget != null
                && ((Vector2)requestedTarget.position - (Vector2)transform.position).sqrMagnitude
                    <= fireRange * fireRange;
        }

        public bool TryUse(Transform requestedTarget)
        {
            if (CurseAltarZone.TryGetConfusedTarget(gameObject, out Transform confusedTarget))
            {
                requestedTarget = confusedTarget;
            }

            if (!CanUse(requestedTarget))
            {
                return false;
            }

            firingTarget = requestedTarget;
            isWindingUp = true;
            fireCompletesAt = Time.time + attackWindup;
            Cave.Combat.AreaPulseEffect.Create(
                firePoint != null ? firePoint.position : transform.position,
                0.55f,
                attackTelegraphColor,
                Mathf.Max(0.15f, attackWindup));
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        private void Fire()
        {
            isWindingUp = false;
            nextFireTime = Time.time + runtimeFireCooldown;
            Transform resolvedTarget = firingTarget;
            firingTarget = null;
            if (resolvedTarget == null)
            {
                return;
            }

            Vector2 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            Vector2 direction = (Vector2)resolvedTarget.position - spawnPosition;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.left;
            }

            if (damageModifiers == null)
            {
                damageModifiers = GetComponent<EnemyDamageModifiers>();
            }

            int resolvedDirectDamage = damageModifiers != null
                ? damageModifiers.ResolveDamage(runtimeDirectDamage)
                : runtimeDirectDamage;
            EnemyPoisonProjectile projectile = projectilePrefab != null
                ? Instantiate(projectilePrefab, spawnPosition, Quaternion.identity)
                : EnemyPoisonProjectile.CreateRuntime(spawnPosition);
            float evolvedProjectileSpeed = runtimeProjectileSpeed
                * (evolutionStage >= EnemyEvolutionStage.EvolutionOne
                    ? evolutionOneProjectileSpeedMultiplier
                    : 1f);
            projectile.Initialize(
                gameObject,
                direction.normalized,
                evolvedProjectileSpeed,
                resolvedDirectDamage,
                poisonDamagePercentPerSecond,
                poisonZoneDamagePercentPerSecond,
                minimumPoisonTickDamage,
                maximumPoisonTickDamage,
                poisonInterval,
                EffectivePoisonDuration,
                projectileLifetime,
                evolutionStage == EnemyEvolutionStage.EvolutionTwo
                    && evolutionTwoCreatesPoisonZone,
                poisonZoneRadius,
                poisonZoneDuration);
        }

        public void SetRuntimeDifficultyValues(
            int resolvedDirectDamage,
            int _,
            float resolvedSpeed,
            float resolvedCooldown)
        {
            runtimeDirectDamage = Mathf.Max(1, resolvedDirectDamage);
            runtimeProjectileSpeed = Mathf.Max(0.01f, resolvedSpeed);
            runtimeFireCooldown = Mathf.Max(0.01f, resolvedCooldown);
        }

        private float EffectivePoisonDuration => poisonDuration
            * (evolutionStage >= EnemyEvolutionStage.EvolutionOne
                ? evolutionOnePoisonDurationMultiplier
                : 1f);

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
        }

        public void Interrupt()
        {
            isWindingUp = false;
            nextFireTime = Mathf.Max(nextFireTime, Time.time + runtimeFireCooldown * 0.5f);
        }

        private void EnsureTarget()
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                return;
            }

            if (Time.time < nextTargetResolveTime)
            {
                return;
            }

            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            target = player != null ? player.transform : null;
            nextTargetResolveTime = Time.time + 1f;
        }

        private void OnDisable()
        {
            isWindingUp = false;
            nextFireTime = 0f;
            firingTarget = null;
        }
    }
}
