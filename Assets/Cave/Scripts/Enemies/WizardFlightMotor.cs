using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Damageable))]
    public sealed class WizardFlightMotor : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
        [SerializeField, Min(0.1f)] private float acceleration = 16f;
        [SerializeField, Min(0f)] private float arrivalRadius = 0.2f;

        private Rigidbody2D body;
        private SpriteRenderer primaryRenderer;
        private Vector2 destination;
        private bool hasMovementIntent;
        private float movementSpeedMultiplier = 1f;
        private float corruptionSpeedMultiplier = 1f;
        private float difficultySpeedMultiplier = 1f;

        public float BaseMoveSpeed => moveSpeed;
        public bool HasMovementIntent => hasMovementIntent;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            primaryRenderer = GetComponentInChildren<SpriteRenderer>();
            EnemyController legacyGroundMotor = GetComponent<EnemyController>();
            if (legacyGroundMotor != null)
            {
                legacyGroundMotor.enabled = false;
            }

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void FixedUpdate()
        {
            Vector2 desiredVelocity = Vector2.zero;
            if (hasMovementIntent)
            {
                Vector2 offset = destination - body.position;
                if (offset.sqrMagnitude > arrivalRadius * arrivalRadius)
                {
                    desiredVelocity = offset.normalized * moveSpeed
                        * movementSpeedMultiplier
                        * corruptionSpeedMultiplier
                        * difficultySpeedMultiplier;
                }
            }

            body.velocity = Vector2.MoveTowards(
                body.velocity,
                desiredVelocity,
                acceleration * Time.fixedDeltaTime);
            if (primaryRenderer != null && Mathf.Abs(body.velocity.x) > 0.05f)
            {
                primaryRenderer.flipX = body.velocity.x < 0f;
            }
        }

        public void MoveTo(Vector2 worldDestination)
        {
            destination = worldDestination;
            hasMovementIntent = true;
        }

        public void HoldPosition()
        {
            destination = body != null ? body.position : (Vector2)transform.position;
            hasMovementIntent = false;
        }

        public void SetMovementSpeedMultiplier(float multiplier)
        {
            movementSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetCorruptionSpeedMultiplier(float multiplier)
        {
            corruptionSpeedMultiplier = Mathf.Max(0.05f, multiplier);
        }

        public void SetDifficultySpeedMultiplier(float multiplier)
        {
            difficultySpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SuspendMovement(float duration)
        {
            HoldPosition();
        }

        public void TeleportTo(Vector2 position)
        {
            body.position = position;
            body.velocity = Vector2.zero;
            destination = position;
            hasMovementIntent = false;
        }

        public void ResetForRespawn()
        {
            movementSpeedMultiplier = 1f;
            corruptionSpeedMultiplier = 1f;
            hasMovementIntent = false;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void OnDisable()
        {
            ResetForRespawn();
        }
    }
}
