using Cave.Audio;
using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class CurseAltarHud : MonoBehaviour
    {
        private GameObject panel;
        private Button[] curseButtons;
        private Image[] curseCards;
        private Text detailTitle;
        private Text detailBody;
        private Text activeState;
        private Button actionButton;
        private Button closeButton;
        private PlayerCurseAltarController altarController;
        private PlayerCurseType selectedCurse = PlayerCurseType.Insanity;

        public void Configure(
            GameObject selectionPanel,
            Button[] configuredCurseButtons,
            Image[] configuredCurseCards,
            Text configuredDetailTitle,
            Text configuredDetailBody,
            Text configuredActiveState,
            Button configuredActionButton,
            Button configuredCloseButton)
        {
            panel = selectionPanel;
            curseButtons = configuredCurseButtons;
            curseCards = configuredCurseCards;
            detailTitle = configuredDetailTitle;
            detailBody = configuredDetailBody;
            activeState = configuredActiveState;
            actionButton = configuredActionButton;
            closeButton = configuredCloseButton;

            PlayerCurseType[] types = CurseTypes;
            for (int index = 0; index < types.Length && index < curseButtons.Length; index++)
            {
                PlayerCurseType capturedType = types[index];
                curseButtons[index].onClick.AddListener(() => Select(capturedType));
            }

            actionButton.onClick.AddListener(() => altarController?.ToggleCurse(selectedCurse));
            closeButton.onClick.AddListener(() => altarController?.CloseSelection());
            panel.SetActive(false);
        }

        /// <summary>
        /// Opens the existing altar UI only when its configured controls are usable.
        /// This is intentionally an explicit handshake with the altar controller so
        /// a missing or inactive runtime HUD can never leave gameplay input locked.
        /// </summary>
        public bool TryOpen(PlayerCurseAltarController controller)
        {
            if (controller == null
                || panel == null
                || curseButtons == null
                || curseCards == null
                || detailTitle == null
                || detailBody == null
                || activeState == null
                || actionButton == null
                || closeButton == null)
            {
                return false;
            }

            try
            {
                Bind(controller);
                panel.SetActive(true);
                Refresh();
                return panel.activeInHierarchy;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Curse altar UI could not open: " + exception.Message, this);
                panel.SetActive(false);
                return false;
            }
        }

        public void Close()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private static PlayerCurseType[] CurseTypes => new[]
        {
            PlayerCurseType.Insanity,
            PlayerCurseType.Detective,
            PlayerCurseType.Avarice,
            PlayerCurseType.Stoneglass,
            PlayerCurseType.CavesGlare
        };

        private void Update()
        {
            if (altarController == null)
            {
                Bind(FindObjectOfType<PlayerCurseAltarController>());
            }

            if (altarController != null && altarController.IsSelectionOpen && GameInput.MenuCancelPressed)
            {
                GameInput.ConsumeMenuInputForCurrentFrame();
                altarController.CloseSelection();
            }
        }

        private void Select(PlayerCurseType type)
        {
            selectedCurse = type;
            CaveSfx.PlayUi(CaveSfxCue.ButtonHover, 0.55f);
            Refresh();
        }

        private void Bind(PlayerCurseAltarController controller)
        {
            if (altarController != null)
            {
                altarController.SelectionChanged -= Refresh;
                if (altarController.Curses != null)
                {
                    altarController.Curses.CurseStateChanged -= Refresh;
                    altarController.Curses.AvariceStateChanged -= Refresh;
                }
            }

            altarController = controller;
            if (altarController != null)
            {
                altarController.SelectionChanged += Refresh;
                if (altarController.Curses != null)
                {
                    altarController.Curses.CurseStateChanged += Refresh;
                    altarController.Curses.AvariceStateChanged += Refresh;
                }
            }

            Refresh();
        }

        private void Refresh()
        {
            bool visible = altarController != null && altarController.IsSelectionOpen;
            if (panel != null)
            {
                panel.SetActive(visible);
            }

            PlayerCurseController curses = altarController != null ? altarController.Curses : null;
            if (!visible || curses == null)
            {
                return;
            }

            PlayerCurseType[] types = CurseTypes;
            for (int index = 0; index < types.Length && index < curseCards.Length; index++)
            {
                bool active = curses.IsActive(types[index]);
                bool selected = types[index] == selectedCurse;
                curseCards[index].color = selected
                    ? new Color(0.2f, 0.08f, 0.31f, 1f)
                    : active ? new Color(0.09f, 0.045f, 0.13f, 1f) : CaveUiTheme.SurfaceInset;

                Text cardLabel = curseButtons[index].GetComponentInChildren<Text>();
                if (cardLabel != null)
                {
                    cardLabel.text = GetCardLabel(types[index])
                        + "\n\n" + (active ? "◆ ACTIVE" : "◇ AVAILABLE");
                }
            }

            bool isActive = curses.IsActive(selectedCurse);
            detailTitle.text = GetName(selectedCurse);
            detailBody.text = GetDetail(selectedCurse, curses);
            activeState.text = isActive ? "◆ ACTIVE" : "◇ UNBOUND";
            activeState.color = isActive ? CaveUiTheme.Gold : CaveUiTheme.SecondaryText;
            Text actionLabel = actionButton.GetComponentInChildren<Text>();
            if (actionLabel != null)
            {
                actionLabel.text = isActive ? "REMOVE CURSE" : "ACCEPT CURSE";
            }
        }

        private static string GetName(PlayerCurseType type)
        {
            switch (type)
            {
                case PlayerCurseType.Insanity: return "CURSE OF INSANITY";
                case PlayerCurseType.Detective: return "DETECTIVE'S CURSE";
                case PlayerCurseType.Avarice: return "CURSE OF AVARICE";
                case PlayerCurseType.Stoneglass: return "CURSE OF STONEGLASS";
                default: return "THE CAVE'S GLARE";
            }
        }

        private static string GetCardLabel(PlayerCurseType type)
        {
            switch (type)
            {
                case PlayerCurseType.Insanity: return "◉\nCURSE OF INSANITY";
                case PlayerCurseType.Detective: return "⌕\nDETECTIVE'S CURSE";
                case PlayerCurseType.Avarice: return "◆\nCURSE OF AVARICE";
                case PlayerCurseType.Stoneglass: return "◈\nCURSE OF STONEGLASS";
                default: return "◉\nTHE CAVE'S GLARE";
            }
        }

        private static string GetDetail(PlayerCurseType type, PlayerCurseController curses)
        {
            switch (type)
            {
                case PlayerCurseType.Insanity:
                    return "POWER: kills build speed and damage.\nPRICE: that hunger also leaves you fragile.";
                case PlayerCurseType.Detective:
                    return "POWER: +" + Mathf.RoundToInt((curses.ConfiguredMasteryGainMultiplier - 1f) * 100f)
                        + "% mastery; slower future World growth.\nPRICE: harder research and guaranteed Detectives in hordes.";
                case PlayerCurseType.Avarice:
                    return "POWER: retain tactical currency after death.\nPRICE: wealth increases pressure, detection, burden, and the death claim.\n"
                        + "CURRENT: " + FormatAvariceTier(curses.CurrentAvariceTier)
                        + "  •  WEALTH " + curses.CurrentAvariceWealth;
                case PlayerCurseType.Stoneglass:
                    return "POWER: full resources grant strength and growth.\nPRICE: taking damage fractures resistance.\n"
                        + "FRACTURES: " + curses.StoneglassFractureStacks;
                default:
                    return "POWER: death teaches the Cave to adapt its strength.\nPRICE: corrupted mobs may mutate or rise again.\n"
                        + "DEATHS WITNESSED: " + curses.CavesGlareDeathCount;
            }
        }

        private static string FormatAvariceTier(int tier)
        {
            return tier > 0 ? "AVARICE " + tier : "AVARICE";
        }

        private void OnDestroy()
        {
            Bind(null);
        }
    }
}
