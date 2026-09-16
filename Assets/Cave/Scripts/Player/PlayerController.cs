using Cave.Audio;
using Cave.Axioms.Control;
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

        [Header("Landing Diagnostics")]
        [SerializeField] private bool logLandingTransitions;

        [Header("Character Body Contact")]
        [SerializeField] private bool useFrictionlessCharacterContacts = true;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private float horizontalInput;
        private PlayerDash playerDash;
        private PlayerFlight playerFlight;
        private PlayerFlightBash flightBash;
        private PlayerBrace brace;
        private bool wasGrounded;
        private bool jumpConsumedForAirborneCycle;
        private float jumpRequestExpiresAt = float.NegativeInfinity;
        private float externalMovementLockUntil;
        private float statusMovementMultiplier = 1f;
        private float curseMovementMultiplier = 1f;
        private float braceMovementMultiplier = 1f;
        private float temporarySpeedMultiplier = 1f;
        private float temporarySpeedEndsAt;
        private PhysicsMaterial2D originalCollisionMaterial;
        private PhysicsMaterial2D runtimeFrictionlessMaterial;
        private PhysicsMaterial2D runtimeCharacterContactMaterial;
        private bool usingCharacterContactMaterial;
        private Collider2D lastGroundCollider;
        private readonly Collider2D[] groundCheckResults = new Collider2D[8];

        public bool IsGrounded { get; private set; }
        public bool IsExternallyMovementLocked => Time.time < externalMovementLockUntil;
        public float StatusMovementMultiplier => statusMovementMultiplier;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            // Enemy bodies already opt into continuous contacts. The player can
            // reach the same speeds through Dash, knockback, and committed moves.
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            if (GetComponent<PlayerBodyDepenetration>() == null)
            {
                gameObject.AddComponent<PlayerBodyDepenetration>();
            }
            playerDash = GetComponent<PlayerDash>();
            playerFlight = GetComponent<PlayerFlight>();
            flightBash = GetComponent<PlayerFlightBash>();
            brace = GetComponent<PlayerBrace>();
            ConfigureCollisionMaterial();
            IsGrounded = CheckGrounded();
            TryGetGroundSurface(out lastGroundCollider);
            UpdateCharacterBodyFriction();
            wasGrounded = IsGrounded;
        }

        private void Update()
        {
            if (IsExternallyMovementLocked || (brace != null && brace.IsActionLocked))
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
            Collider2D groundCollider;
            IsGrounded = TryGetGroundSurface(out groundCollider);
            if (groundCollider != null)
            {
                lastGroundCollider = groundCollider;
            }
            if (IsGrounded != wasGrounded)
            {
                LogLandingTransition(IsGrounded ? "ground acquired" : "ground lost", groundCollider);
            }

            if (IsGrounded && body.velocity.y <= 0.1f)
            {
                // A valid world-floor contact always closes the previous airborne
                // cycle. Character support/depenetration can otherwise skip the
                // airborne observation that previously released this latch.
                if (jumpConsumedForAirborneCycle)
                {
                    LogLandingTransition("landing latch reset", groundCollider);
                }

                jumpConsumedForAirborneCycle = false;
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
            if (brace == null)
            {
                brace = GetComponent<PlayerBrace>();
            }

            bool braceLocked = brace != null && brace.IsActionLocked;
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
                        * braceMovementMultiplier
                        * temporarySpeedMultiplier,
                    body.velocity.y);
            }

            bool bufferedJumpActive = Time.time <= jumpRequestExpiresAt;
            bool characterEscapeSupport = !IsGrounded && IsStandingOnCharacterBody();
            // A jump begins only from a buffered press.  Treating a held input as
            // a new request makes every landing a new jump after the airborne
            // latch is reset, which is especially visible on polygon terrain.
            bool shouldJump = bufferedJumpActive
                && !IsExternallyMovementLocked
                && !braceLocked
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
                jumpRequestExpiresAt = float.NegativeInfinity;
                IsGrounded = false;
                LogLandingTransition("jump initiated", groundCollider);
                Jumped?.Invoke();
            }
        }

        public void ApplyExternalKnockback(Vector2 velocity, float controlLockDuration)
        {
            externalMovementLockUntil = Mathf.Max(
                externalMovementLockUntil,
                Time.time + Mathf.Max(0f, controlLockDuration));
            AxiomPlayerMovementControl axiomMovement = GetComponent<AxiomPlayerMovementControl>();
            float multiplier = axiomMovement != null
                ? axiomMovement.GetIncomingKnockbackMultiplier()
                : 1f;
            body.velocity = velocity * multiplier;
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
            curseMovementMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void SetBraceMovementMultiplier(float multiplier)
        {
            // Deep Brace intentionally has no movement; Quick and Full remain
            // bounded by their own serialized stage tuning.
            braceMovementMultiplier = Mathf.Clamp01(multiplier);
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

        private void LogLandingTransition(string transition, Collider2D groundCollider)
        {
            if (!logLandingTransitions)
            {
                return;
            }

            Collider2D reportedGround = groundCollider != null ? groundCollider : lastGroundCollider;
            Debug.Log(
                "[Cave][Landing] " + transition
                + " grounded=" + IsGrounded
                + " velocityY=" + (body != null ? body.velocity.y.ToString("0.###") : "n/a")
                + " jumpPressed=" + GameInput.JumpPressed
                + " jumpHeld=" + GameInput.JumpHeld
                + " jumpBuffered=" + (Time.time <= jumpRequestExpiresAt)
                + " jumpConsumed=" + jumpConsumedForAirborneCycle
                + " ground=" + (reportedGround != null ? reportedGround.name : "none")
                + " layer=" + (reportedGround != null ? LayerMask.LayerToName(reportedGround.gameObject.layer) : "none")
                + " trigger=" + (reportedGround != null && reportedGround.isTrigger),
                this);
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

        private void UpdateCharacterBodyFriction()
        {
            if (!useFrictionlessCharacterContacts || bodyCollider == null)
            {
                return;
            }

            bool touchingCharacter = false;
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
            int contactCount = bodyCollider.GetContacts(filter, groundCheckResults);
            for (int index = 0; index < contactCount; index++)
            {
                Collider2D candidate = groundCheckResults[index];
                if (candidate != null && IsCharacterBody(candidate))
                {
                    touchingCharacter = true;
                    break;
                }
            }

            if (touchingCharacter == usingCharacterContactMaterial)
            {
                return;
            }

            usingCharacterContactMaterial = touchingCharacter;
            if (touchingCharacter)
            {
                EnsureCharacterContactMaterial();
                bodyCollider.sharedMaterial = runtimeCharacterContactMaterial;
            }
            else
            {
                bodyCollider.sharedMaterial = originalCollisionMaterial != null
                    ? originalCollisionMaterial
                    : runtimeFrictionlessMaterial;
            }
        }

        private static bool IsCharacterBody(Collider2D candidate)
        {
            return candidate.GetComponentInParent<EnemyController>() != null
                || candidate.GetComponentInParent<EnemyArchetypeProfile>() != null
                || candidate.GetComponentInParent<FlyingSwarmController>() != null;
        }

        private void EnsureCharacterContactMaterial()
        {
            if (runtimeCharacterContactMaterial != null)
            {
                return;
            }

            runtimeCharacterContactMaterial = new PhysicsMaterial2D("Player Character Contact No Friction")
            {
                friction = 0f,
                bounciness = originalCollisionMaterial != null
                    ? originalCollisionMaterial.bounciness
                    : 0f,
                hideFlags = HideFlags.DontSave
            };
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
            if (bodyCollider != null
                && (bodyCollider.sharedMaterial == runtimeFrictionlessMaterial
                    || bodyCollider.sharedMaterial == runtimeCharacterContactMaterial))
            {
                bodyCollider.sharedMaterial = originalCollisionMaterial;
            }

            if (runtimeFrictionlessMaterial != null)
            {
                Destroy(runtimeFrictionlessMaterial);
            }

            if (runtimeCharacterContactMaterial != null)
            {
                Destroy(runtimeCharacterContactMaterial);
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
