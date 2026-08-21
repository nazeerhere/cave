using Cave.Combat;
using Cave.InputSystem;
using Cave.Player;
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

            SpinSwordAttack spinSwordAttack = Object.FindObjectOfType<SpinSwordAttack>();
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            PlayerHealthHud existingHud = Object.FindObjectOfType<PlayerHealthHud>(true);
            if (existingHud != null)
            {
                existingHud.Bind(playerHealth);
                EnsureStaminaHud(existingHud.transform.parent, font, spinSwordAttack);
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
                new Vector2(184f, 64f));

            Image healthBackground = healthPanel.gameObject.AddComponent<Image>();
            healthBackground.color = new Color(0.06f, 0.06f, 0.08f, 0.78f);

            CreateText(
                "Health Label",
                healthPanel,
                font,
                "HEALTH",
                15,
                TextAnchor.MiddleLeft,
                new Vector2(12f, -8f),
                new Vector2(160f, 20f));

            RectTransform segments = CreateRect(
                "Segments",
                healthPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(12f, -34f),
                new Vector2(160f, 22f));

            HorizontalLayoutGroup segmentLayout = segments.gameObject.AddComponent<HorizontalLayoutGroup>();
            segmentLayout.spacing = 6f;
            segmentLayout.childAlignment = TextAnchor.MiddleLeft;
            segmentLayout.childControlWidth = false;
            segmentLayout.childControlHeight = false;
            segmentLayout.childForceExpandWidth = false;
            segmentLayout.childForceExpandHeight = false;

            PlayerHealthHud healthHud = healthPanel.gameObject.AddComponent<PlayerHealthHud>();
            healthHud.Configure(segments, healthBackground);
            healthHud.Bind(playerHealth);
            EnsureStaminaHud(gameplayHud, font, spinSwordAttack);

            RectTransform menus = CreateStretchRect("Menus", root.transform);
            GameObject pauseMenu = CreateMenuPanel("Pause Menu", menus, new Vector2(360f, 260f));
            CreateCenteredText("Pause Title", pauseMenu.transform, font, "PAUSED", 30, new Vector2(0f, 82f));
            Button resumeButton = CreateButton("Resume", pauseMenu.transform, font, new Vector2(0f, 20f));
            Button settingsButton = CreateButton("Settings", pauseMenu.transform, font, new Vector2(0f, -42f));

            GameObject settingsPanel = CreateMenuPanel("Settings Panel", menus, new Vector2(760f, 690f));
            CreateCenteredText("Settings Title", settingsPanel.transform, font, "SETTINGS", 28, new Vector2(0f, 310f));
            Text controlsHeader = CreateCenteredText(
                "Controls Header",
                settingsPanel.transform,
                font,
                "CONTROLS",
                18,
                new Vector2(-285f, 274f),
                new Vector2(150f, 30f));
            controlsHeader.alignment = TextAnchor.MiddleLeft;

            GameAction[] configurableActions =
            {
                GameAction.MoveLeft,
                GameAction.MoveRight,
                GameAction.Jump,
                GameAction.BasicAttack,
                GameAction.ChargedAttack,
                GameAction.Parry,
                GameAction.Pause
            };
            Button[] bindingButtons = new Button[configurableActions.Length];

            const float firstBindingY = 235f;
            const float bindingSpacing = 37f;
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
                existingStaminaHud.Bind(spinSwordAttack);
                return;
            }

            RectTransform staminaPanel = CreateRect(
                "Player Stamina",
                gameplayHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -96f),
                new Vector2(184f, 48f));

            Image panelBackground = staminaPanel.gameObject.AddComponent<Image>();
            panelBackground.color = new Color(0.06f, 0.06f, 0.08f, 0.78f);

            CreateText(
                "Stamina Label",
                staminaPanel,
                font,
                "STAMINA",
                13,
                TextAnchor.MiddleLeft,
                new Vector2(10f, -5f),
                new Vector2(164f, 18f));

            RectTransform barBackground = CreateRect(
                "Stamina Bar Background",
                staminaPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(10f, -27f),
                new Vector2(164f, 12f));
            Image backgroundImage = barBackground.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.13f, 0.17f, 1f);

            RectTransform fill = CreateStretchRect("Stamina Fill", barBackground);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.color = new Color(0.25f, 0.78f, 0.95f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            PlayerStaminaHud staminaHud = staminaPanel.gameObject.AddComponent<PlayerStaminaHud>();
            staminaHud.Configure(fillImage);
            staminaHud.Bind(spinSwordAttack);
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
            image.color = new Color(0.045f, 0.045f, 0.065f, 0.94f);
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
            image.color = new Color(0.18f, 0.19f, 0.24f, 1f);

            Button button = buttonRect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.35f, 0.37f, 0.48f, 1f);
            colors.pressedColor = new Color(0.55f, 0.2f, 0.15f, 1f);
            button.colors = colors;

            RectTransform textRect = CreateStretchRect("Label", buttonRect);
            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
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
