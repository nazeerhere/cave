using Cave.Projectiles;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    public sealed class EnemyShooter : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform firePoint;
        [SerializeField] private FireballProjectile projectilePrefab;

        [Header("Fireball")]
        [SerializeField, Min(0.1f)] private float fireInterval = 2f;
        [SerializeField, Min(0f)] private float initialDelay = 1f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 5f;
        [SerializeField, Min(1)] private int projectileDamage = 1;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 6f;
        [SerializeField, Min(0f)] private float firingRange = 10f;

        private float nextFireTime;
        private float runtimeFireInterval;
        private float runtimeProjectileSpeed;
        private int runtimeProjectileDamage;
        private WorldDifficultyManager difficultyManager;

        public float BaseFireInterval => fireInterval;
        public float BaseProjectileSpeed => projectileSpeed;
        public int BaseProjectileDamage => projectileDamage;

        private void Awake()
        {
            runtimeFireInterval = fireInterval;
            runtimeProjectileSpeed = projectileSpeed;
            runtimeProjectileDamage = projectileDamage;
        }

        private void OnEnable()
        {
            nextFireTime = Time.time + initialDelay;
        }

        private void Update()
        {
            if (target == null || projectilePrefab == null || Time.time < nextFireTime)
            {
                return;
            }

            Vector2 direction = target.position - transform.position;
            if (direction.sqrMagnitude > firingRange * firingRange)
            {
                return;
            }

            Fire();
            nextFireTime = Time.time + runtimeFireInterval;
        }

        private void Fire()
        {
            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            Vector2 direction = target.position - spawnPosition;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.left;
            }

            FireballProjectile projectile = Object.Instantiate(
                projectilePrefab,
                spawnPosition,
                Quaternion.identity);

            EnemyDamageModifiers modifiers = GetComponentInParent<EnemyDamageModifiers>();
            int damage = modifiers != null
                ? modifiers.ResolveDamage(runtimeProjectileDamage)
                : runtimeProjectileDamage;
            projectile.Initialize(
                gameObject,
                target,
                direction.normalized,
                runtimeProjectileSpeed,
                damage,
                projectileLifetime,
                difficultyManager);
        }

        public void SetRuntimeDifficultyValues(int damage, float speed, float interval)
        {
            runtimeProjectileDamage = Mathf.Max(1, damage);
            runtimeProjectileSpeed = Mathf.Max(0.01f, speed);
            runtimeFireInterval = Mathf.Max(0.01f, interval);
        }

        public void SetDifficultyManager(WorldDifficultyManager manager)
        {
            difficultyManager = manager;
        }
    }
}
