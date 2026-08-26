using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        public event System.Action Jumped;

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
        private float statusMovementMultiplier = 1f;
        private float curseMovementMultiplier = 1f;
        private float temporarySpeedMultiplier = 1f;
        private float temporarySpeedEndsAt;
        private PhysicsMaterial2D originalCollisionMaterial;
        private PhysicsMaterial2D runtimeFrictionlessMaterial;
        private readonly Collider2D[] groundCheckResults = new Collider2D[8];

        public bool IsGrounded { get; private set; }
        public bool IsExternallyMovementLocked => Time.time < externalMovementLockUntil;

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
            if (IsExternallyMovementLocked)
            {
                horizontalInput = 0f;
                jumpRequestExpiresAt = float.NegativeInfinity;
                return;
            }

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
                if (temporarySpeedEndsAt > 0f && Time.time >= temporarySpeedEndsAt)
                {
                    temporarySpeedMultiplier = 1f;
                    temporarySpeedEndsAt = 0f;
                }

                body.velocity = new Vector2(
                    horizontalInput
                        * moveSpeed
                        * statusMovementMultiplier
                        * curseMovementMultiplier
                        * temporarySpeedMultiplier,
                    body.velocity.y);
            }

            bool bufferedJumpActive = Time.time <= jumpRequestExpiresAt;
            bool characterEscapeSupport = !IsGrounded && IsStandingOnCharacterBody();
            bool shouldJump = (GameInput.JumpHeld || bufferedJumpActive)
                && !IsExternallyMovementLocked
                && (IsGrounded || characterEscapeSupport)
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
                Jumped?.Invoke();
            }
        }

        public void ApplyExternalKnockback(Vector2 velocity, float controlLockDuration)
        {
            externalMovementLockUntil = Mathf.Max(
                externalMovementLockUntil,
                Time.time + Mathf.Max(0f, controlLockDuration));
            body.velocity = velocity;
        }

        public void ApplyExternalControlLock(float duration)
        {
            externalMovementLockUntil = Mathf.Max(
                externalMovementLockUntil,
                Time.time + Mathf.Max(0f, duration));
            if (body != null)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
            }
        }

        public void SetStatusMovementMultiplier(float multiplier)
        {
            statusMovementMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        }

        public void SetCurseMovementMultiplier(float multiplier)
        {
            curseMovementMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        }

        public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
        {
            temporarySpeedMultiplier = Mathf.Max(1f, multiplier);
            temporarySpeedEndsAt = Mathf.Max(
                temporarySpeedEndsAt,
                Time.time + Mathf.Max(0f, duration));
        }

        public bool IsValidGroundCollider(Collider2D candidate)
        {
            return candidate != null
                && !candidate.isTrigger
                && !candidate.transform.IsChildOf(transform)
                && candidate.GetComponentInParent<PlayerController>() == null
                && candidate.GetComponentInParent<EnemyController>() == null
                && candidate.GetComponentInParent<EnemyArchetypeProfile>() == null
                && candidate.GetComponentInParent<FlyingSwarmController>() == null
                && candidate.GetComponentInParent<Damageable>() == null
                && (groundLayer.value & (1 << candidate.gameObject.layer)) != 0;
        }

        public bool TryGetGroundSurface(out Collider2D groundSurface)
        {
            GetGroundCheckGeometry(out Vector2 checkCenter, out Vector2 checkSize);
            int resultCount = Physics2D.OverlapBoxNonAlloc(
                checkCenter,
                checkSize,
                0f,
                groundCheckResults,
                groundLayer);
            for (int index = 0; index < resultCount; index++)
            {
                Collider2D candidate = groundCheckResults[index];
                if (IsValidGroundCollider(candidate))
                {
                    groundSurface = candidate;
                    return true;
                }
            }

            groundSurface = null;
            return false;
        }

        private bool CheckGrounded()
        {
            return TryGetGroundSurface(out _);
        }

        private bool IsStandingOnCharacterBody()
        {
            if (body == null || body.velocity.y > 0.1f)
            {
                return false;
            }

            GetGroundCheckGeometry(out Vector2 checkCenter, out Vector2 checkSize);
            int resultCount = Physics2D.OverlapBoxNonAlloc(
                checkCenter,
                checkSize,
                0f,
                groundCheckResults,
                Physics2D.AllLayers);
            for (int index = 0; index < resultCount; index++)
            {
                Collider2D candidate = groundCheckResults[index];
                if (candidate == null
                    || candidate.isTrigger
                    || candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (candidate.GetComponentInParent<EnemyController>() != null
                    || candidate.GetComponentInParent<EnemyArchetypeProfile>() != null
                    || candidate.GetComponentInParent<FlyingSwarmController>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void GetGroundCheckGeometry(out Vector2 checkCenter, out Vector2 checkSize)
        {
            Bounds bounds = bodyCollider.bounds;
            checkSize = new Vector2(bounds.size.x * groundCheckWidth, groundCheckDistance);
            checkCenter = new Vector2(
                bounds.center.x,
                bounds.min.y - groundCheckDistance * 0.5f);
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
