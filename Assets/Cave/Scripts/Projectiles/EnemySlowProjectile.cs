using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using Cave.Progression;
using UnityEngine;

namespace Cave.Projectiles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemySlowProjectile : MonoBehaviour,
        IDeflectableDamageSource,
        IPlayerParryableProjectile,
        IEnemyParryableProjectile
    {
        [SerializeField] private Color enemyProjectileColor = new Color(0.55f, 0.35f, 1f, 1f);
        [SerializeField] private Color reflectedProjectileColor = new Color(0.55f, 0.9f, 1f, 1f);

        private Rigidbody2D body;
        private Collider2D projectileCollider;
        private SpriteRenderer[] renderers;
        private GameObject originalShooter;
        private GameObject currentOwner;
        private ProjectileTeam currentTeam;
        private Vector2 movementDirection;
        private float speed;
        private int damage;
        private float playerMovementMultiplier;
        private float slowDuration;
        private bool impacted;
        private bool enemyParried;
        private DamageContext reflectedDamageContext;
        private Sprite runtimePlaceholderSprite;

        public bool CanBeParried => !impacted
            && currentTeam == ProjectileTeam.Enemy
            && !enemyParried;
        public bool CanBeEnemyParried => !impacted && currentTeam == ProjectileTeam.Player;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileCollider.isTrigger = true;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        public static EnemySlowProjectile CreateRuntimePlaceholder(Vector2 position)
        {
            GameObject projectileObject = new GameObject("Necromancer Slow Bolt");
            projectileObject.transform.position = position;
            Rigidbody2D rigidbody = projectileObject.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            CircleCollider2D circle = projectileObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.13f;
            circle.isTrigger = true;
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 8;
            EnemySlowProjectile projectile = projectileObject.AddComponent<EnemySlowProjectile>();
            projectile.runtimePlaceholderSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                4f);
            renderer.sprite = projectile.runtimePlaceholderSprite;
            return projectile;
        }

        public void Initialize(
            GameObject shooter,
            Vector2 direction,
            float projectileSpeed,
            int resolvedDamage,
            float movementMultiplier,
            float statusDuration,
            float lifetime)
        {
            originalShooter = shooter;
            currentOwner = shooter;
            currentTeam = ProjectileTeam.Enemy;
            movementDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.left;
            speed = Mathf.Max(0.01f, projectileSpeed);
            damage = Mathf.Max(1, resolvedDamage);
            playerMovementMultiplier = Mathf.Clamp(movementMultiplier, 0.1f, 1f);
            slowDuration = Mathf.Max(0.1f, statusDuration);
            impacted = false;
            enemyParried = false;
            reflectedDamageContext = default;
            SetVisualColor(enemyProjectileColor);
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
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Max(1f, damageMultiplier)));
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
            if (impacted || IsOwnedCollider(other) || IsFriendlyEnemyCollider(other))
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
                    bool damageAccepted = player.TryTakeDamage(
                        damage,
                        new DamageContext(gameObject, DamageTrait.Direct | DamageTrait.Projectile));
                    if (currentTeam != ProjectileTeam.Enemy)
                    {
                        return;
                    }

                    if (damageAccepted)
                    {
                        PlayerSlowStatus slow = player.GetComponent<PlayerSlowStatus>();
                        if (slow == null)
                        {
                            slow = player.gameObject.AddComponent<PlayerSlowStatus>();
                        }

                        slow.ApplySlow(playerMovementMultiplier, slowDuration);
                    }

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

                    damageable.TakeDamage(damage, reflectedDamageContext);
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

        private bool IsFriendlyEnemyCollider(Collider2D other)
        {
            return currentTeam == ProjectileTeam.Enemy
                && other.GetComponentInParent<EnemyArchetypeProfile>() != null;
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
            Color impactColor = currentTeam == ProjectileTeam.Player
                ? reflectedProjectileColor
                : enemyProjectileColor;
            AreaPulseEffect.Create(transform.position, 0.38f, impactColor, 0.16f);
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
        }

        private void OnDestroy()
        {
            if (runtimePlaceholderSprite != null)
            {
                Destroy(runtimePlaceholderSprite);
            }
        }
    }
}
