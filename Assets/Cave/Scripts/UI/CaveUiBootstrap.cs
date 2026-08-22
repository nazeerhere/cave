using Cave.Combat;
using Cave.InputSystem;
using Cave.Player;
using Cave.Projectiles;
using Cave.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cave.UI
{
    public static class CaveUiBootstrap
    {
        private const int UiLayer = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateUiIfMissing()
        {
            PlayerHealth playerHealth = Object.FindObjectOfType<PlayerHealth>();
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
                EnsureResourceGainPopups(
                    existingHud.transform.parent,
                    font,
                    playerHealth,
                    spinSwordAttack,
                    playerMana,
                    playerCurrency);
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
                new Vector2(326f, 70f));

            Image healthBackground = healthPanel.gameObject.AddComponent<Image>();
            healthBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(healthPanel, CaveUiTheme.Health);
            CreateSymbolIcon(
                "Health Icon",
                healthPanel,
                font,
                new Vector2(-128f, 0f),
                54f,
                "♥",
                CaveUiTheme.Health,
                30);

            CreateText(
                "Health Label",
                healthPanel,
                font,
                "HEALTH",
                17,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -8f),
                new Vector2(140f, 24f));

            Text healthValueText = CreateAnchoredText(
                "Health Value",
                healthPanel,
                font,
                "3 / 3",
                16,
                TextAnchor.MiddleRight,
                new Vector2(202f, -8f),
                new Vector2(112f, 24f));

            RectTransform healthBarBackground = CreateRect(
                "Health Bar Background",
                healthPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -42f),
                new Vector2(236f, 16f));
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
            EnsureResourceGainPopups(
                gameplayHud,
                font,
                playerHealth,
                spinSwordAttack,
                playerMana,
                playerCurrency);

            RectTransform menus = CreateStretchRect("Menus", root.transform);
            GameObject pauseMenu = CreateMenuPanel("Pause Menu", menus, new Vector2(420f, 310f));
            Text pauseCrest = CreateCenteredText(
                "Pause Crest",
                pauseMenu.transform,
                font,
                "◆",
                26,
                new Vector2(0f, 132f),
                new Vector2(80f, 30f));
            pauseCrest.color = CaveUiTheme.BorderBright;
            Text pauseTitle = CreateCenteredText(
                "Pause Title",
                pauseMenu.transform,
                font,
                "PAUSED",
                34,
                new Vector2(0f, 92f));
            pauseTitle.fontStyle = FontStyle.Bold;
            pauseTitle.color = CaveUiTheme.Gold;
            Text pauseSubtitle = CreateCenteredText(
                "Pause Subtitle",
                pauseMenu.transform,
                font,
                "THE CAVE WAITS",
                14,
                new Vector2(0f, 58f));
            pauseSubtitle.color = CaveUiTheme.SecondaryText;
            Button resumeButton = CreateButton(
                "RESUME",
                pauseMenu.transform,
                font,
                new Vector2(0f, 8f),
                new Vector2(270f, 50f));
            Button settingsButton = CreateButton(
                "SETTINGS",
                pauseMenu.transform,
                font,
                new Vector2(0f, -56f),
                new Vector2(270f, 50f));
            Text pauseHint = CreateCenteredText(
                "Pause Hint",
                pauseMenu.transform,
                font,
                "ESC  •  RESUME",
                13,
                new Vector2(0f, -126f));
            pauseHint.color = CaveUiTheme.SecondaryText;

            GameObject settingsPanel = CreateMenuPanel("Settings Panel", menus, new Vector2(760f, 690f));
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
            CreateCardSurface("Controls Surface", settingsPanel.transform, new Vector2(0f, 118f), new Vector2(710f, 320f));
            CreateCardSurface("Audio Surface", settingsPanel.transform, new Vector2(0f, -140f), new Vector2(710f, 132f));
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
                GameAction.FireProjectile,
                GameAction.Dash,
                GameAction.Pause
            };
            Button[] bindingButtons = new Button[configurableActions.Length];

            const float firstBindingY = 240f;
            const float bindingSpacing = 30f;
            for (int index = 0; index < configurableActions.Length; index++)
            {
                float rowY = firstBindingY - index * bindingSpacing;
                Text actionLabel = CreateCenteredText(
                    FormatActionObjectName(configurableActions[index]) + " Label",
                    settingsPanel.transform,
                    font,
                    SettingsMenuController.FormatAction(configurableActions[index]),
                    17,
                    new Vector2(-195f, rowY),
                    new Vector2(240f, 34f));
                actionLabel.alignment = TextAnchor.MiddleLeft;

                bindingButtons[index] = CreateButton(
                    FormatActionObjectName(configurableActions[index]) + " Binding",
                    settingsPanel.transform,
                    font,
                    new Vector2(170f, rowY),
                    new Vector2(300f, 32f));
                bindingButtons[index].GetComponentInChildren<Text>().fontSize = 16;
            }

            Text statusText = CreateCenteredText(
                "Settings Status",
                settingsPanel.transform,
                font,
                string.Empty,
                15,
                new Vector2(0f, -40f),
                new Vector2(690f, 38f));
            statusText.color = new Color(1f, 0.82f, 0.42f, 1f);

            Text audioHeader = CreateCenteredText(
                "Audio Header",
                settingsPanel.transform,
                font,
                "AUDIO",
                18,
                new Vector2(-285f, -82f),
                new Vector2(150f, 30f));
            audioHeader.alignment = TextAnchor.MiddleLeft;
            audioHeader.color = CaveUiTheme.BorderBright;

            Slider masterSlider = CreateVolumeRow(
                "Master Volume",
                settingsPanel.transform,
                font,
                -122f,
                out Text masterValue);
            Slider sfxSlider = CreateVolumeRow(
                "SFX Volume",
                settingsPanel.transform,
                font,
                -164f,
                out Text sfxValue);

            Button restoreDefaultsButton = CreateButton(
                "Restore Defaults",
                settingsPanel.transform,
                font,
                new Vector2(-130f, -224f),
                new Vector2(240f, 42f));
            Button cancelButton = CreateButton(
                "Cancel Rebind",
                settingsPanel.transform,
                font,
                new Vector2(130f, -224f),
                new Vector2(240f, 42f));
            Button backButton = CreateButton(
                "Back",
                settingsPanel.transform,
                font,
                new Vector2(0f, -288f));

            SettingsMenuController settingsController = settingsPanel.AddComponent<SettingsMenuController>();
            settingsController.Configure(
                configurableActions,
                bindingButtons,
                statusText,
                masterSlider,
                masterValue,
                sfxSlider,
                sfxValue,
                restoreDefaultsButton,
                cancelButton);

            PauseMenuController pauseController = menus.gameObject.AddComponent<PauseMenuController>();
            pauseController.Configure(
                pauseMenu,
                settingsPanel,
                resumeButton,
                settingsButton,
                backButton,
                settingsController);

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
                new Vector2(24f, -102f),
                new Vector2(326f, 62f));

            Image panelBackground = staminaPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(staminaPanel, CaveUiTheme.Stamina);
            CreateSymbolIcon(
                "Stamina Icon",
                staminaPanel,
                font,
                new Vector2(-128f, 0f),
                50f,
                "ϟ",
                CaveUiTheme.Stamina,
                30);

            CreateText(
                "Stamina Label",
                staminaPanel,
                font,
                "STAMINA",
                17,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -6f),
                new Vector2(140f, 22f));

            Text staminaValueText = CreateAnchoredText(
                "Stamina Value",
                staminaPanel,
                font,
                "300 / 300",
                16,
                TextAnchor.MiddleRight,
                new Vector2(202f, -6f),
                new Vector2(112f, 22f));

            RectTransform barBackground = CreateRect(
                "Stamina Bar Background",
                staminaPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -36f),
                new Vector2(236f, 14f));
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
                new Vector2(24f, -172f),
                new Vector2(326f, 62f));

            Image panelBackground = manaPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(manaPanel, CaveUiTheme.Mana);
            CreateSymbolIcon(
                "Mana Icon",
                manaPanel,
                font,
                new Vector2(-128f, 0f),
                50f,
                "✦",
                CaveUiTheme.Mana,
                27);

            CreateText(
                "Mana Label",
                manaPanel,
                font,
                "MANA",
                17,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -6f),
                new Vector2(140f, 22f));

            Text valueText = CreateAnchoredText(
                "Mana Value",
                manaPanel,
                font,
                "100 / 100",
                16,
                TextAnchor.MiddleRight,
                new Vector2(202f, -6f),
                new Vector2(112f, 22f));

            RectTransform barBackground = CreateRect(
                "Mana Bar Background",
                manaPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -36f),
                new Vector2(236f, 14f));
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
                new Vector2(240f, 48f));
            Image panelBackground = currencyPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(currencyPanel, CaveUiTheme.Gold);
            CreateSymbolIcon(
                "Currency Icon",
                currencyPanel,
                font,
                new Vector2(-95f, 0f),
                36f,
                "◇",
                CaveUiTheme.Gold,
                22);

            RectTransform textRect = CreateStretchRect("Currency Value", currencyPanel);
            textRect.offsetMin = new Vector2(52f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            Text valueText = textRect.gameObject.AddComponent<Text>();
            valueText.font = font;
            valueText.fontSize = 18;
            valueText.fontStyle = FontStyle.Bold;
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.color = CaveUiTheme.Currency;
            valueText.text = "CURRENCY   0";

            PlayerCurrencyHud currencyHud = currencyPanel.gameObject.AddComponent<PlayerCurrencyHud>();
            currencyHud.Configure(valueText);
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
                return;
            }

            RectTransform summaryPanel = CreateRect(
                "Player Special Mode",
                gameplayHud,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -80f),
                new Vector2(240f, 58f));
            Image summaryBackground = summaryPanel.gameObject.AddComponent<Image>();
            summaryBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(summaryPanel, CaveUiTheme.BorderBright);

            Text currentModeText = CreateAnchoredText(
                "Current Mode",
                summaryPanel,
                font,
                "MODE\nSLOW SHOT",
                15,
                TextAnchor.MiddleLeft,
                new Vector2(12f, -6f),
                new Vector2(150f, 46f));
            currentModeText.fontStyle = FontStyle.Bold;
            currentModeText.color = CaveUiTheme.PrimaryText;
            Button toggleButton = CreateButton(
                "Modes",
                summaryPanel,
                font,
                new Vector2(86f, 0f),
                new Vector2(62f, 34f));
            toggleButton.GetComponentInChildren<Text>().fontSize = 14;

            RectTransform selectionPanel = CreateRect(
                "Special Mode Selection",
                gameplayHud,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -104f),
                new Vector2(560f, 590f));
            Image selectionBackground = selectionPanel.gameObject.AddComponent<Image>();
            selectionBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(selectionPanel, CaveUiTheme.BorderBright, 3f);
            AddCornerOrnaments(selectionPanel, CaveUiTheme.Gold, 8f);

            Text panelCrest = CreateCenteredText(
                "Special Mode Crest",
                selectionPanel,
                font,
                "◆",
                24,
                new Vector2(0f, 278f),
                new Vector2(80f, 28f));
            panelCrest.color = CaveUiTheme.BorderBright;

            Button modesTabButton = CreateButton(
                "MODES",
                selectionPanel,
                font,
                new Vector2(-126f, 246f),
                new Vector2(238f, 40f));
            Button shopTabButton = CreateButton(
                "SHOP",
                selectionPanel,
                font,
                new Vector2(126f, 246f),
                new Vector2(238f, 40f));
            modesTabButton.GetComponentInChildren<Text>().fontSize = 16;
            shopTabButton.GetComponentInChildren<Text>().fontSize = 16;

            CreateCardSurface(
                "Active Mode Header",
                selectionPanel,
                new Vector2(0f, 202f),
                new Vector2(516f, 34f));
            Text panelCurrentMode = CreateCenteredText(
                "Selected Mode",
                selectionPanel,
                font,
                "ACTIVE MODE  •  SLOW SHOT",
                14,
                new Vector2(-115f, 202f),
                new Vector2(290f, 26f));
            panelCurrentMode.alignment = TextAnchor.MiddleLeft;
            panelCurrentMode.color = CaveUiTheme.PrimaryText;
            Text panelCurrency = CreateCenteredText(
                "Mode Currency",
                selectionPanel,
                font,
                "Currency: 0",
                14,
                new Vector2(174f, 202f),
                new Vector2(150f, 26f));
            panelCurrency.alignment = TextAnchor.MiddleRight;
            panelCurrency.color = CaveUiTheme.Gold;

            RectTransform modesContent = CreateRect(
                "Modes Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -32f),
                new Vector2(520f, 382f));

            Button[] modeButtons = new Button[4];
            Text[] modeLabels = new Text[4];
            Text[] modeInfoTexts = new Text[4];
            Button[] upgradeButtons = new Button[4];
            string[] initialLabels = { "Slow Shot", "Burn Shot", "Flight", "Strength" };
            for (int index = 0; index < initialLabels.Length; index++)
            {
                float rowY = 142f - index * 92f;
                CreateCardSurface(initialLabels[index] + " Card", modesContent, new Vector2(0f, rowY), new Vector2(508f, 82f));
                modeInfoTexts[index] = CreateCenteredText(
                    initialLabels[index] + " Info",
                    modesContent,
                    font,
                    initialLabels[index].ToUpperInvariant() + "\nTier 1",
                    14,
                    new Vector2(-82f, rowY),
                    new Vector2(318f, 70f));
                modeInfoTexts[index].alignment = TextAnchor.MiddleLeft;

                modeButtons[index] = CreateButton(
                    initialLabels[index] + " Switch",
                    modesContent,
                    font,
                    new Vector2(172f, rowY + 18f),
                    new Vector2(142f, 32f));
                modeLabels[index] = modeButtons[index].GetComponentInChildren<Text>();
                modeLabels[index].fontSize = 13;

                upgradeButtons[index] = CreateButton(
                    initialLabels[index] + " Upgrade",
                    modesContent,
                    font,
                    new Vector2(172f, rowY - 19f),
                    new Vector2(142f, 32f));
                upgradeButtons[index].GetComponentInChildren<Text>().fontSize = 13;
            }

            RectTransform shopContent = CreateRect(
                "Shop Content",
                selectionPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -20f),
                new Vector2(520f, 390f));

            CreateCardSurface("Health Potion Card", shopContent, new Vector2(0f, 96f), new Vector2(500f, 118f));
            CreateCardSurface("Mana Potion Card", shopContent, new Vector2(0f, -52f), new Vector2(500f, 118f));
            CreateItemIcon("Health Potion Icon", shopContent, font, new Vector2(-214f, 96f), "+", CaveUiTheme.Health);
            CreateItemIcon("Mana Potion Icon", shopContent, font, new Vector2(-214f, -52f), "✦", CaveUiTheme.Mana);

            Text healthPotionText = CreateCenteredText(
                "Health Potion Info",
                shopContent,
                font,
                "HEALTH POTION\nRestore: 25%  |  Cost: 5",
                16,
                new Vector2(-42f, 96f),
                new Vector2(310f, 102f));
            healthPotionText.alignment = TextAnchor.MiddleLeft;
            Button healthPotionButton = CreateButton(
                "BUY",
                shopContent,
                font,
                new Vector2(187f, 96f),
                new Vector2(104f, 42f));
            healthPotionButton.GetComponentInChildren<Text>().fontSize = 16;

            Text manaPotionText = CreateCenteredText(
                "Mana Potion Info",
                shopContent,
                font,
                "MANA POTION\nRestore: 25%  |  Cost: 4",
                16,
                new Vector2(-42f, -52f),
                new Vector2(310f, 102f));
            manaPotionText.alignment = TextAnchor.MiddleLeft;
            Button manaPotionButton = CreateButton(
                "BUY",
                shopContent,
                font,
                new Vector2(187f, -52f),
                new Vector2(104f, 42f));
            manaPotionButton.GetComponentInChildren<Text>().fontSize = 16;

            Text feedbackText = CreateCenteredText(
                "Mode Feedback",
                selectionPanel,
                font,
                "Mode switches, upgrades, and potions spend Currency.",
                14,
                new Vector2(0f, -268f),
                new Vector2(516f, 38f));
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
                manaPotionButton);
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
                new Vector2(24f, -242f),
                new Vector2(326f, 58f));
            Image panelBackground = shieldPanel.gameObject.AddComponent<Image>();
            panelBackground.color = CaveUiTheme.Surface;
            AddPanelFrame(shieldPanel, CaveUiTheme.Border);
            CreateSymbolIcon(
                "Shield Icon",
                shieldPanel,
                font,
                new Vector2(-128f, 0f),
                48f,
                "◇",
                CaveUiTheme.BorderBright,
                27);

            Text statusText = CreateAnchoredText(
                "Shield Status",
                shieldPanel,
                font,
                "SHIELD  •  TIER I  •  INACTIVE",
                13,
                TextAnchor.MiddleLeft,
                new Vector2(78f, -7f),
                new Vector2(236f, 22f));

            RectTransform barBackground = CreateRect(
                "Shield Recharge Background",
                shieldPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(78f, -38f),
                new Vector2(236f, 9f));
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
                new Vector2(0f, -18f),
                new Vector2(260f, 96f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = CaveUiTheme.Surface;
            panelImage.raycastTarget = false;
            AddPanelFrame(panel, CaveUiTheme.Gold, 3f);
            AddCornerOrnaments(panel, CaveUiTheme.BorderBright, 7f);

            Text label = CreateCenteredText(
                "World Level Label",
                panel,
                font,
                "WORLD LEVEL",
                18,
                new Vector2(0f, 30f),
                new Vector2(230f, 26f));
            label.fontStyle = FontStyle.Bold;
            label.color = CaveUiTheme.SecondaryText;

            Text value = CreateSymbolIcon(
                "World Level Crest",
                panel,
                font,
                new Vector2(0f, -16f),
                58f,
                "0",
                CaveUiTheme.BorderBright,
                32);

            RectTransform announcement = CreateRect(
                "World Level Announcement",
                gameplayHud,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -126f),
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

        private static void AddPanelFrame(RectTransform panel, Color color, float distance = 2f)
        {
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;

            Color trimColor = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.72f));
            CreateFrameStrip("Top Trim", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(-8f, 2f), trimColor);
            CreateFrameStrip("Bottom Trim", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(-8f, 2f), trimColor);
            CreateFrameStrip("Left Trim", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(2f, -8f), trimColor);
            CreateFrameStrip("Right Trim", panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), new Vector2(2f, -8f), trimColor);
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

        private static void CreateCardSurface(
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
            image.color = CaveUiTheme.SurfaceRaised;
            image.raycastTarget = false;
            AddPanelFrame(card, CaveUiTheme.Border, 1f);
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
            image.color = CaveUiTheme.Surface;
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
            outline.effectColor = CaveUiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            RectTransform textRect = CreateStretchRect("Label", buttonRect);
            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = CaveUiTheme.PrimaryText;
            text.text = label;
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
            return text;
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
