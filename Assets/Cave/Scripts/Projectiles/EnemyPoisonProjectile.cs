using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using Cave.Progression;
using UnityEngine;

namespace Cave.Projectiles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyPoisonProjectile : MonoBehaviour,
        IDeflectableDamageSource,
        IPlayerParryableProjectile,
        IEnemyParryableProjectile
    {
        [SerializeField] private Color enemyProjectileColor = new Color(0.45f, 1f, 0.2f, 1f);
        [SerializeField] private Color evolvedProjectileColor = new Color(0.72f, 1f, 0.12f, 1f);
        [SerializeField] private Color reflectedProjectileColor = new Color(0.75f, 0.95f, 1f, 1f);

        private Rigidbody2D body;
        private Collider2D projectileCollider;
        private SpriteRenderer[] renderers;
        private GameObject originalShooter;
        private GameObject currentOwner;
        private ProjectileTeam currentTeam;
        private Vector2 movementDirection;
        private float speed;
        private int directDamage;
        private float poisonDamagePercentPerTick;
        private int minimumPoisonTickDamage;
        private int maximumPoisonTickDamage;
        private float poisonInterval;
        private float poisonDuration;
        private bool impacted;
        private bool enemyParried;
        private DamageContext reflectedDamageContext;
        private LineRenderer runtimeVisual;
        private Material runtimeVisualMaterial;
        private bool createsPoisonZone;
        private float poisonZoneRadius;
        private float poisonZoneDuration;

        public bool CanBeParried => !impacted
            && currentTeam == ProjectileTeam.Enemy
            && !enemyParried;
        public bool CanBeEnemyParried => !impacted && currentTeam == ProjectileTeam.Player;

        public static EnemyPoisonProjectile CreateRuntime(Vector2 position)
        {
            GameObject projectileObject = new GameObject("Detective Poison Projectile");
            projectileObject.transform.position = position;
            Rigidbody2D runtimeBody = projectileObject.AddComponent<Rigidbody2D>();
            runtimeBody.gravityScale = 0f;
            runtimeBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            runtimeBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            runtimeBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D runtimeCollider = projectileObject.AddComponent<CircleCollider2D>();
            runtimeCollider.radius = 0.12f;
            runtimeCollider.isTrigger = true;
            return projectileObject.AddComponent<EnemyPoisonProjectile>();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileCollider.isTrigger = true;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                EnsureRuntimeVisual();
            }
        }

        public void Initialize(
            GameObject shooter,
            Vector2 direction,
            float projectileSpeed,
            int resolvedDirectDamage,
            float resolvedPoisonDamagePercentPerTick,
            int resolvedMinimumPoisonTickDamage,
            int resolvedMaximumPoisonTickDamage,
            float tickInterval,
            float statusDuration,
            float lifetime,
            bool createPoisonZone,
            float zoneRadius,
            float zoneDuration)
        {
            originalShooter = shooter;
            currentOwner = shooter;
            currentTeam = ProjectileTeam.Enemy;
            movementDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.left;
            speed = Mathf.Max(0.01f, projectileSpeed);
            directDamage = Mathf.Max(1, resolvedDirectDamage);
            poisonDamagePercentPerTick = Mathf.Max(0f, resolvedPoisonDamagePercentPerTick);
            minimumPoisonTickDamage = Mathf.Max(1, resolvedMinimumPoisonTickDamage);
            maximumPoisonTickDamage = Mathf.Max(
                minimumPoisonTickDamage,
                resolvedMaximumPoisonTickDamage);
            poisonInterval = Mathf.Max(0.05f, tickInterval);
            poisonDuration = Mathf.Max(0.05f, statusDuration);
            createsPoisonZone = createPoisonZone;
            poisonZoneRadius = Mathf.Max(0.1f, zoneRadius);
            poisonZoneDuration = Mathf.Max(0.1f, zoneDuration);
            impacted = false;
            enemyParried = false;
            reflectedDamageContext = default;
            SetVisualColor(createsPoisonZone ? evolvedProjectileColor : enemyProjectileColor);
            body.simulated = true;
            projectileCollider.enabled = true;
            body.velocity = movementDirection * speed;
            Destroy(gameObject, Mathf.Max(0.1f, lifetime));
        }

        public bool Parry(
            GameObject newOwner,
            Vector2 fallbackDirection,
            float speedMultiplier,
            float damageMultiplier)
        {
            if (!CanBeParried)
            {
                return false;
            }

            Vector2 direction = originalShooter != null
                ? (Vector2)originalShooter.transform.position - (Vector2)transform.position
                : fallbackDirection;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = -movementDirection;
            }

            currentOwner = newOwner;
            currentTeam = ProjectileTeam.Player;
            movementDirection = direction.normalized;
            speed *= Mathf.Max(1f, speedMultiplier);
            directDamage = Mathf.Max(1, Mathf.RoundToInt(directDamage * Mathf.Max(1f, damageMultiplier)));
            PlayerResourceMastery mastery = newOwner != null
                ? newOwner.GetComponentInParent<PlayerResourceMastery>()
                : null;
            reflectedDamageContext = mastery != null
                ? mastery.CreatePlayerDamageContext().WithTraits(DamageTrait.Projectile)
                : default;
            SetVisualColor(reflectedProjectileColor);
            body.velocity = movementDirection * speed;
            return true;
        }

        public bool DeflectToGround(GameObject newOwner, Vector2 targetPoint)
        {
            if (!CanBeParried)
            {
                return false;
            }

            Vector2 direction = targetPoint - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.down;
            }

            currentOwner = newOwner;
            currentTeam = ProjectileTeam.Neutral;
            movementDirection = direction.normalized;
            body.velocity = movementDirection * speed;
            return true;
        }

        public bool TryDeflect(GameObject defender)
        {
            Vector2 fallback = defender != null
                ? (Vector2)transform.position - (Vector2)defender.transform.position
                : -movementDirection;
            return Parry(defender, fallback, 1f, 1f);
        }

        public bool TryEnemyParry(GameObject defender)
        {
            if (!CanBeEnemyParried)
            {
                return false;
            }

            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            Vector2 direction = player != null
                ? (Vector2)player.transform.position - (Vector2)transform.position
                : -movementDirection;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.left;
            }

            currentOwner = defender;
            currentTeam = ProjectileTeam.Enemy;
            enemyParried = true;
            movementDirection = direction.normalized;
            SetVisualColor(enemyProjectileColor);
            body.velocity = movementDirection * speed;
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (impacted || IsOwnedCollider(other))
            {
                return;
            }

            ParryHitbox parryHitbox = other.GetComponent<ParryHitbox>();
            if (parryHitbox != null && CanBeParried)
            {
                parryHitbox.TryParry(this);
                return;
            }

            if (currentTeam == ProjectileTeam.Enemy)
            {
                PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    player.TryTakeDamage(
                        directDamage,
                        new DamageContext(
                            currentOwner != null ? currentOwner : gameObject,
                            DamageTrait.Direct | DamageTrait.Projectile));
                    if (currentTeam != ProjectileTeam.Enemy)
                    {
                        return;
                    }

                    PlayerPoisonStatus poison = player.GetComponent<PlayerPoisonStatus>();
                    if (poison == null)
                    {
                        poison = player.gameObject.AddComponent<PlayerPoisonStatus>();
                    }

                    poison.ApplyPoison(
                        poisonDamagePercentPerTick,
                        minimumPoisonTickDamage,
                        maximumPoisonTickDamage,
                        poisonInterval,
                        poisonDuration,
                        currentOwner);
                    Impact();
                    return;
                }
            }
            else if (currentTeam == ProjectileTeam.Player)
            {
                Damageable damageable = other.GetComponentInParent<Damageable>();
                if (damageable != null)
                {
                    EnemyDefenseController defense = damageable.GetComponent<EnemyDefenseController>();
                    if (defense != null && defense.TryParryProjectile(this))
                    {
                        return;
                    }

                    damageable.TakeDamage(directDamage, reflectedDamageContext);
                    Impact();
                    return;
                }
            }

            if (!other.isTrigger)
            {
                Impact();
            }
        }

        private bool IsOwnedCollider(Collider2D other)
        {
            return currentOwner != null
                && (other.gameObject == currentOwner
                    || other.transform.IsChildOf(currentOwner.transform));
        }

        private void Impact()
        {
            if (impacted)
            {
                return;
            }

            impacted = true;
            body.velocity = Vector2.zero;
            body.simulated = false;
            projectileCollider.enabled = false;
            if (createsPoisonZone && currentTeam == ProjectileTeam.Enemy)
            {
                EnemyPoisonZone.Create(
                    transform.position,
                    currentOwner,
                    poisonZoneRadius,
                    poisonZoneDuration,
                    poisonDamagePercentPerTick,
                    minimumPoisonTickDamage,
                    maximumPoisonTickDamage,
                    poisonInterval,
                    evolvedProjectileColor);
            }

            Cave.Combat.AreaPulseEffect.Create(
                transform.position,
                0.45f,
                createsPoisonZone ? evolvedProjectileColor : enemyProjectileColor,
                0.18f);
            Destroy(gameObject);
        }

        private void SetVisualColor(Color color)
        {
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = color;
                }
            }

            if (runtimeVisual != null)
            {
                runtimeVisual.startColor = color;
                runtimeVisual.endColor = color;
            }
        }

        private void EnsureRuntimeVisual()
        {
            runtimeVisual = gameObject.AddComponent<LineRenderer>();
            runtimeVisualMaterial = new Material(Shader.Find("Sprites/Default"));
            runtimeVisual.material = runtimeVisualMaterial;
            runtimeVisual.useWorldSpace = false;
            runtimeVisual.loop = true;
            runtimeVisual.positionCount = 12;
            runtimeVisual.startWidth = 0.045f;
            runtimeVisual.endWidth = 0.045f;
            runtimeVisual.sortingOrder = 7;
            for (int index = 0; index < runtimeVisual.positionCount; index++)
            {
                float angle = index / (float)runtimeVisual.positionCount * Mathf.PI * 2f;
                runtimeVisual.SetPosition(
                    index,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.12f);
            }
        }

        private void OnDestroy()
        {
            if (runtimeVisualMaterial != null)
            {
                Destroy(runtimeVisualMaterial);
            }
        }
    }
}
