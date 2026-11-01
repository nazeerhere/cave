using Cave.Combat;
using Cave.Environmental;
using UnityEngine;

namespace Cave.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class KnockbackReceiver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementLockDuration = 0.25f;
        [SerializeField, Min(0.1f)] private float maximumKnockbackSpeed = 25f;

        private Rigidbody2D body;
        private EnemyController enemyController;
        private FlyingSwarmController flyingController;
        private float knockbackResistance;
        private float teamAuraKnockbackResistance;
        private float structuralImpactUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            enemyController = GetComponent<EnemyController>();
            flyingController = GetComponent<FlyingSwarmController>();
        }

        public void ApplyKnockback(Vector2 velocity)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            float appliedMultiplier = 1f - Mathf.Clamp01(Mathf.Max(
                knockbackResistance,
                teamAuraKnockbackResistance));
            if (enemyController == null)
            {
                enemyController = GetComponent<EnemyController>();
            }

            if (flyingController == null)
            {
                flyingController = GetComponent<FlyingSwarmController>();
            }

            enemyController?.SuspendMovement(movementLockDuration * appliedMultiplier);
            flyingController?.SuspendMovement(movementLockDuration * appliedMultiplier);
            body.velocity = Vector2.ClampMagnitude(
                velocity * appliedMultiplier,
                maximumKnockbackSpeed);
            structuralImpactUntil = Time.time + movementLockDuration;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Time.time > structuralImpactUntil || collision == null || body == null) return;
            float speed = body.velocity.magnitude;
            if (speed <= .1f) return;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            StructuralImpactRouter.Apply(collision.collider, new StructuralImpactContext(
                gameObject, point, body.velocity, speed * .2f,
                new DamageContext(gameObject, DamageTrait.Direct), false, false,
                GetInstanceID() ^ Time.frameCount));
        }

        public void SetKnockbackResistance(float resistance)
        {
            knockbackResistance = Mathf.Clamp(resistance, 0f, 0.95f);
        }

        public void SetTeamAuraKnockbackResistance(float resistance)
        {
            teamAuraKnockbackResistance = Mathf.Clamp(resistance, 0f, 0.95f);
        }
    }
}
