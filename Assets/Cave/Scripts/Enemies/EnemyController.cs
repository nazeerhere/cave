using UnityEngine;

namespace Cave.Enemies
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float patrolDistance = 2f;
        [SerializeField] private bool startMovingRight = true;

        private Rigidbody2D body;
        private float startingX;
        private float direction;
        private float movementSuspendedUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            startingX = body.position.x;
            direction = startMovingRight ? 1f : -1f;
        }

        private void FixedUpdate()
        {
            if (Time.time < movementSuspendedUntil)
            {
                return;
            }

            float distanceFromStart = body.position.x - startingX;
            if (distanceFromStart >= patrolDistance)
            {
                direction = -1f;
            }
            else if (distanceFromStart <= -patrolDistance)
            {
                direction = 1f;
            }

            body.velocity = new Vector2(direction * moveSpeed, body.velocity.y);
        }

        public void SuspendMovement(float duration)
        {
            movementSuspendedUntil = Mathf.Max(movementSuspendedUntil, Time.time + duration);
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }
        }

        private void OnDrawGizmosSelected()
        {
            float centerX = Application.isPlaying ? startingX : transform.position.x;
            Vector3 leftLimit = new Vector3(centerX - patrolDistance, transform.position.y, transform.position.z);
            Vector3 rightLimit = new Vector3(centerX + patrolDistance, transform.position.y, transform.position.z);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(leftLimit, rightLimit);
            Gizmos.DrawWireSphere(leftLimit, 0.1f);
            Gizmos.DrawWireSphere(rightLimit, 0.1f);
        }
    }
}
