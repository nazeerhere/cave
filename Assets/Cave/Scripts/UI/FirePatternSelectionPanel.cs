using Cave.Projectiles;
using Cave.Combat;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>
    /// Presentation-only binding for player-owned ordinary-fire selection.
    /// Heavy deliberately remains outside this panel and its input path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirePatternSelectionPanel : MonoBehaviour
    {
        private static readonly string[] Labels =
        {
            "PRECISION\nHELD • ACCURATE",
            "AUTOMATIC\nHELD • BALANCED",
            "BURST\nHELD • 3 SHOTS",
            "VOLLEY\nHELD • SPREAD",
            "SPOOL-UP\nHELD • RAMPS"
        };

        private PlayerFirePatternState state;
        private Button[] buttons;
        private Text[] labels;
        private Text profileText;
        private PlayerProjectileLauncher launcher;
        private PlayerSpecialMode specialMode;
        private PlayerSpecialModeUpgradeState upgrades;

        public void Configure(GameObject player, Button[] patternButtons, Text[] patternLabels,
            Text configuredProfileText)
        {
            if (state != null)
            {
                state.SelectionChanged -= HandleSelectionChanged;
            }

            state = PlayerFirePatternState.EnsureOn(player);
            buttons = patternButtons;
            labels = patternLabels;
            profileText = configuredProfileText;
            launcher = player != null ? player.GetComponent<PlayerProjectileLauncher>() : null;
            specialMode = player != null ? player.GetComponent<PlayerSpecialMode>() : null;
            upgrades = player != null ? player.GetComponent<PlayerSpecialModeUpgradeState>() : null;
            if (state != null)
            {
                state.SelectionChanged += HandleSelectionChanged;
            }
            if (specialMode != null) specialMode.SpecialModeChanged += HandleModeChanged;
            if (upgrades != null)
            {
                upgrades.Tier2OwnershipChanged += HandleTierChanged;
                upgrades.Tier3OwnershipChanged += HandleTierChanged;
            }

            for (int index = 0; buttons != null && index < buttons.Length; index++)
            {
                int captured = index;
                buttons[index].onClick.RemoveAllListeners();
                buttons[index].onClick.AddListener(() => state?.Select((FirePattern)captured));
            }

            Refresh();
        }

        private void HandleSelectionChanged(FirePattern _) => Refresh();

        private void Refresh()
        {
            if (buttons == null || labels == null)
            {
                return;
            }

            for (int index = 0; index < buttons.Length && index < Labels.Length; index++)
            {
                bool selected = state != null && state.SelectedPattern == (FirePattern)index;
                buttons[index].interactable = true;
                Image image = buttons[index].GetComponent<Image>();
                if (image != null)
                {
                    image.color = selected ? CaveUiTheme.SurfaceRaised : CaveUiTheme.SurfaceInset;
                }

                labels[index].text = (selected ? "◆ " : string.Empty) + Labels[index];
                labels[index].color = selected ? CaveUiTheme.Gold : CaveUiTheme.PrimaryText;
            }
            if (profileText != null)
            {
                profileText.text = launcher != null && state != null
                    ? launcher.DescribePattern(state.SelectedPattern)
                    : "Projectile profile unavailable.";
            }
        }

        private void HandleModeChanged(SpecialMode _) => Refresh();
        private void HandleTierChanged(SpecialMode _) => Refresh();

        private void OnDestroy()
        {
            if (state != null)
            {
                state.SelectionChanged -= HandleSelectionChanged;
            }
            if (specialMode != null) specialMode.SpecialModeChanged -= HandleModeChanged;
            if (upgrades != null)
            {
                upgrades.Tier2OwnershipChanged -= HandleTierChanged;
                upgrades.Tier3OwnershipChanged -= HandleTierChanged;
            }
        }
    }
}
