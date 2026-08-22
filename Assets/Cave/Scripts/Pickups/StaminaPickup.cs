using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Pickups
{
    public sealed class StaminaPickup : PickupBase
    {
        [SerializeField, Min(0.01f)] private float amountRestored = 25f;

        protected override bool TryApply(PlayerHealth playerHealth, GameObject playerObject)
        {
            SpinSwordAttack spinSwordAttack = playerObject.GetComponent<SpinSwordAttack>();
            return spinSwordAttack != null && spinSwordAttack.RestoreStamina(amountRestored);
        }
    }
}
