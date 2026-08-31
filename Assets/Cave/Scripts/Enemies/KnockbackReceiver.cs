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

            float appliedMultiplier = 1f - Mathf.Clamp01(knockbackResistance);
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
        }

        public void SetKnockbackResistance(float resistance)
        {
            knockbackResistance = Mathf.Clamp(resistance, 0f, 0.95f);
        }
    }
}
