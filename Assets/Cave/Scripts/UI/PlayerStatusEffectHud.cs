using Cave.Enemies;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerStatusEffectHud : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private GameObject[] slots;
        private Text[] labels;
        private PlayerPoisonStatus poison;
        private PlayerSlowStatus slow;
        private PlayerStunStatus stun;
        private PlayerRecoveryModifiers recovery;
        private PlayerMana mana;
        private DetectiveEncounterCoordinator research;
        private GameObject playerObject;
        private float nextRefreshTime;

        public void Configure(GameObject[] statusSlots, Text[] statusLabels, GameObject player)
        {
            slots = statusSlots;
            labels = statusLabels;
            playerObject = player;
            if (player != null)
            {
                poison = player.GetComponent<PlayerPoisonStatus>();
                slow = player.GetComponent<PlayerSlowStatus>();
                stun = player.GetComponent<PlayerStunStatus>();
                recovery = player.GetComponent<PlayerRecoveryModifiers>();
                mana = player.GetComponent<PlayerMana>();
            }

            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            if (research == null)
            {
                research = FindObjectOfType<DetectiveEncounterCoordinator>();
            }

            if (playerObject != null)
            {
                poison = poison != null ? poison : playerObject.GetComponent<PlayerPoisonStatus>();
                slow = slow != null ? slow : playerObject.GetComponent<PlayerSlowStatus>();
                stun = stun != null ? stun : playerObject.GetComponent<PlayerStunStatus>();
                recovery = recovery != null
                    ? recovery
                    : playerObject.GetComponent<PlayerRecoveryModifiers>();
                mana = mana != null ? mana : playerObject.GetComponent<PlayerMana>();
            }

            Refresh();
        }

        private void Refresh()
        {
            int index = 0;
            AddStatus(ref index, poison != null && poison.IsPoisoned, "☠", "POISON");
            AddStatus(ref index, slow != null && slow.IsSlowed, "❄", "SLOW");
            AddStatus(ref index, stun != null && stun.IsStunned, "!", "STUN");
            bool towerInterference = playerObject != null
                && DetectiveTower.IsPlayerInsideAnyTower(playerObject.transform.position);
            AddStatus(
                ref index,
                towerInterference
                    && ((recovery != null && recovery.HasRecoverySuppression)
                        || (mana != null && mana.HasManaCostPenalty)),
                "↓",
                "TOWER INTERFERENCE");
            AddStatus(ref index, research != null && research.IsActivelyBeingStudied, "◉", "STUDIED");

            while (slots != null && index < slots.Length)
            {
                slots[index++].SetActive(false);
            }
        }

        private void AddStatus(ref int index, bool active, string icon, string tooltipLabel)
        {
            if (!active || slots == null || labels == null || index >= slots.Length)
            {
                return;
            }

            slots[index].SetActive(true);
            labels[index].text = icon;
            slots[index].name = "Status " + tooltipLabel;
            index++;
        }
    }
}
