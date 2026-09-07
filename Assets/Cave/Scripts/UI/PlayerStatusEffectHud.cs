using Cave.Enemies;
using Cave.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerStatusEffectHud : MonoBehaviour
    {
        private const string IconRegistryResourceName = "MobStatusIconRegistry";
        private const float DependencyRetryInterval = 1f;

        private struct SlotPresentation
        {
            public bool Initialized;
            public bool Active;
            public Sprite Icon;
            public string FallbackGlyph;
            public string TooltipLabel;
            public string Description;
            public int DurationTenths;
        }

        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private GameObject[] slots;
        private Text[] labels;
        private Image[] icons;
        private StatusIconTooltip[] tooltips;
        private SlotPresentation[] appliedPresentations;
        private StatusTooltipPanel tooltipPanel;
        private MobStatusIconRegistry iconRegistry;
        private PlayerPoisonStatus poison;
        private PlayerSlowStatus slow;
        private PlayerStunStatus stun;
        private PlayerRecoveryModifiers recovery;
        private PlayerMana mana;
        private DetectiveEncounterCoordinator research;
        private PlayerWarpStatus warp;
        private PlayerBrace brace;
        private GameObject playerObject;
        private float nextRefreshTime;
        private float nextDependencyResolveTime;
        private Sprite poisonIcon;
        private Sprite slowIcon;
        private Sprite towerSuppressionIcon;
        private Sprite watcherMarkIcon;

        public void Configure(
            GameObject[] statusSlots,
            Text[] statusLabels,
            Image[] statusIcons,
            GameObject player,
            StatusTooltipPanel sharedTooltipPanel)
        {
            slots = statusSlots;
            labels = statusLabels;
            icons = statusIcons;
            tooltipPanel = sharedTooltipPanel;
            tooltips = new StatusIconTooltip[slots != null ? slots.Length : 0];
            appliedPresentations = new SlotPresentation[slots != null ? slots.Length : 0];
            for (int index = 0; slots != null && index < slots.Length; index++)
            {
                tooltips[index] = slots[index].GetComponent<StatusIconTooltip>();
                if (tooltips[index] == null)
                {
                    tooltips[index] = slots[index].AddComponent<StatusIconTooltip>();
                }

                tooltips[index].Bind(tooltipPanel);
            }

            playerObject = player;
            ResolvePlayerDependencies();
            ResolveSharedIcons();

            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            if (Time.unscaledTime >= nextDependencyResolveTime)
            {
                ResolvePlayerDependencies();
            }

            Refresh();
        }

        private void Refresh()
        {
            int index = 0;
            AddStatus(ref index, poison != null && poison.IsPoisoned, poisonIcon, "☠", "POISON",
                "Deals damage over time.");
            AddStatus(ref index, slow != null && slow.IsSlowed, slowIcon, "❄", "SLOW",
                "Reduces movement speed.");
            AddStatus(ref index, stun != null && stun.IsStunned, null, "!", "STUN",
                "Temporarily prevents movement and combat actions.",
                stun != null ? stun.CurrentStunRemaining : 0f);
            AddStatus(ref index, brace != null && brace.IsBraced, null, "◆", "BRACED",
                "Settled Guard stance: move slowly, recover Stamina, and watch for a Deflect.");
            bool towerInterference = playerObject != null
                && DetectiveTower.IsPlayerInsideAnyTower(playerObject.transform.position);
            AddStatus(
                ref index,
                towerInterference
                    && ((recovery != null && recovery.HasRecoverySuppression)
                        || (mana != null && mana.HasManaCostPenalty)),
                towerSuppressionIcon,
                "↓",
                "TOWER INTERFERENCE",
                "Suppresses recovery, raises Mana costs, and reduces ordinary drops inside the field.");
            AddStatus(ref index, research != null && research.IsActivelyBeingStudied, watcherMarkIcon, "◉", "STUDIED",
                "A Detective is recording and predicting your combat actions.");
            AddStatus(ref index, warp != null && warp.IsMarked, null, "↯", "WARP MARK",
                "A Wizard has marked you for a position swap.");

            while (slots != null && index < slots.Length)
            {
                ApplyInactivePresentation(index++);
            }
        }

        private void AddStatus(
            ref int index,
            bool active,
            Sprite icon,
            string fallbackGlyph,
            string tooltipLabel,
            string description,
            float remainingDuration = 0f)
        {
            if (!active || slots == null || index >= slots.Length)
            {
                return;
            }

            SlotPresentation presentation = new SlotPresentation
            {
                Initialized = true,
                Active = true,
                Icon = icon,
                FallbackGlyph = fallbackGlyph,
                TooltipLabel = tooltipLabel,
                Description = description,
                DurationTenths = remainingDuration > 0f
                    ? Mathf.CeilToInt(remainingDuration * 10f - 0.0001f)
                    : 0
            };
            ApplyPresentation(index, presentation);

            index++;
        }

        private void ApplyPresentation(int index, SlotPresentation presentation)
        {
            if (slots == null || index < 0 || index >= slots.Length)
            {
                return;
            }

            SlotPresentation previous = appliedPresentations[index];
            bool useSprite = presentation.Icon != null;
            bool presentationChanged = !previous.Initialized
                || !previous.Active
                || previous.Icon != presentation.Icon
                || previous.FallbackGlyph != presentation.FallbackGlyph
                || slots[index].activeSelf != presentation.Active;
            if (presentationChanged)
            {
                if (slots[index].activeSelf != presentation.Active)
                {
                    slots[index].SetActive(presentation.Active);
                }

                if (icons != null && index < icons.Length && icons[index] != null)
                {
                    Image image = icons[index];
                    if (image.sprite != presentation.Icon)
                    {
                        image.sprite = presentation.Icon;
                    }

                    if (image.enabled != useSprite)
                    {
                        image.enabled = useSprite;
                    }
                }

                if (labels != null && index < labels.Length && labels[index] != null)
                {
                    Text label = labels[index];
                    if (label.enabled != !useSprite)
                    {
                        label.enabled = !useSprite;
                    }

                    if (!useSprite && label.text != presentation.FallbackGlyph)
                    {
                        label.text = presentation.FallbackGlyph;
                    }
                }
            }

            bool tooltipChanged = !previous.Initialized
                || previous.TooltipLabel != presentation.TooltipLabel
                || previous.Description != presentation.Description
                || previous.DurationTenths != presentation.DurationTenths;
            if (tooltipChanged && tooltips != null && index < tooltips.Length && tooltips[index] != null)
            {
                tooltips[index].Configure(
                    presentation.TooltipLabel,
                    presentation.Description,
                    presentation.DurationTenths > 0
                        ? (presentation.DurationTenths * 0.1f).ToString("0.0") + "s remaining"
                        : string.Empty);
            }

            appliedPresentations[index] = presentation;
        }

        private void ApplyInactivePresentation(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length)
            {
                return;
            }

            SlotPresentation previous = appliedPresentations[index];
            if (!previous.Initialized || previous.Active || slots[index].activeSelf)
            {
                if (slots[index].activeSelf)
                {
                    slots[index].SetActive(false);
                }

                appliedPresentations[index] = new SlotPresentation
                {
                    Initialized = true,
                    Active = false
                };
            }
        }

        private void ResolvePlayerDependencies()
        {
            nextDependencyResolveTime = Time.unscaledTime + DependencyRetryInterval;
            if (research == null)
            {
                research = FindObjectOfType<DetectiveEncounterCoordinator>();
            }

            if (playerObject == null)
            {
                return;
            }

            poison = poison != null ? poison : playerObject.GetComponent<PlayerPoisonStatus>();
            slow = slow != null ? slow : playerObject.GetComponent<PlayerSlowStatus>();
            stun = stun != null ? stun : playerObject.GetComponent<PlayerStunStatus>();
            recovery = recovery != null
                ? recovery
                : playerObject.GetComponent<PlayerRecoveryModifiers>();
            mana = mana != null ? mana : playerObject.GetComponent<PlayerMana>();
            warp = warp != null ? warp : playerObject.GetComponent<PlayerWarpStatus>();
            brace = brace != null ? brace : playerObject.GetComponent<PlayerBrace>();
        }

        private void ResolveSharedIcons()
        {
            iconRegistry = Resources.Load<MobStatusIconRegistry>(IconRegistryResourceName);
            if (iconRegistry == null)
            {
                return;
            }

            poisonIcon = iconRegistry.GetIcon(MobStatusIconKind.Poison);
            slowIcon = iconRegistry.GetIcon(MobStatusIconKind.Slow);
            towerSuppressionIcon = iconRegistry.GetIcon(MobStatusIconKind.TowerSuppression);
            watcherMarkIcon = iconRegistry.GetIcon(MobStatusIconKind.WatcherMark);
        }
    }

    public sealed class StatusIconTooltip : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private StatusTooltipPanel panel;
        private string statusName;
        private string description;
        private string duration;
        private bool hovered;

        public void Bind(StatusTooltipPanel sharedPanel)
        {
            panel = sharedPanel;
        }

        public void Configure(string name, string effectDescription, string durationText)
        {
            statusName = name;
            description = effectDescription;
            duration = durationText;
            if (hovered)
            {
                panel?.Show(statusName, description, duration);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            panel?.Show(statusName, description, duration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            panel?.Hide(this);
        }

        private void OnDisable()
        {
            hovered = false;
            panel?.Hide(this);
        }
    }

    public sealed class StatusTooltipPanel : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private Text title;
        private Text body;
        public void Configure(CanvasGroup group, Text titleLabel, Text bodyLabel)
        {
            canvasGroup = group;
            title = titleLabel;
            body = bodyLabel;
            SetVisible(false);
        }

        public void Show(string statusName, string description, string duration)
        {
            if (title != null)
            {
                title.text = statusName;
            }

            if (body != null)
            {
                body.text = string.IsNullOrEmpty(duration)
                    ? description
                    : description + "\n" + duration;
            }

            SetVisible(true);
        }

        public void Hide(StatusIconTooltip requester)
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
