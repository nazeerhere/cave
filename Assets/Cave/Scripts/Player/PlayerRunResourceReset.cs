using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerRunResourceReset : MonoBehaviour
    {
        private PlayerHealth health;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += ResetRunState;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= ResetRunState;
            }
        }

        private void ResetRunState()
        {
            GetComponent<PlayerMana>()?.ResetCurrentMana();
            GetComponent<SpinSwordAttack>()?.ResetCurrentStamina();
            PlayerCurseController curses = GetComponent<PlayerCurseController>();
            if (curses != null)
            {
                curses.ApplyDeathCurrencyRule();
            }
            else
            {
                GetComponent<PlayerCurrency>()?.ResetRunCurrency();
            }
            GetComponent<PlayerLandmineInventory>()?.ResetRunInventory();
            GetComponent<PlayerSpecialMode>()?.ResetRunMode();
            GetComponent<PlayerRecoveryModifiers>()?.ClearTemporaryModifiers();
            GetComponent<PlayerSlowStatus>()?.ClearSlow();
            GetComponent<PlayerStunStatus>()?.ClearStun();
            CursedDistraction.ClearOwnedBy(gameObject);
        }
    }
}
