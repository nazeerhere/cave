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
        private int poisonTickDamage;
        private float poisonInterval;
        private float poisonDuration;
        private bool impacted;
        private bool enemyParried;
        private DamageContext reflectedDamageContext;

        public bool CanBeParried => !impacted
            && currentTeam == ProjectileTeam.Enemy
            && !enemyParried;
        public bool CanBeEnemyParried => !impacted && currentTeam == ProjectileTeam.Player;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileCollider.isTrigger = true;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        public void Initialize(
            GameObject shooter,
            Vector2 direction,
            float projectileSpeed,
            int resolvedDirectDamage,
            int resolvedPoisonDamage,
            float tickInterval,
            float statusDuration,
            float lifetime)
        {
            originalShooter = shooter;
            currentOwner = shooter;
            currentTeam = ProjectileTeam.Enemy;
            movementDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.left;
            speed = Mathf.Max(0.01f, projectileSpeed);
            directDamage = Mathf.Max(1, resolvedDirectDamage);
            poisonTickDamage = Mathf.Max(1, resolvedPoisonDamage);
            poisonInterval = Mathf.Max(0.05f, tickInterval);
            poisonDuration = Mathf.Max(0.05f, statusDuration);
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
                        new DamageContext(gameObject, DamageTrait.Direct | DamageTrait.Projectile));
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
                        poisonTickDamage,
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
            Cave.Combat.AreaPulseEffect.Create(
                transform.position,
                0.45f,
                enemyProjectileColor,
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
        }
    }
}
