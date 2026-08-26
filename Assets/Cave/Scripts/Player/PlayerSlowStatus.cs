using Cave.Enemies;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerHealth))]
    public sealed class PlayerSlowStatus : MonoBehaviour
    {
        private PlayerController playerController;
        private PlayerHealth playerHealth;
        private EnemyStatusVisuals statusVisuals;
        private float activeMovementMultiplier = 1f;
        private float expiresAt;

        public bool IsSlowed => Time.time < expiresAt && activeMovementMultiplier < 1f;
        public float ActiveMovementMultiplier => IsSlowed ? activeMovementMultiplier : 1f;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerHealth = GetComponent<PlayerHealth>();
            statusVisuals = GetComponent<EnemyStatusVisuals>();
            if (statusVisuals == null)
            {
                statusVisuals = gameObject.AddComponent<EnemyStatusVisuals>();
            }

            statusVisuals.Configure(
                Resources.Load<SpecialModeTier2Settings>("SpecialModeTier2Settings"));

            playerHealth.Died += ClearSlow;
        }

        private void Update()
        {
            if (expiresAt > 0f && Time.time >= expiresAt)
            {
                ClearSlow();
            }
        }

        public void ApplySlow(float movementMultiplier, float duration)
        {
            float resolvedMultiplier = Mathf.Clamp(movementMultiplier, 0.1f, 1f);
            if (resolvedMultiplier >= 1f || duration <= 0f)
            {
                return;
            }

            float requestedExpiry = Time.time + duration;
            bool noActiveSlow = !IsSlowed;
            bool strongerSlow = resolvedMultiplier < activeMovementMultiplier;
            bool sameStrength = Mathf.Approximately(resolvedMultiplier, activeMovementMultiplier);
            if (noActiveSlow || strongerSlow)
            {
                activeMovementMultiplier = resolvedMultiplier;
                expiresAt = requestedExpiry;
            }
            else if (sameStrength)
            {
                expiresAt = Mathf.Max(expiresAt, requestedExpiry);
            }
            else
            {
                return;
            }

            playerController.SetStatusMovementMultiplier(activeMovementMultiplier);
            statusVisuals.SetFrostActive(true, false);
        }

        public void ClearSlow()
        {
            activeMovementMultiplier = 1f;
            expiresAt = 0f;
            playerController?.SetStatusMovementMultiplier(1f);
            statusVisuals?.SetFrostActive(false, false);
        }

        private void OnDisable()
        {
            ClearSlow();
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= ClearSlow;
            }
        }
    }
}
