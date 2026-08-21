using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Projectiles
{
    public enum ProjectileTeam
    {
        Enemy,
        Player
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class FireballProjectile : MonoBehaviour
    {
        [Header("Impact")]
        [SerializeField, Min(0.01f)] private float impactVisualLifetime = 0.25f;
        [SerializeField, Min(1f)] private float impactEndScale = 3f;

        private Rigidbody2D body;
        private Collider2D projectileCollider;
        private SpriteRenderer projectileRenderer;
        private GameObject originalShooter;
        private GameObject currentOwner;
        private float speed;
        private bool explosionStarted;

        public GameObject OriginalShooter => originalShooter;
        public GameObject CurrentOwner => currentOwner;
        public ProjectileTeam CurrentTeam { get; private set; }
        public Vector2 MovementDirection { get; private set; }
        public int Damage { get; private set; }
        public bool IsParried { get; private set; }
        public bool CanBeParried => !explosionStarted && CurrentTeam == ProjectileTeam.Enemy && !IsParried;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(
            GameObject shooter,
            Vector2 direction,
            float projectileSpeed,
            int damage,
            float lifetime)
        {
            originalShooter = shooter;
            currentOwner = shooter;
            CurrentTeam = ProjectileTeam.Enemy;
            MovementDirection = direction.normalized;
            speed = projectileSpeed;
            Damage = damage;
            body.velocity = MovementDirection * speed;
            Destroy(gameObject, lifetime);
        }

        public bool Parry(GameObject newOwner, Vector2 fallbackDirection, float speedMultiplier)
        {
            if (!CanBeParried)
            {
                return false;
            }

            Vector2 reflectedDirection = -MovementDirection;
            if (originalShooter != null && originalShooter.activeInHierarchy)
            {
                Vector2 directionToShooter = originalShooter.transform.position - transform.position;
                if (directionToShooter.sqrMagnitude > 0.001f)
                {
                    reflectedDirection = directionToShooter.normalized;
                }
            }
            else if (fallbackDirection.sqrMagnitude > 0.001f)
            {
                reflectedDirection = fallbackDirection.normalized;
            }

            currentOwner = newOwner;
            CurrentTeam = ProjectileTeam.Player;
            IsParried = true;
            MovementDirection = reflectedDirection;
            speed *= speedMultiplier;
            body.velocity = MovementDirection * speed;
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log(
                $"Fireball touched: {other.name}, " + 
                $"layer={LayerMask.LayerToName(other.gameObject.layer)}, " + $"trigger={other.isTrigger}"
            );
            if (explosionStarted)
            {
                return;
            }

            ParryHitbox parryHitbox = other.GetComponent<ParryHitbox>();
            if (parryHitbox != null)
            {
                parryHitbox.TryParry(this);
                return;
            }

            if (IsIgnoredAttackHitbox(other))
            {
                return;
            }

            if (IsOwnedCollider(other))
            {
                return;
            }

            if (CurrentTeam == ProjectileTeam.Enemy)
            {
                PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TryTakeDamage(Damage);
                    BeginExplosion();
                    return;
                }

                if (other.GetComponentInParent<Damageable>() != null)
                {
                    return;
                }
            }
            else
            {
                Damageable damageable = other.GetComponentInParent<Damageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(Damage);
                    BeginExplosion();
                    return;
                }
            }

            if (!other.isTrigger)
            {
                BeginExplosion();
            }
        }

        private static bool IsIgnoredAttackHitbox(Collider2D other)
        {
            if (other.GetComponent<ChargedAttackHitbox>() != null)
            {
                return true;
            }

            SpinSwordAttack swordAttack = other.GetComponentInParent<SpinSwordAttack>();
            return swordAttack != null && swordAttack.UsesAttackCollider(other);
        }

        private void BeginExplosion()
        {
            Debug.Log("Fireball explosion triggered");
            if (explosionStarted) 
            {
                return;
            }

            explosionStarted = true;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            projectileCollider.enabled = false;

            if (projectileRenderer != null)
            {
                FireballImpactEffect.Create(
                    projectileRenderer,
                    impactVisualLifetime,
                    impactEndScale);
                projectileRenderer.enabled = false;
            }

            Destroy(gameObject);
        }

        private bool IsOwnedCollider(Collider2D other)
        {
            return currentOwner != null && other.transform.root == currentOwner.transform.root;
        }
    }
}
