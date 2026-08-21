using Cave.Projectiles;
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
            nextFireTime = Time.time + fireInterval;
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

            projectile.Initialize(
                gameObject,
                direction.normalized,
                projectileSpeed,
                projectileDamage,
                projectileLifetime);
        }
    }
}
