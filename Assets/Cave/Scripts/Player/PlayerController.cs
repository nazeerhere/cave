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

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private float horizontalInput;
        private bool jumpRequested;
        private PlayerDash playerDash;
        private PlayerFlightBash flightBash;
        private bool wasGrounded;

        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            playerDash = GetComponent<PlayerDash>();
            flightBash = GetComponent<PlayerFlightBash>();
            IsGrounded = CheckGrounded();
            wasGrounded = IsGrounded;
        }

        private void Update()
        {
            horizontalInput = GameInput.Horizontal;

            if (GameInput.JumpPressed)
            {
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();
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
            if (!horizontalOverrideActive)
            {
                body.velocity = new Vector2(horizontalInput * moveSpeed, body.velocity.y);
            }

            if (jumpRequested && IsGrounded)
            {
                body.velocity = new Vector2(body.velocity.x, jumpVelocity);
                IsGrounded = false;
            }

            jumpRequested = false;
        }

        private bool CheckGrounded()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 checkSize = new Vector2(bounds.size.x * groundCheckWidth, groundCheckDistance);
            Vector2 checkCenter = new Vector2(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f);

            return Physics2D.OverlapBox(checkCenter, checkSize, 0f, groundLayer) != null;
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
