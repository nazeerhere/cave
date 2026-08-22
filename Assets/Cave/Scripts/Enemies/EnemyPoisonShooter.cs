using Cave.Combat;
using Cave.Player;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyArchetypeProfile))]
    public sealed class EnemyPoisonShooter : MonoBehaviour, IEnemyInterruptible
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform firePoint;
        [SerializeField] private EnemyPoisonProjectile projectilePrefab;

        [Header("Poison Projectile")]
        [SerializeField, Min(1)] private int directDamage = 1;
        [SerializeField, Min(1)] private int poisonTickDamage = 1;
        [SerializeField, Min(0.05f)] private float poisonInterval = 1.5f;
        [SerializeField, Min(0.1f)] private float poisonDuration = 6f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 6f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 7f;
        [SerializeField, Min(0f)] private float fireCooldown = 2.2f;
        [SerializeField, Min(0f)] private float fireRange = 9f;
        [SerializeField, Min(0f)] private float attackWindup = 0.3f;
        [SerializeField] private Color attackTelegraphColor = new Color(0.45f, 1f, 0.2f, 0.85f);

        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private float nextFireTime;
        private float fireCompletesAt;
        private bool isWindingUp;
        private int runtimeDirectDamage;
        private int runtimePoisonDamage;
        private float runtimeProjectileSpeed;
        private float runtimeFireCooldown;

        public int BaseDirectDamage => directDamage;
        public int BasePoisonDamage => poisonTickDamage;
        public float BaseProjectileSpeed => projectileSpeed;
        public float BaseFireCooldown => fireCooldown;

        private void Awake()
        {
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            GetComponent<EnemyArchetypeProfile>().AddRuntimeArchetype(EnemyArchetype.Ranged);
            runtimeDirectDamage = directDamage;
            runtimePoisonDamage = poisonTickDamage;
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

            if (target == null
                || projectilePrefab == null
                || Time.time < nextFireTime
                || (stagger != null && !stagger.CanAct))
            {
                return;
            }

            Vector2 distance = target.position - transform.position;
            if (distance.sqrMagnitude > fireRange * fireRange)
            {
                return;
            }

            isWindingUp = true;
            fireCompletesAt = Time.time + attackWindup;
            Cave.Combat.AreaPulseEffect.Create(
                firePoint != null ? firePoint.position : transform.position,
                0.55f,
                attackTelegraphColor,
                Mathf.Max(0.15f, attackWindup));
        }

        private void Fire()
        {
            isWindingUp = false;
            nextFireTime = Time.time + runtimeFireCooldown;
            if (target == null || projectilePrefab == null)
            {
                return;
            }

            Vector2 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            Vector2 direction = (Vector2)target.position - spawnPosition;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.left;
            }

            int resolvedDirectDamage = damageModifiers != null
                ? damageModifiers.ResolveDamage(runtimeDirectDamage)
                : runtimeDirectDamage;
            int resolvedPoisonDamage = damageModifiers != null
                ? damageModifiers.ResolveDamage(runtimePoisonDamage)
                : runtimePoisonDamage;
            EnemyPoisonProjectile projectile = Instantiate(
                projectilePrefab,
                spawnPosition,
                Quaternion.identity);
            projectile.Initialize(
                gameObject,
                direction.normalized,
                runtimeProjectileSpeed,
                resolvedDirectDamage,
                resolvedPoisonDamage,
                poisonInterval,
                poisonDuration,
                projectileLifetime);
        }

        public void SetRuntimeDifficultyValues(
            int resolvedDirectDamage,
            int resolvedPoisonDamage,
            float resolvedSpeed,
            float resolvedCooldown)
        {
            runtimeDirectDamage = Mathf.Max(1, resolvedDirectDamage);
            runtimePoisonDamage = Mathf.Max(1, resolvedPoisonDamage);
            runtimeProjectileSpeed = Mathf.Max(0.01f, resolvedSpeed);
            runtimeFireCooldown = Mathf.Max(0.01f, resolvedCooldown);
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

            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            target = player != null ? player.transform : null;
        }

        private void OnDisable()
        {
            isWindingUp = false;
            nextFireTime = 0f;
        }
    }
}
