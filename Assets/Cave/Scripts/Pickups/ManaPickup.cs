using Cave.Player;
using UnityEngine;

namespace Cave.Pickups
{
    public sealed class ManaPickup : PickupBase
    {
        [SerializeField, Min(0.01f)] private float amountRestored = 25f;

        public void SetAmount(float amount)
        {
            amountRestored = Mathf.Max(0.01f, amount);
        }

        protected override bool TryApply(PlayerHealth playerHealth, GameObject playerObject)
        {
            PlayerMana playerMana = playerObject.GetComponent<PlayerMana>();
            return playerMana != null && playerMana.RestoreMana(amountRestored);
        }
    }
}
