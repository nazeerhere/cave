using Cave.Player;
using UnityEngine;

namespace Cave.Pickups
{
    public sealed class HealthPickup : PickupBase
    {
        [SerializeField, Min(1)] private int amountRestored = 1;

        public void SetAmount(int amount)
        {
            amountRestored = Mathf.Max(1, amount);
        }

        protected override bool TryApply(PlayerHealth playerHealth, GameObject playerObject)
        {
            return playerHealth.RestoreHealth(amountRestored);
        }
    }
}
