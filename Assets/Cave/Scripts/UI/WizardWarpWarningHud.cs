using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class WizardWarpWarningHud : MonoBehaviour
    {
        private CanvasGroup group;
        private Text title;
        private Text prompt;
        private GameObject player;
        private PlayerWarpStatus warp;

        public void Configure(
            CanvasGroup canvasGroup,
            Text titleText,
            Text promptText,
            GameObject playerObject)
        {
            group = canvasGroup;
            title = titleText;
            prompt = promptText;
            player = playerObject;
            warp = player != null ? player.GetComponent<PlayerWarpStatus>() : null;
            Refresh();
        }

        private void Update()
        {
            if (player == null || !player.activeInHierarchy)
            {
                PlayerHealth health = FindObjectOfType<PlayerHealth>();
                player = health != null ? health.gameObject : null;
                warp = player != null ? player.GetComponent<PlayerWarpStatus>() : null;
            }
            else if (warp == null)
            {
                warp = player.GetComponent<PlayerWarpStatus>();
            }

            Refresh();
        }

        private void Refresh()
        {
            bool visible = warp != null && warp.IsMarked;
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            if (!visible)
            {
                return;
            }

            if (title != null)
            {
                title.text = "LOW MANA • WARP MARK";
            }

            if (prompt != null)
            {
                string binding = SettingsMenuController.FormatBinding(GameAction.GuardBreak);
                prompt.text = binding
                    + " • RESIST WARP   COST: "
                    + Mathf.CeilToInt(warp.RejectCost)
                    + " STAMINA";
                prompt.color = warp.CanAffordReject
                    ? CaveUiTheme.Gold
                    : new Color(1f, 0.35f, 0.3f, 1f);
            }
        }
    }
}
