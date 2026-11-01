using Cave.InputSystem;
using Cave.Projectiles;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Presentation-only Nexus binding for the player-owned origin authority.</summary>
    [DisallowMultipleComponent]
    public sealed class ManifestationOriginSelectionPanel : MonoBehaviour
    {
        private static readonly string[] Descriptions =
        {
            "Default origin. Projectiles manifest directly from your body.",
            "Basilisk Gate. A stationary ethereal origin placed ahead of you.",
            "Gilded Illusion. Traverses smoothly above and follows your center."
        };

        private PlayerManifestationOrigin state;
        private Button[] buttons;
        private Text[] labels;
        private Text status;

        public void Configure(GameObject player, Button[] configuredButtons,
            Text[] configuredLabels, Text configuredStatus)
        {
            Unsubscribe();
            state = PlayerManifestationOrigin.EnsureOn(player);
            buttons = configuredButtons;
            labels = configuredLabels;
            status = configuredStatus;
            if (state != null)
            {
                state.Changed += Refresh;
                state.FeedbackRequested += HandleFeedback;
            }
            for (int index = 0; buttons != null && index < buttons.Length; index++)
            {
                int captured = index;
                buttons[index].onClick.RemoveAllListeners();
                buttons[index].onClick.AddListener(() => state?.Select((ManifestationOrigin)captured));
            }
            Refresh();
        }

        private void Refresh()
        {
            if (state == null || buttons == null || labels == null) return;
            for (int index = 0; index < buttons.Length && index < labels.Length; index++)
            {
                ManifestationOrigin origin = (ManifestationOrigin)index;
                bool selected = state.SelectedOrigin == origin;
                bool unlocked = state.IsUnlocked(origin);
                buttons[index].interactable = unlocked;
                Image image = buttons[index].GetComponent<Image>();
                if (image != null)
                {
                    image.color = selected ? CaveUiTheme.SurfaceRaised
                        : unlocked ? CaveUiTheme.SurfaceInset
                        : new Color(.13f, .13f, .16f, .88f);
                }
                string requirement = origin == ManifestationOrigin.Body
                    ? "ALWAYS AVAILABLE"
                    : unlocked ? "UNLOCKED"
                    : state.RequirementText(origin).ToUpperInvariant();
                labels[index].text = (selected ? "◆ " : string.Empty)
                    + PlayerManifestationOrigin.DisplayName(origin)
                    + "\n" + Descriptions[index]
                    + "\n" + requirement;
                labels[index].color = selected ? CaveUiTheme.Gold
                    : unlocked ? CaveUiTheme.PrimaryText : CaveUiTheme.SecondaryText;
            }
            if (status != null)
            {
                bool body = state.SelectedOrigin == ManifestationOrigin.Body;
                status.text = "Selected: " + state.SelectedOrigin
                    + "  •  " + (body ? "DIRECT FIRE"
                        : state.IsDeployed ? "DEPLOYED: " + state.DeployedOrigin : "UNDEPLOYED")
                    + (body ? "  •  No deployment or upkeep"
                        : "  •  Deploy " + state.DeploymentManaCost.ToString("0") + " mana"
                            + "  •  Upkeep " + state.CurrentUpkeepPerSecond.ToString("0") + "/s"
                            + "  •  Recall "
                            + SettingsMenuController.FormatBinding(GameAction.RecallManifestation));
            }
        }

        private void HandleFeedback(string message)
        {
            Refresh();
            if (status != null) status.text = message + "  •  " + status.text;
        }

        private void Unsubscribe()
        {
            if (state == null) return;
            state.Changed -= Refresh;
            state.FeedbackRequested -= HandleFeedback;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
