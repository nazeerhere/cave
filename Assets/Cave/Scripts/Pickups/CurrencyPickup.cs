using Cave.Player;
using UnityEngine;

namespace Cave.Pickups
{
    public sealed class CurrencyPickup : PickupBase
    {
        [SerializeField, Min(1)] private int value = 1;

        public void SetAmount(int amount)
        {
            value = Mathf.Max(1, amount);
        }

        protected override bool TryApply(PlayerHealth playerHealth, GameObject playerObject)
        {
            PlayerCurrency playerCurrency = playerObject.GetComponent<PlayerCurrency>();
            if (playerCurrency == null || value <= 0)
            {
                return false;
            }

            playerCurrency.AddCurrency(value);
            return true;
        }
    }
}
