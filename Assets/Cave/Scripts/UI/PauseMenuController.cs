using Cave.InputSystem;
using Cave.Player;
using Cave.Enemies;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Cave.UI
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        private GameObject pauseMenu;
        private GameObject settingsPanel;
        private GameObject movesListPanel;
        private GameObject ledgerPanel;
        private Text ledgerBodyText;
        private EnemyLedgerPagesHud ledgerPages;
        private Text movesLeftText;
        private Text movesRightText;
        private Text movesChainLeftText;
        private Text movesChainRightText;
        private Text movesPageText;
        private Button movesPageButton;
        private MovesListPresentation movesPresentation;
        private SettingsMenuController settingsController;
        private bool isPaused;
        private float previousTimeScale = 1f;
        private int movesPageIndex;

        public void Configure(
            GameObject pauseMenuObject,
            GameObject settingsPanelObject,
            Button resumeButton,
            Button resetLevelButton,
            Button settingsButton,
            Button backButton,
            SettingsMenuController configuredSettingsController,
            GameObject movesPanelObject,
            Button movesButton,
            Button movesBackButton,
            GameObject ledgerPanelObject,
            Button ledgerButton,
            Button ledgerBackButton,
            Text configuredLedgerBodyText,
            Text configuredMovesLeftText,
            Text configuredMovesRightText,
            Text configuredMovesChainLeftText,
            Text configuredMovesChainRightText,
            Text configuredMovesPageText,
            Button configuredMovesPageButton,
            MovesListPresentation configuredMovesPresentation)
        {
            pauseMenu = pauseMenuObject;
            settingsPanel = settingsPanelObject;
            settingsController = configuredSettingsController;
            movesListPanel = movesPanelObject;
            ledgerPanel = ledgerPanelObject;
            ledgerBodyText = configuredLedgerBodyText;
            ledgerPages = ledgerPanel != null ? ledgerPanel.GetComponent<EnemyLedgerPagesHud>() : null;
            movesLeftText = configuredMovesLeftText;
            movesRightText = configuredMovesRightText;
            movesChainLeftText = configuredMovesChainLeftText;
            movesChainRightText = configuredMovesChainRightText;
            movesPageText = configuredMovesPageText;
            movesPageButton = configuredMovesPageButton;
            movesPresentation = configuredMovesPresentation;

            resumeButton.onClick.AddListener(Resume);
            resetLevelButton.onClick.AddListener(ResetCurrentLevel);
            settingsButton.onClick.AddListener(ShowSettings);
            backButton.onClick.AddListener(ShowPauseMenu);
            movesButton.onClick.AddListener(ShowMovesList);
            movesBackButton.onClick.AddListener(ShowPauseMenu);
            ledgerButton.onClick.AddListener(ShowLedger);
            ledgerBackButton.onClick.AddListener(ShowPauseMenu);
            movesPageButton.onClick.AddListener(ToggleMovesPage);

            pauseMenu.SetActive(false);
            settingsPanel.SetActive(false);
            movesListPanel.SetActive(false);
            ledgerPanel.SetActive(false);
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
                    || (movesListPanel != null && movesListPanel.activeSelf)
                    || (ledgerPanel != null && ledgerPanel.activeSelf))
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
            if (ledgerPanel != null) ledgerPanel.SetActive(false);
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
            if (ledgerPanel != null) ledgerPanel.SetActive(false);
            Time.timeScale = previousTimeScale;
            GameInput.EnableGameplayAfterInputRelease();
        }

        public void ResetCurrentLevel()
        {
            settingsController?.CancelRebind();
            isPaused = false;
            if (pauseMenu != null)
            {
                pauseMenu.SetActive(false);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            if (movesListPanel != null)
            {
                movesListPanel.SetActive(false);
            }

            if (ledgerPanel != null)
            {
                ledgerPanel.SetActive(false);
            }

            Time.timeScale = 1f;
            GameInput.EnableGameplayAfterInputRelease();
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
        }

        public void ShowSettings()
        {
            if (!isPaused)
            {
                return;
            }

            pauseMenu.SetActive(false);
            movesListPanel.SetActive(false);
            if (ledgerPanel != null) ledgerPanel.SetActive(false);
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
            if (ledgerPanel != null) ledgerPanel.SetActive(false);
            movesListPanel.SetActive(true);
            movesPageIndex = 0;
            RefreshMovesList();
        }

        public void ShowLedger()
        {
            if (!isPaused || ledgerPanel == null)
            {
                return;
            }

            pauseMenu.SetActive(false);
            settingsPanel.SetActive(false);
            movesListPanel.SetActive(false);
            ledgerPanel.SetActive(true);
            if (ledgerPages != null)
            {
                ledgerPages.ShowRoster();
            }
            else if (ledgerBodyText != null)
            {
                ledgerBodyText.text = EnemyLedger.BuildSummary();
            }
        }

        public void ShowPauseMenu()
        {
            if (!isPaused)
            {
                return;
            }

            settingsPanel.SetActive(false);
            movesListPanel.SetActive(false);
            if (ledgerPanel != null) ledgerPanel.SetActive(false);
            settingsController?.CancelRebind();
            pauseMenu.SetActive(true);
        }

        private void RefreshMovesList()
        {
            if (movesLeftText != null)
            {
                movesLeftText.text =
                    "1. BASIC MOVES\n\n"
                    + Key(GameAction.MoveLeft) + " / " + Key(GameAction.MoveRight) + "  Move\n"
                    + Key(GameAction.Jump) + "  Jump\n"
                    + Key(GameAction.Dash) + "  Dash\n\n"
                    + "2. COMBAT\n\n"
                    + Key(GameAction.BasicAttack) + "  Hold — Sustained Spin\n"
                    + Key(GameAction.ChargedAttack) + "  Hold / Release — Heavy\n"
                    + "Heavy has charge tiers; a committed release attacks.\n"
                    + Key(GameAction.Parry) + "  Hold — Guard / Parry\n"
                    + Key(GameAction.GuardBreak) + "  Guard Break / Counter\n\n"
                    + "3. AIR HEAVY\n\n"
                    + Key(GameAction.ChargedAttack) + " in air — Ground Smash\n"
                    + "Uses Stamina. Tap falls vertical; hold aims a diagonal dive.";
            }

            if (movesRightText != null)
            {
                movesRightText.text =
                    "DEFENSE FOLLOW-UPS\n\n"
                    + "Normal Parry → Heavy begins at Tier 1.\n"
                    + "Perfect Parry → Heavy begins at Tier 2.\n"
                    + "Successful Guard Break → Heavy begins at Tier 1.\n"
                    + "After Parry timing: Guard + GB enters Brace.\n\n"
                    + "PROJECTILES & ITEMS\n\n"
                    + Key(GameAction.FireProjectile) + "  Fire Current Mode Projectile\n"
                    + Key(GameAction.UseHealthPotion) + "  Health Potion\n"
                    + Key(GameAction.UseManaPotion) + "  Mana Potion\n"
                    + Key(GameAction.PlaceLandmine) + "  Place Landmine\n"
                    + Key(GameAction.UseDistraction) + "  Use Distraction\n\n"
                    + "WORLD\n"
                    + Key(GameAction.Interact) + "  Interact when no combat priority\n"
                    + Key(GameAction.SummonCurseAltar) + "  Summon Curse Altar";
            }

            RefreshMovesPage();
        }

        private void ToggleMovesPage()
        {
            movesPageIndex = (movesPageIndex + 1) % MovesListPresentation.PageCount;
            RefreshMovesPage();
        }

        private void RefreshMovesPage()
        {
            if (movesPresentation == null && movesListPanel != null)
            {
                movesPresentation = movesListPanel.GetComponent<MovesListPresentation>();
            }

            bool showBasic = movesPageIndex == 0;
            if (movesLeftText != null)
            {
                movesLeftText.gameObject.SetActive(showBasic);
            }

            if (movesRightText != null)
            {
                movesRightText.gameObject.SetActive(showBasic);
            }

            if (movesChainLeftText != null)
            {
                movesChainLeftText.gameObject.SetActive(!showBasic);
            }

            if (movesChainRightText != null)
            {
                movesChainRightText.gameObject.SetActive(!showBasic);
            }

            if (movesPageIndex == 1)
            {
                PopulateFollowUpsPage();
            }
            else if (movesPageIndex == 2)
            {
                PopulateSkillsPage();
            }

            if (movesPageText != null)
            {
                movesPageText.text = movesPageIndex == 0
                    ? "PAGE 1 / 4  •  BASIC MOVES"
                    : movesPageIndex == 1
                        ? "PAGE 2 / 4  •  FOLLOW-UPS / BRACE"
                        : movesPageIndex == 2
                            ? "PAGE 3 / 4  •  SKILLS / RESOURCES"
                            : "PAGE 4 / 4  •  AXIOMS";
            }

            if (movesPageButton != null)
            {
                Text label = movesPageButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = "NEXT PAGE";
                }
            }

            if (movesPresentation != null)
            {
                movesPresentation.ShowPage(movesPageIndex);
                if (movesLeftText != null) movesLeftText.gameObject.SetActive(false);
                if (movesRightText != null) movesRightText.gameObject.SetActive(false);
                if (movesChainLeftText != null) movesChainLeftText.gameObject.SetActive(false);
                if (movesChainRightText != null) movesChainRightText.gameObject.SetActive(false);
            }
        }

        private void PopulateFollowUpsPage()
        {
            if (movesChainLeftText != null)
            {
                movesChainLeftText.text =
                    "2. FOLLOW-UPS / CHAINS\n\n"
                    + "Spin → GB = Bash.\n"
                    + "Parry → Heavy starts at Tier 1.\n"
                    + "Perfect Parry → Heavy starts at Tier 2.\n"
                    + "Successful GB → Heavy starts at Tier 1.\n"
                    + "Heavy or GB → Dash = Cross Step.\n"
                    + "Successful defense → Dash = Slip.\n"
                    + "Successful defense → GB = Counter Push.\n\n"
                    + "Brace exits through Spin or Heavy. Quick Brace makes that exit cheaper.";
            }

            if (movesChainRightText != null)
            {
                movesChainRightText.text =
                    "BRACE STAGES\n\n"
                    + "QUICK BRACE\n"
                    + "Guard + GB after the Parry timing window. Brief and mobile; its next Spin or Heavy costs less.\n\n"
                    + "FULL BRACE\n"
                    + "BRACE I. Reached automatically from Quick Brace. Main defensive stance; restores 5% maximum Stamina per second and can Deflect.\n\n"
                    + "DEEP BRACE\n"
                    + "BRACE II. Press GB again from Full. Immobile and no Guard; restores 3% maximum Stamina and 5% maximum Mana per second while held.";
            }
        }

        private void PopulateSkillsPage()
        {
            if (movesChainLeftText != null)
            {
                movesChainLeftText.text =
                    "3. SKILLS / RESOURCES\n\n"
                    + "ELEMENTAL MODES\n"
                    + "Slow Shot — Frost response.\n"
                    + "Burn Shot — Fire response.\n"
                    + "Flight — Wind movement and Ground Smash.\n"
                    + "Strength — shield and physical pressure.\n\n"
                    + "Each mode and tier is shown in the Modes / Shop panel. Use the selected mode's projectile with "
                    + Key(GameAction.FireProjectile) + ".";
            }

            if (movesChainRightText != null)
            {
                movesChainRightText.text =
                    "SPECIALS / RULES\n\n"
                    + "FRENZY BREAK\n"
                    + Key(GameAction.Interact) + " tap prepares Physical; hold infuses Mana. The next new qualifying attack claims it.\n\n"
                    + "RESOURCES\n"
                    + "Mana powers projectiles and skills. Stamina powers Spin, Heavy actions, movement, and Ground Smash.\n\n"
                    + "UNBLOCKABLE ≠ UNPARRYABLE\n"
                    + "Some attacks bypass Guard but can still be perfectly parried.";
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
