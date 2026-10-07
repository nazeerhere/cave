using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Sprite references for the Domain-only ornamental presentation.
    /// This is visual data only; no Domain rule or selection state lives here.</summary>
    [CreateAssetMenu(fileName = "DomainUiSkin", menuName = "Cave/UI/Domain Skin")]
    public sealed class DomainUiSkin : ScriptableObject
    {
        [SerializeField] private Sprite largePanel;
        [SerializeField] private Sprite mediumPanel;
        [SerializeField] private Sprite smallPanel;
        [SerializeField] private Sprite navigation;
        [SerializeField] private Sprite navigationSelected;
        [SerializeField] private Sprite card;
        [SerializeField] private Sprite cardSelected;
        [SerializeField] private Sprite cardDisabled;
        [SerializeField] private Sprite actionButton;
        [SerializeField] private Sprite progressBar;
        [SerializeField] private Sprite reserveBar;
        [SerializeField] private Sprite divider;
        [SerializeField] private Sprite reserveIcon;
        private static DomainUiSkin cached;

        public static DomainUiSkin Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<DomainUiSkin>("UI/Domain/DomainUiSkin");
                return cached;
            }
        }

        public Sprite ReserveIcon => reserveIcon;
        public Sprite ProgressBar => progressBar;
        public Sprite ReserveBar => reserveBar;
        public Sprite Divider => divider;

        public static void ApplyPanel(Image image, DomainUiPanelKind kind)
        {
            DomainUiSkin skin = Current;
            if (skin == null || image == null) return;
            Sprite sprite = kind == DomainUiPanelKind.Large ? skin.largePanel
                : kind == DomainUiPanelKind.Medium ? skin.mediumPanel : skin.smallPanel;
            Apply(image, sprite);
        }

        public static void ApplyOption(Button button, bool selected, bool interactable, bool action = false)
        {
            DomainUiSkin skin = Current;
            Image image = button != null ? button.GetComponent<Image>() : null;
            if (skin == null || image == null) return;
            Sprite sprite = !interactable ? skin.cardDisabled : action ? skin.actionButton
                : selected ? skin.cardSelected : skin.card;
            Apply(image, sprite);
            button.transition = Selectable.Transition.SpriteSwap;
            Sprite interactiveState = action ? skin.actionButton : skin.cardSelected;
            SpriteState states = button.spriteState;
            states.highlightedSprite = interactiveState;
            states.selectedSprite = interactiveState;
            states.pressedSprite = interactiveState;
            states.disabledSprite = skin.cardDisabled;
            button.spriteState = states;
        }

        public static void ApplyNavigation(Button button, bool selected)
        {
            DomainUiSkin skin = Current;
            Image image = button != null ? button.GetComponent<Image>() : null;
            if (skin == null || image == null) return;
            Apply(image, selected ? skin.navigationSelected : skin.navigation);
        }

        public static void ApplyProgress(Image image, bool reserve)
        {
            DomainUiSkin skin = Current;
            if (skin == null || image == null) return;
            Apply(image, reserve ? skin.reserveBar : skin.progressBar);
        }

        private static void Apply(Image image, Sprite sprite)
        {
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

#if UNITY_EDITOR
        public void SetForEditor(Sprite configuredLargePanel, Sprite configuredMediumPanel, Sprite configuredSmallPanel,
            Sprite configuredNavigation, Sprite configuredNavigationSelected, Sprite configuredCard,
            Sprite configuredCardSelected, Sprite configuredCardDisabled, Sprite configuredActionButton,
            Sprite configuredProgressBar, Sprite configuredReserveBar, Sprite configuredDivider, Sprite configuredReserveIcon)
        {
            largePanel = configuredLargePanel; mediumPanel = configuredMediumPanel; smallPanel = configuredSmallPanel;
            navigation = configuredNavigation; navigationSelected = configuredNavigationSelected; card = configuredCard;
            cardSelected = configuredCardSelected; cardDisabled = configuredCardDisabled; actionButton = configuredActionButton;
            progressBar = configuredProgressBar; reserveBar = configuredReserveBar; divider = configuredDivider; reserveIcon = configuredReserveIcon;
        }
#endif
    }

    public enum DomainUiPanelKind { Large, Medium, Small }
}
