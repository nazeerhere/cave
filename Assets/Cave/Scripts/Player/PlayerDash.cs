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
        private float dashEndsAt;
        private float nextDashTime;
        private PlayerFlightBash flightBash;

        public bool IsDashing { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stamina = GetComponent<SpinSwordAttack>();
            flightBash = GetComponent<PlayerFlightBash>();
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
                if (flightBash == null)
                {
                    flightBash = GetComponent<PlayerFlightBash>();
                }

                if (flightBash != null && flightBash.ShouldHandleDashInput)
                {
                    flightBash.TryStartBash(horizontalInput, facingDirection);
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

            if (Time.time >= dashEndsAt)
            {
                EndDash();
                return;
            }

            body.velocity = new Vector2(dashDirection * dashSpeed, body.velocity.y);
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
            dashEndsAt = Time.time + dashDuration;
            nextDashTime = dashEndsAt + dashCooldown;
            body.velocity = new Vector2(dashDirection * dashSpeed, body.velocity.y);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.75f);
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
