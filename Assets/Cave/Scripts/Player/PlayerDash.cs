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

        public bool IsDashing { get; private set; }
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stamina = GetComponent<SpinSwordAttack>();
            flightBash = GetComponent<PlayerFlightBash>();
            crowdResponse = GetComponent<PlayerCrowdResponse>();
            actionGate = GetComponent<PlayerGuardBreak>();
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
            body.velocity = new Vector2(
                dashDirection * activeDashSpeed,
                body.velocity.y);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.9f);
            return true;
        }

        private void EndDash()
        {
            IsDashing = false;
            body.velocity = new Vector2(0f, body.velocity.y);
        }

        private void OnDisable()
        {
            if (IsDashing && body != null)
            {
                EndDash();
            }
        }
    }
}
