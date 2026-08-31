using Cave.Audio;
using Cave.Combat;
using Cave.InputSystem;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerController), typeof(SpinSwordAttack))]
    public sealed class PlayerDash : MonoBehaviour
    {
        [Header("Dash")]
        [SerializeField, Min(0f)] private float staminaCost = 30f;
        [SerializeField, Min(0f)] private float dashSpeed = 18f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.15f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.25f;

        [Header("Cross Step Follow-up")]
        [SerializeField, Min(0.05f)] private float crossStepFollowUpWindow = 0.55f;
        [SerializeField, Min(0.1f)] private float crossStepSpeed = 22f;
        [SerializeField, Min(0f)] private float crossStepExtraClearance = 0.6f;
        [SerializeField, Min(0f)] private float crossStepBlockPushback = 0.5f;
        [SerializeField, Min(0f)] private float crossStepBlockRecovery = 0.2f;

        private Rigidbody2D body;
        private SpinSwordAttack stamina;
        private float facingDirection = 1f;
        private float dashDirection;
        private float activeDashSpeed;
        private float dashEndsAt;
        private float nextDashTime;
        private PlayerFlightBash flightBash;
        private PlayerCrowdResponse crowdResponse;
        private PlayerGuardBreak actionGate;
        private PlayerController controller;
        private Collider2D bodyCollider;
        private CommittedAttackCollisionPhasing collisionPhasing;
        private PlayerCrossStep crossStep;

        public bool IsDashing { get; private set; }
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;

        internal void SetFacingDirection(float direction)
        {
            if (Mathf.Abs(direction) > 0.001f)
            {
                facingDirection = Mathf.Sign(direction);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stamina = GetComponent<SpinSwordAttack>();
            flightBash = GetComponent<PlayerFlightBash>();
            crowdResponse = GetComponent<PlayerCrowdResponse>();
            actionGate = GetComponent<PlayerGuardBreak>();
            controller = GetComponent<PlayerController>();
            bodyCollider = GetComponent<Collider2D>();
            collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
            if (collisionPhasing == null)
            {
                collisionPhasing = gameObject.AddComponent<CommittedAttackCollisionPhasing>();
            }

            crossStep = GetComponent<PlayerCrossStep>();
            if (crossStep == null)
            {
                crossStep = gameObject.AddComponent<PlayerCrossStep>();
            }

            crossStep.Configure(
                crossStepFollowUpWindow,
                crossStepSpeed,
                crossStepExtraClearance,
                crossStepBlockPushback,
                crossStepBlockRecovery);
        }

        internal void Configure(ProgressionDifficultySettings settings)
        {
            if (settings == null)
            {
                return;
            }

            staminaCost = settings.DashStaminaCost;
            dashSpeed = settings.DashSpeed;
            dashDuration = settings.DashDuration;
            dashCooldown = settings.DashCooldown;
        }

        private void Update()
        {
            float horizontalInput = GameInput.Horizontal;
            if (!Mathf.Approximately(horizontalInput, 0f))
            {
                facingDirection = Mathf.Sign(horizontalInput);
            }

            if (GameInput.DashPressed)
            {
                PlayerGuardBreak guardBreak = GetComponent<PlayerGuardBreak>();
                if (guardBreak != null && !guardBreak.CanUseCombatActions)
                {
                    if (crossStep != null
                        && crossStep.CanStartDuringCurrentActionLock
                        && crossStep.TryStart())
                    {
                        return;
                    }

                    return;
                }

                if (crowdResponse == null)
                {
                    crowdResponse = GetComponent<PlayerCrowdResponse>();
                }

                if (crowdResponse != null
                    && crowdResponse.TrySlip(horizontalInput, facingDirection))
                {
                    return;
                }

                if (crossStep != null && crossStep.TryStart())
                {
                    return;
                }

                TryStartDash(horizontalInput);
            }
        }

        private void FixedUpdate()
        {
            if (!IsDashing)
            {
                return;
            }

            if (actionGate != null && !actionGate.CanUseCombatActions)
            {
                EndDash();
                return;
            }

            if (Time.time >= dashEndsAt)
            {
                EndDash();
                return;
            }

            body.velocity = new Vector2(dashDirection * activeDashSpeed, body.velocity.y);
        }

        public bool TryStartDash(float horizontalInput)
        {
            if (flightBash == null)
            {
                flightBash = GetComponent<PlayerFlightBash>();
            }

            if (IsDashing
                || (flightBash != null && flightBash.IsBashing)
                || (controller != null && controller.IsExternallyMovementLocked)
                || Time.time < nextDashTime
                || stamina == null
                || !stamina.TrySpendStamina(staminaCost))
            {
                return false;
            }

            dashDirection = Mathf.Approximately(horizontalInput, 0f)
                ? facingDirection
                : Mathf.Sign(horizontalInput);
            facingDirection = dashDirection;
            IsDashing = true;
            activeDashSpeed = dashSpeed;
            dashEndsAt = Time.time + dashDuration;
            nextDashTime = dashEndsAt + dashCooldown;
            // Normal Dodge deliberately keeps character bodies solid. Target-only
            // Cross Step owns its own, earned collision exception.
            body.velocity = new Vector2(dashDirection * dashSpeed, body.velocity.y);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.75f);
            return true;
        }

        public bool TryStartSpecialDash(
            float horizontalInput,
            float fallbackFacing,
            float speedMultiplier,
            float duration)
        {
            if (IsDashing || (flightBash != null && flightBash.IsBashing))
            {
                return false;
            }

            dashDirection = Mathf.Approximately(horizontalInput, 0f)
                ? Mathf.Approximately(fallbackFacing, 0f) ? facingDirection : Mathf.Sign(fallbackFacing)
                : Mathf.Sign(horizontalInput);
            facingDirection = dashDirection;
            IsDashing = true;
            activeDashSpeed = dashSpeed * Mathf.Max(0.1f, speedMultiplier);
            dashEndsAt = Time.time + Mathf.Max(0.01f, duration);
            nextDashTime = Mathf.Max(nextDashTime, dashEndsAt + dashCooldown);
            // Defensive Slip manages its dedicated collision window itself.
            body.velocity = new Vector2(
                dashDirection * activeDashSpeed,
                body.velocity.y);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.9f);
            return true;
        }

        private void EndDash()
        {
            IsDashing = false;
            collisionPhasing?.End();
            body.velocity = new Vector2(0f, body.velocity.y);
        }

        private void OnDisable()
        {
            if (IsDashing && body != null)
            {
                EndDash();
            }
            collisionPhasing?.End();
        }

        private void OnDestroy()
        {
            collisionPhasing?.End();
        }
    }
}
