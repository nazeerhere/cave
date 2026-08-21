using Cave.InputSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        private GameObject pauseMenu;
        private GameObject settingsPanel;
        private SettingsMenuController settingsController;
        private bool isPaused;
        private float previousTimeScale = 1f;

        public void Configure(
            GameObject pauseMenuObject,
            GameObject settingsPanelObject,
            Button resumeButton,
            Button settingsButton,
            Button backButton,
            SettingsMenuController configuredSettingsController)
        {
            pauseMenu = pauseMenuObject;
            settingsPanel = settingsPanelObject;
            settingsController = configuredSettingsController;

            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(ShowSettings);
            backButton.onClick.AddListener(ShowPauseMenu);

            pauseMenu.SetActive(false);
            settingsPanel.SetActive(false);
        }

        private void Update()
        {
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
                if (settingsPanel != null && settingsPanel.activeSelf)
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
            settingsPanel.SetActive(true);
            settingsController?.PrepareToShow();
        }

        public void ShowPauseMenu()
        {
            if (!isPaused)
            {
                return;
            }

            settingsPanel.SetActive(false);
            settingsController?.CancelRebind();
            pauseMenu.SetActive(true);
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
