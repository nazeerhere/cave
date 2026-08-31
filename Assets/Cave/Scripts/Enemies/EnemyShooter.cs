using System.Collections;
using Cave.Combat;
using Cave.Projectiles;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    public sealed class EnemyShooter : MonoBehaviour, IEnemyInterruptible
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform firePoint;
        [SerializeField] private FireballProjectile projectilePrefab;

        [Header("Fireball")]
        [SerializeField, Min(0.1f)] private float fireInterval = 2f;
        [SerializeField, Min(0f)] private float initialDelay = 1f;
        [SerializeField, Min(0f)] private float castWindup = 0.4f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 5f;
        [SerializeField, Min(1)] private int projectileDamage = 1;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 6f;
        [SerializeField, Min(0f)] private float firingRange = 10f;

        [Header("Wizard Basic Echo")]
        [SerializeField] private bool wizardEchoEnabled;
        [SerializeField, Min(0.05f)] private float echoDelay = 0.42f;
        [SerializeField, Range(0.1f, 1f)] private float echoDamageMultiplier = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float echoVisualScale = 0.82f;
        [SerializeField] private Color echoTint = new Color(0.65f, 0.55f, 1f, 0.72f);
        [SerializeField] private GameObject echoVfxPrefab;
        [SerializeField, Min(0.05f)] private float echoVfxLifetime = 1.25f;

        private float nextFireTime;
        private float runtimeFireInterval;
        private float runtimeProjectileSpeed;
        private int runtimeProjectileDamage;
        private WorldDifficultyManager difficultyManager;
        private bool brainControlled;
        private bool isWindingUp;
        private float castCompletesAt;

        public float BaseFireInterval => fireInterval;
        public float BaseProjectileSpeed => projectileSpeed;
        public int BaseProjectileDamage => projectileDamage;
        public bool IsBusy => isWindingUp;
        public bool IsReady => !isWindingUp
            && projectilePrefab != null
            && Time.time >= nextFireTime;

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
            if (isWindingUp)
            {
                if (Time.time >= castCompletesAt)
                {
                    Fire();
                }

                return;
            }

            if (brainControlled
                || target == null
                || projectilePrefab == null
                || Time.time < nextFireTime)
            {
                return;
            }

            Vector2 direction = target.position - transform.position;
            if (direction.sqrMagnitude > firingRange * firingRange)
            {
                return;
            }

            TryUse(target);
        }

        public bool TryUse(Transform requestedTarget)
        {
            if (!IsReady || requestedTarget == null)
            {
                return false;
            }

            Vector2 direction = requestedTarget.position - transform.position;
            if (direction.sqrMagnitude > firingRange * firingRange)
            {
                return false;
            }

            target = requestedTarget;
            isWindingUp = true;
            castCompletesAt = Time.time + castWindup;
            AreaPulseEffect.Create(
                firePoint != null ? firePoint.position : transform.position,
                0.35f,
                new Color(0.55f, 0.35f, 1f, 0.8f),
                Mathf.Max(0.15f, castWindup));
            return true;
        }

        private void Fire()
        {
            isWindingUp = false;
            nextFireTime = Time.time + runtimeFireInterval;
            if (target == null || !target.gameObject.activeInHierarchy || projectilePrefab == null)
            {
                return;
            }

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
            Damageable ownerDamageable = GetComponentInParent<Damageable>();
            GameObject owner = ownerDamageable != null ? ownerDamageable.gameObject : gameObject;
            projectile.Initialize(
                owner,
                target,
                direction.normalized,
                runtimeProjectileSpeed,
                damage,
                projectileLifetime,
                difficultyManager);
            if (wizardEchoEnabled)
            {
                StartCoroutine(FireEchoAfterDelay(
                    spawnPosition,
                    direction.normalized,
                    damage,
                    owner));
            }
        }

        private IEnumerator FireEchoAfterDelay(
            Vector3 spawnPosition,
            Vector2 direction,
            int originalDamage,
            GameObject owner)
        {
            yield return new WaitForSeconds(echoDelay);
            if (projectilePrefab == null || owner == null || !owner.activeInHierarchy)
            {
                yield break;
            }

            FireballProjectile echo = Object.Instantiate(
                projectilePrefab,
                spawnPosition,
                Quaternion.identity);
            echo.Initialize(
                owner,
                null,
                direction,
                runtimeProjectileSpeed,
                Mathf.Max(1, Mathf.RoundToInt(originalDamage * echoDamageMultiplier)),
                projectileLifetime,
                null);
            echo.ConfigureAsSecondaryEcho(echoTint, echoVisualScale);
            if (echoVfxPrefab != null)
            {
                GameObject vfx = Object.Instantiate(
                    echoVfxPrefab,
                    spawnPosition,
                    Quaternion.identity);
                Object.Destroy(vfx, echoVfxLifetime);
            }
        }

        public void EnableWizardEcho()
        {
            wizardEchoEnabled = true;
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

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        public void ConfigureWizardFallback(
            Transform configuredFirePoint,
            FireballProjectile configuredPrefab,
            int damage,
            float cooldown,
            float speed,
            float lifetime,
            float range,
            float windup)
        {
            firePoint = configuredFirePoint;
            projectilePrefab = configuredPrefab;
            projectileDamage = Mathf.Max(1, damage);
            fireInterval = Mathf.Max(0.1f, cooldown);
            projectileSpeed = Mathf.Max(0.01f, speed);
            projectileLifetime = Mathf.Max(0.1f, lifetime);
            firingRange = Mathf.Max(0.1f, range);
            castWindup = Mathf.Max(0f, windup);
            runtimeProjectileDamage = projectileDamage;
            runtimeFireInterval = fireInterval;
            runtimeProjectileSpeed = projectileSpeed;
            brainControlled = true;
        }

        public void Interrupt()
        {
            if (!isWindingUp)
            {
                return;
            }

            isWindingUp = false;
            nextFireTime = Mathf.Max(nextFireTime, Time.time + runtimeFireInterval * 0.5f);
        }

        private void OnDisable()
        {
            isWindingUp = false;
        }
    }
}
