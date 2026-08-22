using Cave.Combat;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerController))]
    [RequireComponent(typeof(PlayerMana), typeof(PlayerSpecialMode), typeof(PlayerHealth))]
    public sealed class PlayerFlight : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float manaDrainPerSecond = 20f;
        [SerializeField, Range(0f, 1f)] private float gravityMultiplier = 0.2f;
        [SerializeField, Min(0f)] private float maximumFallSpeed = 1f;

        private Rigidbody2D body;
        private PlayerController playerController;
        private PlayerMana playerMana;
        private PlayerSpecialMode specialMode;
        private PlayerHealth playerHealth;
        private PlayerSpecialModeUpgradeState upgradeState;
        private float originalGravityScale;

        public bool IsFlying { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            playerController = GetComponent<PlayerController>();
            playerMana = GetComponent<PlayerMana>();
            specialMode = GetComponent<PlayerSpecialMode>();
            playerHealth = GetComponent<PlayerHealth>();
            upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            originalGravityScale = body.gravityScale;
        }

        private void OnEnable()
        {
            specialMode.SpecialModeChanged += HandleModeChanged;
            playerHealth.Died += ResetFlight;
            playerHealth.Respawned += ResetFlight;
        }

        private void Update()
        {
            bool shouldFly = specialMode.CurrentMode == SpecialMode.Flight
                && !playerController.IsGrounded
                && GameInput.JumpHeld
                && playerMana.CurrentMana > 0f;

            if (!shouldFly)
            {
                StopFlying();
                return;
            }

            if (!IsFlying)
            {
                IsFlying = true;
                body.gravityScale = originalGravityScale * gravityMultiplier;
            }

            float requestedDrain = manaDrainPerSecond * GetTierDrainMultiplier() * Time.deltaTime;
            float actualDrain = Mathf.Min(requestedDrain, playerMana.CurrentMana);
            if (actualDrain > 0f)
            {
                playerMana.TrySpendMana(actualDrain);
            }

            if (playerMana.CurrentMana <= 0f)
            {
                StopFlying();
            }
        }

        private float GetTierDrainMultiplier()
        {
            if (upgradeState == null)
            {
                upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            if (upgradeState == null || upgradeState.Settings == null)
            {
                return 1f;
            }

            float efficiencyBonus = upgradeState.IsTier3Owned(SpecialMode.Flight)
                ? upgradeState.Settings.FlightTier3EfficiencyBonus
                : upgradeState.IsTier2Owned(SpecialMode.Flight)
                    ? upgradeState.Settings.FlightTier2EfficiencyBonus
                    : 0f;
            return Mathf.Clamp01(1f - efficiencyBonus);
        }

        private void FixedUpdate()
        {
            if (IsFlying && body.velocity.y < -maximumFallSpeed)
            {
                body.velocity = new Vector2(body.velocity.x, -maximumFallSpeed);
            }
        }

        private void HandleModeChanged(SpecialMode newMode)
        {
            if (newMode != SpecialMode.Flight)
            {
                StopFlying();
            }
        }

        private void StopFlying()
        {
            if (!IsFlying && Mathf.Approximately(body.gravityScale, originalGravityScale))
            {
                return;
            }

            IsFlying = false;
            body.gravityScale = originalGravityScale;
        }

        private void ResetFlight()
        {
            StopFlying();
        }

        private void OnDisable()
        {
            if (specialMode != null)
            {
                specialMode.SpecialModeChanged -= HandleModeChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= ResetFlight;
                playerHealth.Respawned -= ResetFlight;
            }

            if (body != null)
            {
                IsFlying = false;
                body.gravityScale = originalGravityScale;
            }
        }
    }
}
