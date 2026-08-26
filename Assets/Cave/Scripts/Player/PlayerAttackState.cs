using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAttackState : MonoBehaviour
    {
        private SpinSwordAttack spinAttack;
        private ChargedAttack chargedAttack;
        private PlayerGuardBreak guardBreak;
        private SidewaysParryAttack defense;
        private PlayerCrowdResponse crowdResponse;

        public bool IsActivelyAttacking => (spinAttack != null && spinAttack.IsAttacking)
            || (chargedAttack != null && chargedAttack.IsAttacking)
            || (guardBreak != null && guardBreak.IsGuardBreaking);
        public bool IsDefending => defense != null && defense.IsGuardHeld;
        public bool IsSlipping => crowdResponse != null && crowdResponse.IsSlipping;

        private void Awake()
        {
            spinAttack = GetComponent<SpinSwordAttack>();
            chargedAttack = GetComponent<ChargedAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            defense = GetComponent<SidewaysParryAttack>();
            crowdResponse = GetComponent<PlayerCrowdResponse>();
        }

        private void Start()
        {
            guardBreak = GetComponent<PlayerGuardBreak>();
            defense = GetComponent<SidewaysParryAttack>();
            crowdResponse = GetComponent<PlayerCrowdResponse>();
        }
    }
}
