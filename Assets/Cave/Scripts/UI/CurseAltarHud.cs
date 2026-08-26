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
        private Text distractionState;
        private Text detectiveState;
        private Text detectiveDescription;
        private Text avariceState;
        private Text avariceDetails;
        private Button distractionButton;
        private Button detectiveButton;
        private Button avariceButton;
        private Button closeButton;
        private Image distractionCard;
        private Image detectiveCard;
        private Image avariceCard;
        private PlayerCurseAltarController altarController;

        public void Configure(
            GameObject selectionPanel,
            Text distractionStatus,
            Text detectiveStatus,
            Text configuredDetectiveDescription,
            Text avariceStatus,
            Text configuredAvariceDetails,
            Button distractionToggle,
            Button detectiveToggle,
            Button avariceToggle,
            Button close,
            Image configuredDistractionCard,
            Image configuredDetectiveCard,
            Image configuredAvariceCard)
        {
            panel = selectionPanel;
            distractionState = distractionStatus;
            detectiveState = detectiveStatus;
            detectiveDescription = configuredDetectiveDescription;
            avariceState = avariceStatus;
            avariceDetails = configuredAvariceDetails;
            distractionButton = distractionToggle;
            detectiveButton = detectiveToggle;
            avariceButton = avariceToggle;
            closeButton = close;
            distractionCard = configuredDistractionCard;
            detectiveCard = configuredDetectiveCard;
            avariceCard = configuredAvariceCard;
            distractionButton.onClick.AddListener(
                () => altarController?.ToggleCurse(PlayerCurseType.Distraction));
            detectiveButton.onClick.AddListener(
                () => altarController?.ToggleCurse(PlayerCurseType.Detective));
            avariceButton.onClick.AddListener(
                () => altarController?.ToggleCurse(PlayerCurseType.Avarice));
            closeButton.onClick.AddListener(() => altarController?.CloseSelection());
            panel.SetActive(false);
        }

        private void Update()
        {
            if (altarController == null)
            {
                Bind(FindObjectOfType<PlayerCurseAltarController>());
            }

            if (altarController != null
                && altarController.IsSelectionOpen
                && GameInput.MenuCancelPressed)
            {
                GameInput.ConsumeMenuInputForCurrentFrame();
                altarController.CloseSelection();
            }
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

            bool distractionActive = curses.CurseOfDistractionActive;
            bool detectiveActive = curses.DetectivesCurseActive;
            bool avariceActive = curses.CurseOfAvariceActive;
            SetState(distractionState, distractionButton, distractionCard, distractionActive, "ACTIVE");
            SetState(detectiveState, detectiveButton, detectiveCard, detectiveActive, "ACTIVE");
            SetState(
                avariceState,
                avariceButton,
                avariceCard,
                avariceActive,
                "ACTIVE • " + FormatAvariceTier(curses.CurrentAvariceTier));

            if (detectiveDescription != null)
            {
                detectiveDescription.text =
                    "DETECTIVE'S CURSE\nBenefit: +"
                    + Mathf.RoundToInt((curses.ConfiguredMasteryGainMultiplier - 1f) * 100f)
                    + "% mastery • "
                    + Mathf.RoundToInt(curses.ConfiguredWorldLevelGrowthMultiplier * 100f)
                    + "% future World growth\nCost: harder research • hordes guarantee "
                    + curses.ConfiguredGuaranteedRealDetectives + " real Detectives";
            }

            if (avariceDetails != null)
            {
                avariceDetails.text = avariceActive
                    ? FormatAvariceTier(curses.CurrentAvariceTier) + "\nWealth: "
                        + curses.CurrentAvariceWealth
                        + "  •  Death Claim: " + FormatPercent(curses.CurrentAvariceDeathClaim)
                        + "\nMovement: -" + FormatPercent(curses.CurrentAvariceMovementPenalty)
                        + "  •  Enemy Detection: +" + FormatPercent(curses.CurrentAvariceDetectionBonus)
                        + "  •  Enemy Pressure: +" + FormatPercent(curses.CurrentAvariceDangerBonus)
                    : "Keep part of your tactical currency after death.\n"
                        + "More wealth increases enemy pressure and detection,\n"
                        + "movement burden, and the share claimed on death.";
            }
        }

        private static void SetState(
            Text state,
            Button button,
            Image card,
            bool active,
            string activeLabel)
        {
            if (state != null)
            {
                state.text = active ? "◆ " + activeLabel : "◇ INACTIVE";
                state.color = active ? CaveUiTheme.Gold : CaveUiTheme.SecondaryText;
            }

            Text buttonLabel = button != null ? button.GetComponentInChildren<Text>() : null;
            if (buttonLabel != null)
            {
                buttonLabel.text = active ? "REMOVE" : "ACCEPT";
            }

            if (card != null)
            {
                card.color = active
                    ? new Color(0.075f, 0.045f, 0.105f, 0.99f)
                    : CaveUiTheme.SurfaceInset;
            }
        }

        private static string FormatAvariceTier(int tier)
        {
            switch (tier)
            {
                case 1: return "AVARICE I";
                case 2: return "AVARICE II";
                case 3: return "AVARICE III";
                case 4: return "AVARICE IV";
                default: return "AVARICE";
            }
        }

        private static string FormatPercent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }

        private void OnDestroy()
        {
            Bind(null);
        }
    }
}
