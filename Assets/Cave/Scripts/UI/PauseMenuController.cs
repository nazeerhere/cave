using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        private GameObject pauseMenu;
        private GameObject settingsPanel;
        private GameObject movesListPanel;
        private Text movesLeftText;
        private Text movesRightText;
        private Text movesChainLeftText;
        private Text movesChainRightText;
        private Text movesPageText;
        private Button movesPageButton;
        private SettingsMenuController settingsController;
        private bool isPaused;
        private float previousTimeScale = 1f;
        private int movesPageIndex;

        public void Configure(
            GameObject pauseMenuObject,
            GameObject settingsPanelObject,
            Button resumeButton,
            Button settingsButton,
            Button backButton,
            SettingsMenuController configuredSettingsController,
            GameObject movesPanelObject,
            Button movesButton,
            Button movesBackButton,
            Text configuredMovesLeftText,
            Text configuredMovesRightText,
            Text configuredMovesChainLeftText,
            Text configuredMovesChainRightText,
            Text configuredMovesPageText,
            Button configuredMovesPageButton)
        {
            pauseMenu = pauseMenuObject;
            settingsPanel = settingsPanelObject;
            settingsController = configuredSettingsController;
            movesListPanel = movesPanelObject;
            movesLeftText = configuredMovesLeftText;
            movesRightText = configuredMovesRightText;
            movesChainLeftText = configuredMovesChainLeftText;
            movesChainRightText = configuredMovesChainRightText;
            movesPageText = configuredMovesPageText;
            movesPageButton = configuredMovesPageButton;

            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(ShowSettings);
            backButton.onClick.AddListener(ShowPauseMenu);
            movesButton.onClick.AddListener(ShowMovesList);
            movesBackButton.onClick.AddListener(ShowPauseMenu);
            movesPageButton.onClick.AddListener(ToggleMovesPage);

            pauseMenu.SetActive(false);
            settingsPanel.SetActive(false);
            movesListPanel.SetActive(false);
        }

        private void Update()
        {
            if (PlayerCurseAltarController.AnySelectionOpen)
            {
                return;
            }

            if (settingsController != null &&
                (settingsController.IsWaitingForKey || settingsController.ConsumedMenuInputThisFrame))
            {
                return;
            }

            if (!isPaused)
            {
                if (GameInput.PausePressed)
                {
                    Pause();
                }
            }
            else if (GameInput.PausePressed || GameInput.MenuCancelPressed)
            {
                if ((settingsPanel != null && settingsPanel.activeSelf)
                    || (movesListPanel != null && movesListPanel.activeSelf))
                {
                    ShowPauseMenu();
                }
                else
                {
                    Resume();
                }
            }
        }

        public void Pause()
        {
            if (isPaused || pauseMenu == null)
            {
                return;
            }

            isPaused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameInput.SetGameplayInputEnabled(false);
            settingsPanel.SetActive(false);
            movesListPanel.SetActive(false);
            pauseMenu.SetActive(true);
        }

        public void Resume()
        {
            if (!isPaused)
            {
                return;
            }

            isPaused = false;
            settingsController?.CancelRebind();
            pauseMenu.SetActive(false);
            settingsPanel.SetActive(false);
            movesListPanel.SetActive(false);
            Time.timeScale = previousTimeScale;
            GameInput.EnableGameplayAfterInputRelease();
        }

        public void ShowSettings()
        {
            if (!isPaused)
            {
                return;
            }

            pauseMenu.SetActive(false);
            movesListPanel.SetActive(false);
            settingsPanel.SetActive(true);
            settingsController?.PrepareToShow();
        }

        public void ShowMovesList()
        {
            if (!isPaused)
            {
                return;
            }

            pauseMenu.SetActive(false);
            settingsPanel.SetActive(false);
            movesListPanel.SetActive(true);
            movesPageIndex = 0;
            RefreshMovesList();
        }

        public void ShowPauseMenu()
        {
            if (!isPaused)
            {
                return;
            }

            settingsPanel.SetActive(false);
            movesListPanel.SetActive(false);
            settingsController?.CancelRebind();
            pauseMenu.SetActive(true);
        }

        private void RefreshMovesList()
        {
            if (movesLeftText != null)
            {
                movesLeftText.text =
                    "BASIC COMBAT\n"
                    + Key(GameAction.BasicAttack) + "  Hold — Sustained Spin\n"
                    + Key(GameAction.ChargedAttack) + "  Hold / Release — Charged Attack\n"
                    + Key(GameAction.GuardBreak) + "  Guard Break / Counter\n\n"
                    + "DEFENSE\n"
                    + Key(GameAction.Parry) + "  Hold — Guard / Parry\n"
                    + "Normal Parry → Charged starts Tier 1\n"
                    + "Perfect Parry → Charged starts Tier 2\n"
                    + "Successful Guard Break → Charged starts Tier 1\n\n"
                    + "MOVEMENT\n"
                    + Key(GameAction.MoveLeft) + " / " + Key(GameAction.MoveRight) + "  Move\n"
                    + Key(GameAction.Jump) + "  Jump\n"
                    + Key(GameAction.Dash) + "  Dash";
            }

            if (movesRightText != null)
            {
                movesRightText.text =
                    "FRENZY BREAK\n"
                    + Key(GameAction.Interact) + "  Tap — Physical Critical\n"
                    + Key(GameAction.Interact) + "  Hold — Mana Infusion I / II / III\n"
                    + "Next NEW attack activation claims Frenzy\n"
                    + "Physical 275% • Mana 355 / 395 / 435%\n"
                    + "Strength 500 / 510 / 525%\n"
                    + "Slow Shot → Ice  •  Burn Shot → Fire\n"
                    + "Flight → Wind  •  Strength → Strength\n\n"
                    + "MAGIC & ITEMS\n"
                    + Key(GameAction.FireProjectile) + "  Fire Current Mode Projectile\n"
                    + Key(GameAction.UseHealthPotion) + "  Health Potion\n"
                    + Key(GameAction.UseManaPotion) + "  Mana Potion\n"
                    + Key(GameAction.PlaceLandmine) + "  Place Landmine\n"
                    + Key(GameAction.UseDistraction) + "  Use Distraction\n\n"
                    + "WORLD\n"
                    + Key(GameAction.Interact) + "  Interact when no combat priority\n"
                    + Key(GameAction.SummonCurseAltar) + "  Summon Curse Altar";
            }

            if (movesChainLeftText != null)
            {
                movesChainLeftText.text =
                    "BASIC & CHAINED ATTACKS\n\n"
                    + "BASIC: the standard action from ordinary combat context.\n"
                    + "CHAINED: an action changed by the move/state before it.\n\n"
                    + Key(GameAction.BasicAttack) + "  SPIN\n"
                    + Key(GameAction.GuardBreak) + "  GUARD BREAK — from neutral\n"
                    + Key(GameAction.ChargedAttack) + "  HEAVY — grounded Charged Attack\n\n"
                    + "Normal Parry → Charged I\n"
                    + "Perfect Parry → Charged II\n"
                    + "Successful GB → Charged I\n"
                    + "Spin → GB input becomes Bash\n"
                    + "Air Tap Heavy → Vertical Slam\n"
                    + "Air Hold Heavy → 40° Diagonal Slam";
            }

            if (movesChainRightText != null)
            {
                movesChainRightText.text =
                    "FRENZY BREAK — OPENER OR FINISHER\n\n"
                    + "Pay the configured large resource cost. Frenzy stays armed "
                    + "during its targeting window.\n\n"
                    + "The next NEW qualifying attack activation claims it. One activation "
                    + "Crits; later attacks are normal. Multi-target attacks may Crit each "
                    + "unique target once.\n\n"
                    + "PHYSICAL  275%\n"
                    + "MANA I / II / III  355% / 395% / 435%\n"
                    + "STRENGTH I / II / III  500% / 510% / 525%\n\n"
                    + "Critical Resistance reduces only the Critical bonus.\n"
                    + "A committed whiff still spends Frenzy.";
            }

            RefreshMovesPage();
        }

        private void ToggleMovesPage()
        {
            movesPageIndex = movesPageIndex == 0 ? 1 : 0;
            RefreshMovesPage();
        }

        private void RefreshMovesPage()
        {
            bool showChains = movesPageIndex == 1;
            if (movesLeftText != null)
            {
                movesLeftText.gameObject.SetActive(!showChains);
            }

            if (movesRightText != null)
            {
                movesRightText.gameObject.SetActive(!showChains);
            }

            if (movesChainLeftText != null)
            {
                movesChainLeftText.gameObject.SetActive(showChains);
            }

            if (movesChainRightText != null)
            {
                movesChainRightText.gameObject.SetActive(showChains);
            }

            if (movesPageText != null)
            {
                movesPageText.text = showChains
                    ? "PAGE 2 / 2  •  BASIC & CHAINED ATTACKS"
                    : "PAGE 1 / 2  •  CONTROLS";
            }

            if (movesPageButton != null)
            {
                Text label = movesPageButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = showChains ? "PREVIOUS" : "NEXT";
                }
            }
        }

        private static string Key(GameAction action)
        {
            return "[" + SettingsMenuController.FormatBinding(action) + "]";
        }

        private void OnDestroy()
        {
            if (isPaused)
            {
                Time.timeScale = previousTimeScale;
                GameInput.EnableGameplayAfterInputRelease();
            }
        }
    }
}
