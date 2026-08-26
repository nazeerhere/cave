using Cave.Combat;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Enemies
{
    public enum EnemySlowUseRejection
    {
        None,
        MissingTarget,
        MissingProjectilePrefab,
        Busy,
        Cooldown,
        Staggered,
        OutOfRange,
        BlockedLineOfSight
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyArchetypeProfile))]
    public sealed class EnemySlowShooter : MonoBehaviour, IEnemyInterruptible
    {
        [Header("References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private EnemySlowProjectile projectilePrefab;

        [Header("Slow Bolt")]
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Range(0.1f, 1f)] private float playerMovementMultiplier = 0.7f;
        [SerializeField, Min(0.1f)] private float slowDuration = 2f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 6f;
        [SerializeField, Min(0f)] private float cooldown = 1.75f;
        [SerializeField, Min(0f)] private float castWindup = 0.35f;
        [SerializeField, Min(0.1f)] private float castRange = 8f;
        [SerializeField] private LayerMask lineOfSightBlockingLayers;
        [SerializeField] private Color castTelegraphColor = new Color(0.55f, 0.35f, 1f, 0.85f);

        [Header("Current Cast Check (Read Only)")]
        [SerializeField] private EnemySlowUseRejection lastUseRejection;
        [SerializeField] private float lastTargetDistance;
        [SerializeField] private string lastLineOfSightBlocker;

        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private Transform target;
        private float nextCastTime;
        private float castCompletesAt;
        private bool isWindingUp;
        private bool brainControlled;

        public bool IsBusy => isWindingUp;
        public bool IsReady => !isWindingUp
            && projectilePrefab != null
            && Time.time >= nextCastTime
            && (stagger == null || stagger.CanAct);
        public EnemySlowUseRejection LastUseRejection => lastUseRejection;
        public float LastTargetDistance => lastTargetDistance;
        public float CooldownRemaining => Mathf.Max(0f, nextCastTime - Time.time);
        public string LastLineOfSightBlocker => lastLineOfSightBlocker;
        public bool HasProjectilePrefab => projectilePrefab != null;

        private void Awake()
        {
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            GetComponent<EnemyArchetypeProfile>()?.AddRuntimeArchetype(EnemyArchetype.Ranged);
        }

        private void Update()
        {
            if (isWindingUp && Time.time >= castCompletesAt)
            {
                Fire();
            }

            if (!brainControlled && !isWindingUp)
            {
                TryUse(target);
            }
        }

        public void Configure(
            Transform configuredFirePoint,
            EnemySlowProjectile configuredPrefab,
            int baseDamage,
            float movementMultiplier,
            float statusDuration,
            float speed,
            float lifetime,
            float castCooldown,
            float windup,
            float range,
            LayerMask blockingLayers)
        {
            firePoint = configuredFirePoint;
            projectilePrefab = configuredPrefab;

            damage = Mathf.Max(1, baseDamage);
            playerMovementMultiplier = Mathf.Clamp(movementMultiplier, 0.1f, 1f);
            slowDuration = Mathf.Max(0.1f, statusDuration);
            projectileSpeed = Mathf.Max(0.01f, speed);
            projectileLifetime = Mathf.Max(0.1f, lifetime);
            cooldown = Mathf.Max(0f, castCooldown);
            castWindup = Mathf.Max(0f, windup);
            castRange = Mathf.Max(0.1f, range);
            lineOfSightBlockingLayers = blockingLayers;
        }

        public bool CanUse(Transform requestedTarget)
        {
            EnemySlowUseRejection rejection = GetUseRejection(requestedTarget);
            lastUseRejection = rejection;
            return rejection == EnemySlowUseRejection.None;
        }

        private EnemySlowUseRejection GetUseRejection(Transform requestedTarget)
        {
            lastTargetDistance = 0f;
            lastLineOfSightBlocker = string.Empty;
            if (requestedTarget == null)
            {
                return EnemySlowUseRejection.MissingTarget;
            }

            if (projectilePrefab == null)
            {
                return EnemySlowUseRejection.MissingProjectilePrefab;
            }

            if (isWindingUp)
            {
                return EnemySlowUseRejection.Busy;
            }

            if (Time.time < nextCastTime)
            {
                return EnemySlowUseRejection.Cooldown;
            }

            if (stagger != null && !stagger.CanAct)
            {
                return EnemySlowUseRejection.Staggered;
            }

            Vector2 origin = firePoint != null ? firePoint.position : transform.position;
            Vector2 destination = requestedTarget.position;
            lastTargetDistance = Vector2.Distance(origin, destination);
            if ((destination - origin).sqrMagnitude > castRange * castRange)
            {
                return EnemySlowUseRejection.OutOfRange;
            }

            if (!HasLineOfSight(origin, destination))
            {
                return EnemySlowUseRejection.BlockedLineOfSight;
            }

            return EnemySlowUseRejection.None;
        }

        public bool TryUse(Transform requestedTarget)
        {
            if (!CanUse(requestedTarget))
            {
                return false;
            }

            target = requestedTarget;
            lastUseRejection = EnemySlowUseRejection.None;
            isWindingUp = true;
            castCompletesAt = Time.time + castWindup;
            AreaPulseEffect.Create(
                firePoint != null ? firePoint.position : transform.position,
                0.45f,
                castTelegraphColor,
                Mathf.Max(0.15f, castWindup));
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        public void Interrupt()
        {
            isWindingUp = false;
            nextCastTime = Mathf.Max(nextCastTime, Time.time + cooldown * 0.5f);
        }

        private void Fire()
        {
            isWindingUp = false;
            nextCastTime = Time.time + cooldown;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            Vector2 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            Vector2 direction = (Vector2)target.position - spawnPosition;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.left;
            }

            if (projectilePrefab == null)
            {
                return;
            }

            EnemySlowProjectile projectile = Instantiate(
                projectilePrefab,
                spawnPosition,
                Quaternion.identity);
            if (projectile == null)
            {
                return;
            }

            if (damageModifiers == null)
            {
                damageModifiers = GetComponent<EnemyDamageModifiers>();
            }

            int resolvedDamage = damageModifiers != null
                ? damageModifiers.ResolveDamage(damage)
                : damage;
            projectile.Initialize(
                gameObject,
                direction.normalized,
                projectileSpeed,
                resolvedDamage,
                playerMovementMultiplier,
                slowDuration,
                projectileLifetime);
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 destination)
        {
            if (lineOfSightBlockingLayers.value == 0)
            {
                return true;
            }

            RaycastHit2D hit = Physics2D.Linecast(origin, destination, lineOfSightBlockingLayers);
            lastLineOfSightBlocker = hit.collider != null ? hit.collider.name : string.Empty;
            return hit.collider == null;
        }

        private void OnDisable()
        {
            isWindingUp = false;
            nextCastTime = 0f;
        }
    }
}
