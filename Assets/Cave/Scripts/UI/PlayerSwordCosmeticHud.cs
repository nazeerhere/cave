using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Build-once Sword Collection presentation inside the existing Modes panel.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSwordCosmeticHud : MonoBehaviour
    {
        private PlayerSwordCosmetics cosmetics;
        private Font font;
        private Button[] buttons;
        private Text[] labels;
        private bool built;

        public void Configure(PlayerSwordCosmetics configuredCosmetics, Font configuredFont)
        {
            cosmetics = configuredCosmetics;
            font = configuredFont;
            if (!built)
            {
                Build();
                built = true;
            }

            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Build()
        {
            RectTransform panel = CreateRect("Sword Collection", transform, new Vector2(0f, -215f), new Vector2(416f, 92f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = CaveUiTheme.SurfaceInset;

            Text title = CreateText("Sword Collection Title", panel, "SWORD COLLECTION", 12);
            title.fontStyle = FontStyle.Bold;
            title.color = CaveUiTheme.Gold;
            title.alignment = TextAnchor.UpperLeft;
            title.rectTransform.anchoredPosition = new Vector2(-194f, 33f);
            title.rectTransform.sizeDelta = new Vector2(180f, 20f);

            buttons = new Button[4];
            labels = new Text[4];
            for (int index = 0; index < buttons.Length; index++)
            {
                int selectionIndex = index;
                RectTransform card = CreateRect("Sword Card " + index, panel, new Vector2(-153f + index * 102f, -8f), new Vector2(94f, 62f));
                Image cardImage = card.gameObject.AddComponent<Image>();
                cardImage.color = CaveUiTheme.SurfaceRaised;
                Button button = card.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => Select((SwordCosmeticSelection)selectionIndex));
                buttons[index] = button;

                Text label = CreateText("Sword Card Label", card, string.Empty, 10);
                label.alignment = TextAnchor.MiddleCenter;
                label.color = CaveUiTheme.PrimaryText;
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(88f, 56f);
                labels[index] = label;
            }
        }

        private void Select(SwordCosmeticSelection selection)
        {
            if (cosmetics != null)
            {
                cosmetics.Select(selection);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (!built || cosmetics == null)
            {
                return;
            }

            for (int index = 0; index < labels.Length; index++)
            {
                SwordCosmeticSelection selection = (SwordCosmeticSelection)index;
                bool unlocked = IsUnlocked(selection);
                bool equipped = cosmetics.SelectedAppearance == selection;
                labels[index].text = TitleFor(selection) + "\n"
                    + (equipped ? "EQUIPPED" : unlocked ? "UNLOCKED" : UnlockRuleFor(selection));
                labels[index].color = equipped
                    ? CaveUiTheme.BorderBright
                    : unlocked ? CaveUiTheme.PrimaryText : new Color(0.82f, 0.38f, 0.34f, 1f);
                buttons[index].interactable = unlocked;
            }
        }

        private bool IsUnlocked(SwordCosmeticSelection selection)
        {
            switch (selection)
            {
                case SwordCosmeticSelection.Sword1:
                    return cosmetics.Sword1Unlocked;
                case SwordCosmeticSelection.Sword2:
                    return cosmetics.Sword2Unlocked;
                case SwordCosmeticSelection.Sword3:
                    return cosmetics.Sword3Unlocked;
                default:
                    return true;
            }
        }

        private static string TitleFor(SwordCosmeticSelection selection)
        {
            return selection == SwordCosmeticSelection.Default
                ? "DEFAULT"
                : "SWORD " + (int)selection;
        }

        private static string UnlockRuleFor(SwordCosmeticSelection selection)
        {
            switch (selection)
            {
                case SwordCosmeticSelection.Sword1:
                    return "GENERAL";
                case SwordCosmeticSelection.Sword2:
                    return "ARCHETYPES";
                case SwordCosmeticSelection.Sword3:
                    return "WORLD LV 10";
                default:
                    return string.Empty;
            }
        }

        private Text CreateText(string name, Transform parent, string value, int size)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
