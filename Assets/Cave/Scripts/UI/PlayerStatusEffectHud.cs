using Cave.Enemies;
using Cave.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerStatusEffectHud : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private GameObject[] slots;
        private Text[] labels;
        private StatusIconTooltip[] tooltips;
        private StatusTooltipPanel tooltipPanel;
        private PlayerPoisonStatus poison;
        private PlayerSlowStatus slow;
        private PlayerStunStatus stun;
        private PlayerRecoveryModifiers recovery;
        private PlayerMana mana;
        private DetectiveEncounterCoordinator research;
        private PlayerWarpStatus warp;
        private PlayerBrace brace;
        private GameObject playerObject;
        private float nextRefreshTime;

        public void Configure(
            GameObject[] statusSlots,
            Text[] statusLabels,
            GameObject player,
            StatusTooltipPanel sharedTooltipPanel)
        {
            slots = statusSlots;
            labels = statusLabels;
            tooltipPanel = sharedTooltipPanel;
            tooltips = new StatusIconTooltip[slots != null ? slots.Length : 0];
            for (int index = 0; slots != null && index < slots.Length; index++)
            {
                tooltips[index] = slots[index].GetComponent<StatusIconTooltip>();
                if (tooltips[index] == null)
                {
                    tooltips[index] = slots[index].AddComponent<StatusIconTooltip>();
                }

                tooltips[index].Bind(tooltipPanel);
            }

            playerObject = player;
            if (player != null)
            {
                poison = player.GetComponent<PlayerPoisonStatus>();
                slow = player.GetComponent<PlayerSlowStatus>();
                stun = player.GetComponent<PlayerStunStatus>();
                recovery = player.GetComponent<PlayerRecoveryModifiers>();
                mana = player.GetComponent<PlayerMana>();
                warp = player.GetComponent<PlayerWarpStatus>();
                brace = player.GetComponent<PlayerBrace>();
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
                warp = warp != null ? warp : playerObject.GetComponent<PlayerWarpStatus>();
                brace = brace != null ? brace : playerObject.GetComponent<PlayerBrace>();
            }

            Refresh();
        }

        private void Refresh()
        {
            int index = 0;
            AddStatus(ref index, poison != null && poison.IsPoisoned, "☠", "POISON",
                "Deals damage over time.");
            AddStatus(ref index, slow != null && slow.IsSlowed, "❄", "SLOW",
                "Reduces movement speed.");
            AddStatus(ref index, stun != null && stun.IsStunned, "!", "STUN",
                "Temporarily prevents movement and combat actions.",
                stun != null ? stun.CurrentStunRemaining : 0f);
            AddStatus(ref index, brace != null && brace.IsBraced, "◆", "BRACED",
                "Settled Guard stance: move slowly, recover Stamina, and watch for a Deflect.");
            bool towerInterference = playerObject != null
                && DetectiveTower.IsPlayerInsideAnyTower(playerObject.transform.position);
            AddStatus(
                ref index,
                towerInterference
                    && ((recovery != null && recovery.HasRecoverySuppression)
                        || (mana != null && mana.HasManaCostPenalty)),
                "↓",
                "TOWER INTERFERENCE",
                "Suppresses recovery, raises Mana costs, and reduces ordinary drops inside the field.");
            AddStatus(ref index, research != null && research.IsActivelyBeingStudied, "◉", "STUDIED",
                "A Detective is recording and predicting your combat actions.");
            AddStatus(ref index, warp != null && warp.IsMarked, "↯", "WARP MARK",
                "A Wizard has marked you for a position swap.");

            while (slots != null && index < slots.Length)
            {
                slots[index++].SetActive(false);
            }
        }

        private void AddStatus(
            ref int index,
            bool active,
            string icon,
            string tooltipLabel,
            string description,
            float remainingDuration = 0f)
        {
            if (!active || slots == null || labels == null || index >= slots.Length)
            {
                return;
            }

            slots[index].SetActive(true);
            labels[index].text = icon;
            slots[index].name = "Status " + tooltipLabel;
            if (tooltips != null && index < tooltips.Length && tooltips[index] != null)
            {
                tooltips[index].Configure(
                    tooltipLabel,
                    description,
                    remainingDuration > 0f
                        ? remainingDuration.ToString("0.0") + "s remaining"
                        : string.Empty);
            }

            index++;
        }
    }

    public sealed class StatusIconTooltip : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private StatusTooltipPanel panel;
        private string statusName;
        private string description;
        private string duration;
        private bool hovered;

        public void Bind(StatusTooltipPanel sharedPanel)
        {
            panel = sharedPanel;
        }

        public void Configure(string name, string effectDescription, string durationText)
        {
            statusName = name;
            description = effectDescription;
            duration = durationText;
            if (hovered)
            {
                panel?.Show(statusName, description, duration);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            panel?.Show(statusName, description, duration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            panel?.Hide(this);
        }

        private void OnDisable()
        {
            hovered = false;
            panel?.Hide(this);
        }
    }

    public sealed class StatusTooltipPanel : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private Text title;
        private Text body;
        public void Configure(CanvasGroup group, Text titleLabel, Text bodyLabel)
        {
            canvasGroup = group;
            title = titleLabel;
            body = bodyLabel;
            SetVisible(false);
        }

        public void Show(string statusName, string description, string duration)
        {
            if (title != null)
            {
                title.text = statusName;
            }

            if (body != null)
            {
                body.text = string.IsNullOrEmpty(duration)
                    ? description
                    : description + "\n" + duration;
            }

            SetVisible(true);
        }

        public void Hide(StatusIconTooltip requester)
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
