using Cave.Combat;
using Cave.Diagnostics;
using Cave.Domain;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Inventory;
using Cave.Gambit;
using Cave.Player;
using Cave.Projectiles;
using Cave.Progression;
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
                Transform existingGameplayHud = existingHud.transform.parent;
                Transform uiRoot = existingGameplayHud != null ? existingGameplayHud.parent : null;
                Canvas rootCanvas = existingHud.GetComponentInParent<Canvas>();
                EnsureMenuCanvas(
                    uiRoot != null ? uiRoot.Find("Menus") : null,
                    rootCanvas != null ? rootCanvas.sortingOrder + 1 : 101);
                EnsureHealthRatioHud(existingHud, font);
                existingHud.Bind(playerHealth);
                EnsureCarapaceHealthHud(
                    existingHud,
                    existingHud.transform.Find("Health Bar Background") as RectTransform);
                EnsureStaminaHud(existingHud.transform.parent, font, spinSwordAttack);
                EnsureManaHud(existingHud.transform.parent, font, playerMana);
                EnsureCurrencyHud(existingHud.transform.parent, font, playerCurrency);
                EnsureSpecialModeHud(existingHud.transform.parent, font, specialMode, playerCurrency);
                EnsureDomainChargeHud(existingHud.transform.parent, font, specialMode);
                EnsureCompactPlayerInformation(existingHud.transform.parent, font, playerHealth, playerCurrency, specialMode);
                EnsureShieldHud(existingHud.transform.parent);
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
                EnsureEquipmentWheel(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureSovereignGambitTacticalHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureEnemyLocationBar(existingHud.transform.parent, font, playerHealth.transform);
                EnsureDetectiveResearchHud(existingHud.transform.parent, font);
                EnsureStatusEffectHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureWizardWarpWarningHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureCurseHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureCurseAltarHud(existingHud.transform.parent, font);
                EnsureFrenzyBreakHud(existingHud.transform.parent, font, playerHealth.gameObject);
                EnsureExistingMenuPresentation(uiRoot != null ? uiRoot.Find("Menus") : null, font);
                ApplyFreeUiPackageSkin(existingHud.transform.parent);
                CaveUiArt.ApplyOnce(existingHud.transform.parent, uiRoot != null ? uiRoot.Find("Menus") : null);
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
            EnsureCarapaceHealthHud(healthHud, healthBarBackground);
            EnsureStaminaHud(gameplayHud, font, spinSwordAttack);
            EnsureManaHud(gameplayHud, font, playerMana);
            EnsureCurrencyHud(gameplayHud, font, playerCurrency);
            EnsureSpecialModeHud(gameplayHud, font, specialMode, playerCurrency);
            EnsureDomainChargeHud(gameplayHud, font, specialMode);
            EnsureCompactPlayerInformation(gameplayHud, font, playerHealth, playerCurrency, specialMode);
            EnsureShieldHud(gameplayHud);
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
            EnsureEquipmentWheel(gameplayHud, font, playerHealth.gameObject);
            EnsureSovereignGambitTacticalHud(gameplayHud, font, playerHealth.gameObject);
            EnsureEnemyLocationBar(gameplayHud, font, playerHealth.transform);
            EnsureDetectiveResearchHud(gameplayHud, font);
            EnsureStatusEffectHud(gameplayHud, font, playerHealth.gameObject);
            EnsureWizardWarpWarningHud(gameplayHud, font, playerHealth.gameObject);
            EnsureCurseHud(gameplayHud, font, playerHealth.gameObject);
            EnsureCurseAltarHud(gameplayHud, font);
            EnsureFrenzyBreakHud(gameplayHud, font, playerHealth.gameObject);
            ApplyFreeUiPackageSkin(gameplayHud);

            RectTransform menus = CreateStretchRect("Menus", root.transform);
            EnsureMenuCanvas(menus, canvas.sortingOrder + 1);
            GameObject pauseMenu = CreateMenuPanel("Pause Menu", menus, new Vector2(460f, 570f));
            Text pauseCrest = CreateCenteredText(
                "Pause Crest",
                pauseMenu.transform,
                font,
                "◆",
                26,
                new Vector2(0f, 272f),
                new Vector2(80f, 30f));
            pauseCrest.color = CaveUiTheme.BorderBright;
            Text pauseTitle = CreateCenteredText(
                "Pause Title",
                pauseMenu.transform,
                font,
                "PAUSED",
                34,
                new Vector2(0f, 236f));
            pauseTitle.fontStyle = FontStyle.Bold;
            pauseTitle.color = CaveUiTheme.Gold;
            Text pauseSubtitle = CreateCenteredText(
                "Pause Subtitle",
                pauseMenu.transform,
                font,
                "THE CAVE WAITS",
                14,
                new Vector2(0f, 202f));
            pauseSubtitle.color = CaveUiTheme.SecondaryText;
            Button resumeButton = CreateButton(
                "RESUME",
                pauseMenu.transform,
                font,
                new Vector2(0f, 134f),
                new Vector2(270f, 50f));
            Button resetLevelButton = CreateButton(
                "RESET LEVEL",
                pauseMenu.transform,
                font,
                new Vector2(0f, 72f),
                new Vector2(270f, 50f));
            Button movesButton = CreateButton(
                "MOVES LIST",
                pauseMenu.transform,
                font,
                new Vector2(0f, 10f),
                new Vector2(270f, 50f));
            Button settingsButton = CreateButton(
                "SETTINGS",
                pauseMenu.transform,
                font,
                new Vector2(0f, -176f),
                new Vector2(270f, 50f));
            Button ledgerButton = CreateButton(
                "ENEMY LEDGER",
                pauseMenu.transform,
                font,
                new Vector2(0f, -52f),
                new Vector2(270f, 50f));
            Button inventoryButton = CreateButton(
                "INVENTORY",
                pauseMenu.transform,
                font,
                new Vector2(0f, -114f),
                new Vector2(270f, 50f));
            // Inventory migrated to Nexus Cosmetics; retain no standalone Pause entry.
            inventoryButton.gameObject.SetActive(false);
            Text pauseHint = CreateCenteredText(
                "Pause Hint",
                pauseMenu.transform,
                font,
                "ESC  •  RESUME",
                13,
                new Vector2(0f, -266f));
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
            Button developerSettingsButton = CreateButton(
                "DEV",
                settingsPanel.transform,
                font,
                new Vector2(278f, 302f),
                new Vector2(100f, 34f));
            developerSettingsButton.GetComponentInChildren<Text>().fontSize = 14;
            developerSettingsButton.gameObject.SetActive(DeveloperDiagnosticsSettings.FeatureAvailable);
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
                GameAction.SelectEquipment,
                GameAction.ActivateEquipment,
                GameAction.Interact,
                GameAction.SummonCurseAltar,
                GameAction.Dash,
                GameAction.Pause,
                GameAction.Nexus,
                GameAction.RecallManifestation
            };
            Button[] bindingButtons = new Button[configurableActions.Length];

            const float firstBindingY = 240f;
            const float bindingSpacing = 19f;
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

            GameObject developerSettingsPanel = CreateMenuPanel("Developer Settings Panel", menus, new Vector2(760f, 780f));
            Text developerTitle = CreateCenteredText("Developer Settings Title", developerSettingsPanel.transform, font, "DEVELOPER SETTINGS", 26, new Vector2(0f, 342f));
            developerTitle.fontStyle = FontStyle.Bold;
            developerTitle.color = CaveUiTheme.Gold;
            Text developerHint = CreateCenteredText("Developer Settings Hint", developerSettingsPanel.transform, font, "Development-only controls. Runtime systems remain authoritative.", 13, new Vector2(0f, 310f), new Vector2(680f, 30f));
            developerHint.color = CaveUiTheme.SecondaryText;
            CreateCardSurface("Developer Settings Surface", developerSettingsPanel.transform, new Vector2(0f, 8f), new Vector2(700f, 620f));
            Button diagnosticsAvailability = CreateButton("", developerSettingsPanel.transform, font, new Vector2(-152f, 112f), new Vector2(278f, 66f));
            Button inspectorDetail = CreateButton("", developerSettingsPanel.transform, font, new Vector2(152f, 112f), new Vector2(278f, 66f));
            Button traceMode = CreateButton("", developerSettingsPanel.transform, font, new Vector2(-152f, 34f), new Vector2(278f, 66f));
            Button ctcToggle = CreateButton("", developerSettingsPanel.transform, font, new Vector2(152f, 34f), new Vector2(278f, 66f));
            Button jevToggle = CreateButton("", developerSettingsPanel.transform, font, new Vector2(-152f, -44f), new Vector2(278f, 66f));
            Button governorControl = CreateButton("", developerSettingsPanel.transform, font, new Vector2(152f, -44f), new Vector2(278f, 66f));
            Button domainTestOverride = CreateButton("", developerSettingsPanel.transform, font, new Vector2(-152f, -122f), new Vector2(278f, 66f));
            Button unlockAllCosmetics = CreateButton("", developerSettingsPanel.transform, font, new Vector2(152f, -122f), new Vector2(278f, 66f));
            Button grantCarapace = CreateButton("", developerSettingsPanel.transform, font, new Vector2(-174f, -200f), new Vector2(330f, 66f));
            Button unlockManifestations = CreateButton("", developerSettingsPanel.transform, font, new Vector2(174f, -200f), new Vector2(330f, 66f));
            Button resetManifestations = CreateButton("", developerSettingsPanel.transform, font, new Vector2(0f, -270f), new Vector2(430f, 48f));
            Text manifestationDiagnostics = CreateCenteredText("Manifestation Diagnostics", developerSettingsPanel.transform, font,
                "MANIFESTATION RUNTIME", 12, new Vector2(0f, -315f), new Vector2(650f, 48f));
            manifestationDiagnostics.color = CaveUiTheme.SecondaryText;
            Button developerBackButton = CreateButton("BACK", developerSettingsPanel.transform, font, new Vector2(0f, -360f), new Vector2(230f, 42f));
            DeveloperDiagnosticsSettingsPanel developerSettings = developerSettingsPanel.AddComponent<DeveloperDiagnosticsSettingsPanel>();
            developerSettings.Configure(settingsPanel, settingsController, diagnosticsAvailability, inspectorDetail, traceMode, ctcToggle, jevToggle, governorControl, domainTestOverride, unlockAllCosmetics, grantCarapace, unlockManifestations, resetManifestations, manifestationDiagnostics, developerBackButton);
            if (DeveloperDiagnosticsSettings.FeatureAvailable)
            {
                developerSettingsButton.onClick.AddListener(() => { settingsPanel.SetActive(false); developerSettingsPanel.SetActive(true); });
            }
            developerSettingsPanel.SetActive(false);

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
            MovesListPresentation movesPresentation = movesPanel.AddComponent<MovesListPresentation>();
            movesPresentation.Configure(
                font,
                new[] { movesLeftText, movesRightText, movesChainLeftText, movesChainRightText });
            movesPresentation.EnsureAxiomPresentation(font);

            GameObject ledgerPanel = CreateMenuPanel("Enemy Ledger Panel", menus, new Vector2(1120f, 690f));
            Text ledgerTitle = CreateCenteredText(
                "Ledger Title", ledgerPanel.transform, font, "ENEMY LEDGER", 30, new Vector2(0f, 280f));
            ledgerTitle.fontStyle = FontStyle.Bold;
            ledgerTitle.color = CaveUiTheme.Gold;
            CreateCardSurface("Ledger Surface", ledgerPanel.transform, new Vector2(0f, 8f), new Vector2(1040f, 520f));
            Text ledgerBody = CreateCenteredText(
                "Ledger Body", ledgerPanel.transform, font, string.Empty, 16, new Vector2(0f, 8f), new Vector2(1000f, 490f));
            ledgerBody.alignment = TextAnchor.UpperLeft;
            ledgerBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            ledgerBody.verticalOverflow = VerticalWrapMode.Overflow;
            Button ledgerBackButton = CreateButton(
                "Back", ledgerPanel.transform, font, new Vector2(0f, -284f), new Vector2(220f, 44f));
            EnemyLedgerPagesHud ledgerPages = ledgerPanel.AddComponent<EnemyLedgerPagesHud>();
            ledgerPages.Configure(ledgerBody);

            GameObject inventoryPanel = CreateMenuPanel("Inventory Panel", menus, new Vector2(860f, 620f));
            Text inventoryTitle = CreateCenteredText("Inventory Title", inventoryPanel.transform, font, "INVENTORY — MATERIALS", 28, new Vector2(0f, 260f), new Vector2(760f, 38f));
            inventoryTitle.fontStyle = FontStyle.Bold;
            inventoryTitle.color = CaveUiTheme.Gold;
            CreateCardSurface("Inventory Surface", inventoryPanel.transform, new Vector2(0f, 12f), new Vector2(760f, 430f));
            Text inventoryBody = CreateCenteredText("Inventory Body", inventoryPanel.transform, font, string.Empty, 15, new Vector2(-2f, 10f), new Vector2(716f, 390f));
            inventoryBody.alignment = TextAnchor.UpperLeft;
            inventoryBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            inventoryBody.verticalOverflow = VerticalWrapMode.Overflow;
            Image inventoryIcon = CreateCardSurface("Inventory Icon", inventoryPanel.transform, new Vector2(0f, 0f), Vector2.zero);
            inventoryIcon.enabled = false;
            Button materialsButton = CreateButton("MATERIALS", inventoryPanel.transform, font, new Vector2(-220f, 202f), new Vector2(190f, 42f));
            Button consumablesButton = CreateButton("CONSUMABLES", inventoryPanel.transform, font, new Vector2(0f, 202f), new Vector2(190f, 42f));
            Button equipmentButton = CreateButton("EQUIPMENT", inventoryPanel.transform, font, new Vector2(220f, 202f), new Vector2(190f, 42f));
            Button inventoryBackButton = CreateButton("BACK", inventoryPanel.transform, font, new Vector2(0f, -260f), new Vector2(220f, 44f));
            PauseInventoryPanel inventoryPresentation = inventoryPanel.AddComponent<PauseInventoryPanel>();
            inventoryPresentation.Configure(inventoryTitle, inventoryBody, inventoryIcon);
            materialsButton.onClick.AddListener(() => inventoryPresentation.ShowCategory(InventoryItemCategory.Materials));
            consumablesButton.onClick.AddListener(() => inventoryPresentation.ShowCategory(InventoryItemCategory.Consumables));
            equipmentButton.onClick.AddListener(() => inventoryPresentation.ShowCategory(InventoryItemCategory.Equipment));

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
                inventoryPanel,
                inventoryButton,
                inventoryBackButton,
                ledgerBody,
                movesLeftText,
                movesRightText,
                movesChainLeftText,
                movesChainRightText,
                movesPageText,
                movesPageButton,
                movesPresentation,
                null);
            pauseController.ConfigureDeveloperSettingsPanel(developerSettingsPanel);

            CaveUiArt.ApplyOnce(gameplayHud, menus);

            EnsureEventSystem();
        }

        private static void EnsureExistingMenuPresentation(Transform menus, Font font)
        {
            if (menus == null)
            {
                return;
            }

            Transform moves = menus.Find("Moves List Panel");
            MovesListPresentation presentation = moves != null ? moves.GetComponent<MovesListPresentation>() : null;
            if (moves != null)
            {
                if (presentation == null)
                {
                    presentation = moves.gameObject.AddComponent<MovesListPresentation>();
                }

                presentation.Configure(
                    font,
                    new[]
                    {
                        moves.Find("Moves Left Text")?.GetComponent<Text>(),
                        moves.Find("Moves Right Text")?.GetComponent<Text>(),
                        moves.Find("Moves Chain Left Text")?.GetComponent<Text>(),
                        moves.Find("Moves Chain Right Text")?.GetComponent<Text>()
                    });
            }

            Transform legacyAxioms = menus.Find("Axioms Panel");
            if (presentation != null)
            {
                if (legacyAxioms != null)
                {
                    presentation.AdoptLegacyAxiomPresentation(legacyAxioms, font);
                }
                else
                {
                    presentation.EnsureAxiomPresentation(font);
                }
            }

            if (legacyAxioms != null)
            {
                Object.Destroy(legacyAxioms.gameObject);
            }

            RemoveStandaloneAxiomsButton(menus.Find("Pause Menu") as RectTransform);

            Transform ledger = menus.Find("Enemy Ledger Panel");
            if (ledger != null)
            {
                RectTransform ledgerRect = ledger as RectTransform;
                if (ledgerRect != null)
                {
                    ledgerRect.sizeDelta = new Vector2(1120f, 690f);
                }

                Text body = ledger.Find("Ledger Body")?.GetComponent<Text>();
                EnemyLedgerPagesHud pages = ledger.GetComponent<EnemyLedgerPagesHud>();
                if (body != null && pages != null)
                {
                    pages.Configure(body);
                }
            }
        }

        private static void RemoveStandaloneAxiomsButton(RectTransform pauseMenu)
        {
            if (pauseMenu == null)
            {
                return;
            }

            pauseMenu.sizeDelta = new Vector2(460f, 500f);
            RepositionPauseElement(pauseMenu, "SETTINGS Button", -146f);
            RepositionPauseElement(pauseMenu, "Pause Hint", -226f);
            Transform button = pauseMenu.Find("CAVE AXIOMS Button");
            if (button != null)
            {
                Object.Destroy(button.gameObject);
            }
        }

        private static void RepositionPauseElement(RectTransform parent, string childName, float y)
        {
            RectTransform child = parent.Find(childName) as RectTransform;
            if (child != null)
            {
                Vector2 position = child.anchoredPosition;
                position.y = y;
                child.anchoredPosition = position;
            }
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
            EnsureCarapaceHealthHud(healthHud, barBackground);
        }

        private static void EnsureCarapaceHealthHud(PlayerHealthHud healthHud, RectTransform healthBar)
        {
            if (healthHud == null || healthBar == null) return;
            Transform panel = healthHud.transform;
            RectTransform overlayFill = healthBar.Find("Carapace Overlay Fill") as RectTransform;
            Image overlayImage;
            if (overlayFill == null)
            {
                overlayFill = CreateStretchRect("Carapace Overlay Fill", healthBar);
                overlayFill.offsetMin = new Vector2(1f, 1f);
                overlayFill.offsetMax = new Vector2(-1f, -1f);
                overlayFill.pivot = new Vector2(0f, .5f);
                overlayImage = overlayFill.gameObject.AddComponent<Image>();
                overlayImage.raycastTarget = false;
            }
            else
            {
                overlayImage = overlayFill.GetComponent<Image>();
            }

            Transform healthFill = healthBar.Find("Health Fill");
            if (healthFill != null)
            {
                // The integrity fill is part of the track, below the health fill.
                overlayFill.SetSiblingIndex(healthFill.GetSiblingIndex());
            }
            Image detail = EnsureCarapaceHudImage(
                healthBar, "Carapace Overlay Detail", Vector2.zero, healthBar.sizeDelta);
            detail.rectTransform.anchorMin = Vector2.zero;
            detail.rectTransform.anchorMax = Vector2.one;
            detail.rectTransform.pivot = new Vector2(.5f, .5f);
            detail.rectTransform.offsetMin = new Vector2(1f, 1f);
            detail.rectTransform.offsetMax = new Vector2(-1f, -1f);
            detail.raycastTarget = false;

            Image emblem = EnsureCarapaceHudImage(panel, "Carapace Shield Emblem", new Vector2(57f, -48f), new Vector2(25f, 28f));
            Image state = EnsureCarapaceHudImage(panel, "Carapace State", new Vector2(286f, -48f), new Vector2(22f, 22f));
            Image fracture = EnsureCarapaceHudImage(panel, "Carapace Fracture Response", new Vector2(178f, -48f), new Vector2(236f, 28f));
            fracture.enabled = false;
            PlayerCarapaceHealthHud carapaceHud = healthHud.GetComponent<PlayerCarapaceHealthHud>();
            if (carapaceHud == null) carapaceHud = healthHud.gameObject.AddComponent<PlayerCarapaceHealthHud>();
            carapaceHud.Configure(overlayImage, overlayFill, detail, emblem, state, fracture);
            carapaceHud.Bind(Object.FindObjectOfType<PlayerResonantCarapace>());
        }

        private static Image EnsureCarapaceHudImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(.5f, .5f), position, size);
            }
            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
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

            RectTransform modalBackdrop = CreateStretchRect("Special Mode Backdrop", gameplayHud);
            Image modalBackdropImage = modalBackdrop.gameObject.AddComponent<Image>();
            modalBackdropImage.color = new Color(0.003f, 0.006f, 0.012f, 0.91f);
            modalBackdropImage.raycastTarget = true;

            RectTransform selectionPanel = CreateRect(
                "Special Mode Selection",
                gameplayHud,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1340f, 760f));
            Image selectionBackground = selectionPanel.gameObject.AddComponent<Image>();
            selectionBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(selectionPanel, CaveUiTheme.BronzeLight, 4f);
            AddCornerOrnaments(selectionPanel, CaveUiTheme.BorderBright, 10f);

            RectTransform modalInterior = CreateStretchRect("Modal Interior Backing", selectionPanel);
            modalInterior.offsetMin = new Vector2(18f, 18f);
            modalInterior.offsetMax = new Vector2(-18f, -18f);
            Image modalInteriorImage = modalInterior.gameObject.AddComponent<Image>();
            modalInteriorImage.color = new Color(0.006f, 0.010f, 0.018f, 0.985f);
            modalInteriorImage.raycastTarget = true;
            modalInterior.SetAsFirstSibling();

            Text panelCrest = CreateCenteredText(
                "Special Mode Crest",
                selectionPanel,
                font,
                "NEXUS STAGING",
                24,
                new Vector2(0f, 330f),
                new Vector2(420f, 32f));
            panelCrest.fontStyle = FontStyle.Bold;
            panelCrest.color = CaveUiTheme.Gold;

            Button modesTabButton = CreateButton(
                "MODES",
                selectionPanel,
                font,
                new Vector2(-408f, 296f),
                new Vector2(190f, 44f));
            Button shopTabButton = CreateButton(
                "SHOP",
                selectionPanel,
                font,
                new Vector2(-204f, 296f),
                new Vector2(190f, 44f));
            Button domainTabButton = CreateButton(
                "DOMAIN",
                selectionPanel,
                font,
                new Vector2(0f, 296f),
                new Vector2(190f, 44f));
            Button masteryTabButton = CreateButton(
                "MASTERY",
                selectionPanel,
                font,
                new Vector2(204f, 296f),
                new Vector2(190f, 44f));
            Button cosmeticsTabButton = CreateButton(
                "LOADOUT",
                selectionPanel,
                font,
                new Vector2(408f, 296f),
                new Vector2(190f, 44f));
            modesTabButton.GetComponentInChildren<Text>().fontSize = 18;
            shopTabButton.GetComponentInChildren<Text>().fontSize = 18;
            domainTabButton.GetComponentInChildren<Text>().fontSize = 16;
            masteryTabButton.GetComponentInChildren<Text>().fontSize = 16;
            cosmeticsTabButton.GetComponentInChildren<Text>().fontSize = 16;
            modesTabButton.GetComponentInChildren<Text>().text = "SKILL PATH";
            Button closeModalButton = CreateButton(
                "CLOSE  [" + SettingsMenuController.FormatBinding(GameAction.Nexus).ToUpperInvariant() + "]",
                selectionPanel, font, new Vector2(570f, 330f), new Vector2(146f, 30f));
            closeModalButton.GetComponentInChildren<Text>().fontSize = 11;

            CreateCardSurface(
                "Active Mode Header",
                selectionPanel,
                new Vector2(0f, 218f),
                new Vector2(1240f, 42f));
            Text panelCurrentMode = CreateCenteredText(
                "Selected Mode",
                selectionPanel,
                font,
                "ACTIVE PATH  •  SLOW SHOT",
                14,
                new Vector2(-366f, 218f),
                new Vector2(690f, 30f));
            panelCurrentMode.alignment = TextAnchor.MiddleLeft;
            panelCurrentMode.color = CaveUiTheme.PrimaryText;
            Text panelCurrency = CreateCenteredText(
                "Mode Currency",
                selectionPanel,
                font,
                "◇ 0",
                14,
                new Vector2(504f, 218f),
                new Vector2(160f, 30f));
            panelCurrency.alignment = TextAnchor.MiddleRight;
            panelCurrency.color = CaveUiTheme.Gold;

            RectTransform modesContent = CreateRect(
                "Modes Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -62f),
                new Vector2(1260f, 548f));

            Button[] modeButtons = new Button[4];
            Text[] modeLabels = new Text[4];
            Text[] modeInfoTexts = new Text[4];
            Button[] upgradeButtons = new Button[4];
            string[] initialLabels = { "Slow Shot", "Burn Shot", "Flight", "Strength" };
            string[] modeIconNames = { "SlowShot", "BurnShot", "Flight", "Strength" };
            for (int index = 0; index < initialLabels.Length; index++)
            {
                float rowY = 180f - index * 65f;
                RectTransform modeCard = CreateCardSurface(initialLabels[index] + " Card", modesContent, new Vector2(0f, rowY), new Vector2(1240f, 58f)).rectTransform;
                CreateNexusSkillIcon(initialLabels[index] + " Icon", modeCard, modeIconNames[index], new Vector2(-574f, 0f), new Vector2(48f, 48f));
                modeInfoTexts[index] = CreateCenteredText(
                    initialLabels[index] + " Info",
                    modesContent,
                    font,
                    initialLabels[index].ToUpperInvariant() + "\nTier 1",
                    13,
                    new Vector2(-164f, rowY),
                    new Vector2(760f, 52f));
                modeInfoTexts[index].alignment = TextAnchor.MiddleLeft;

                modeButtons[index] = CreateButton(
                    initialLabels[index] + " Switch",
                    modesContent,
                    font,
                    new Vector2(506f, rowY + 14f),
                    new Vector2(174f, 26f));
                modeLabels[index] = modeButtons[index].GetComponentInChildren<Text>();
                modeLabels[index].fontSize = 13;

                upgradeButtons[index] = CreateButton(
                    initialLabels[index] + " Upgrade",
                    modesContent,
                    font,
                    new Vector2(506f, rowY - 14f),
                    new Vector2(174f, 26f));
                upgradeButtons[index].GetComponentInChildren<Text>().fontSize = 13;
            }

            Text firePatternHeading = CreateCenteredText(
                "Fire Pattern Header",
                modesContent,
                font,
                "◆  FIRE PATTERN  •  HOLD TO FIRE  ◆",
                13,
                new Vector2(0f, -69f),
                new Vector2(760f, 22f));
            firePatternHeading.fontStyle = FontStyle.Bold;
            firePatternHeading.color = CaveUiTheme.Gold;
            Button[] firePatternButtons = new Button[5];
            Text[] firePatternLabels = new Text[5];
            string[] patternIconNames = { "Precision", "Automatic", "Burst", "Volley", "SpoolUp" };
            float[] firePatternPositions = { -492f, -246f, 0f, 246f, 492f };
            for (int index = 0; index < firePatternButtons.Length; index++)
            {
                firePatternButtons[index] = CreateButton(
                    "Fire Pattern " + index,
                    modesContent,
                    font,
                    new Vector2(firePatternPositions[index], -108f),
                    new Vector2(232f, 56f));
                firePatternLabels[index] = firePatternButtons[index].GetComponentInChildren<Text>();
                firePatternLabels[index].fontSize = 10;
                RectTransform patternRect = firePatternButtons[index].GetComponent<RectTransform>();
                CreateNexusSkillIcon("Pattern Icon " + index, patternRect, patternIconNames[index],
                    new Vector2(-88f, 0f), new Vector2(44f, 44f));
                RectTransform labelRect = firePatternLabels[index].rectTransform;
                labelRect.offsetMin = new Vector2(50f, 2f);
                labelRect.offsetMax = new Vector2(-4f, -2f);
            }
            Text firePatternProfile = CreateCenteredText("Fire Pattern Profile", modesContent, font,
                "Projectile profile unavailable.", 10, new Vector2(0f, -164f), new Vector2(1210f, 54f));
            firePatternProfile.alignment = TextAnchor.MiddleLeft;
            firePatternProfile.color = CaveUiTheme.SecondaryText;
            FirePatternSelectionPanel firePatternPanel = modesContent.gameObject.AddComponent<FirePatternSelectionPanel>();
            firePatternPanel.Configure(specialMode.gameObject, firePatternButtons, firePatternLabels, firePatternProfile);

            Text originHeading = CreateCenteredText("Manifestation Origin Header", modesContent, font,
                "◆  MANIFESTATION ORIGIN  •  CHOOSE WHERE YOUR ETHEREAL FORM MANIFESTS  ◆",
                13, new Vector2(0f, -204f), new Vector2(1000f, 22f));
            originHeading.fontStyle = FontStyle.Bold;
            originHeading.color = CaveUiTheme.Gold;
            Button[] originButtons = new Button[3];
            Text[] originLabels = new Text[3];
            string[] originIcons = { "Body", "Anchor", "Orbital" };
            float[] originX = { -412f, 0f, 412f };
            for (int index = 0; index < originButtons.Length; index++)
            {
                originButtons[index] = CreateButton("Manifestation Origin " + index, modesContent, font,
                    new Vector2(originX[index], -250f), new Vector2(394f, 80f));
                originLabels[index] = originButtons[index].GetComponentInChildren<Text>();
                originLabels[index].fontSize = 9;
                originLabels[index].alignment = TextAnchor.MiddleLeft;
                CreateNexusSkillIcon("Origin Icon " + index, originButtons[index].GetComponent<RectTransform>(),
                    originIcons[index], new Vector2(-154f, 0f), new Vector2(64f, 64f));
                originLabels[index].rectTransform.offsetMin = new Vector2(78f, 3f);
                originLabels[index].rectTransform.offsetMax = new Vector2(-5f, -3f);
            }
            Text originStatus = CreateCenteredText("Manifestation Origin Status", modesContent, font,
                "BODY  •  UNDEPLOYED", 10, new Vector2(0f, -306f), new Vector2(1220f, 20f));
            originStatus.color = CaveUiTheme.BorderBright;
            ManifestationOriginSelectionPanel originPanel = modesContent.gameObject.AddComponent<ManifestationOriginSelectionPanel>();
            originPanel.Configure(specialMode.gameObject, originButtons, originLabels, originStatus);

            RectTransform shopContent = CreateRect(
                "Shop Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -42f),
                new Vector2(960f, 486f));

            RectTransform domainContent = CreateRect(
                "Domain Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f),
                new Vector2(1260f, 630f));
            DomainPageView domainPage = BuildDomainPage(domainContent, font);

            RectTransform masteryContent = CreateRect(
                "Mastery Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f),
                new Vector2(1260f, 630f));
            NexusMasteryPage masteryPage = masteryContent.gameObject.AddComponent<NexusMasteryPage>();

            RectTransform cosmeticsContent = CreateRect(
                "Loadout Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f),
                new Vector2(1260f, 630f));
            PlayerSwordCosmetics.EnsureInstalled(specialMode.gameObject);
            PlayerSkinCosmetics skins = PlayerSkinCosmetics.EnsureInstalled(specialMode.gameObject);
            NexusLoadoutPage cosmeticsPage = cosmeticsContent.gameObject.AddComponent<NexusLoadoutPage>();

            Text consumablesHeading = CreateCenteredText("Consumables Header", shopContent, font,
                "◆  CONSUMABLES  ◆", 16, new Vector2(0f, 208f), new Vector2(520f, 26f));
            consumablesHeading.fontStyle = FontStyle.Bold;
            consumablesHeading.color = CaveUiTheme.Gold;
            CreateCardSurface("Health Potion Card", shopContent, new Vector2(-310f, 116f), new Vector2(280f, 154f));
            CreateCardSurface("Mana Potion Card", shopContent, new Vector2(0f, 116f), new Vector2(280f, 154f));
            CreateCardSurface("Oblivion Disk Card", shopContent, new Vector2(310f, 116f), new Vector2(280f, 154f));
            CreateItemIcon("Health Potion Icon", shopContent, font, new Vector2(-310f, 153f), "+", CaveUiTheme.Health);
            CreateItemIcon("Mana Potion Icon", shopContent, font, new Vector2(0f, 153f), "✦", CaveUiTheme.Mana);
            CreateItemIcon("Oblivion Disk Icon", shopContent, font, new Vector2(310f, 153f), "◎", CaveUiTheme.Gold);

            Text healthPotionText = CreateCenteredText(
                "Health Potion Info",
                shopContent,
                font,
                "HEALTH POTION\nRestore: 25%  |  Cost: 5",
                13,
                new Vector2(-310f, 91f),
                new Vector2(250f, 60f));
            healthPotionText.alignment = TextAnchor.MiddleCenter;
            Button healthPotionButton = CreateButton(
                "BUY",
                shopContent,
                font,
                new Vector2(-310f, 49f),
                new Vector2(150f, 30f));
            healthPotionButton.GetComponentInChildren<Text>().fontSize = 14;

            Text manaPotionText = CreateCenteredText(
                "Mana Potion Info",
                shopContent,
                font,
                "MANA POTION\nRestore: 25%  |  Cost: 4",
                13,
                new Vector2(0f, 91f),
                new Vector2(250f, 60f));
            manaPotionText.alignment = TextAnchor.MiddleCenter;
            Button manaPotionButton = CreateButton(
                "BUY",
                shopContent,
                font,
                new Vector2(0f, 49f),
                new Vector2(150f, 30f));
            manaPotionButton.GetComponentInChildren<Text>().fontSize = 14;

            Text landmineText = CreateCenteredText(
                "Oblivion Disk Info",
                shopContent,
                font,
                "OBLIVION DISK I\nCharges: ◇  |  Cost: 18\nRecharge: 12s  |  Place: Q",
                13,
                new Vector2(310f, 91f),
                new Vector2(250f, 60f));
            landmineText.alignment = TextAnchor.MiddleCenter;
            Button landmineButton = CreateButton(
                "UPGRADE",
                shopContent,
                font,
                new Vector2(310f, 49f),
                new Vector2(150f, 30f));
            landmineButton.GetComponentInChildren<Text>().fontSize = 12;

            Text progressionDivider = CreateCenteredText(
                "Permanent Progression Header",
                shopContent,
                font,
                "◆  GENERAL SHARD UPGRADES  ◆",
                14,
                new Vector2(0f, -2f),
                new Vector2(840f, 26f));
            progressionDivider.fontStyle = FontStyle.Bold;
            progressionDivider.color = CaveUiTheme.GeneralShard;

            RectTransform progressionMount = CreateRect(
                "Permanent Progression Mount",
                shopContent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -120f),
                new Vector2(920f, 340f));

            RectTransform shardSummaryMount = CreateRect(
                "General Shards Summary Mount",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-130f, -282f),
                new Vector2(160f, 32f));

            Text feedbackText = CreateCenteredText(
                "Mode Feedback",
                selectionPanel,
                font,
                "Mode switches, upgrades, and potions spend Currency.",
                14,
                new Vector2(86f, -282f),
                new Vector2(244f, 34f));
            feedbackText.color = CaveUiTheme.Gold;

            PlayerSpecialModeHud modeHud = summaryPanel.gameObject.AddComponent<PlayerSpecialModeHud>();
            modeHud.Configure(
                currentModeText,
                panelCurrentMode,
                panelCurrency,
                feedbackText,
                selectionPanel.gameObject,
                modalBackdrop.gameObject,
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
                shardSummaryMount,
                domainContent.gameObject,
                domainTabButton,
                domainPage);
            closeModalButton.onClick.AddListener(modeHud.ToggleSelectionPanelFromUi);
            modeHud.Bind(specialMode, playerCurrency);
            masteryPage.Configure(specialMode.gameObject, font);
            cosmeticsPage.Configure(skins, specialMode.GetComponent<PlayerSwordCosmetics>(), font);
            modeHud.ConfigureNexusTabs(
                masteryContent.gameObject,
                masteryTabButton,
                cosmeticsContent.gameObject,
                cosmeticsTabButton);
        }

        private static void EnsureDomainChargeHud(
            Transform gameplayHud,
            Font font,
            PlayerSpecialMode specialMode)
        {
            if (gameplayHud == null || specialMode == null)
            {
                return;
            }

            PlayerDomainReserve reserve = PlayerDomainReserve.EnsureOn(specialMode.gameObject);
            PlayerDomainChargeHud existing = Object.FindObjectOfType<PlayerDomainChargeHud>(true);
            if (existing != null)
            {
                existing.Bind(reserve);
                return;
            }

            RectTransform panel = CreateRect(
                "Domain Charge",
                gameplayHud,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -170f),
                new Vector2(236f, 58f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = CaveUiTheme.Surface;
            AddPanelFrame(panel, new Color(0.38f, 0.76f, 0.92f, 0.82f));

            Text label = CreateAnchoredText(
                "Domain Charge Label",
                panel,
                font,
                "DOMAIN CHARGE",
                12,
                TextAnchor.MiddleLeft,
                new Vector2(14f, 13f),
                new Vector2(118f, 20f));
            label.fontStyle = FontStyle.Bold;
            label.color = CaveUiTheme.PrimaryText;

            Text value = CreateAnchoredText(
                "Domain Charge Value",
                panel,
                font,
                "0 / 0",
                13,
                TextAnchor.MiddleRight,
                new Vector2(150f, 13f),
                new Vector2(70f, 20f));
            value.fontStyle = FontStyle.Bold;
            value.color = new Color(0.64f, 0.88f, 1f, 1f);

            RectTransform track = CreateRect(
                "Domain Charge Track",
                panel,
                new Vector2(.5f, .5f),
                new Vector2(.5f, .5f),
                new Vector2(.5f, .5f),
                new Vector2(0f, -13f),
                new Vector2(198f, 10f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.018f, 0.035f, 0.065f, 0.96f);

            RectTransform fill = CreateStretchRect("Domain Charge Fill", track);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            fill.pivot = new Vector2(0f, .5f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.35f, 0.78f, 1f, .92f);

            PlayerDomainChargeHud hud = panel.gameObject.AddComponent<PlayerDomainChargeHud>();
            hud.Configure(fillImage, value);
            hud.Bind(reserve);
        }

        /// <summary>
        /// Replaces only the old stacked upper-right presentation. The existing
        /// resource owners and modal controls remain intact beneath this view.
        /// </summary>
        private static void EnsureCompactPlayerInformation(
            Transform gameplayHud,
            Font font,
            PlayerHealth playerHealth,
            PlayerCurrency playerCurrency,
            PlayerSpecialMode specialMode)
        {
            if (gameplayHud == null || playerHealth == null || specialMode == null)
            {
                return;
            }

            CompactPlayerInformationHud compact = gameplayHud.GetComponentInChildren<CompactPlayerInformationHud>(true);
            if (compact == null)
            {
                RectTransform root = CreateRect(
                    "Compact Player Information",
                    gameplayHud,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-18f, -22f), new Vector2(370f, 248f));
                // The extracted paired panels are 2.59:1.  Preserve that art
                // ratio instead of crushing ornamental borders into 45 pixels.
                RectTransform currencyCard = CreateCompactHudCard(root, "Currency", new Vector2(-94f, -4f), new Vector2(174f, 66f), "CurrencyPanel");
                RectTransform shardCard = CreateCompactHudCard(root, "General Shards", new Vector2(94f, -4f), new Vector2(174f, 66f), "GeneralShardsPanel");
                RectTransform skillCard = CreateCompactHudCard(root, "Skill Path", new Vector2(-94f, -76f), new Vector2(174f, 66f), "SkillPathPanel");
                RectTransform fireCard = CreateCompactHudCard(root, "Fire Pattern", new Vector2(94f, -76f), new Vector2(174f, 66f), "FirePatternPanel");
                RectTransform chargeCard = CreateCompactHudCard(root, "Domain Charge", new Vector2(0f, -148f), new Vector2(362f, 66f), "DomainChargePanel");

                Text currencyValue = CreateCompactHudValue(currencyCard, font, "Currency Value", CaveUiTheme.Currency);
                Text shardsValue = CreateCompactHudValue(shardCard, font, "General Shards Value", CaveUiTheme.GeneralShard);
                Text skillValue = CreateCompactHudValue(skillCard, font, "Skill Path Value", CaveUiTheme.PrimaryText);
                Text fireValue = CreateCompactHudValue(fireCard, font, "Fire Pattern Value", CaveUiTheme.PrimaryText);
                CreateCompactHudLabel(currencyCard, font, "Currency Label", "CURRENCY");
                CreateCompactHudLabel(shardCard, font, "General Shards Label", "GENERAL SHARDS");
                CreateCompactHudLabel(skillCard, font, "Skill Path Label", "SKILL PATH");
                CreateCompactHudLabel(fireCard, font, "Fire Pattern Label", "FIRE PATTERN");

                Text chargeLabel = CreateAnchoredText("Domain Charge Label", chargeCard, font, "DOMAIN CHARGE", 12,
                    TextAnchor.MiddleLeft, new Vector2(76f, -18f), new Vector2(150f, 18f));
                chargeLabel.fontStyle = FontStyle.Bold;
                chargeLabel.color = CaveUiTheme.PrimaryText;
                Text chargeValue = CreateAnchoredText("Domain Charge Value", chargeCard, font, "0 / 0", 13,
                    TextAnchor.MiddleRight, new Vector2(236f, -18f), new Vector2(108f, 18f));
                chargeValue.fontStyle = FontStyle.Bold;
                chargeValue.color = CaveUiTheme.BorderBright;
                RectTransform chargeTrack = CreateRect("Domain Charge Track", chargeCard,
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(48f, -44f), new Vector2(254f, 11f));
                Image trackImage = chargeTrack.gameObject.AddComponent<Image>();
                trackImage.color = CaveUiTheme.SurfaceInset;
                RectTransform chargeFill = CreateStretchRect("Domain Charge Fill", chargeTrack);
                chargeFill.offsetMin = new Vector2(1f, 1f);
                chargeFill.offsetMax = new Vector2(-1f, -1f);
                chargeFill.pivot = new Vector2(0f, .5f);
                Image chargeFillImage = chargeFill.gameObject.AddComponent<Image>();
                chargeFillImage.color = new Color(.22f, .8f, 1f, .94f);
                Text manifestationStatus = CreateCenteredText("Manifestation Origin Status", root, font,
                    "ORIGIN  BODY", 9, new Vector2(0f, -218f), new Vector2(360f, 22f));
                manifestationStatus.color = CaveUiTheme.BorderBright;

                compact = root.gameObject.AddComponent<CompactPlayerInformationHud>();
                compact.Configure(currencyValue, shardsValue, skillValue, fireValue, chargeFillImage, chargeValue,
                    null, null, null, null, manifestationStatus);

                PlayerSpecialModeHud specialHud = Object.FindObjectOfType<PlayerSpecialModeHud>(true);
                Button skillButton = skillCard.gameObject.AddComponent<Button>();
                skillButton.transition = Selectable.Transition.None;
                skillButton.targetGraphic = skillCard.GetComponent<Image>();
                if (specialHud != null)
                {
                    skillButton.onClick.AddListener(specialHud.ToggleSelectionPanelFromUi);
                }
            }

            PlayerPermanentProgression progression = playerHealth.GetComponent<PlayerPermanentProgression>();
            PlayerFirePatternState firePatterns = playerHealth.GetComponent<PlayerFirePatternState>();
            PlayerDomainReserve reserve = PlayerDomainReserve.EnsureOn(specialMode.gameObject);
            compact.Bind(playerCurrency, progression, specialMode, firePatterns, reserve);

            HideLegacyRightHud(gameplayHud, "Player Currency");
            HideLegacyRightHud(gameplayHud, "Player Special Mode");
            HideLegacyDomainChargeHuds();
        }

        private static RectTransform CreateCompactHudCard(RectTransform parent, string name, Vector2 position, Vector2 size, string spriteName)
        {
            RectTransform card = CreateRect(name, parent, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(.5f, 1f), position, size);
            Image image = card.gameObject.AddComponent<Image>();
            Sprite sprite = Resources.Load<Sprite>("UI/HudCompact/Frames/" + spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = CaveUiTheme.Surface;
                AddPanelFrame(card, CaveUiTheme.BronzeLight, 1f);
            }
            return card;
        }

        private static void CreateCompactHudLabel(RectTransform card, Font font, string name, string value)
        {
            Text label = CreateAnchoredText(name, card, font, value, 10, TextAnchor.MiddleLeft,
                new Vector2(47f, -17f), new Vector2(116f, 18f));
            label.fontStyle = FontStyle.Bold;
            label.color = CaveUiTheme.Gold;
        }

        private static Text CreateCompactHudValue(RectTransform card, Font font, string name, Color color)
        {
            Text value = CreateAnchoredText(name, card, font, "0", 16, TextAnchor.MiddleLeft,
                new Vector2(47f, -45f), new Vector2(116f, 24f));
            value.fontStyle = FontStyle.Bold;
            value.color = color;
            return value;
        }

        private static void HideLegacyRightHud(Transform gameplayHud, string childName)
        {
            Transform legacy = gameplayHud.Find(childName);
            if (legacy != null && legacy.name != "Compact Player Information")
            {
                // The legacy special-mode bar also owns the live Nexus and Shop
                // controller.  Removing its presentation must never disable its
                // Update loop, click listeners, or runtime bindings.
                if (legacy.GetComponent<PlayerSpecialModeHud>() != null)
                {
                    // Recover sessions produced by the old compact HUD path,
                    // which deactivated this GameObject outright.
                    legacy.gameObject.SetActive(true);
                    CanvasGroup group = legacy.GetComponent<CanvasGroup>();
                    if (group == null) group = legacy.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                    return;
                }

                legacy.gameObject.SetActive(false);
            }
        }

        private static void HideLegacyDomainChargeHuds()
        {
            // Only the compact presentation owns a visible charge fill.  Legacy
            // widgets remain bound for old scene compatibility but are hidden.
            foreach (PlayerDomainChargeHud legacy in Object.FindObjectsOfType<PlayerDomainChargeHud>(true))
            {
                if (legacy == null) continue;
                // Unlike the Nexus owner, this has no input/controller duties.
                // Deactivation prevents its own reserve callback from restoring
                // alpha and producing a second visible bar.
                legacy.gameObject.SetActive(false);
            }
        }

        private static void EnsureShieldHud(Transform gameplayHud)
        {
            if (gameplayHud == null)
            {
                return;
            }

            // Preserve legacy objects for scene compatibility, but remove the
            // old standalone panel from presentation. The compact segment below
            // is the sole visible shield readout.
            Transform legacyPanel = gameplayHud.Find("Player Strength Shield");
            if (legacyPanel != null)
            {
                legacyPanel.gameObject.SetActive(false);
            }

            PlayerHealthHud healthHud = gameplayHud.GetComponentInChildren<PlayerHealthHud>(true);
            RectTransform healthBar = healthHud != null
                ? healthHud.transform.Find("Health Bar Background") as RectTransform
                : null;
            if (healthBar == null)
            {
                return;
            }

            RectTransform shieldSegment = healthBar.Find("Shield Segment") as RectTransform;
            if (shieldSegment == null)
            {
                shieldSegment = CreateRect(
                    "Shield Segment",
                    healthBar,
                    new Vector2(0.8f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero);
                shieldSegment.offsetMin = new Vector2(1f, 1f);
                shieldSegment.offsetMax = new Vector2(-1f, -1f);
                Image segmentBackground = shieldSegment.gameObject.AddComponent<Image>();
                segmentBackground.color = new Color(0.05f, 0.18f, 0.28f, 0.95f);
            }

            RectTransform fill = shieldSegment.Find("Shield Fill") as RectTransform;
            if (fill == null)
            {
                fill = CreateStretchRect("Shield Fill", shieldSegment);
                fill.offsetMin = new Vector2(1f, 1f);
                fill.offsetMax = new Vector2(-1f, -1f);
                fill.pivot = new Vector2(0f, 0.5f);
                Image createdFill = fill.gameObject.AddComponent<Image>();
                createdFill.color = new Color(0.28f, 0.82f, 1f, 1f);
            }

            Image fillImage = fill.GetComponent<Image>();
            PlayerShieldHud shieldHud = shieldSegment.GetComponent<PlayerShieldHud>();
            if (shieldHud == null)
            {
                shieldHud = shieldSegment.gameObject.AddComponent<PlayerShieldHud>();
            }

            shieldHud.ConfigureCompact(
                fillImage,
                healthBar.Find("Health Fill") as RectTransform,
                shieldSegment.gameObject);
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
                new Vector2(226f, 82f));

            string[] slotNames = { "HEALTH", "MANA", "EQUIPMENT" };
            string[] symbols = { "+", "✦", "◎" };
            Color[] accents =
            {
                CaveUiTheme.Health,
                CaveUiTheme.Mana,
                CaveUiTheme.Gold
            };
            GameAction[] actions =
            {
                GameAction.UseHealthPotion,
                GameAction.UseManaPotion,
                GameAction.ActivateEquipment
            };

            Image[] backgrounds = new Image[slotNames.Length];
            Text[] keyLabels = new Text[slotNames.Length];
            Text[] quantityLabels = new Text[slotNames.Length];
            Text[] nameLabels = new Text[slotNames.Length];
            Text[] symbolLabels = new Text[slotNames.Length];
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
                itemName.color = CaveUiTheme.PrimaryText;
                itemName.raycastTarget = false;
                nameLabels[index] = itemName;

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
                symbolLabels[index] = symbol;

                keyLabels[index] = CreateCenteredText(
                    slotNames[index] + " Key",
                    slot,
                    font,
                    GameInput.Bindings.GetBinding(actions[index]).Primary.ToString(),
                    12,
                    new Vector2(-19f, -25f),
                    new Vector2(28f, 20f));
                keyLabels[index].color = CaveUiTheme.Gold;
                keyLabels[index].raycastTarget = false;

                quantityLabels[index] = CreateCenteredText(
                    slotNames[index] + " Owned Count",
                    slot,
                    font,
                    "x0",
                    12,
                    new Vector2(18f, -25f),
                    new Vector2(30f, 20f));
                quantityLabels[index].color = CaveUiTheme.PrimaryText;
                quantityLabels[index].raycastTarget = false;
            }

            PlayerActionHotbarHud hotbarHud = hotbar.gameObject.AddComponent<PlayerActionHotbarHud>();
            PlayerEquipmentController equipment = PlayerEquipmentController.EnsureOn(player);
            hotbarHud.Configure(
                backgrounds,
                keyLabels,
                quantityLabels,
                nameLabels,
                symbolLabels,
                player != null ? player.GetComponent<PlayerLandmineInventory>() : null,
                equipment);
        }

        private static void EnsureEnemyLocationBar(
            Transform gameplayHud,
            Font font,
            Transform player)
        {
            if (gameplayHud == null
                || player == null
                || Object.FindObjectOfType<EnemyLocationBarHud>(true) != null)
            {
                return;
            }

            RectTransform track = CreateRect(
                "Enemy Location Bar",
                gameplayHud,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(430f, 24f));
            Image background = track.gameObject.AddComponent<Image>();
            background.color = CaveUiTheme.Surface;
            background.raycastTarget = false;
            AddPanelFrame(track, CaveUiTheme.BronzeLight, 1f);
            CanvasGroup group = track.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            const int markerCount = 7;
            Text[] markers = new Text[markerCount];
            for (int index = 0; index < markerCount; index++)
            {
                markers[index] = CreateCenteredText(
                    "Enemy Threat " + index,
                    track,
                    font,
                    "◆",
                    13,
                    Vector2.zero,
                    new Vector2(24f, 20f));
                markers[index].fontStyle = FontStyle.Bold;
                markers[index].raycastTarget = false;
            }

            EnemyLocationBarHud hud = track.gameObject.AddComponent<EnemyLocationBarHud>();
            hud.Configure(track, background, group, markers, player, 20f);
        }

        private static void EnsureEquipmentWheel(
            Transform gameplayHud,
            Font font,
            GameObject player)
        {
            if (gameplayHud == null
                || player == null
                || Object.FindObjectOfType<EquipmentWheelHud>(true) != null)
            {
                return;
            }

            PlayerEquipmentController controller = PlayerEquipmentController.EnsureOn(player);
            if (controller == null)
            {
                return;
            }

            RectTransform wheel = CreateStretchRect("Equipment Wheel", gameplayHud);
            Image veil = wheel.gameObject.AddComponent<Image>();
            veil.color = new Color(0.005f, 0.008f, 0.014f, 0.36f);
            veil.raycastTarget = false;

            RectTransform ring = CreateRect(
                "Equipment Wheel Center",
                wheel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(252f, 252f));
            Image ringImage = ring.gameObject.AddComponent<Image>();
            ringImage.color = new Color(
                CaveUiTheme.Surface.r,
                CaveUiTheme.Surface.g,
                CaveUiTheme.Surface.b,
                0.94f);
            ringImage.raycastTarget = false;
            AddPanelFrame(ring, CaveUiTheme.Gold, 3f);

            Text centerName = CreateCenteredText(
                "Equipment Name",
                ring,
                font,
                "EQUIPMENT",
                19,
                new Vector2(0f, 24f),
                new Vector2(216f, 30f));
            centerName.color = CaveUiTheme.Gold;
            centerName.fontStyle = FontStyle.Bold;
            Text centerDescription = CreateCenteredText(
                "Equipment Description",
                ring,
                font,
                string.Empty,
                12,
                new Vector2(0f, -28f),
                new Vector2(210f, 78f));
            centerDescription.color = CaveUiTheme.SecondaryText;
            centerDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            centerDescription.verticalOverflow = VerticalWrapMode.Truncate;

            int itemCount = PlayerEquipmentController.EquipmentSlotCapacity;
            Image[] backgrounds = new Image[itemCount];
            Text[] names = new Text[itemCount];
            Text[] symbols = new Text[itemCount];
            Text[] statuses = new Text[itemCount];
            for (int index = 0; index < itemCount; index++)
            {
                float angle = itemCount > 0 ? index * Mathf.PI * 2f / itemCount : 0f;
                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 246f;
                RectTransform card = CreateRect(
                    "Equipment Choice " + index,
                    wheel,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    position,
                    new Vector2(184f, 116f));
                backgrounds[index] = card.gameObject.AddComponent<Image>();
                backgrounds[index].color = CaveUiTheme.SurfaceInset;
                backgrounds[index].raycastTarget = false;
                AddPanelFrame(card, CaveUiTheme.BronzeLight, 2f);

                names[index] = CreateCenteredText(
                    "Choice Name",
                    card,
                    font,
                    string.Empty,
                    13,
                    new Vector2(0f, 38f),
                    new Vector2(170f, 22f));
                names[index].fontStyle = FontStyle.Bold;
                symbols[index] = CreateCenteredText(
                    "Choice Symbol",
                    card,
                    font,
                    string.Empty,
                    34,
                    new Vector2(0f, 4f),
                    new Vector2(80f, 46f));
                statuses[index] = CreateCenteredText(
                    "Choice Status",
                    card,
                    font,
                    string.Empty,
                    11,
                    new Vector2(0f, -39f),
                    new Vector2(170f, 20f));
            }

            Text hint = CreateCenteredText(
                "Equipment Wheel Hint",
                wheel,
                font,
                string.Empty,
                13,
                new Vector2(0f, -244f),
                new Vector2(690f, 26f));
            hint.color = CaveUiTheme.PrimaryText;

            EquipmentWheelHud hud = wheel.gameObject.AddComponent<EquipmentWheelHud>();
            hud.Configure(
                wheel.gameObject,
                backgrounds,
                names,
                symbols,
                statuses,
                centerName,
                centerDescription,
                hint,
                controller);
        }

        private static void EnsureSovereignGambitTacticalHud(Transform gameplayHud,Font font,GameObject player)
        {
            if(gameplayHud==null||player==null||Object.FindObjectOfType<SovereignGambitTacticalHud>(true)!=null)return;
            SovereignGambitTacticalCommandController controller=player.GetComponent<SovereignGambitTacticalCommandController>();
            if(controller==null)controller=player.AddComponent<SovereignGambitTacticalCommandController>();
            RectTransform panel=CreateRect("Sovereign Gambit Tactical HUD",gameplayHud,
                new Vector2(.5f,0f),new Vector2(.5f,0f),new Vector2(.5f,0f),new Vector2(0f,132f),new Vector2(820f,206f));
            Image background=panel.gameObject.AddComponent<Image>();background.color=new Color(.015f,.025f,.045f,.95f);background.raycastTarget=false;
            AddPanelFrame(panel,CaveUiTheme.Gold,2f);
            Text title=CreateCenteredText("Title",panel,font,"SOVEREIGN GAMBIT  •  TACTICAL COMMAND",15,new Vector2(0f,86f),new Vector2(790f,24f));
            title.color=CaveUiTheme.Gold;title.fontStyle=FontStyle.Bold;
            Text[] hand=new Text[4];
            for(int i=0;i<hand.Length;i++)
            {
                hand[i]=CreateCenteredText("Available Command "+i,panel,font,string.Empty,11,new Vector2(-294f+i*196f,46f),new Vector2(186f,50f));
                hand[i].color=CaveUiTheme.PrimaryText;
            }
            Text queueTitle=CreateCenteredText("Queue Title",panel,font,"QUEUE",10,new Vector2(-336f,6f),new Vector2(90f,20f));queueTitle.color=CaveUiTheme.SecondaryText;
            Text[] queue=new Text[2];
            for(int i=0;i<queue.Length;i++)
            { queue[i]=CreateCenteredText("Queued Command "+i,panel,font,string.Empty,10,new Vector2(-208f+i*232f,6f),new Vector2(220f,20f));queue[i].color=CaveUiTheme.SecondaryText; }
            Text disk=CreateCenteredText("Selected Disk",panel,font,string.Empty,11,new Vector2(0f,-43f),new Vector2(780f,72f));disk.color=CaveUiTheme.PrimaryText;
            Text feedback=CreateCenteredText("Command Feedback",panel,font,string.Empty,11,new Vector2(0f,-88f),new Vector2(780f,22f));feedback.color=CaveUiTheme.BorderBright;
            SovereignGambitTacticalHud hud=panel.gameObject.AddComponent<SovereignGambitTacticalHud>();
            hud.Configure(panel.gameObject,hand,queue,disk,feedback,controller);
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
                new Vector2(0f, 250f),
                new Vector2(700f, 148f));
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
                new Vector2(0f, 58f),
                new Vector2(650f, 22f));
            observationLabel.fontStyle = FontStyle.Bold;
            observationLabel.color = CaveUiTheme.PrimaryText;

            Text evidenceLabel = CreateCenteredText(
                "Evidence Progress",
                panel,
                font,
                "EVIDENCE  0/7",
                11,
                new Vector2(-232f, 33f),
                new Vector2(210f, 20f));
            evidenceLabel.color = CaveUiTheme.BorderBright;
            evidenceLabel.fontStyle = FontStyle.Bold;

            Text predictionProgressLabel = CreateCenteredText(
                "Correct Prediction Progress",
                panel,
                font,
                "CORRECT PREDICTIONS  0/1",
                11,
                new Vector2(0f, 33f),
                new Vector2(260f, 20f));
            predictionProgressLabel.color = CaveUiTheme.Gold;
            predictionProgressLabel.fontStyle = FontStyle.Bold;

            Text towerStageLabel = CreateCenteredText(
                "Tower Stage",
                panel,
                font,
                "TOWER STAGE  0",
                11,
                new Vector2(240f, 33f),
                new Vector2(190f, 20f));
            towerStageLabel.color = CaveUiTheme.BronzeLight;
            towerStageLabel.fontStyle = FontStyle.Bold;

            const int slotCount = DetectiveResearchRules.RequiredEvidenceCount;
            Text[] historyTexts = new Text[slotCount];
            Image[] historyFrames = new Image[slotCount];
            for (int index = 0; index < slotCount; index++)
            {
                float x = -252f + index * 72f;
                RectTransform slot = CreateRect(
                    "Observed Action " + (index + 1),
                    panel,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(x, -8f),
                    new Vector2(58f, 36f));
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
                    new Vector2(52f, 28f));
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
                        new Vector2(x + 36f, -8f),
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
                new Vector2(282f, -8f),
                new Vector2(108f, 48f));
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
                new Vector2(98f, 42f));
            predictedText.fontStyle = FontStyle.Bold;
            predictedText.color = CaveUiTheme.Gold;
            predictedText.raycastTarget = false;

            Text outcomeText = CreateCenteredText(
                "Research Outcome",
                panel,
                font,
                string.Empty,
                12,
                new Vector2(0f, -58f),
                new Vector2(650f, 20f));
            outcomeText.fontStyle = FontStyle.Bold;
            outcomeText.raycastTarget = false;

            CanvasGroup canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            DetectiveResearchHud hud = panel.gameObject.AddComponent<DetectiveResearchHud>();
            hud.Configure(
                canvasGroup,
                observationLabel,
                evidenceLabel,
                predictionProgressLabel,
                towerStageLabel,
                historyTexts,
                historyFrames,
                predictedText,
                predictionFrame,
                outcomeText);
        }

        private static void EnsureStatusEffectHud(Transform gameplayHud, Font font, GameObject player)
        {
            if (gameplayHud == null)
            {
                return;
            }

            PlayerStatusEffectHud existingHud = Object.FindObjectOfType<PlayerStatusEffectHud>(true);
            if (existingHud != null)
            {
                ConfigureExistingStatusEffectHud(existingHud, font, player);
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
            ConfigureCompactStatusStrip(row);

            const int slotCount = 9;
            GameObject[] slots = new GameObject[slotCount];
            Text[] labels = new Text[slotCount];
            Image[] icons = new Image[slotCount];
            for (int index = 0; index < slotCount; index++)
            {
                RectTransform slot = CreateRect(
                    "Status Slot " + (index + 1),
                    row,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(32f, 32f));
                Image image = slot.gameObject.AddComponent<Image>();
                image.color = new Color(0.02f, 0.04f, 0.06f, 0.45f);
                image.raycastTarget = true;
                labels[index] = CreateCenteredText(
                    "Status Icon",
                    slot,
                    font,
                    string.Empty,
                    18,
                    Vector2.zero,
                    new Vector2(24f, 24f));
                labels[index].fontStyle = FontStyle.Bold;
                labels[index].color = CaveUiTheme.PrimaryText;
                labels[index].raycastTarget = false;
                icons[index] = EnsureStatusSlotSprite(slot);
                ConfigureCompactStatusSlot(slot, index, labels[index], icons[index]);
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
            hud.Configure(slots, labels, icons, player, tooltipPanel);
        }

        private static void ConfigureExistingStatusEffectHud(
            PlayerStatusEffectHud hud,
            Font font,
            GameObject player)
        {
            const int slotCount = 9;
            RectTransform row = hud.transform as RectTransform;
            ConfigureCompactStatusStrip(row);
            GameObject[] slots = new GameObject[slotCount];
            Text[] labels = new Text[slotCount];
            Image[] icons = new Image[slotCount];
            for (int index = 0; index < slotCount; index++)
            {
                Transform slotTransform = hud.transform.Find("Status Slot " + (index + 1));
                RectTransform slot = slotTransform as RectTransform;
                if (slot == null)
                {
                    slot = CreateRect(
                        "Status Slot " + (index + 1),
                        hud.transform,
                        new Vector2(0f, 0.5f),
                        new Vector2(0f, 0.5f),
                        new Vector2(0f, 0.5f),
                        Vector2.zero,
                        new Vector2(32f, 32f));
                    slot.gameObject.AddComponent<Image>();
                }

                Text label = slot.Find("Status Icon")?.GetComponent<Text>();
                if (label == null)
                {
                    label = CreateCenteredText(
                        "Status Icon",
                        slot,
                        font,
                        string.Empty,
                        18,
                        Vector2.zero,
                        new Vector2(24f, 24f));
                    label.fontStyle = FontStyle.Bold;
                    label.raycastTarget = false;
                }

                slots[index] = slot.gameObject;
                labels[index] = label;
                icons[index] = EnsureStatusSlotSprite(slot);
                ConfigureCompactStatusSlot(slot, index, label, icons[index]);
            }

            StatusTooltipPanel tooltipPanel = hud.transform.Find("Status Tooltip")
                ?.GetComponent<StatusTooltipPanel>();
            hud.Configure(slots, labels, icons, player, tooltipPanel);
        }

        private static void ConfigureCompactStatusStrip(RectTransform row)
        {
            if (row == null)
            {
                return;
            }

            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(24f, -108f);
            row.sizeDelta = new Vector2(320f, 32f);
        }

        private static void ConfigureCompactStatusSlot(
            RectTransform slot,
            int index,
            Text label,
            Image icon)
        {
            const float SlotSize = 32f;
            const float Spacing = 4f;
            slot.anchorMin = new Vector2(0f, 0.5f);
            slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(index * (SlotSize + Spacing), 16f);
            slot.sizeDelta = new Vector2(SlotSize, SlotSize);

            Image backing = slot.GetComponent<Image>();
            if (backing != null)
            {
                backing.color = new Color(0.02f, 0.04f, 0.06f, 0.45f);
                backing.raycastTarget = true;
            }

            Outline outline = slot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            DisableDecorativeFrameChildren(slot);

            if (label != null)
            {
                label.fontSize = 18;
                label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(24f, 24f);
            }

            if (icon != null)
            {
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
                icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
                icon.preserveAspect = true;
            }
        }

        private static void DisableDecorativeFrameChildren(RectTransform root)
        {
            foreach (Transform child in root)
            {
                string childName = child.name;
                if (childName.StartsWith("Iron ")
                    || childName.StartsWith("Bronze ")
                    || childName.StartsWith("Accent "))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static Image EnsureStatusSlotSprite(RectTransform slot)
        {
            Transform existingSprite = slot.Find("Status Sprite");
            Image image = existingSprite != null ? existingSprite.GetComponent<Image>() : null;
            if (image == null)
            {
                RectTransform icon = CreateRect(
                    "Status Sprite",
                    slot,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(24f, 24f));
                image = icon.gameObject.AddComponent<Image>();
            }

            image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = new Vector2(24f, 24f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.enabled = false;
            return image;
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
            if (gameplayHud == null)
            {
                return;
            }

            PlayerCurseHud existingHud = Object.FindObjectOfType<PlayerCurseHud>(true);
            if (existingHud != null)
            {
                ConfigureCompactCurseHud(existingHud, font, player);
                return;
            }

            RectTransform panel = CreateRect(
                "Player Curse Strip",
                gameplayHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -380f),
                new Vector2(146f, 26f));
            GameObject distraction = CreateCurseIcon(panel, font, "INSANITY", "◉", 0);
            GameObject detective = CreateCurseIcon(panel, font, "DETECTIVE", "◉", 1);
            GameObject avarice = CreateCurseIcon(panel, font, "AVARICE", "◆", 2);
            GameObject stoneglass = CreateCurseIcon(panel, font, "STONEGLASS", "◈", 3);
            GameObject cavesGlare = CreateCurseIcon(panel, font, "GLARE", "◉", 4);
            Text insanityLabel = distraction.transform.Find("Curse Tier")?.GetComponent<Text>();
            Text avariceLabel = avarice.transform.Find("Curse Tier")?.GetComponent<Text>();
            Text stoneglassLabel = stoneglass.transform.Find("Curse Tier")?.GetComponent<Text>();
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
                stoneglass,
                cavesGlare,
                insanityLabel,
                avariceLabel,
                stoneglassLabel,
                deathClaimNotice,
                player != null ? player.GetComponent<PlayerCurseController>() : null);
        }

        private static void ConfigureCompactCurseHud(
            PlayerCurseHud hud,
            Font font,
            GameObject player)
        {
            RectTransform panel = hud.transform as RectTransform;
            if (panel == null)
            {
                return;
            }

            panel.name = "Player Curse Strip";
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(24f, -380f);
            panel.sizeDelta = new Vector2(146f, 26f);

            Transform header = panel.Find("Curse Header");
            if (header != null)
            {
                header.gameObject.SetActive(false);
            }

            GameObject distraction = EnsureCompactCurseIcon(panel, font, "INSANITY", "◉", 0);
            GameObject detective = EnsureCompactCurseIcon(panel, font, "DETECTIVE", "◉", 1);
            GameObject avarice = EnsureCompactCurseIcon(panel, font, "AVARICE", "◆", 2);
            GameObject stoneglass = EnsureCompactCurseIcon(panel, font, "STONEGLASS", "◈", 3);
            GameObject cavesGlare = EnsureCompactCurseIcon(panel, font, "GLARE", "◉", 4);
            Text insanityLabel = distraction.transform.Find("Curse Tier")?.GetComponent<Text>();
            Text avariceLabel = avarice.transform.Find("Curse Tier")?.GetComponent<Text>();
            Text stoneglassLabel = stoneglass.transform.Find("Curse Tier")?.GetComponent<Text>();
            Text deathClaimNotice = hud.transform.parent != null
                ? hud.transform.parent.Find("Avarice Death Claim Notice")?.GetComponent<Text>()
                : null;
            hud.Configure(
                distraction,
                detective,
                avarice,
                stoneglass,
                cavesGlare,
                insanityLabel,
                avariceLabel,
                stoneglassLabel,
                deathClaimNotice,
                player != null ? player.GetComponent<PlayerCurseController>() : null);
        }

        private static GameObject CreateCurseIcon(
            Transform parent,
            Font font,
            string label,
            string symbol,
            int index)
        {
            RectTransform slot = CreateRect(
                label + " Curse",
                parent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(26f, 26f));
            Image image = slot.gameObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.02f, 0.08f, 0.45f);
            image.raycastTarget = true;
            Text icon = CreateCenteredText(
                "Curse Glyph",
                slot,
                font,
                symbol,
                18,
                Vector2.zero,
                new Vector2(22f, 22f));
            icon.color = new Color(0.72f, 0.45f, 1f, 1f);
            Text tier = CreateCenteredText(
                "Curse Tier",
                slot,
                font,
                string.Empty,
                8,
                new Vector2(8f, 8f),
                new Vector2(12f, 12f));
            tier.color = CaveUiTheme.Gold;
            tier.fontStyle = FontStyle.Bold;
            ConfigureCompactCurseIcon(slot, index, icon, tier);
            return slot.gameObject;
        }

        private static GameObject EnsureCompactCurseIcon(
            RectTransform panel,
            Font font,
            string label,
            string symbol,
            int index)
        {
            RectTransform slot = panel.Find(label + " Curse") as RectTransform;
            if (slot == null && label == "INSANITY")
            {
                slot = panel.Find("DISTRACTION Curse") as RectTransform;
                if (slot != null)
                {
                    slot.name = "INSANITY Curse";
                }
            }
            if (slot == null)
            {
                return CreateCurseIcon(panel, font, label, symbol, index);
            }

            Image backing = slot.GetComponent<Image>();
            if (backing == null)
            {
                backing = slot.gameObject.AddComponent<Image>();
            }

            Text glyph = slot.Find("Curse Glyph")?.GetComponent<Text>()
                ?? slot.Find("Curse Symbol")?.GetComponent<Text>();
            if (glyph == null)
            {
                glyph = CreateCenteredText(
                    "Curse Glyph",
                    slot,
                    font,
                    symbol,
                    18,
                    Vector2.zero,
                    new Vector2(22f, 22f));
            }
            else
            {
                glyph.text = symbol;
            }

            Text oldName = slot.Find("Curse Name")?.GetComponent<Text>();
            if (oldName != null)
            {
                oldName.gameObject.SetActive(false);
            }

            Text tier = slot.Find("Curse Tier")?.GetComponent<Text>();
            if (tier == null)
            {
                tier = CreateCenteredText(
                    "Curse Tier",
                    slot,
                    font,
                    string.Empty,
                    8,
                    new Vector2(8f, 8f),
                    new Vector2(12f, 12f));
            }

            ConfigureCompactCurseIcon(slot, index, glyph, tier);
            return slot.gameObject;
        }

        private static void ConfigureCompactCurseIcon(
            RectTransform slot,
            int index,
            Text glyph,
            Text tier)
        {
            const float SlotSize = 26f;
            const float Spacing = 4f;
            slot.anchorMin = new Vector2(0f, 0.5f);
            slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(index * (SlotSize + Spacing), 13f);
            slot.sizeDelta = new Vector2(SlotSize, SlotSize);

            Image backing = slot.GetComponent<Image>();
            if (backing != null)
            {
                backing.color = new Color(0.05f, 0.02f, 0.08f, 0.45f);
                backing.raycastTarget = true;
            }

            Outline outline = slot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            DisableDecorativeFrameChildren(slot);

            if (glyph != null)
            {
                glyph.fontSize = 18;
                glyph.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                glyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                glyph.rectTransform.anchoredPosition = Vector2.zero;
                glyph.rectTransform.sizeDelta = new Vector2(22f, 22f);
                glyph.color = new Color(0.72f, 0.45f, 1f, 1f);
            }

            if (tier != null)
            {
                tier.fontSize = 8;
                tier.rectTransform.anchorMin = new Vector2(1f, 1f);
                tier.rectTransform.anchorMax = new Vector2(1f, 1f);
                tier.rectTransform.pivot = new Vector2(1f, 1f);
                tier.rectTransform.anchoredPosition = new Vector2(-1f, -1f);
                tier.rectTransform.sizeDelta = new Vector2(12f, 12f);
                tier.color = CaveUiTheme.Gold;
            }
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
            string[] iconNames =
            {
                "Ui_CurseInsanity",
                "Ui_CurseDetective",
                "Ui_CurseAvarice",
                "Ui_CurseStoneglass",
                "Ui_CurseGlare"
            };
            Button[] curseButtons = new Button[5];
            Image[] curseCards = new Image[5];
            Image[] curseIcons = new Image[5];
            for (int index = 0; index < curseButtons.Length; index++)
            {
                curseButtons[index] = CreateButton(
                    labels[index], panel, font, positions[index], new Vector2(196f, 180f));
                curseCards[index] = curseButtons[index].GetComponent<Image>();
                curseCards[index].color = CaveUiTheme.SurfaceInset;
                AddPanelFrame(curseButtons[index].GetComponent<RectTransform>(), new Color(0.55f, 0.25f, 0.82f, 1f), 1f);
                Text label = curseButtons[index].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.LowerCenter;
                    label.rectTransform.anchoredPosition = new Vector2(0f, -16f);
                    label.rectTransform.sizeDelta = new Vector2(172f, 62f);
                }

                curseIcons[index] = CreateCurseEmblem(
                    curseButtons[index].transform,
                    iconNames[index]);
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
                curseIcons,
                detailTitle,
                detailBody,
                activeState,
                actionButton,
                close);
        }

        private static Image CreateCurseEmblem(Transform parent, string spriteName)
        {
            GameObject icon = new GameObject("Curse Emblem", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(parent, false);
            Image image = icon.GetComponent<Image>();
            image.sprite = CaveUiArt.GetSprite(spriteName);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 27f);
            rect.sizeDelta = new Vector2(86f, 86f);
            return image;
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

            ApplyPackageIcon(gameplayHud, "Player Health/Health Icon", skin.IconFrame, CaveUiTheme.Health);
            ApplyPackageIcon(gameplayHud, "Player Stamina/Stamina Icon", skin.IconFrame, CaveUiTheme.Stamina);
            ApplyPackageIcon(gameplayHud, "Player Mana/Mana Icon", skin.IconFrame, CaveUiTheme.Mana);
            ApplyPackageIcon(gameplayHud, "Player Strength Shield/Shield Icon", skin.IconFrame, CaveUiTheme.BorderBright);
            ApplyPackageIcon(gameplayHud, "Player Currency/Currency Icon", skin.IconFrame, CaveUiTheme.Gold);
            ApplyPackageIcon(gameplayHud, "Player Special Mode/Mode Icon", skin.IconFrame, CaveUiTheme.BorderBright);
            ApplyPackageIcon(gameplayHud, "World Level/World Level Crest", skin.Gem, CaveUiTheme.BorderBright);

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
                    "MINE Slot"
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

        private static DomainPageView BuildDomainPage(RectTransform content, Font font)
        {
            Text heading = CreateCenteredText(
                "Domain Page Heading", content, font, "DOMAIN CONFIGURATION", 24,
                new Vector2(0f, 252f), new Vector2(620f, 34f));
            heading.fontStyle = FontStyle.Bold;
            heading.color = CaveUiTheme.PrimaryText;

            Text subtitle = CreateCenteredText(
                "Domain Page Subtitle", content, font,
                "Construct the laws and structure of your Domain.",
                13, new Vector2(0f, 222f), new Vector2(760f, 24f));
            subtitle.color = CaveUiTheme.SecondaryText;

            const float leftX = -440f;
            const float centerX = 0f;
            const float rightX = 440f;

            CreateDomainSection("Domain Seed Card", content, new Vector2(leftX, 160f), new Vector2(330f, 118f));
            Text seedHeading = CreateCenteredText("Domain Seed Heading", content, font, "DOMAIN SEED", 17,
                new Vector2(leftX, 196f), new Vector2(290f, 28f));
            seedHeading.fontStyle = FontStyle.Bold;
            seedHeading.color = CaveUiTheme.Gold;
            Text seedStatus = CreateCenteredText("Domain Seed Status", content, font, "AWAKENED", 14,
                new Vector2(leftX, 146f), new Vector2(274f, 54f));
            seedStatus.color = CaveUiTheme.PrimaryText;

            CreateDomainSection("Domain Complexity Card", content, new Vector2(leftX, 42f), new Vector2(330f, 100f));
            Text complexityHeading = CreateCenteredText("Domain Complexity Heading", content, font, "COMPLEXITY", 17,
                new Vector2(leftX, 76f), new Vector2(290f, 26f));
            complexityHeading.fontStyle = FontStyle.Bold;
            complexityHeading.color = CaveUiTheme.Gold;
            Text complexityStatus = CreateCenteredText("Domain Complexity Status", content, font,
                "0 / 0", 14, new Vector2(leftX, 40f), new Vector2(274f, 18f));
            complexityStatus.fontStyle = FontStyle.Bold;
            complexityStatus.color = CaveUiTheme.PrimaryText;
            Text complexityTier = CreateCenteredText("Domain Complexity Tier", content, font,
                "CAPACITY LOCKED", 10, new Vector2(leftX, 23f), new Vector2(274f, 14f));
            complexityTier.color = CaveUiTheme.Gold;
            RectTransform complexityTrack = CreateRect("Domain Complexity Track", content,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(leftX, 10f), new Vector2(260f, 7f));
            Image complexityTrackImage = complexityTrack.gameObject.AddComponent<Image>();
            complexityTrackImage.color = CaveUiTheme.IronDark;
            complexityTrackImage.raycastTarget = false;
            DomainUiSkin.ApplyProgress(complexityTrackImage, false);
            RectTransform complexityFill = CreateRect("Domain Complexity Fill", content,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(leftX - 130f, 10f), new Vector2(0f, 7f));
            Image complexityFillImage = complexityFill.gameObject.AddComponent<Image>();
            complexityFillImage.color = CaveUiTheme.Gold;
            complexityFillImage.raycastTarget = false;
            Text complexityNextTier = CreateCenteredText("Domain Complexity Next Tier", content, font,
                string.Empty, 9, new Vector2(leftX, -12f), new Vector2(274f, 20f));
            complexityNextTier.color = CaveUiTheme.SecondaryText;

            CreateDomainSection("Domain Reserve Card", content, new Vector2(leftX, -72f), new Vector2(330f, 100f));
            Text reserveHeading = CreateCenteredText("Domain Reserve Heading", content, font, "DOMAIN CHARGE", 17,
                new Vector2(leftX, -38f), new Vector2(290f, 26f));
            reserveHeading.fontStyle = FontStyle.Bold;
            reserveHeading.color = CaveUiTheme.Gold;
            Text reserveStatus = CreateCenteredText("Domain Reserve Status", content, font, "CHARGE UNAVAILABLE", 14,
                new Vector2(leftX, -72f), new Vector2(274f, 18f));
            reserveStatus.fontStyle = FontStyle.Bold;
            reserveStatus.color = CaveUiTheme.PrimaryText;
            RectTransform reserveIcon = CreateRect("Domain Reserve Icon", content,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(leftX - 123f, -69f), new Vector2(26f, 26f));
            Image reserveIconImage = reserveIcon.gameObject.AddComponent<Image>();
            reserveIconImage.sprite = DomainUiSkin.Current != null ? DomainUiSkin.Current.ReserveIcon : null;
            reserveIconImage.color = reserveIconImage.sprite != null ? Color.white : Color.clear;
            reserveIconImage.preserveAspect = true;
            reserveIconImage.raycastTarget = false;
            RectTransform reserveTrack = CreateRect("Domain Reserve Track", content,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(leftX, -88f), new Vector2(260f, 7f));
            Image reserveTrackImage = reserveTrack.gameObject.AddComponent<Image>();
            reserveTrackImage.color = CaveUiTheme.IronDark;
            reserveTrackImage.raycastTarget = false;
            DomainUiSkin.ApplyProgress(reserveTrackImage, true);
            RectTransform reserveFill = CreateRect("Domain Reserve Fill", content,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(leftX - 130f, -88f), new Vector2(0f, 7f));
            Image reserveFillImage = reserveFill.gameObject.AddComponent<Image>();
            reserveFillImage.color = CaveUiTheme.Gold;
            reserveFillImage.raycastTarget = false;
            Text reserveDetail = CreateCenteredText("Domain Reserve Detail", content, font, "Uncharged capacity and allocated Reserve share one pool.", 9,
                new Vector2(leftX, -108f), new Vector2(274f, 30f));
            reserveDetail.color = CaveUiTheme.SecondaryText;

            CreateDomainSection("Owned Laws Card", content, new Vector2(leftX, -220f), new Vector2(330f, 150f));
            Text lawsHeading = CreateCenteredText("Owned Laws Heading", content, font, "OWNED LAWS", 17,
                new Vector2(leftX, -166f), new Vector2(290f, 26f));
            lawsHeading.fontStyle = FontStyle.Bold;
            lawsHeading.color = CaveUiTheme.Gold;
            Text ownedLawsStatus = CreateCenteredText("Owned Laws Status", content, font,
                "NO LAWS AUTHORED", 13,
                new Vector2(leftX, -229f), new Vector2(274f, 78f));
            ownedLawsStatus.color = CaveUiTheme.SecondaryText;
            RectTransform ownedLawsViewport = CreateRect("Owned Laws Viewport", content,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(leftX, -229f), new Vector2(286f, 82f));
            Image ownedLawsViewportImage = ownedLawsViewport.gameObject.AddComponent<Image>();
            ownedLawsViewportImage.color = new Color(0f, 0f, 0f, 0f);
            ownedLawsViewportImage.raycastTarget = true;
            ownedLawsViewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect ownedLawsScroll = ownedLawsViewport.gameObject.AddComponent<ScrollRect>();
            ownedLawsScroll.horizontal = false;
            ownedLawsScroll.vertical = true;
            ownedLawsScroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform ownedLawsContent = CreateRect("Owned Laws List Content", ownedLawsViewport,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 1f));
            ownedLawsScroll.viewport = ownedLawsViewport;
            ownedLawsScroll.content = ownedLawsContent;
            ownedLawsStatus.rectTransform.SetParent(ownedLawsViewport, false);
            ownedLawsStatus.rectTransform.anchoredPosition = Vector2.zero;
            ownedLawsStatus.rectTransform.sizeDelta = new Vector2(274f, 78f);
            DomainOwnedLawList ownedLawList = ownedLawsContent.gameObject.AddComponent<DomainOwnedLawList>();
            ownedLawList.Configure(ownedLawsContent, ownedLawsStatus, font);

            CreateDomainSection("Domain Expression Section", content, new Vector2(centerX, 115f), new Vector2(570f, 178f));
            Text expressionHeading = CreateCenteredText("Expression Heading", content, font, "EXPRESSION", 17,
                new Vector2(centerX, 180f), new Vector2(530f, 26f));
            expressionHeading.fontStyle = FontStyle.Bold;
            expressionHeading.color = CaveUiTheme.Gold;
            Text expressionHint = CreateCenteredText("Expression Hint", content, font,
                "Choose how your Domain expresses authority.", 12,
                new Vector2(centerX, 157f), new Vector2(530f, 20f));
            expressionHint.color = CaveUiTheme.SecondaryText;
            Button projectile = CreateButton("◆\nPROJECTILE\nEmitted authority", content, font,
                new Vector2(-190f, 100f), new Vector2(172f, 86f));
            Button frenzy = CreateButton("✦\nFRENZY\nEmbodied authority", content, font,
                new Vector2(0f, 100f), new Vector2(172f, 86f));
            Button trap = CreateButton("◎\nTRAP\nPrepared authority", content, font,
                new Vector2(190f, 100f), new Vector2(172f, 86f));
            projectile.GetComponentInChildren<Text>().fontSize = 13;
            frenzy.GetComponentInChildren<Text>().fontSize = 13;
            trap.GetComponentInChildren<Text>().fontSize = 13;

            CreateDomainSection("Domain Phenomena Section", content, new Vector2(centerX, -58f), new Vector2(570f, 148f));
            Text phenomenaHeading = CreateCenteredText("Phenomena Heading", content, font, "PHENOMENA", 17,
                new Vector2(centerX, -8f), new Vector2(530f, 26f));
            phenomenaHeading.fontStyle = FontStyle.Bold;
            phenomenaHeading.color = CaveUiTheme.Gold;
            Text phenomenaHint = CreateCenteredText("Phenomena Hint", content, font,
                "Select the phenomena your Domain understands.", 11,
                new Vector2(centerX, -30f), new Vector2(530f, 20f));
            phenomenaHint.color = CaveUiTheme.SecondaryText;
            string[] phenomenonLabels = { "HEAT", "FLOW", "MASS", "COMPRESSION", "POTENTIAL", "RESONANCE", "PHASE", "ORDER" };
            Button[] phenomena = new Button[phenomenonLabels.Length];
            for (int index = 0; index < phenomenonLabels.Length; index++)
            {
                float x = -198f + (index % 4) * 132f;
                float y = index < 4 ? -73f : -115f;
                phenomena[index] = CreateButton(phenomenonLabels[index], content, font,
                    new Vector2(x, y), new Vector2(124f, 36f));
                phenomena[index].GetComponentInChildren<Text>().fontSize = 12;
            }

            CreateDomainSection("Domain Territory Section", content, new Vector2(centerX, -220f), new Vector2(570f, 150f));
            Text territoryHeading = CreateCenteredText("Territory Principle Heading", content, font,
                "TERRITORY PRINCIPLE", 17, new Vector2(centerX, -165f), new Vector2(530f, 26f));
            territoryHeading.fontStyle = FontStyle.Bold;
            territoryHeading.color = CaveUiTheme.Gold;
            Text territoryHint = CreateCenteredText("Territory Principle Hint", content, font,
                "Define how your Domain is spatially organized.", 11,
                new Vector2(centerX, -186f), new Vector2(530f, 20f));
            territoryHint.color = CaveUiTheme.SecondaryText;
            string[] territoryLabels = { "ACCUMULATION", "PROPAGATION", "SYNCHRONIZATION", "CATALYSIS", "INTERFERENCE", "REVERSAL" };
            Button[] territories = new Button[territoryLabels.Length];
            for (int index = 0; index < territoryLabels.Length; index++)
            {
                float x = -186f + (index % 3) * 186f;
                float y = index < 3 ? -226f : -264f;
                territories[index] = CreateButton(territoryLabels[index], content, font,
                    new Vector2(x, y), new Vector2(170f, 34f));
                territories[index].GetComponentInChildren<Text>().fontSize = 11;
            }

            Text selectionStatus = CreateCenteredText("Domain Selection Status", content, font,
                "PREVIEW", 10, new Vector2(centerX, -289f), new Vector2(530f, 16f));
            selectionStatus.color = CaveUiTheme.SecondaryText;

            CreateDomainSection("Domain Context Card", content, new Vector2(rightX, 16f), new Vector2(330f, 432f));
            Text contextHeading = CreateCenteredText("Domain Context Heading", content, font, "DOMAIN MASTERY", 17,
                new Vector2(rightX, 204f), new Vector2(292f, 26f));
            contextHeading.fontStyle = FontStyle.Bold;
            contextHeading.color = CaveUiTheme.Gold;
            Text contextBody = CreateCenteredText("Domain Context Body", content, font,
                "Select an Expression, Phenomenon, or Territory Principle.", 10,
                new Vector2(rightX, 8f), new Vector2(282f, 360f));
            contextBody.alignment = TextAnchor.UpperLeft;
            contextBody.color = CaveUiTheme.PrimaryText;
            contextBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            contextBody.verticalOverflow = VerticalWrapMode.Truncate;
            Text authoringStatus = CreateCenteredText("Domain Authoring Status", content, font,
                "PROFILE REQUIRED", 10, new Vector2(rightX, -224f), new Vector2(292f, 52f));
            authoringStatus.color = CaveUiTheme.SecondaryText;
            Button createLaw = CreateButton("CREATE LAW\nPROFILE REQUIRED", content, font,
                new Vector2(rightX, -266f), new Vector2(330f, 66f));
            createLaw.GetComponentInChildren<Text>().fontSize = 14;

            DomainPageView view = content.gameObject.AddComponent<DomainPageView>();
            view.Configure(seedStatus, complexityStatus, complexityTier, complexityNextTier, complexityFillImage,
                reserveStatus, reserveDetail, reserveFillImage,
                ownedLawsStatus, ownedLawList, projectile, frenzy, trap,
                phenomena, territories, selectionStatus, contextHeading, contextBody,
                authoringStatus, createLaw);
            return view;
        }

        private static RectTransform CreateDomainSection(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            RectTransform section = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size);
            Image image = section.gameObject.AddComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            DomainUiPanel panel = section.gameObject.AddComponent<DomainUiPanel>();
            panel.Configure(size.x >= 500f ? DomainUiPanelKind.Large
                : size.y >= 180f ? DomainUiPanelKind.Medium : DomainUiPanelKind.Small);
            if (DomainUiSkin.Current == null)
            {
                AddPanelFrame(section, CaveUiTheme.BronzeLight, 1.5f);
                AddCornerOrnaments(section, CaveUiTheme.BronzeLight, 6f);
            }
            return section;
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

        private static Image CreateNexusSkillIcon(string name, Transform parent,
            string spriteName, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = NexusSkillArt.Get(spriteName);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = image.sprite != null ? Color.white : new Color(.25f, .72f, 1f, .45f);
            return image;
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

        private static void EnsureMenuCanvas(Transform menus, int sortingOrder)
        {
            if (menus == null)
            {
                return;
            }

            Canvas menuCanvas = menus.GetComponent<Canvas>();
            if (menuCanvas == null)
            {
                menuCanvas = menus.gameObject.AddComponent<Canvas>();
            }

            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = sortingOrder;
            if (menus.GetComponent<GraphicRaycaster>() == null)
            {
                menus.gameObject.AddComponent<GraphicRaycaster>();
            }
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
