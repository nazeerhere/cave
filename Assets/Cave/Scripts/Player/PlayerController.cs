using Cave.Audio;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 7f;
        [SerializeField, Min(0f)] private float jumpVelocity = 12f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float groundCheckWidth = 0.8f;

        [Header("Jump Request")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.1f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private float horizontalInput;
        private PlayerDash playerDash;
        private PlayerFlight playerFlight;
        private PlayerFlightBash flightBash;
        private bool wasGrounded;
        private bool jumpConsumedForAirborneCycle;
        private bool hasBeenAirborneSinceJump;
        private float jumpRequestExpiresAt = float.NegativeInfinity;
        private float externalMovementLockUntil;
        private PhysicsMaterial2D originalCollisionMaterial;
        private PhysicsMaterial2D runtimeFrictionlessMaterial;

        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            playerDash = GetComponent<PlayerDash>();
            playerFlight = GetComponent<PlayerFlight>();
            flightBash = GetComponent<PlayerFlightBash>();
            ConfigureCollisionMaterial();
            IsGrounded = CheckGrounded();
            wasGrounded = IsGrounded;
        }

        private void Update()
        {
            horizontalInput = GameInput.Horizontal;

            if (GameInput.JumpPressed)
            {
                jumpRequestExpiresAt = Time.time + jumpBufferTime;
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();
            if (!IsGrounded)
            {
                hasBeenAirborneSinceJump = true;
            }
            else if (hasBeenAirborneSinceJump)
            {
                jumpConsumedForAirborneCycle = false;
                hasBeenAirborneSinceJump = false;
            }

            if (!wasGrounded && IsGrounded && body.velocity.y <= 0f)
            {
                CaveSfx.Play(CaveSfxCue.Landing, 0.65f);
            }

            wasGrounded = IsGrounded;
            if (playerDash == null)
            {
                playerDash = GetComponent<PlayerDash>();
            }

            if (flightBash == null)
            {
                flightBash = GetComponent<PlayerFlightBash>();
            }

            bool horizontalOverrideActive = (playerDash != null && playerDash.IsDashing)
                || (flightBash != null && flightBash.IsBashing);
            if (!horizontalOverrideActive && Time.time >= externalMovementLockUntil)
            {
                body.velocity = new Vector2(horizontalInput * moveSpeed, body.velocity.y);
            }

            bool bufferedJumpActive = Time.time <= jumpRequestExpiresAt;
            bool shouldJump = (GameInput.JumpHeld || bufferedJumpActive)
                && IsGrounded
                && !jumpConsumedForAirborneCycle;
            if (shouldJump)
            {
                if (playerFlight == null)
                {
                    playerFlight = GetComponent<PlayerFlight>();
                }

                playerFlight?.StopForGroundJump();
                body.velocity = new Vector2(body.velocity.x, jumpVelocity);
                jumpConsumedForAirborneCycle = true;
                hasBeenAirborneSinceJump = false;
                jumpRequestExpiresAt = float.NegativeInfinity;
                IsGrounded = false;
            }
        }

        public void ApplyExternalKnockback(Vector2 velocity, float controlLockDuration)
        {
            externalMovementLockUntil = Mathf.Max(
                externalMovementLockUntil,
                Time.time + Mathf.Max(0f, controlLockDuration));
            body.velocity = velocity;
        }

        private bool CheckGrounded()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 checkSize = new Vector2(bounds.size.x * groundCheckWidth, groundCheckDistance);
            Vector2 checkCenter = new Vector2(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f);

            return Physics2D.OverlapBox(checkCenter, checkSize, 0f, groundLayer) != null;
        }

        private void ConfigureCollisionMaterial()
        {
            originalCollisionMaterial = bodyCollider.sharedMaterial;
            if (originalCollisionMaterial != null)
            {
                return;
            }

            runtimeFrictionlessMaterial = new PhysicsMaterial2D("Player Runtime Frictionless")
            {
                friction = 0f,
                bounciness = 0f,
                hideFlags = HideFlags.DontSave
            };
            bodyCollider.sharedMaterial = runtimeFrictionlessMaterial;
        }

        private void OnDestroy()
        {
            if (bodyCollider != null && bodyCollider.sharedMaterial == runtimeFrictionlessMaterial)
            {
                bodyCollider.sharedMaterial = originalCollisionMaterial;
            }

            if (runtimeFrictionlessMaterial != null)
            {
                Destroy(runtimeFrictionlessMaterial);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D currentCollider = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
            if (currentCollider == null)
            {
                return;
            }

            Bounds bounds = currentCollider.bounds;
            Vector3 checkSize = new Vector3(bounds.size.x * groundCheckWidth, groundCheckDistance, 0f);
            Vector3 checkCenter = new Vector3(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f, 0f);

            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(checkCenter, checkSize);
        }
    }
}
