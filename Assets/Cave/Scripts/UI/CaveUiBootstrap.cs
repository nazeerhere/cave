using Cave.Combat;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Player;
using Cave.Projectiles;
using Cave.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cave.UI
{
    public static class CaveUiBootstrap
    {
        private const int UiLayer = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateUiIfMissing();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateUiIfMissing()
        {
            PlayerHealth playerHealth = PlayerRunPersistence.CurrentPlayerHealth
                ?? Object.FindObjectOfType<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            PlayerMana playerMana = playerHealth.GetComponent<PlayerMana>();
            if (playerMana == null)
            {
                playerMana = playerHealth.gameObject.AddComponent<PlayerMana>();
            }

            PlayerCurrency playerCurrency = playerHealth.GetComponent<PlayerCurrency>();
            if (playerCurrency == null)
            {
                playerCurrency = playerHealth.gameObject.AddComponent<PlayerCurrency>();
            }

            PlayerSpecialMode specialMode = playerHealth.GetComponent<PlayerSpecialMode>();
            if (specialMode == null)
            {
                specialMode = playerHealth.gameObject.AddComponent<PlayerSpecialMode>();
            }

            SpinSwordAttack spinSwordAttack = Object.FindObjectOfType<SpinSwordAttack>();
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            PlayerHealthHud existingHud = Object.FindObjectOfType<PlayerHealthHud>(true);
            if (existingHud != null)
            {
                EnsureHealthRatioHud(existingHud, font);
                existingHud.Bind(playerHealth);
                EnsureStaminaHud(existingHud.transform.parent, font, spinSwordAttack);
                EnsureManaHud(existingHud.transform.parent, font, playerMana);
                EnsureCurrencyHud(existingHud.transform.parent, font, playerCurrency);
                EnsureSpecialModeHud(existingHud.transform.parent, font, specialMode, playerCurrency);
                EnsureShieldHud(existingHud.transform.parent, font);
                EnsureWorldLevelHud(existingHud.transform.parent, font);
                EnsureDetectiveTowerStatusHud(existingHud.transform.parent, font);
                EnsureResourceGainPopups(
                    existingHud.transform.parent,
                    font,
                    playerHealth,
                    spinSwordAttack,
                    playerMana,
                    playerCurrency);
                EnsureActionHotbar(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureDetectiveResearchHud(existingHud.transform.parent, font);
                EnsureStatusEffectHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureWizardWarpWarningHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureCurseHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureCurseAltarHud(existingHud.transform.parent, font);
                EnsureFrenzyBreakHud(existingHud.transform.parent, font, playerHealth.gameObject);
                ApplyFreeUiPackageSkin(existingHud.transform.parent);
                EnsureEventSystem();
                return;
            }

            GameObject root = CreateUiObject("Cave UI", null);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            RectTransform gameplayHud = CreateStretchRect("Gameplay HUD", root.transform);
            RectTransform healthPanel = CreateRect(
                "Player Health",
                gameplayHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(326f, 78f));

            Image healthBackground = healthPanel.gameObject.AddComponent<Image>();
            healthBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(healthPanel, CaveUiTheme.Health);
            AddResourcePanelEndCap(healthPanel, CaveUiTheme.Health);
            CreateSymbolIcon(
                "Health Icon",
                healthPanel,
                font,
                new Vector2(-128f, 0f),
                60f,
                "♥",
                CaveUiTheme.Health,
                34);

            CreateText(
                "Health Label",
                healthPanel,
                font,
                "HEALTH",
                19,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -9f),
                new Vector2(150f, 26f));

            Text healthValueText = CreateAnchoredText(
                "Health Value",
                healthPanel,
                font,
                "3 / 3",
                17,
                TextAnchor.MiddleRight,
                new Vector2(202f, -9f),
                new Vector2(116f, 26f));

            RectTransform healthBarBackground = CreateRect(
                "Health Bar Background",
                healthPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -48f),
                new Vector2(236f, 18f));
            Image healthBarBackgroundImage = healthBarBackground.gameObject.AddComponent<Image>();
            healthBarBackgroundImage.color = CaveUiTheme.HealthTrack;
            RectTransform healthFill = CreateStretchRect("Health Fill", healthBarBackground);
            healthFill.offsetMin = new Vector2(1f, 1f);
            healthFill.offsetMax = new Vector2(-1f, -1f);
            healthFill.pivot = new Vector2(0f, 0.5f);
            Image healthFillImage = healthFill.gameObject.AddComponent<Image>();
            healthFillImage.color = CaveUiTheme.Health;

            PlayerHealthHud healthHud = healthPanel.gameObject.AddComponent<PlayerHealthHud>();
            healthHud.Configure(healthFillImage, healthValueText, healthBackground);
            healthHud.Bind(playerHealth);
            EnsureStaminaHud(gameplayHud, font, spinSwordAttack);
            EnsureManaHud(gameplayHud, font, playerMana);
            EnsureCurrencyHud(gameplayHud, font, playerCurrency);
            EnsureSpecialModeHud(gameplayHud, font, specialMode, playerCurrency);
            EnsureShieldHud(gameplayHud, font);
            EnsureWorldLevelHud(gameplayHud, font);
            EnsureDetectiveTowerStatusHud(gameplayHud, font);
            EnsureResourceGainPopups(
                gameplayHud,
                font,
                playerHealth,
                spinSwordAttack,
                playerMana,
                playerCurrency);
            EnsureActionHotbar(gameplayHud, font, playerHealth.gameObject);
            EnsureDetectiveResearchHud(gameplayHud, font);
            EnsureStatusEffectHud(gameplayHud, font, playerHealth.gameObject);
            EnsureWizardWarpWarningHud(gameplayHud, font, playerHealth.gameObject);
            EnsureCurseHud(gameplayHud, font, playerHealth.gameObject);
            EnsureCurseAltarHud(gameplayHud, font);
            EnsureFrenzyBreakHud(gameplayHud, font, playerHealth.gameObject);
            ApplyFreeUiPackageSkin(gameplayHud);

            RectTransform menus = CreateStretchRect("Menus", root.transform);
            GameObject pauseMenu = CreateMenuPanel("Pause Menu", menus, new Vector2(420f, 380f));
            Text pauseCrest = CreateCenteredText(
                "Pause Crest",
                pauseMenu.transform,
                font,
                "◆",
                26,
                new Vector2(0f, 164f),
                new Vector2(80f, 30f));
            pauseCrest.color = CaveUiTheme.BorderBright;
            Text pauseTitle = CreateCenteredText(
                "Pause Title",
                pauseMenu.transform,
                font,
                "PAUSED",
                34,
                new Vector2(0f, 124f));
            pauseTitle.fontStyle = FontStyle.Bold;
            pauseTitle.color = CaveUiTheme.Gold;
            Text pauseSubtitle = CreateCenteredText(
                "Pause Subtitle",
                pauseMenu.transform,
                font,
                "THE CAVE WAITS",
                14,
                new Vector2(0f, 90f));
            pauseSubtitle.color = CaveUiTheme.SecondaryText;
            Button resumeButton = CreateButton(
                "RESUME",
                pauseMenu.transform,
                font,
                new Vector2(0f, 36f),
                new Vector2(270f, 50f));
            Button resetLevelButton = CreateButton(
                "RESET LEVEL",
                pauseMenu.transform,
                font,
                new Vector2(0f, -28f),
                new Vector2(270f, 50f));
            Button movesButton = CreateButton(
                "MOVES LIST",
                pauseMenu.transform,
                font,
                new Vector2(0f, -92f),
                new Vector2(270f, 50f));
            Button settingsButton = CreateButton(
                "SETTINGS",
                pauseMenu.transform,
                font,
                new Vector2(0f, -156f),
                new Vector2(270f, 50f));
            Button ledgerButton = CreateButton(
                "ENEMY LEDGER",
                pauseMenu.transform,
                font,
                new Vector2(0f, -220f),
                new Vector2(270f, 50f));
            Text pauseHint = CreateCenteredText(
                "Pause Hint",
                pauseMenu.transform,
                font,
                "ESC  •  RESUME",
                13,
                new Vector2(0f, -284f));
            pauseHint.color = CaveUiTheme.SecondaryText;

            // The camera row extends the existing Settings menu without reducing
            // the readable binding spacing or clipping the bottom controls.
            GameObject settingsPanel = CreateMenuPanel("Settings Panel", menus, new Vector2(760f, 780f));
            Text settingsCrest = CreateCenteredText(
                "Settings Crest",
                settingsPanel.transform,
                font,
                "◆",
                22,
                new Vector2(0f, 330f),
                new Vector2(70f, 26f));
            settingsCrest.color = CaveUiTheme.BorderBright;
            Text settingsTitle = CreateCenteredText(
                "Settings Title",
                settingsPanel.transform,
                font,
                "SETTINGS",
                30,
                new Vector2(0f, 302f));
            settingsTitle.fontStyle = FontStyle.Bold;
            settingsTitle.color = CaveUiTheme.Gold;
            CreateCardSurface("Controls Surface", settingsPanel.transform, new Vector2(0f, 102f), new Vector2(710f, 364f));
            CreateCardSurface("Audio Surface", settingsPanel.transform, new Vector2(0f, -168f), new Vector2(710f, 132f));
            Text controlsHeader = CreateCenteredText(
                "Controls Header",
                settingsPanel.transform,
                font,
                "CONTROLS",
                18,
                new Vector2(-285f, 274f),
                new Vector2(150f, 30f));
            controlsHeader.alignment = TextAnchor.MiddleLeft;
            controlsHeader.color = CaveUiTheme.BorderBright;

            GameAction[] configurableActions =
            {
                GameAction.MoveLeft,
                GameAction.MoveRight,
                GameAction.Jump,
                GameAction.BasicAttack,
                GameAction.ChargedAttack,
                GameAction.Parry,
                GameAction.GuardBreak,
                GameAction.FireProjectile,
                GameAction.UseHealthPotion,
                GameAction.UseManaPotion,
                GameAction.PlaceLandmine,
                GameAction.UseDistraction,
                GameAction.Interact,
                GameAction.SummonCurseAltar,
                GameAction.Dash,
                GameAction.Pause
            };
            Button[] bindingButtons = new Button[configurableActions.Length];

            const float firstBindingY = 240f;
            const float bindingSpacing = 20f;
            for (int index = 0; index < configurableActions.Length; index++)
            {
                float rowY = firstBindingY - index * bindingSpacing;
                Text actionLabel = CreateCenteredText(
                    FormatActionObjectName(configurableActions[index]) + " Label",
                    settingsPanel.transform,
                    font,
                    SettingsMenuController.FormatAction(configurableActions[index]),
                    15,
                    new Vector2(-195f, rowY),
                    new Vector2(240f, 24f));
                actionLabel.alignment = TextAnchor.MiddleLeft;

                bindingButtons[index] = CreateButton(
                    FormatActionObjectName(configurableActions[index]) + " Binding",
                    settingsPanel.transform,
                    font,
                    new Vector2(170f, rowY),
                    new Vector2(300f, 22f));
                bindingButtons[index].GetComponentInChildren<Text>().fontSize = 14;
            }

            Text statusText = CreateCenteredText(
                "Settings Status",
                settingsPanel.transform,
                font,
                string.Empty,
                15,
                new Vector2(0f, -84f),
                new Vector2(690f, 38f));
            statusText.color = new Color(1f, 0.82f, 0.42f, 1f);

            Text audioHeader = CreateCenteredText(
                "Audio Header",
                settingsPanel.transform,
                font,
                "AUDIO",
                18,
                new Vector2(-285f, -108f),
                new Vector2(150f, 30f));
            audioHeader.alignment = TextAnchor.MiddleLeft;
            audioHeader.color = CaveUiTheme.BorderBright;

            Slider masterSlider = CreateVolumeRow(
                "Master Volume",
                settingsPanel.transform,
                font,
                -148f,
                out Text masterValue);
            Slider sfxSlider = CreateVolumeRow(
                "SFX Volume",
                settingsPanel.transform,
                font,
                -190f,
                out Text sfxValue);
            Slider cameraZoomSlider = CreateVolumeRow(
                "Camera Zoom",
                settingsPanel.transform,
                font,
                -232f,
                out Text cameraZoomValue);
            cameraZoomSlider.minValue = Cave.CameraSystem.CameraZoomSettings.MinimumZoomScale;
            cameraZoomSlider.maxValue = Cave.CameraSystem.CameraZoomSettings.MaximumZoomScale;

            Button restoreDefaultsButton = CreateButton(
                "Restore Defaults",
                settingsPanel.transform,
                font,
                new Vector2(-130f, -288f),
                new Vector2(240f, 42f));
            Button cancelButton = CreateButton(
                "Cancel Rebind",
                settingsPanel.transform,
                font,
                new Vector2(130f, -288f),
                new Vector2(240f, 42f));
            Button backButton = CreateButton(
                "Back",
                settingsPanel.transform,
                font,
                new Vector2(0f, -348f));

            SettingsMenuController settingsController = settingsPanel.AddComponent<SettingsMenuController>();
            settingsController.Configure(
                configurableActions,
                bindingButtons,
                statusText,
                masterSlider,
                masterValue,
                sfxSlider,
                sfxValue,
                cameraZoomSlider,
                cameraZoomValue,
                restoreDefaultsButton,
                cancelButton);

            GameObject movesPanel = CreateMenuPanel("Moves List Panel", menus, new Vector2(940f, 690f));
            Text movesCrest = CreateCenteredText(
                "Moves Crest",
                movesPanel.transform,
                font,
                "◆",
                22,
                new Vector2(0f, 330f),
                new Vector2(70f, 26f));
            movesCrest.color = CaveUiTheme.BorderBright;
            Text movesTitle = CreateCenteredText(
                "Moves Title",
                movesPanel.transform,
                font,
                "MOVES LIST",
                30,
                new Vector2(0f, 300f));
            movesTitle.fontStyle = FontStyle.Bold;
            movesTitle.color = CaveUiTheme.Gold;
            CreateCardSurface(
                "Moves Left Surface",
                movesPanel.transform,
                new Vector2(-226f, 2f),
                new Vector2(430f, 540f));
            CreateCardSurface(
                "Moves Right Surface",
                movesPanel.transform,
                new Vector2(226f, 2f),
                new Vector2(430f, 540f));
            Text movesLeftText = CreateCenteredText(
                "Moves Left Text",
                movesPanel.transform,
                font,
                string.Empty,
                16,
                new Vector2(-226f, 2f),
                new Vector2(390f, 500f));
            movesLeftText.alignment = TextAnchor.UpperLeft;
            movesLeftText.horizontalOverflow = HorizontalWrapMode.Wrap;
            movesLeftText.verticalOverflow = VerticalWrapMode.Overflow;
            Text movesRightText = CreateCenteredText(
                "Moves Right Text",
                movesPanel.transform,
                font,
                string.Empty,
                16,
                new Vector2(226f, 2f),
                new Vector2(390f, 500f));
            movesRightText.alignment = TextAnchor.UpperLeft;
            movesRightText.horizontalOverflow = HorizontalWrapMode.Wrap;
            movesRightText.verticalOverflow = VerticalWrapMode.Overflow;
            Text movesChainLeftText = CreateCenteredText(
                "Moves Chain Left Text",
                movesPanel.transform,
                font,
                string.Empty,
                16,
                new Vector2(-226f, 2f),
                new Vector2(390f, 500f));
            movesChainLeftText.alignment = TextAnchor.UpperLeft;
            movesChainLeftText.horizontalOverflow = HorizontalWrapMode.Wrap;
            movesChainLeftText.verticalOverflow = VerticalWrapMode.Overflow;
            Text movesChainRightText = CreateCenteredText(
                "Moves Chain Right Text",
                movesPanel.transform,
                font,
                string.Empty,
                16,
                new Vector2(226f, 2f),
                new Vector2(390f, 500f));
            movesChainRightText.alignment = TextAnchor.UpperLeft;
            movesChainRightText.horizontalOverflow = HorizontalWrapMode.Wrap;
            movesChainRightText.verticalOverflow = VerticalWrapMode.Overflow;
            Button movesBackButton = CreateButton(
                "Back",
                movesPanel.transform,
                font,
                new Vector2(-160f, -306f),
                new Vector2(220f, 44f));
            Text movesPageText = CreateCenteredText(
                "Moves Page Indicator",
                movesPanel.transform,
                font,
                "PAGE 1 / 2  •  CONTROLS",
                13,
                new Vector2(0f, -306f),
                new Vector2(250f, 36f));
            movesPageText.color = CaveUiTheme.SecondaryText;
            Button movesPageButton = CreateButton(
                "Next Moves Page",
                movesPanel.transform,
                font,
                new Vector2(160f, -306f),
                new Vector2(220f, 44f));

            GameObject ledgerPanel = CreateMenuPanel("Enemy Ledger Panel", menus, new Vector2(620f, 660f));
            Text ledgerTitle = CreateCenteredText(
                "Ledger Title", ledgerPanel.transform, font, "ENEMY LEDGER", 30, new Vector2(0f, 280f));
            ledgerTitle.fontStyle = FontStyle.Bold;
            ledgerTitle.color = CaveUiTheme.Gold;
            CreateCardSurface("Ledger Surface", ledgerPanel.transform, new Vector2(0f, 8f), new Vector2(550f, 520f));
            Text ledgerBody = CreateCenteredText(
                "Ledger Body", ledgerPanel.transform, font, string.Empty, 16, new Vector2(0f, 8f), new Vector2(510f, 490f));
            ledgerBody.alignment = TextAnchor.UpperLeft;
            ledgerBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            ledgerBody.verticalOverflow = VerticalWrapMode.Overflow;
            Button ledgerBackButton = CreateButton(
                "Back", ledgerPanel.transform, font, new Vector2(0f, -284f), new Vector2(220f, 44f));

            PauseMenuController pauseController = menus.gameObject.AddComponent<PauseMenuController>();
            pauseController.Configure(
                pauseMenu,
                settingsPanel,
                resumeButton,
                resetLevelButton,
                settingsButton,
                backButton,
                settingsController,
                movesPanel,
                movesButton,
                movesBackButton,
                ledgerPanel,
                ledgerButton,
                ledgerBackButton,
                ledgerBody,
                movesLeftText,
                movesRightText,
                movesChainLeftText,
                movesChainRightText,
                movesPageText,
                movesPageButton);

            EnsureEventSystem();
        }

        private static void EnsureHealthRatioHud(PlayerHealthHud healthHud, Font font)
        {
            Transform panel = healthHud.transform;
            Transform legacySegments = panel.Find("Segments");
            if (legacySegments != null)
            {
                legacySegments.gameObject.SetActive(false);
            }

            Transform valueTransform = panel.Find("Health Value");
            Text valueText = valueTransform != null
                ? valueTransform.GetComponent<Text>()
                : CreateAnchoredText(
                    "Health Value",
                    panel,
                    font,
                    "0 / 0",
                    12,
                    TextAnchor.MiddleRight,
                    new Vector2(92f, -8f),
                    new Vector2(80f, 18f));

            Transform backgroundTransform = panel.Find("Health Bar Background");
            RectTransform barBackground;
            if (backgroundTransform != null)
            {
                barBackground = (RectTransform)backgroundTransform;
            }
            else
            {
                barBackground = CreateRect(
                    "Health Bar Background",
                    panel,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(12f, -38f),
                    new Vector2(160f, 14f));
                Image barImage = barBackground.gameObject.AddComponent<Image>();
                barImage.color = new Color(0.2f, 0.08f, 0.08f, 0.95f);
            }

            Transform fillTransform = barBackground.Find("Health Fill");
            Image fillImage;
            if (fillTransform != null)
            {
                fillImage = fillTransform.GetComponent<Image>();
            }
            else
            {
                RectTransform fill = CreateStretchRect("Health Fill", barBackground);
                fill.offsetMin = new Vector2(1f, 1f);
                fill.offsetMax = new Vector2(-1f, -1f);
                fill.pivot = new Vector2(0f, 0.5f);
                fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = new Color(0.9f, 0.16f, 0.12f, 1f);
            }

            healthHud.Configure(fillImage, valueText, panel.GetComponent<Image>());
        }

        private static void EnsureStaminaHud(
            Transform gameplayHud,
            Font font,
            SpinSwordAttack spinSwordAttack)
        {
            if (gameplayHud == null || spinSwordAttack == null)
            {
                return;
            }

            PlayerStaminaHud existingStaminaHud = Object.FindObjectOfType<PlayerStaminaHud>(true);
            if (existingStaminaHud != null)
            {
                Transform existingValue = existingStaminaHud.transform.Find("Stamina Value");
                Text valueText = existingValue != null
                    ? existingValue.GetComponent<Text>()
                    : CreateAnchoredText(
                        "Stamina Value",
                        existingStaminaHud.transform,
                        font,
                        "0 / 0",
                        11,
                        TextAnchor.MiddleRight,
                        new Vector2(88f, -5f),
                        new Vector2(86f, 18f));
                existingStaminaHud.UseValueTextIfMissing(valueText);
                existingStaminaHud.Bind(spinSwordAttack);
                return;
            }

            RectTransform staminaPanel = CreateRect(
                "Player Stamina",
                gameplayHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -110f),
                new Vector2(326f, 70f));

            Image panelBackground = staminaPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(staminaPanel, CaveUiTheme.Stamina);
            AddResourcePanelEndCap(staminaPanel, CaveUiTheme.Stamina);
            CreateSymbolIcon(
                "Stamina Icon",
                staminaPanel,
                font,
                new Vector2(-128f, 0f),
                56f,
                "ϟ",
                CaveUiTheme.Stamina,
                32);

            CreateText(
                "Stamina Label",
                staminaPanel,
                font,
                "STAMINA",
                18,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -7f),
                new Vector2(150f, 24f));

            Text staminaValueText = CreateAnchoredText(
                "Stamina Value",
                staminaPanel,
                font,
                "300 / 300",
                17,
                TextAnchor.MiddleRight,
                new Vector2(202f, -7f),
                new Vector2(116f, 24f));

            RectTransform barBackground = CreateRect(
                "Stamina Bar Background",
                staminaPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -42f),
                new Vector2(236f, 16f));
            Image backgroundImage = barBackground.gameObject.AddComponent<Image>();
            backgroundImage.color = CaveUiTheme.StaminaTrack;

            RectTransform fill = CreateStretchRect("Stamina Fill", barBackground);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            fill.pivot = new Vector2(0f, 0.5f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = CaveUiTheme.Stamina;

            PlayerStaminaHud staminaHud = staminaPanel.gameObject.AddComponent<PlayerStaminaHud>();
            staminaHud.Configure(fillImage, staminaValueText);
            staminaHud.Bind(spinSwordAttack);
        }

        private static void EnsureManaHud(Transform gameplayHud, Font font, PlayerMana playerMana)
        {
            if (gameplayHud == null || playerMana == null)
            {
                return;
            }

            PlayerManaHud existingManaHud = Object.FindObjectOfType<PlayerManaHud>(true);
            if (existingManaHud != null)
            {
                existingManaHud.Bind(playerMana);
                return;
            }

            RectTransform manaPanel = CreateRect(
                "Player Mana",
                gameplayHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -188f),
                new Vector2(326f, 70f));

            Image panelBackground = manaPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(manaPanel, CaveUiTheme.Mana);
            AddResourcePanelEndCap(manaPanel, CaveUiTheme.Mana);
            CreateSymbolIcon(
                "Mana Icon",
                manaPanel,
                font,
                new Vector2(-128f, 0f),
                56f,
                "✦",
                CaveUiTheme.Mana,
                30);

            CreateText(
                "Mana Label",
                manaPanel,
                font,
                "MANA",
                18,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -7f),
                new Vector2(150f, 24f));

            Text valueText = CreateAnchoredText(
                "Mana Value",
                manaPanel,
                font,
                "100 / 100",
                17,
                TextAnchor.MiddleRight,
                new Vector2(202f, -7f),
                new Vector2(116f, 24f));

            RectTransform barBackground = CreateRect(
                "Mana Bar Background",
                manaPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -42f),
                new Vector2(236f, 16f));
            Image backgroundImage = barBackground.gameObject.AddComponent<Image>();
            backgroundImage.color = CaveUiTheme.ManaTrack;

            RectTransform fill = CreateStretchRect("Mana Fill", barBackground);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            fill.pivot = new Vector2(0f, 0.5f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = CaveUiTheme.Mana;

            PlayerManaHud manaHud = manaPanel.gameObject.AddComponent<PlayerManaHud>();
            manaHud.Configure(fillImage, valueText);
            manaHud.Bind(playerMana);
        }

        private static void EnsureCurrencyHud(
            Transform gameplayHud,
            Font font,
            PlayerCurrency playerCurrency)
        {
            if (gameplayHud == null || playerCurrency == null)
            {
                return;
            }

            PlayerCurrencyHud existingCurrencyHud = Object.FindObjectOfType<PlayerCurrencyHud>(true);
            if (existingCurrencyHud != null)
            {
                existingCurrencyHud.Bind(playerCurrency);
                return;
            }

            RectTransform currencyPanel = CreateRect(
                "Player Currency",
                gameplayHud,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(236f, 66f));
            Image panelBackground = currencyPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(currencyPanel, CaveUiTheme.Gold);
            CreateSymbolIcon(
                "Currency Icon",
                currencyPanel,
                font,
                new Vector2(-94f, 0f),
                44f,
                "◇",
                CaveUiTheme.Gold,
                24);

            Text currencyLabel = CreateCenteredText(
                "Currency Label",
                currencyPanel,
                font,
                "CURRENCY",
                15,
                new Vector2(30f, 16f),
                new Vector2(150f, 22f));
            currencyLabel.fontStyle = FontStyle.Bold;
            currencyLabel.color = CaveUiTheme.PrimaryText;

            RectTransform textRect = CreateStretchRect("Currency Value", currencyPanel);
            textRect.offsetMin = new Vector2(52f, 27f);
            textRect.offsetMax = new Vector2(-12f, -5f);
            Text valueText = textRect.gameObject.AddComponent<Text>();
            valueText.font = font;
            valueText.fontSize = 24;
            valueText.fontStyle = FontStyle.Bold;
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.color = CaveUiTheme.Currency;
            valueText.text = "0";

            PlayerCurrencyHud currencyHud = currencyPanel.gameObject.AddComponent<PlayerCurrencyHud>();
            currencyHud.Configure(valueText, false);
            currencyHud.Bind(playerCurrency);
        }

        private static void EnsureSpecialModeHud(
            Transform gameplayHud,
            Font font,
            PlayerSpecialMode specialMode,
            PlayerCurrency playerCurrency)
        {
            if (gameplayHud == null || specialMode == null || playerCurrency == null)
            {
                return;
            }

            PlayerSpecialModeHud existingModeHud = Object.FindObjectOfType<PlayerSpecialModeHud>(true);
            if (existingModeHud != null)
            {
                existingModeHud.Bind(specialMode, playerCurrency);
                EnsureModeBarClickTarget(existingModeHud);
                return;
            }

            RectTransform summaryPanel = CreateRect(
                "Player Special Mode",
                gameplayHud,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -98f),
                new Vector2(236f, 64f));
            Image summaryBackground = summaryPanel.gameObject.AddComponent<Image>();
            summaryBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(summaryPanel, CaveUiTheme.BorderBright);

            CreateSymbolIcon(
                "Mode Icon",
                summaryPanel,
                font,
                new Vector2(-94f, 0f),
                44f,
                "✦",
                CaveUiTheme.BorderBright,
                22);

            Text currentModeText = CreateAnchoredText(
                "Current Mode",
                summaryPanel,
                font,
                "MODE\nSLOW SHOT",
                14,
                TextAnchor.MiddleLeft,
                new Vector2(52f, -8f),
                new Vector2(126f, 50f));
            currentModeText.fontStyle = FontStyle.Bold;
            currentModeText.color = CaveUiTheme.PrimaryText;
            Button toggleButton = CreateButton(
                string.Empty,
                summaryPanel,
                font,
                Vector2.zero,
                new Vector2(236f, 64f));
            toggleButton.gameObject.name = "Mode Bar Click Target";
            toggleButton.GetComponent<Image>().color = Color.clear;
            toggleButton.GetComponentInChildren<Text>().gameObject.SetActive(false);
            ColorBlock modeBarColors = toggleButton.colors;
            modeBarColors.normalColor = Color.clear;
            modeBarColors.highlightedColor = new Color(0.15f, 0.65f, 0.9f, 0.08f);
            modeBarColors.pressedColor = new Color(0.15f, 0.65f, 0.9f, 0.14f);
            toggleButton.colors = modeBarColors;

            RectTransform selectionPanel = CreateRect(
                "Special Mode Selection",
                gameplayHud,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -166f),
                new Vector2(460f, 500f));
            Image selectionBackground = selectionPanel.gameObject.AddComponent<Image>();
            selectionBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(selectionPanel, CaveUiTheme.BronzeLight, 4f);
            AddCornerOrnaments(selectionPanel, CaveUiTheme.BorderBright, 10f);

            Text panelCrest = CreateCenteredText(
                "Special Mode Crest",
                selectionPanel,
                font,
                "◆",
                22,
                new Vector2(0f, 237f),
                new Vector2(80f, 28f));
            panelCrest.color = CaveUiTheme.BorderBright;

            Button modesTabButton = CreateButton(
                "MODES",
                selectionPanel,
                font,
                new Vector2(-105f, 207f),
                new Vector2(194f, 42f));
            Button shopTabButton = CreateButton(
                "SHOP",
                selectionPanel,
                font,
                new Vector2(105f, 207f),
                new Vector2(194f, 42f));
            modesTabButton.GetComponentInChildren<Text>().fontSize = 18;
            shopTabButton.GetComponentInChildren<Text>().fontSize = 18;

            CreateCardSurface(
                "Active Mode Header",
                selectionPanel,
                new Vector2(0f, 163f),
                new Vector2(424f, 36f));
            Text panelCurrentMode = CreateCenteredText(
                "Selected Mode",
                selectionPanel,
                font,
                "ACTIVE MODE  •  SLOW SHOT",
                14,
                new Vector2(-77f, 163f),
                new Vector2(260f, 28f));
            panelCurrentMode.alignment = TextAnchor.MiddleLeft;
            panelCurrentMode.color = CaveUiTheme.PrimaryText;
            Text panelCurrency = CreateCenteredText(
                "Mode Currency",
                selectionPanel,
                font,
                "◇ 0",
                14,
                new Vector2(154f, 163f),
                new Vector2(120f, 28f));
            panelCurrency.alignment = TextAnchor.MiddleRight;
            panelCurrency.color = CaveUiTheme.Gold;

            RectTransform modesContent = CreateRect(
                "Modes Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -25f),
                new Vector2(424f, 368f));

            Button[] modeButtons = new Button[4];
            Text[] modeLabels = new Text[4];
            Text[] modeInfoTexts = new Text[4];
            Button[] upgradeButtons = new Button[4];
            string[] initialLabels = { "Slow Shot", "Burn Shot", "Flight", "Strength" };
            for (int index = 0; index < initialLabels.Length; index++)
            {
                float rowY = 132f - index * 88f;
                CreateCardSurface(initialLabels[index] + " Card", modesContent, new Vector2(0f, rowY), new Vector2(416f, 78f));
                modeInfoTexts[index] = CreateCenteredText(
                    initialLabels[index] + " Info",
                    modesContent,
                    font,
                    initialLabels[index].ToUpperInvariant() + "\nTier 1",
                    13,
                    new Vector2(-66f, rowY),
                    new Vector2(270f, 68f));
                modeInfoTexts[index].alignment = TextAnchor.MiddleLeft;

                modeButtons[index] = CreateButton(
                    initialLabels[index] + " Switch",
                    modesContent,
                    font,
                    new Vector2(154f, rowY + 17f),
                    new Vector2(96f, 30f));
                modeLabels[index] = modeButtons[index].GetComponentInChildren<Text>();
                modeLabels[index].fontSize = 13;

                upgradeButtons[index] = CreateButton(
                    initialLabels[index] + " Upgrade",
                    modesContent,
                    font,
                    new Vector2(154f, rowY - 17f),
                    new Vector2(96f, 30f));
                upgradeButtons[index].GetComponentInChildren<Text>().fontSize = 13;
            }

            RectTransform shopContent = CreateRect(
                "Shop Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f),
                new Vector2(424f, 370f));

            CreateCardSurface("Health Potion Card", shopContent, new Vector2(0f, 139f), new Vector2(416f, 62f));
            CreateCardSurface("Mana Potion Card", shopContent, new Vector2(0f, 71f), new Vector2(416f, 62f));
            CreateCardSurface("Landmine Card", shopContent, new Vector2(0f, 3f), new Vector2(416f, 62f));
            CreateItemIcon("Health Potion Icon", shopContent, font, new Vector2(-174f, 139f), "+", CaveUiTheme.Health);
            CreateItemIcon("Mana Potion Icon", shopContent, font, new Vector2(-174f, 71f), "✦", CaveUiTheme.Mana);
            CreateItemIcon("Landmine Icon", shopContent, font, new Vector2(-174f, 3f), "◎", CaveUiTheme.Gold);

            Text healthPotionText = CreateCenteredText(
                "Health Potion Info",
                shopContent,
                font,
                "HEALTH POTION\nRestore: 25%  |  Cost: 5",
                13,
                new Vector2(-43f, 139f),
                new Vector2(242f, 56f));
            healthPotionText.alignment = TextAnchor.MiddleLeft;
            Button healthPotionButton = CreateButton(
                "BUY",
                shopContent,
                font,
                new Vector2(158f, 139f),
                new Vector2(82f, 34f));
            healthPotionButton.GetComponentInChildren<Text>().fontSize = 14;

            Text manaPotionText = CreateCenteredText(
                "Mana Potion Info",
                shopContent,
                font,
                "MANA POTION\nRestore: 25%  |  Cost: 4",
                13,
                new Vector2(-43f, 71f),
                new Vector2(242f, 56f));
            manaPotionText.alignment = TextAnchor.MiddleLeft;
            Button manaPotionButton = CreateButton(
                "BUY",
                shopContent,
                font,
                new Vector2(158f, 71f),
                new Vector2(82f, 34f));
            manaPotionButton.GetComponentInChildren<Text>().fontSize = 14;

            Text landmineText = CreateCenteredText(
                "Landmine Info",
                shopContent,
                font,
                "LANDMINE\nCrowd control  |  Cost: 8\nOwned: 0  |  Place: Q",
                13,
                new Vector2(-43f, 3f),
                new Vector2(242f, 56f));
            landmineText.alignment = TextAnchor.MiddleLeft;
            Button landmineButton = CreateButton(
                "BUY MINE",
                shopContent,
                font,
                new Vector2(158f, 3f),
                new Vector2(82f, 34f));
            landmineButton.GetComponentInChildren<Text>().fontSize = 12;

            Text progressionDivider = CreateCenteredText(
                "Permanent Progression Header",
                shopContent,
                font,
                "◆  GENERAL SHARD UPGRADES  ◆",
                14,
                new Vector2(0f, -42f),
                new Vector2(404f, 26f));
            progressionDivider.fontStyle = FontStyle.Bold;
            progressionDivider.color = CaveUiTheme.GeneralShard;

            RectTransform progressionMount = CreateRect(
                "Permanent Progression Mount",
                shopContent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -119f),
                new Vector2(416f, 128f));

            RectTransform shardSummaryMount = CreateRect(
                "General Shards Summary Mount",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-130f, -229f),
                new Vector2(160f, 32f));

            Text feedbackText = CreateCenteredText(
                "Mode Feedback",
                selectionPanel,
                font,
                "Mode switches, upgrades, and potions spend Currency.",
                14,
                new Vector2(86f, -229f),
                new Vector2(244f, 34f));
            feedbackText.color = CaveUiTheme.Gold;

            PlayerSpecialModeHud modeHud = summaryPanel.gameObject.AddComponent<PlayerSpecialModeHud>();
            modeHud.Configure(
                currentModeText,
                panelCurrentMode,
                panelCurrency,
                feedbackText,
                selectionPanel.gameObject,
                toggleButton,
                modeButtons,
                modeLabels,
                modeInfoTexts,
                upgradeButtons,
                modesContent.gameObject,
                shopContent.gameObject,
                modesTabButton,
                shopTabButton,
                healthPotionText,
                manaPotionText,
                healthPotionButton,
                manaPotionButton,
                landmineText,
                landmineButton,
                progressionMount,
                shardSummaryMount);
            modeHud.Bind(specialMode, playerCurrency);
        }

        private static void EnsureShieldHud(Transform gameplayHud, Font font)
        {
            if (gameplayHud == null || Object.FindObjectOfType<PlayerShieldHud>(true) != null)
            {
                return;
            }

            RectTransform shieldPanel = CreateRect(
                "Player Strength Shield",
                gameplayHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -266f),
                new Vector2(326f, 66f));
            Image panelBackground = shieldPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(shieldPanel, CaveUiTheme.BorderBright);
            AddResourcePanelEndCap(shieldPanel, CaveUiTheme.BorderBright);
            CreateSymbolIcon(
                "Shield Icon",
                shieldPanel,
                font,
                new Vector2(-128f, 0f),
                52f,
                "◇",
                CaveUiTheme.BorderBright,
                28);

            Text statusText = CreateAnchoredText(
                "Shield Status",
                shieldPanel,
                font,
                "SHIELD  •  TIER I  •  INACTIVE",
                14,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -7f),
                new Vector2(236f, 24f));

            RectTransform barBackground = CreateRect(
                "Shield Recharge Background",
                shieldPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -42f),
                new Vector2(236f, 10f));
            Image backgroundImage = barBackground.gameObject.AddComponent<Image>();
            backgroundImage.color = CaveUiTheme.StaminaTrack;

            RectTransform fill = CreateStretchRect("Shield Recharge Fill", barBackground);
            fill.pivot = new Vector2(0f, 0.5f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = CaveUiTheme.BorderBright;

            PlayerShieldHud shieldHud = shieldPanel.gameObject.AddComponent<PlayerShieldHud>();
            shieldHud.Configure(statusText, fillImage);
            shieldHud.Bind(Object.FindObjectOfType<PlayerStrengthShield>());
        }

        private static void EnsureWorldLevelHud(Transform gameplayHud, Font font)
        {
            if (gameplayHud == null || Object.FindObjectOfType<WorldLevelHud>(true) != null)
            {
                return;
            }

            RectTransform panel = CreateRect(
                "World Level",
                gameplayHud,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(360f, 118f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = CaveUiTheme.Surface;
            panelImage.raycastTarget = false;
            AddPanelFrame(panel, CaveUiTheme.BronzeLight, 4f);
            AddCornerOrnaments(panel, CaveUiTheme.BorderBright, 10f);
            AddWorldLevelWings(panel);

            Text label = CreateCenteredText(
                "World Level Label",
                panel,
                font,
                "WORLD LEVEL",
                20,
                new Vector2(0f, 40f),
                new Vector2(300f, 28f));
            label.fontStyle = FontStyle.Bold;
            label.color = CaveUiTheme.Gold;

            Text value = CreateSymbolIcon(
                "World Level Crest",
                panel,
                font,
                new Vector2(0f, -18f),
                72f,
                "0",
                CaveUiTheme.BorderBright,
                40);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 24;
            value.resizeTextMaxSize = 40;

            RectTransform announcement = CreateRect(
                "World Level Announcement",
                gameplayHud,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -250f),
                new Vector2(340f, 44f));
            Text announcementText = announcement.gameObject.AddComponent<Text>();
            announcementText.font = font;
            announcementText.fontSize = 24;
            announcementText.fontStyle = FontStyle.Bold;
            announcementText.alignment = TextAnchor.MiddleCenter;
            announcementText.color = CaveUiTheme.Gold;
            Outline announcementOutline = announcement.gameObject.AddComponent<Outline>();
            announcementOutline.effectColor = Color.black;
            announcementOutline.effectDistance = new Vector2(2f, -2f);
            CanvasGroup announcementGroup = announcement.gameObject.AddComponent<CanvasGroup>();
            announcementGroup.alpha = 0f;
            announcementGroup.blocksRaycasts = false;
            announcementGroup.interactable = false;

            WorldLevelHud worldLevelHud = panel.gameObject.AddComponent<WorldLevelHud>();
            worldLevelHud.Configure(value, announcementText, announcementGroup);
            worldLevelHud.Bind(Object.FindObjectOfType<WorldDifficultyManager>());
        }

        private static void EnsureDetectiveTowerStatusHud(Transform gameplayHud, Font font)
        {
            if (gameplayHud == null
                || Object.FindObjectOfType<DetectiveTowerStatusHud>(true) != null)
            {
                return;
            }

            Transform existingAnnouncement = gameplayHud.Find("World Level Announcement");
            if (existingAnnouncement is RectTransform announcementRect)
            {
                announcementRect.anchoredPosition = new Vector2(0f, -250f);
            }

            RectTransform panel = CreateRect(
                "Detective Tower Status",
                gameplayHud,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -142f),
                new Vector2(360f, 96f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = CaveUiTheme.Surface;
            panelImage.raycastTarget = false;
            AddPanelFrame(panel, CaveUiTheme.BronzeLight, 3f);
            AddCornerOrnaments(panel, CaveUiTheme.BorderBright, 7f);

            CaveUiPackageSkin skin = Resources.Load<CaveUiPackageSkin>("CaveUiPackageSkin");
            if (skin != null && skin.HorizontalFrame != null)
            {
                panelImage.sprite = skin.HorizontalFrame;
                panelImage.type = Image.Type.Simple;
                panelImage.color = PackageTint(CaveUiTheme.BronzeLight, 0.88f);
            }

            Text title = CreateCenteredText(
                "Tower Stage",
                panel,
                font,
                "TOWER  •  STAGE 0",
                15,
                new Vector2(-48f, 28f),
                new Vector2(250f, 24f));
            title.fontStyle = FontStyle.Bold;
            title.color = CaveUiTheme.BronzeLight;
            title.raycastTarget = false;

            Text threat = CreateCenteredText(
                "Tower Threat Marks",
                panel,
                font,
                "◆",
                13,
                new Vector2(122f, 28f),
                new Vector2(86f, 24f));
            threat.fontStyle = FontStyle.Bold;
            threat.color = CaveUiTheme.BronzeLight;
            threat.raycastTarget = false;

            Text recovery = CreateCenteredText(
                "Tower Recovery Suppression",
                panel,
                font,
                "REGEN  HP 65%  •  STA 70%  •  MANA 70%",
                12,
                new Vector2(0f, 1f),
                new Vector2(332f, 22f));
            recovery.fontStyle = FontStyle.Bold;
            recovery.color = CaveUiTheme.PrimaryText;
            recovery.raycastTarget = false;

            Text radius = CreateCenteredText(
                "Tower Field Radius",
                panel,
                font,
                "FIELD  4.5 / 7.5  •  EXPANDING",
                12,
                new Vector2(0f, -26f),
                new Vector2(332f, 22f));
            radius.color = CaveUiTheme.SecondaryText;
            radius.raycastTarget = false;

            CanvasGroup canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            DetectiveTowerStatusHud hud = panel.gameObject.AddComponent<DetectiveTowerStatusHud>();
            hud.Configure(canvasGroup, panelImage, title, recovery, radius, threat);
        }

        private static void EnsureResourceGainPopups(
            Transform gameplayHud,
            Font font,
            PlayerHealth playerHealth,
            SpinSwordAttack spinSwordAttack,
            PlayerMana playerMana,
            PlayerCurrency playerCurrency)
        {
            if (gameplayHud == null || Object.FindObjectOfType<ResourceGainPopupController>(true) != null)
            {
                return;
            }

            RectTransform popupLayer = CreateStretchRect("Resource Gain Popups", gameplayHud);
            ResourceGainPopupController controller = popupLayer.gameObject.AddComponent<ResourceGainPopupController>();
            controller.Configure(
                popupLayer,
                font,
                playerHealth,
                spinSwordAttack,
                playerMana,
                playerCurrency);
        }

        private static void EnsureActionHotbar(
            Transform gameplayHud,
            Font font,
            GameObject player)
        {
            if (gameplayHud == null || Object.FindObjectOfType<PlayerActionHotbarHud>(true) != null)
            {
                return;
            }

            RectTransform hotbar = CreateRect(
                "Player Action Hotbar",
                gameplayHud,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 22f),
                new Vector2(378f, 82f));

            string[] slotNames = { "HEALTH", "MANA", "MINE", "RESERVED", "RESERVED" };
            string[] symbols = { "+", "✦", "◎", "·", "·" };
            Color[] accents =
            {
                CaveUiTheme.Health,
                CaveUiTheme.Mana,
                CaveUiTheme.Gold,
                CaveUiTheme.Iron,
                CaveUiTheme.Iron
            };
            GameAction[] actions =
            {
                GameAction.UseHealthPotion,
                GameAction.UseManaPotion,
                GameAction.PlaceLandmine
            };

            Image[] backgrounds = new Image[slotNames.Length];
            Text[] keyLabels = new Text[slotNames.Length];
            Text[] quantityLabels = new Text[slotNames.Length];
            for (int index = 0; index < slotNames.Length; index++)
            {
                float x = 36f + index * 76f;
                RectTransform slot = CreateRect(
                    slotNames[index] + " Slot",
                    hotbar,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(x, 41f),
                    new Vector2(68f, 76f));
                backgrounds[index] = slot.gameObject.AddComponent<Image>();
                backgrounds[index].color = CaveUiTheme.SurfaceInset;
                backgrounds[index].raycastTarget = false;
                AddPanelFrame(slot, accents[index], index == 0 ? 3f : 2f);

                Text itemName = CreateCenteredText(
                    slotNames[index] + " Name",
                    slot,
                    font,
                    slotNames[index],
                    9,
                    new Vector2(0f, 27f),
                    new Vector2(60f, 16f));
                itemName.color = index < actions.Length
                    ? CaveUiTheme.PrimaryText
                    : CaveUiTheme.SecondaryText;
                itemName.raycastTarget = false;

                Text symbol = CreateCenteredText(
                    slotNames[index] + " Symbol",
                    slot,
                    font,
                    symbols[index],
                    24,
                    new Vector2(0f, 4f),
                    new Vector2(52f, 34f));
                symbol.color = accents[index];
                symbol.raycastTarget = false;

                keyLabels[index] = CreateCenteredText(
                    slotNames[index] + " Key",
                    slot,
                    font,
                    index < actions.Length
                        ? GameInput.Bindings.GetBinding(actions[index]).Primary.ToString()
                        : string.Empty,
                    12,
                    new Vector2(-19f, -25f),
                    new Vector2(28f, 20f));
                keyLabels[index].color = CaveUiTheme.Gold;
                keyLabels[index].raycastTarget = false;

                quantityLabels[index] = CreateCenteredText(
                    slotNames[index] + " Owned Count",
                    slot,
                    font,
                    index < actions.Length ? "x0" : string.Empty,
                    12,
                    new Vector2(18f, -25f),
                    new Vector2(30f, 20f));
                quantityLabels[index].color = CaveUiTheme.PrimaryText;
                quantityLabels[index].raycastTarget = false;
            }

            PlayerActionHotbarHud hotbarHud = hotbar.gameObject.AddComponent<PlayerActionHotbarHud>();
            hotbarHud.Configure(
                backgrounds,
                keyLabels,
                quantityLabels,
                player != null ? player.GetComponent<PlayerLandmineInventory>() : null);
        }

        private static void EnsureDetectiveResearchHud(Transform gameplayHud, Font font)
        {
            if (gameplayHud == null
                || Object.FindObjectOfType<DetectiveResearchHud>(true) != null)
            {
                return;
            }

            RectTransform panel = CreateRect(
                "Detective Research HUD",
                gameplayHud,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-200f, 112f),
                new Vector2(520f, 104f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = CaveUiTheme.Surface;
            panelImage.raycastTarget = false;
            AddPanelFrame(panel, CaveUiTheme.BorderBright, 2f);
            AddCornerOrnaments(panel, CaveUiTheme.BronzeLight, 7f);

            CaveUiPackageSkin skin = Resources.Load<CaveUiPackageSkin>("CaveUiPackageSkin");
            if (skin != null && skin.HorizontalFrame != null)
            {
                panelImage.sprite = skin.HorizontalFrame;
                panelImage.type = Image.Type.Simple;
                panelImage.color = PackageTint(CaveUiTheme.BorderBright, 0.9f);
            }

            Text observationLabel = CreateCenteredText(
                "Observation Label",
                panel,
                font,
                "DETECTIVE STUDY  •  OBSERVING",
                13,
                new Vector2(0f, 34f),
                new Vector2(470f, 22f));
            observationLabel.fontStyle = FontStyle.Bold;
            observationLabel.color = CaveUiTheme.PrimaryText;

            const int slotCount = 6;
            Text[] historyTexts = new Text[slotCount];
            Image[] historyFrames = new Image[slotCount];
            for (int index = 0; index < slotCount; index++)
            {
                float x = -192f + index * 76f;
                RectTransform slot = CreateRect(
                    "Observed Action " + (index + 1),
                    panel,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(x, -5f),
                    new Vector2(62f, 36f));
                historyFrames[index] = slot.gameObject.AddComponent<Image>();
                historyFrames[index].color = CaveUiTheme.SurfaceInset;
                historyFrames[index].raycastTarget = false;
                AddPanelFrame(slot, CaveUiTheme.Border, 1f);
                if (skin != null && skin.IconFrame != null)
                {
                    historyFrames[index].sprite = skin.IconFrame;
                    historyFrames[index].type = Image.Type.Simple;
                }

                historyTexts[index] = CreateCenteredText(
                    "Action Symbol",
                    slot,
                    font,
                    "·",
                    13,
                    Vector2.zero,
                    new Vector2(56f, 28f));
                historyTexts[index].fontStyle = FontStyle.Bold;
                historyTexts[index].raycastTarget = false;

                if (index < slotCount - 1)
                {
                    Text arrow = CreateCenteredText(
                        "Sequence Arrow " + (index + 1),
                        panel,
                        font,
                        ">",
                        13,
                        new Vector2(x + 38f, -5f),
                        new Vector2(14f, 24f));
                    arrow.color = CaveUiTheme.BronzeLight;
                    arrow.raycastTarget = false;
                }
            }

            RectTransform predictionSlot = CreateRect(
                "Predicted Action",
                panel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(223f, -5f),
                new Vector2(104f, 48f));
            Image predictionFrame = predictionSlot.gameObject.AddComponent<Image>();
            predictionFrame.color = CaveUiTheme.SurfaceInset;
            predictionFrame.raycastTarget = false;
            AddPanelFrame(predictionSlot, CaveUiTheme.Gold, 2f);
            if (skin != null && skin.IconFrame != null)
            {
                predictionFrame.sprite = skin.IconFrame;
                predictionFrame.type = Image.Type.Simple;
            }

            Text predictedText = CreateCenteredText(
                "Predicted Symbol",
                predictionSlot,
                font,
                "NEXT\n?",
                12,
                Vector2.zero,
                new Vector2(94f, 42f));
            predictedText.fontStyle = FontStyle.Bold;
            predictedText.color = CaveUiTheme.Gold;
            predictedText.raycastTarget = false;

            Text outcomeText = CreateCenteredText(
                "Research Outcome",
                panel,
                font,
                string.Empty,
                12,
                new Vector2(0f, -38f),
                new Vector2(470f, 20f));
            outcomeText.fontStyle = FontStyle.Bold;
            outcomeText.raycastTarget = false;

            CanvasGroup canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            DetectiveResearchHud hud = panel.gameObject.AddComponent<DetectiveResearchHud>();
            hud.Configure(
                canvasGroup,
                observationLabel,
                historyTexts,
                historyFrames,
                predictedText,
                predictionFrame,
                outcomeText);
        }

        private static void EnsureStatusEffectHud(Transform gameplayHud, Font font, GameObject player)
        {
            if (gameplayHud == null || Object.FindObjectOfType<PlayerStatusEffectHud>(true) != null)
            {
                return;
            }

            RepositionTopLeftPanel(gameplayHud, "Player Stamina", new Vector2(24f, -142f));
            RepositionTopLeftPanel(gameplayHud, "Player Mana", new Vector2(24f, -222f));
            RepositionTopLeftPanel(gameplayHud, "Player Strength Shield", new Vector2(24f, -302f));

            RectTransform row = CreateRect(
                "Player Status Effects",
                gameplayHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -108f),
                new Vector2(350f, 28f));

            const int slotCount = 6;
            GameObject[] slots = new GameObject[slotCount];
            Text[] labels = new Text[slotCount];
            Color[] colors =
            {
                new Color(0.48f, 1f, 0.25f, 1f),
                CaveUiTheme.Stamina,
                CaveUiTheme.Gold,
                CaveUiTheme.Mana,
                CaveUiTheme.BorderBright,
                new Color(0.75f, 0.35f, 1f, 1f)
            };
            for (int index = 0; index < slotCount; index++)
            {
                RectTransform slot = CreateRect(
                    "Status Slot " + (index + 1),
                    row,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(0f, 0.5f),
                    new Vector2(24f + index * 58f, 14f),
                    new Vector2(50f, 24f));
                Image image = slot.gameObject.AddComponent<Image>();
                image.color = CaveUiTheme.SurfaceInset;
                image.raycastTarget = true;
                AddPanelFrame(slot, colors[index], 1f);
                labels[index] = CreateCenteredText(
                    "Status Icon",
                    slot,
                    font,
                    string.Empty,
                    14,
                    Vector2.zero,
                    new Vector2(44f, 20f));
                labels[index].fontStyle = FontStyle.Bold;
                labels[index].color = colors[index];
                labels[index].raycastTarget = false;
                slots[index] = slot.gameObject;
            }

            RectTransform tooltip = CreateRect(
                "Status Tooltip",
                row,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(140f, -8f),
                new Vector2(300f, 72f));
            Image tooltipBackground = tooltip.gameObject.AddComponent<Image>();
            tooltipBackground.color = CaveUiTheme.Surface;
            tooltipBackground.raycastTarget = false;
            AddPanelFrame(tooltip, CaveUiTheme.BronzeLight, 2f);
            Text tooltipTitle = CreateCenteredText(
                "Tooltip Status Name",
                tooltip,
                font,
                string.Empty,
                13,
                new Vector2(0f, 21f),
                new Vector2(276f, 22f));
            tooltipTitle.fontStyle = FontStyle.Bold;
            tooltipTitle.color = CaveUiTheme.Gold;
            tooltipTitle.raycastTarget = false;
            Text tooltipBody = CreateCenteredText(
                "Tooltip Description",
                tooltip,
                font,
                string.Empty,
                11,
                new Vector2(0f, -12f),
                new Vector2(276f, 42f));
            tooltipBody.color = CaveUiTheme.PrimaryText;
            tooltipBody.raycastTarget = false;
            CanvasGroup tooltipGroup = tooltip.gameObject.AddComponent<CanvasGroup>();
            StatusTooltipPanel tooltipPanel = tooltip.gameObject.AddComponent<StatusTooltipPanel>();
            tooltipPanel.Configure(tooltipGroup, tooltipTitle, tooltipBody);

            PlayerStatusEffectHud hud = row.gameObject.AddComponent<PlayerStatusEffectHud>();
            hud.Configure(slots, labels, player, tooltipPanel);
        }

        private static void EnsureWizardWarpWarningHud(
            Transform gameplayHud,
            Font font,
            GameObject player)
        {
            if (gameplayHud == null || Object.FindObjectOfType<WizardWarpWarningHud>(true) != null)
            {
                return;
            }

            RectTransform panel = CreateRect(
                "Wizard Warp Warning",
                gameplayHud,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -118f),
                new Vector2(420f, 74f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = CaveUiTheme.Surface;
            background.raycastTarget = false;
            AddPanelFrame(panel, new Color(0.72f, 0.3f, 1f, 1f), 2f);

            Text title = CreateCenteredText(
                "Warp Warning Title",
                panel,
                font,
                "LOW MANA • WARP MARK",
                18,
                new Vector2(0f, 14f),
                new Vector2(390f, 24f));
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.88f, 0.68f, 1f, 1f);
            Text prompt = CreateCenteredText(
                "Warp Resist Prompt",
                panel,
                font,
                string.Empty,
                14,
                new Vector2(0f, -14f),
                new Vector2(390f, 22f));
            prompt.color = CaveUiTheme.Gold;

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            WizardWarpWarningHud hud = panel.gameObject.AddComponent<WizardWarpWarningHud>();
            hud.Configure(group, title, prompt, player);
        }

        private static void EnsureCurseHud(Transform gameplayHud, Font font, GameObject player)
        {
            if (gameplayHud == null || Object.FindObjectOfType<PlayerCurseHud>(true) != null)
            {
                return;
            }

            RectTransform panel = CreateRect(
                "Active Curse HUD",
                gameplayHud,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(414f, 22f),
                new Vector2(230f, 82f));
            Text title = CreateCenteredText(
                "Curse Header",
                panel,
                font,
                "ACTIVE CURSES",
                10,
                new Vector2(0f, 66f),
                new Vector2(224f, 16f));
            title.color = CaveUiTheme.BronzeLight;

            GameObject distraction = CreateCurseIcon(panel, font, "DISTRACTION", "◇", -76f);
            GameObject detective = CreateCurseIcon(panel, font, "DETECTIVE", "◉", 0f);
            GameObject avarice = CreateCurseIcon(panel, font, "AVARICE", "◆", 76f);
            Text avariceLabel = avarice.transform.Find("Curse Name")?.GetComponent<Text>();
            Text deathClaimNotice = CreateCenteredText(
                "Avarice Death Claim Notice",
                gameplayHud,
                font,
                string.Empty,
                18,
                new Vector2(0f, -92f),
                new Vector2(390f, 58f));
            deathClaimNotice.fontStyle = FontStyle.Bold;
            deathClaimNotice.color = CaveUiTheme.Gold;
            PlayerCurseHud hud = panel.gameObject.AddComponent<PlayerCurseHud>();
            hud.Configure(
                distraction,
                detective,
                avarice,
                avariceLabel,
                deathClaimNotice,
                player != null ? player.GetComponent<PlayerCurseController>() : null);
        }

        private static GameObject CreateCurseIcon(
            Transform parent,
            Font font,
            string label,
            string symbol,
            float x)
        {
            RectTransform slot = CreateRect(
                label + " Curse",
                parent,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f),
                new Vector2(x + 77f, 32f),
                new Vector2(68f, 54f));
            Image image = slot.gameObject.AddComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            AddPanelFrame(slot, new Color(0.62f, 0.3f, 1f, 1f), 1f);
            Text icon = CreateCenteredText(
                "Curse Symbol",
                slot,
                font,
                symbol,
                19,
                new Vector2(0f, 8f),
                new Vector2(60f, 26f));
            icon.color = new Color(0.72f, 0.45f, 1f, 1f);
            Text name = CreateCenteredText(
                "Curse Name",
                slot,
                font,
                label,
                8,
                new Vector2(0f, -17f),
                new Vector2(64f, 14f));
            name.color = CaveUiTheme.PrimaryText;
            return slot.gameObject;
        }

        private static void EnsureCurseAltarHud(Transform gameplayHud, Font font)
        {
            if (gameplayHud == null || Object.FindObjectOfType<CurseAltarHud>(true) != null)
            {
                return;
            }

            RectTransform panel = CreateRect(
                "Curse Altar Selection",
                gameplayHud,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1120f, 650f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.018f, 0.014f, 0.028f, 0.98f);
            AddPanelFrame(panel, new Color(0.6f, 0.28f, 1f, 1f), 4f);
            AddCornerOrnaments(panel, CaveUiTheme.BronzeLight, 11f);

            Text title = CreateCenteredText(
                "Altar Title", panel, font, "THE CAVE'S CURSES", 25,
                new Vector2(0f, 286f), new Vector2(900f, 40f));
            title.fontStyle = FontStyle.Bold;
            title.color = CaveUiTheme.Gold;
            Text subtitle = CreateCenteredText(
                "Altar Subtitle", panel, font, "Desire power? Can you handle its price?", 14,
                new Vector2(0f, 250f), new Vector2(820f, 28f));
            subtitle.color = CaveUiTheme.SecondaryText;

            string[] symbols = { "◉", "⌕", "◆", "◈", "◉" };
            string[] labels =
            {
                "CURSE OF\nINSANITY",
                "DETECTIVE'S\nCURSE",
                "CURSE OF\nAVARICE",
                "CURSE OF\nSTONEGLASS",
                "THE CAVE'S\nGLARE"
            };
            Vector2[] positions =
            {
                new Vector2(-432f, 105f), new Vector2(-216f, 105f), new Vector2(0f, 105f),
                new Vector2(216f, 105f), new Vector2(432f, 105f)
            };
            Button[] curseButtons = new Button[5];
            Image[] curseCards = new Image[5];
            for (int index = 0; index < curseButtons.Length; index++)
            {
                curseButtons[index] = CreateButton(
                    symbols[index] + "\n\n" + labels[index], panel, font, positions[index], new Vector2(196f, 180f));
                curseCards[index] = curseButtons[index].GetComponent<Image>();
                curseCards[index].color = CaveUiTheme.SurfaceInset;
                AddPanelFrame(curseButtons[index].GetComponent<RectTransform>(), new Color(0.55f, 0.25f, 0.82f, 1f), 1f);
            }

            Image detailCard = CreateCardSurface(
                "Selected Curse Detail", panel, new Vector2(0f, -132f), new Vector2(820f, 148f));
            Text detailTitle = CreateCenteredText(
                "Selected Curse Name", panel, font, "CURSE OF INSANITY", 20,
                new Vector2(0f, -88f), new Vector2(730f, 30f));
            detailTitle.color = CaveUiTheme.Gold;
            Text detailBody = CreateCenteredText(
                "Selected Curse Detail", panel, font, string.Empty, 14,
                new Vector2(0f, -140f), new Vector2(710f, 76f));
            detailBody.alignment = TextAnchor.MiddleLeft;
            Text activeState = CreateCenteredText(
                "Selected Curse State", panel, font, "◇ UNBOUND", 13,
                new Vector2(-205f, -208f), new Vector2(190f, 28f));
            Button actionButton = CreateButton(
                "ACCEPT CURSE", panel, font, new Vector2(115f, -208f), new Vector2(210f, 38f));
            Button close = CreateButton(
                "LEAVE ALTAR", panel, font, new Vector2(0f, -284f), new Vector2(200f, 38f));
            CurseAltarHud hud = panel.gameObject.AddComponent<CurseAltarHud>();
            hud.Configure(
                panel.gameObject,
                curseButtons,
                curseCards,
                detailTitle,
                detailBody,
                activeState,
                actionButton,
                close);
        }

        private static void RepositionTopLeftPanel(Transform root, string name, Vector2 position)
        {
            Transform panel = root.Find(name);
            if (panel is RectTransform rect)
            {
                rect.anchoredPosition = position;
            }
        }

        private static void EnsureModeBarClickTarget(PlayerSpecialModeHud hud)
        {
            if (hud == null || hud.GetComponent<Button>() != null)
            {
                return;
            }

            Button button = hud.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(hud.ToggleSelectionPanelFromUi);
        }

        private static void AddPanelFrame(RectTransform panel, Color color, float distance = 2f)
        {
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = CaveUiTheme.IronDark;
            outline.effectDistance = new Vector2(distance + 2f, -(distance + 2f));
            outline.useGraphicAlpha = true;

            CreateFrameStrip("Iron Top", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(-10f, 6f), CaveUiTheme.Iron);
            CreateFrameStrip("Iron Bottom", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f), new Vector2(-10f, 6f), CaveUiTheme.IronDark);
            CreateFrameStrip("Iron Left", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f), new Vector2(6f, -10f), CaveUiTheme.Iron);
            CreateFrameStrip("Iron Right", panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3f, 0f), new Vector2(6f, -10f), CaveUiTheme.IronDark);

            Color accent = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.82f));
            CreateFrameStrip("Bronze Top Inlay", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -7f), new Vector2(-18f, 2f), CaveUiTheme.BronzeLight);
            CreateFrameStrip("Accent Bottom Inlay", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 7f), new Vector2(-18f, 2f), accent);
        }

        private static void ApplyFreeUiPackageSkin(Transform gameplayHud)
        {
            if (gameplayHud == null)
            {
                return;
            }

            CaveUiPackageSkin skin = Resources.Load<CaveUiPackageSkin>("CaveUiPackageSkin");
            if (skin == null)
            {
                return;
            }

            ApplyPackageFrame(gameplayHud, "Player Health", skin.HorizontalFrame, CaveUiTheme.Health);
            ApplyPackageFrame(gameplayHud, "Player Stamina", skin.HorizontalFrame, CaveUiTheme.Stamina);
            ApplyPackageFrame(gameplayHud, "Player Mana", skin.HorizontalFrame, CaveUiTheme.Mana);
            ApplyPackageFrame(gameplayHud, "Player Strength Shield", skin.HorizontalFrame, CaveUiTheme.BorderBright);
            ApplyPackageFrame(gameplayHud, "World Level", skin.HorizontalFrame, CaveUiTheme.BronzeLight);
            ApplyPackageFrame(gameplayHud, "Player Currency", skin.HorizontalFrame, CaveUiTheme.Gold);
            ApplyPackageFrame(gameplayHud, "Player Special Mode", skin.HorizontalFrame, CaveUiTheme.BorderBright);
            ApplyPackageFrame(gameplayHud, "Special Mode Selection", skin.PanelFrame, CaveUiTheme.BronzeLight);

            ApplyPackageIcon(gameplayHud, "Player Health/Health Icon", skin.IconFrame, CaveUiTheme.Health);
            ApplyPackageIcon(gameplayHud, "Player Stamina/Stamina Icon", skin.IconFrame, CaveUiTheme.Stamina);
            ApplyPackageIcon(gameplayHud, "Player Mana/Mana Icon", skin.IconFrame, CaveUiTheme.Mana);
            ApplyPackageIcon(gameplayHud, "Player Strength Shield/Shield Icon", skin.IconFrame, CaveUiTheme.BorderBright);
            ApplyPackageIcon(gameplayHud, "Player Currency/Currency Icon", skin.IconFrame, CaveUiTheme.Gold);
            ApplyPackageIcon(gameplayHud, "Player Special Mode/Mode Icon", skin.IconFrame, CaveUiTheme.BorderBright);
            ApplyPackageIcon(gameplayHud, "World Level/World Level Crest", skin.Gem, CaveUiTheme.BorderBright);

            Transform selectionPanel = gameplayHud.Find("Special Mode Selection");
            if (selectionPanel != null)
            {
                AddPackageRod(selectionPanel, skin.LongRod);
            }

            Transform hotbar = gameplayHud.Find("Player Action Hotbar");
            if (hotbar != null)
            {
                Image hotbarBackground = hotbar.GetComponent<Image>();
                if (hotbarBackground == null)
                {
                    hotbarBackground = hotbar.gameObject.AddComponent<Image>();
                }

                hotbarBackground.sprite = skin.HorizontalFrame;
                hotbarBackground.type = Image.Type.Simple;
                hotbarBackground.color = PackageTint(CaveUiTheme.BronzeLight, 0.9f);
                hotbarBackground.raycastTarget = false;

                string[] slotNames =
                {
                    "HEALTH Slot",
                    "MANA Slot",
                    "MINE Slot",
                    "RESERVED Slot"
                };

                foreach (string slotName in slotNames)
                {
                    foreach (Transform child in hotbar)
                    {
                        if (child.name != slotName)
                        {
                            continue;
                        }

                        Image slotImage = child.GetComponent<Image>();
                        if (slotImage != null)
                        {
                            slotImage.sprite = skin.IconFrame;
                            slotImage.type = Image.Type.Simple;
                            slotImage.preserveAspect = false;
                        }
                    }
                }
            }
        }

        private static void ApplyPackageFrame(
            Transform root,
            string panelPath,
            Sprite frameSprite,
            Color accent)
        {
            if (frameSprite == null)
            {
                return;
            }

            Transform panel = root.Find(panelPath);
            if (panel == null || panel.Find("Free UI Package Frame") != null)
            {
                return;
            }

            RectTransform frame = CreateStretchRect("Free UI Package Frame", panel);
            frame.offsetMin = new Vector2(-3f, -3f);
            frame.offsetMax = new Vector2(3f, 3f);
            Image image = frame.gameObject.AddComponent<Image>();
            image.sprite = frameSprite;
            image.type = Image.Type.Simple;
            image.color = PackageTint(accent, 0.88f);
            image.raycastTarget = false;
            frame.SetAsFirstSibling();
        }

        private static void ApplyPackageIcon(
            Transform root,
            string iconPath,
            Sprite iconSprite,
            Color accent)
        {
            if (iconSprite == null)
            {
                return;
            }

            Transform icon = root.Find(iconPath);
            Image image = icon != null ? icon.GetComponent<Image>() : null;
            if (image == null)
            {
                return;
            }

            image.sprite = iconSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = PackageTint(accent, 0.95f);
        }

        private static void AddPackageRod(Transform panel, Sprite rodSprite)
        {
            if (rodSprite == null || panel.Find("Free UI Package Header Rod") != null)
            {
                return;
            }

            RectTransform rod = CreateRect(
                "Free UI Package Header Rod",
                panel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -64f),
                new Vector2(356f, 12f));
            Image image = rod.gameObject.AddComponent<Image>();
            image.sprite = rodSprite;
            image.type = Image.Type.Simple;
            image.color = PackageTint(CaveUiTheme.BronzeLight, 0.86f);
            image.raycastTarget = false;
            rod.SetAsFirstSibling();
        }

        private static Color PackageTint(Color accent, float alpha)
        {
            Color neutralMetal = new Color(0.72f, 0.68f, 0.62f, 1f);
            Color tint = Color.Lerp(neutralMetal, accent, 0.32f);
            tint.a = alpha;
            return tint;
        }

        private static void AddResourcePanelEndCap(RectTransform panel, Color accent)
        {
            RectTransform cap = CreateRect(
                "Forged End Cap",
                panel,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-3f, 0f),
                new Vector2(18f, 18f));
            cap.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image capImage = cap.gameObject.AddComponent<Image>();
            capImage.color = CaveUiTheme.IronLight;
            capImage.raycastTarget = false;

            RectTransform rune = CreateRect(
                "End Cap Rune",
                panel,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-3f, 0f),
                new Vector2(6f, 6f));
            rune.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image runeImage = rune.gameObject.AddComponent<Image>();
            runeImage.color = accent;
            runeImage.raycastTarget = false;
        }

        private static void AddWorldLevelWings(RectTransform panel)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                RectTransform wing = CreateRect(
                    side < 0 ? "Left Forged Wing" : "Right Forged Wing",
                    panel,
                    new Vector2(side < 0 ? 0f : 1f, 0.5f),
                    new Vector2(side < 0 ? 0f : 1f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(side * 12f, -2f),
                    new Vector2(52f, 24f));
                wing.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
                Image wingImage = wing.gameObject.AddComponent<Image>();
                wingImage.color = CaveUiTheme.Iron;
                wingImage.raycastTarget = false;

                RectTransform rune = CreateRect(
                    side < 0 ? "Left World Rune" : "Right World Rune",
                    panel,
                    new Vector2(side < 0 ? 0f : 1f, 0.5f),
                    new Vector2(side < 0 ? 0f : 1f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(side * 42f, 8f),
                    new Vector2(12f, 12f));
                rune.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image runeImage = rune.gameObject.AddComponent<Image>();
                runeImage.color = CaveUiTheme.BorderBright;
                runeImage.raycastTarget = false;
            }
        }

        private static void CreateFrameStrip(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            RectTransform strip = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                new Vector2(0.5f, 0.5f),
                position,
                size);
            Image stripImage = strip.gameObject.AddComponent<Image>();
            stripImage.color = color;
            stripImage.raycastTarget = false;
        }

        private static void AddCornerOrnaments(RectTransform panel, Color color, float size)
        {
            Vector2[] anchors =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f)
            };

            for (int index = 0; index < anchors.Length; index++)
            {
                Vector2 anchor = anchors[index];
                Vector2 offset = new Vector2(anchor.x < 0.5f ? 5f : -5f, anchor.y < 0.5f ? 5f : -5f);
                RectTransform ornament = CreateRect(
                    "Corner Ornament " + index,
                    panel,
                    anchor,
                    anchor,
                    new Vector2(0.5f, 0.5f),
                    offset,
                    new Vector2(size, size));
                ornament.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image ornamentImage = ornament.gameObject.AddComponent<Image>();
                ornamentImage.color = color;
                ornamentImage.raycastTarget = false;
            }
        }

        private static Image CreateCardSurface(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            RectTransform card = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size);
            Image image = card.gameObject.AddComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            AddPanelFrame(card, CaveUiTheme.Border, 1f);
            return image;
        }

        private static void EnsureFrenzyBreakHud(
            Transform gameplayHud,
            Font font,
            GameObject player)
        {
            if (gameplayHud == null || player == null)
            {
                return;
            }

            PlayerCombatFlow flow = player.GetComponent<PlayerCombatFlow>();
            if (flow == null)
            {
                if (player.GetComponent<SpinSwordAttack>() == null)
                {
                    return;
                }

                flow = player.AddComponent<PlayerCombatFlow>();
            }

            FrenzyBreakHud existing = Object.FindObjectOfType<FrenzyBreakHud>(true);
            if (existing != null)
            {
                Text existingText = existing.GetComponentInChildren<Text>(true);
                existing.Configure(existingText, player);
                return;
            }

            RectTransform panel = CreateRect(
                "Frenzy Break State",
                gameplayHud,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(390f, 42f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.07f, 0.90f);
            AddPanelFrame(panel, CaveUiTheme.BorderBright);
            Text state = CreateCenteredText(
                "Frenzy State Text",
                panel,
                font,
                "FRENZY BREAK",
                15,
                Vector2.zero,
                new Vector2(364f, 30f));
            state.color = CaveUiTheme.Gold;
            state.fontStyle = FontStyle.Bold;
            FrenzyBreakHud hud = panel.gameObject.AddComponent<FrenzyBreakHud>();
            hud.Configure(state, player);
        }

        private static void CreateItemIcon(
            string name,
            Transform parent,
            Font font,
            Vector2 position,
            string symbol,
            Color accent)
        {
            CreateSymbolIcon(name, parent, font, position, 54f, symbol, accent, 28);
        }

        private static Text CreateSymbolIcon(
            string name,
            Transform parent,
            Font font,
            Vector2 position,
            float size,
            string symbol,
            Color accent,
            int fontSize)
        {
            RectTransform icon = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                new Vector2(size, size));
            Image image = icon.gameObject.AddComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            AddPanelFrame(icon, accent, 2f);

            RectTransform symbolRect = CreateStretchRect("Symbol", icon);
            Text symbolText = symbolRect.gameObject.AddComponent<Text>();
            symbolText.font = font;
            symbolText.fontSize = fontSize;
            symbolText.fontStyle = FontStyle.Bold;
            symbolText.alignment = TextAnchor.MiddleCenter;
            symbolText.color = accent;
            symbolText.text = symbol;
            symbolText.raycastTarget = false;
            return symbolText;
        }

        private static GameObject CreateMenuPanel(string name, Transform parent, Vector2 size)
        {
            RectTransform panel = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size);
            Image image = panel.gameObject.AddComponent<Image>();
            image.color = CaveUiTheme.Surface;
            AddPanelFrame(panel, CaveUiTheme.BorderBright, 3f);
            AddCornerOrnaments(panel, CaveUiTheme.Gold, 8f);
            return panel.gameObject;
        }

        private static Button CreateButton(
            string label,
            Transform parent,
            Font font,
            Vector2 position,
            Vector2? size = null)
        {
            RectTransform buttonRect = CreateRect(
                label + " Button",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size ?? new Vector2(220f, 46f));
            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = CaveUiTheme.SurfaceRaised;

            Button button = buttonRect.gameObject.AddComponent<Button>();
            buttonRect.gameObject.AddComponent<UiButtonAudioFeedback>();
            ColorBlock colors = button.colors;
            colors.normalColor = CaveUiTheme.SurfaceRaised;
            colors.highlightedColor = new Color(0.12f, 0.38f, 0.52f, 1f);
            colors.pressedColor = new Color(0.16f, 0.58f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Outline outline = buttonRect.gameObject.AddComponent<Outline>();
            outline.effectColor = CaveUiTheme.IronLight;
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform textRect = CreateStretchRect("Label", buttonRect);
            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = CaveUiTheme.PrimaryText;
            text.text = label;
            AddTextShadow(text);
            return button;
        }

        private static Text CreateCenteredText(
            string name,
            Transform parent,
            Font font,
            string value,
            int fontSize,
            Vector2 position,
            Vector2? size = null)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size ?? new Vector2(320f, 42f));
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = value;
            AddTextShadow(text);
            return text;
        }

        private static Slider CreateVolumeRow(
            string label,
            Transform parent,
            Font font,
            float rowY,
            out Text valueText)
        {
            Text rowLabel = CreateCenteredText(
                label + " Label",
                parent,
                font,
                label,
                17,
                new Vector2(-220f, rowY),
                new Vector2(190f, 32f));
            rowLabel.alignment = TextAnchor.MiddleLeft;

            RectTransform sliderRect = CreateRect(
                label + " Slider",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(70f, rowY),
                new Vector2(300f, 24f));

            RectTransform background = CreateStretchRect("Background", sliderRect);
            background.offsetMin = new Vector2(0f, 7f);
            background.offsetMax = new Vector2(0f, -7f);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.13f, 0.17f, 1f);

            RectTransform fillArea = CreateStretchRect("Fill Area", sliderRect);
            fillArea.offsetMin = new Vector2(5f, 7f);
            fillArea.offsetMax = new Vector2(-5f, -7f);
            RectTransform fill = CreateStretchRect("Fill", fillArea);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.9f, 0.3f, 0.15f, 1f);

            RectTransform handleArea = CreateStretchRect("Handle Slide Area", sliderRect);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            RectTransform handle = CreateRect(
                "Handle",
                handleArea,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(18f, 28f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;

            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;

            valueText = CreateCenteredText(
                label + " Value",
                parent,
                font,
                "100%",
                16,
                new Vector2(265f, rowY),
                new Vector2(70f, 32f));
            return slider;
        }

        private static string FormatActionObjectName(GameAction action)
        {
            return SettingsMenuController.FormatAction(action).Replace(" ", string.Empty);
        }

        private static void CreateText(
            string name,
            Transform parent,
            Font font,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 position,
            Vector2 size)
        {
            CreateAnchoredText(name, parent, font, value, fontSize, alignment, position, size);
        }

        private static Text CreateAnchoredText(
            string name,
            Transform parent,
            Font font,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 position,
            Vector2 size)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                position,
                size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = value;
            AddTextShadow(text);
            return text;
        }

        private static void AddTextShadow(Text text)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = UiLayer;
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
    }
}
