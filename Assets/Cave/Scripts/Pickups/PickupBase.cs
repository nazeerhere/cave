using Cave.Audio;
using Cave.Player;
using UnityEngine;

namespace Cave.Pickups
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class PickupBase : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float lifetime = 15f;

        private Collider2D[] pickupColliders;
        private bool isConsumed;
        private CurseAltarZone configuredAvariceZone;
        private float configuredAvariceDepositMultiplier = 1f;

        protected virtual void Awake()
        {
            pickupColliders = GetComponents<Collider2D>();
            foreach (Collider2D pickupCollider in pickupColliders)
            {
                pickupCollider.isTrigger = true;
            }

            if (lifetime > 0f)
            {
                Destroy(gameObject, lifetime);
            }
        }

        private void Start()
        {
            // Drop configuration (including currency amount) is completed by the
            // spawner before Start, so the altar receives the authoritative value.
            bool absorbed = configuredAvariceZone != null
                ? CurseAltarZone.TryAbsorbPickup(
                    this,
                    configuredAvariceZone,
                    configuredAvariceDepositMultiplier)
                : CurseAltarZone.TryAbsorbPickup(this);
            if (!isConsumed && absorbed)
            {
                ConsumeWithoutPlayerPickup();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isConsumed)
            {
                return;
            }

            // Only the player's root body collider has PlayerHealth directly on it.
            // Child sword, charged-attack, and parry colliders are intentionally rejected.
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null || !TryApply(playerHealth, other.gameObject))
            {
                return;
            }

            isConsumed = true;
            DisablePickupColliders();

            CaveSfx.Play(CaveSfxCue.Bonus, 0.75f);
            Destroy(gameObject);
        }

        protected abstract bool TryApply(PlayerHealth playerHealth, GameObject playerObject);

        public virtual bool TryGetAltarValue(out int value)
        {
            value = 0;
            return false;
        }

        /// <summary>Assigns a death-owned Avarice deposit before Start so the
        /// pickup cannot be captured by a different overlapping altar.</summary>
        public void ConfigureAvariceDeposit(CurseAltarZone zone, float multiplier)
        {
            configuredAvariceZone = zone;
            configuredAvariceDepositMultiplier = Mathf.Max(0f, multiplier);
        }

        private void ConsumeWithoutPlayerPickup()
        {
            isConsumed = true;
            DisablePickupColliders();
            Destroy(gameObject);
        }

        private void DisablePickupColliders()
        {
            foreach (Collider2D pickupCollider in pickupColliders)
            {
                pickupCollider.enabled = false;
            }
        }
    }
}
