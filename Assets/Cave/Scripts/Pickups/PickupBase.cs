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
            foreach (Collider2D pickupCollider in pickupColliders)
            {
                pickupCollider.enabled = false;
            }

            CaveSfx.Play(CaveSfxCue.Bonus, 0.75f);
            Destroy(gameObject);
        }

        protected abstract bool TryApply(PlayerHealth playerHealth, GameObject playerObject);
    }
}
