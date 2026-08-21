using UnityEngine;

namespace Cave.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class KnockbackReceiver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementLockDuration = 0.25f;

        private Rigidbody2D body;
        private EnemyController enemyController;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            enemyController = GetComponent<EnemyController>();
        }

        public void ApplyKnockback(Vector2 velocity)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            enemyController?.SuspendMovement(movementLockDuration);
            body.velocity = velocity;
        }
    }
}
