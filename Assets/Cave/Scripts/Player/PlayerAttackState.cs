using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAttackState : MonoBehaviour
    {
        private SpinSwordAttack spinAttack;
        private ChargedAttack chargedAttack;

        public bool IsActivelyAttacking => (spinAttack != null && spinAttack.IsAttacking)
            || (chargedAttack != null && chargedAttack.IsAttacking);

        private void Awake()
        {
            spinAttack = GetComponent<SpinSwordAttack>();
            chargedAttack = GetComponent<ChargedAttack>();
        }
    }
}
