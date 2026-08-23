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
        private float movementSpeedMultiplier = 1f;
        private float difficultySpeedMultiplier = 1f;
        private float archetypeSpeedMultiplier = 1f;
        private float supportSpeedMultiplier = 1f;
        private float brainSpeedMultiplier = 1f;
        private float combatMovementDirection;
        private float combatMovementUntil;

        public float BaseMoveSpeed => moveSpeed;

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
                body.velocity = new Vector2(0f, body.velocity.y);
                return;
            }

            if (Time.time < combatMovementUntil)
            {
                body.velocity = new Vector2(
                    combatMovementDirection * GetCurrentMoveSpeed(),
                    body.velocity.y);
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

            body.velocity = new Vector2(
                direction * GetCurrentMoveSpeed(),
                body.velocity.y);
        }

        public void SuspendMovement(float duration)
        {
            movementSuspendedUntil = Mathf.Max(movementSuspendedUntil, Time.time + duration);
            if (body != null)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
            }
        }

        public void SetCombatMovementIntent(float horizontalDirection, float duration)
        {
            combatMovementDirection = Mathf.Clamp(horizontalDirection, -1f, 1f);
            combatMovementUntil = Mathf.Max(combatMovementUntil, Time.time + Mathf.Max(0f, duration));
        }

        private float GetCurrentMoveSpeed()
        {
            return moveSpeed
                * difficultySpeedMultiplier
                * archetypeSpeedMultiplier
                * supportSpeedMultiplier
                * brainSpeedMultiplier
                * movementSpeedMultiplier;
        }

        public void SetMovementSpeedMultiplier(float multiplier)
        {
            movementSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetDifficultySpeedMultiplier(float multiplier)
        {
            difficultySpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetArchetypeSpeedMultiplier(float multiplier)
        {
            archetypeSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetSupportSpeedMultiplier(float multiplier)
        {
            supportSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetBrainMoveSpeed(float requestedSpeed)
        {
            brainSpeedMultiplier = moveSpeed > 0f
                ? Mathf.Max(0f, requestedSpeed) / moveSpeed
                : 0f;
        }

        public void ClearBrainMovement()
        {
            combatMovementDirection = 0f;
            combatMovementUntil = 0f;
            brainSpeedMultiplier = 1f;
        }

        public void ResetForRespawn()
        {
            startingX = body.position.x;
            direction = startMovingRight ? 1f : -1f;
            movementSuspendedUntil = 0f;
            combatMovementDirection = 0f;
            combatMovementUntil = 0f;
            movementSpeedMultiplier = 1f;
            supportSpeedMultiplier = 1f;
            brainSpeedMultiplier = 1f;
            combatMovementDirection = 0f;
            combatMovementUntil = 0f;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private void OnDisable()
        {
            movementSpeedMultiplier = 1f;
            supportSpeedMultiplier = 1f;
            brainSpeedMultiplier = 1f;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
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
