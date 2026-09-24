using System;
using System.Collections.Generic;
using Cave.Audio;
using Cave.Axioms.Mastery;
using Cave.Combat;
using Cave.Domain;
using Cave.InputSystem;
using Cave.Player;
using Cave.Projectiles;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerSpecialModeHud : MonoBehaviour
    {
        private enum SelectionTab
        {
            Modes,
            Shop,
            Domain
        }

        [SerializeField] private Text currentModeText;
        [SerializeField] private Text selectionCurrentModeText;
        [SerializeField] private Text currencyText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private GameObject modalBackdrop;
        [SerializeField] private Button toggleButton;

        [Header("Modes Tab")]
        [SerializeField] private GameObject modesContent;
        [SerializeField] private Button modesTabButton;
        [SerializeField] private Button[] modeButtons;
        [SerializeField] private Text[] modeButtonLabels;
        [SerializeField] private Text[] modeInfoTexts;
        [SerializeField] private Button[] upgradeButtons;

        [Header("Shop Tab")]
        [SerializeField] private GameObject shopContent;
        [SerializeField] private Button shopTabButton;
        [SerializeField] private Text healthPotionText;
        [SerializeField] private Text manaPotionText;
        [SerializeField] private Button healthPotionButton;
        [SerializeField] private Button manaPotionButton;
        [SerializeField] private Text landmineText;
        [SerializeField] private Button landmineButton;
        [SerializeField] private RectTransform permanentProgressionMount;
        [SerializeField] private RectTransform generalShardSummaryMount;

        [Header("Domain Tab")]
        [SerializeField] private GameObject domainContent;
        [SerializeField] private Button domainTabButton;
        [SerializeField] private DomainPageView domainPage;

        private static readonly SpecialMode[] Modes =
        {
            SpecialMode.SlowShot,
            SpecialMode.BurnShot,
            SpecialMode.Flight,
            SpecialMode.DamageBoost
        };

        private PlayerSpecialMode specialMode;
        private PlayerCurrency playerCurrency;
        private PlayerSpecialModeUpgradeState upgradeState;
        private PlayerResourceShop resourceShop;
        private PlayerHealth playerHealth;
        private PlayerMana playerMana;
        private PlayerProjectileLauncher projectileLauncher;
        private PlayerLandmineInventory landmines;
        private AxiomMasteryState domainMastery;
        private PlayerMasteryEvidenceRuntime domainEvidence;
        private PlayerDomainLawCollection domainLaws;
        private bool domainSeedSubscribed;
        private readonly Dictionary<CanvasGroup, HudGroupState> suppressedHudGroups = new Dictionary<CanvasGroup, HudGroupState>();

        private struct HudGroupState
        {
            public float Alpha;
            public bool Interactable;
            public bool BlocksRaycasts;
            public bool IgnoreParentGroups;
        }

        public RectTransform PermanentProgressionMount => permanentProgressionMount;
        public RectTransform GeneralShardSummaryMount => generalShardSummaryMount;

        public void Configure(
            Text modeText,
            Text panelModeText,
            Text currentCurrencyText,
            Text statusText,
            GameObject modeSelectionPanel,
            GameObject selectionBackdrop,
            Button panelToggleButton,
            Button[] selectionButtons,
            Text[] selectionLabels,
            Text[] modeInformationTexts,
            Button[] tier2UpgradeButtons,
            GameObject modesTabContent,
            GameObject shopTabContent,
            Button modesTab,
            Button shopTab,
            Text healthProductText,
            Text manaProductText,
            Button healthBuyButton,
            Button manaBuyButton,
            Text landmineProductText,
            Button landmineBuyButton,
            RectTransform progressionMount,
            RectTransform shardSummaryMount,
            GameObject domainTabContent,
            Button domainTab,
            DomainPageView domainPageView)
        {
            currentModeText = modeText;
            selectionCurrentModeText = panelModeText;
            currencyText = currentCurrencyText;
            feedbackText = statusText;
            selectionPanel = modeSelectionPanel;
            modalBackdrop = selectionBackdrop;
            toggleButton = panelToggleButton;
            modeButtons = selectionButtons;
            modeButtonLabels = selectionLabels;
            modeInfoTexts = modeInformationTexts;
            upgradeButtons = tier2UpgradeButtons;
            modesContent = modesTabContent;
            shopContent = shopTabContent;
            modesTabButton = modesTab;
            shopTabButton = shopTab;
            healthPotionText = healthProductText;
            manaPotionText = manaProductText;
            healthPotionButton = healthBuyButton;
            manaPotionButton = manaBuyButton;
            landmineText = landmineProductText;
            landmineButton = landmineBuyButton;
            permanentProgressionMount = progressionMount;
            generalShardSummaryMount = shardSummaryMount;
            domainContent = domainTabContent;
            domainTabButton = domainTab;
            domainPage = domainPageView;

            toggleButton.onClick.AddListener(ToggleSelectionPanel);
            modesTabButton.onClick.AddListener(() => ShowTab(SelectionTab.Modes));
            shopTabButton.onClick.AddListener(() => ShowTab(SelectionTab.Shop));
            domainTabButton.onClick.AddListener(() => ShowTab(SelectionTab.Domain));
            healthPotionButton.onClick.AddListener(BuyHealthPotion);
            manaPotionButton.onClick.AddListener(BuyManaPotion);
            landmineButton.onClick.AddListener(BuyLandmine);

            for (int index = 0; index < modeButtons.Length && index < Modes.Length; index++)
            {
                SpecialMode mode = Modes[index];
                modeButtons[index].onClick.AddListener(() => SelectMode(mode));
                if (index < upgradeButtons.Length)
                {
                    upgradeButtons[index].onClick.AddListener(() => UpgradeMode(mode));
                }
            }

            SubscribeDomainSeed();
            ShowTab(SelectionTab.Modes);
            SetModalVisible(true);
        }

        public void Bind(PlayerSpecialMode modeState, PlayerCurrency currency)
        {
            if (specialMode != modeState)
            {
                UnsubscribeMode();
                BindDomainMastery(null);
                specialMode = modeState;
                SubscribeMode();
                BindDomainMastery(specialMode != null
                    ? specialMode.GetComponent<AxiomMasteryState>()
                    : null);
                BindDomainProduction(specialMode != null
                    ? PlayerMasteryEvidenceRuntime.EnsureOn(specialMode.gameObject) : null,
                    specialMode != null ? PlayerDomainLawCollection.EnsureOn(specialMode.gameObject) : null);
            }

            if (playerCurrency != currency)
            {
                UnsubscribeCurrency();
                playerCurrency = currency;
                SubscribeCurrency();
            }

            RefreshAll();
        }

        public void BindProgression(
            PlayerSpecialModeUpgradeState upgrades,
            PlayerResourceShop shop,
            PlayerHealth health,
            PlayerMana mana)
        {
            UnsubscribeProgression();
            upgradeState = upgrades;
            resourceShop = shop;
            playerHealth = health;
            playerMana = mana;
            landmines = shop != null ? shop.GetComponent<PlayerLandmineInventory>() : null;
            SubscribeProgression();
            RefreshAll();
        }

        private void Start()
        {
            if (specialMode == null || playerCurrency == null)
            {
                PlayerSpecialMode foundMode = FindObjectOfType<PlayerSpecialMode>();
                Bind(foundMode, foundMode != null ? foundMode.GetComponent<PlayerCurrency>() : null);
            }

            PlayerSpecialModeUpgradeState foundUpgrades = FindObjectOfType<PlayerSpecialModeUpgradeState>();
            if (foundUpgrades != null)
            {
                BindProgression(
                    foundUpgrades,
                    foundUpgrades.GetComponent<PlayerResourceShop>(),
                    foundUpgrades.GetComponent<PlayerHealth>(),
                    foundUpgrades.GetComponent<PlayerMana>());
            }

            BindLauncher(FindObjectOfType<PlayerProjectileLauncher>());
        }

        private void Update()
        {
            if (GameInput.GameplayInputEnabled && UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleSelectionPanel();
            }
        }

        private void SelectMode(SpecialMode selectedMode)
        {
            if (specialMode == null || playerCurrency == null)
            {
                return;
            }

            if (selectedMode == specialMode.CurrentMode)
            {
                SetFeedback(FormatMode(selectedMode) + " is already active. No currency spent.");
                return;
            }

            int cost = specialMode.GetSwitchCost(selectedMode);
            if (!specialMode.TrySwitchMode(selectedMode))
            {
                SetFeedback("Need " + cost + " currency for " + FormatMode(selectedMode) + ".");
                return;
            }

            SetFeedback("Switched to " + FormatMode(selectedMode) + " for " + cost + " currency.");
            CaveSfx.Play(CaveSfxCue.Bonus, 0.55f);
        }

        private void UpgradeMode(SpecialMode mode)
        {
            if (upgradeState == null || playerCurrency == null)
            {
                return;
            }

            if (upgradeState.IsTier3Owned(mode))
            {
                SetFeedback(FormatMode(mode) + " Tier 3 is already owned. No currency spent.");
                return;
            }

            if (!upgradeState.IsTier2Owned(mode))
            {
                int tier2Cost = upgradeState.GetTier2Cost(mode);
                if (!upgradeState.TryPurchaseTier2(mode))
                {
                    SetFeedback("Need " + tier2Cost + " currency to upgrade " + FormatMode(mode) + ".");
                    return;
                }

                SetFeedback(
                    FormatMode(mode) + " upgraded to Tier 2 — "
                    + upgradeState.GetTier2AbilityName(mode) + ".");
                CaveSfx.Play(CaveSfxCue.Bonus, 0.9f);
                return;
            }

            int tier3Cost = upgradeState.GetTier3Cost(mode);
            if (!upgradeState.TryPurchaseTier3(mode))
            {
                SetFeedback("Need " + tier3Cost + " currency for " + FormatMode(mode) + " Tier 3.");
                return;
            }

            SetFeedback(
                FormatMode(mode) + " upgraded to Tier 3 — "
                + upgradeState.GetTier3AbilityName(mode) + ".");
            CaveSfx.Play(CaveSfxCue.Bonus, 1f);
        }

        private void BuyHealthPotion()
        {
            if (resourceShop == null)
            {
                return;
            }

            if (resourceShop.TryBuyHealthPotion(out int owned))
            {
                SetFeedback("Health Potion stored. Owned: " + owned + ".");
                CaveSfx.Play(CaveSfxCue.Bonus, 0.7f);
            }
            else
            {
                SetFeedback("Cannot afford Health Potion. No currency spent.");
            }

            RefreshShop();
        }

        private void BuyManaPotion()
        {
            if (resourceShop == null)
            {
                return;
            }

            if (resourceShop.TryBuyManaPotion(out int owned))
            {
                SetFeedback("Mana Potion stored. Owned: " + owned + ".");
                CaveSfx.Play(CaveSfxCue.Bonus, 0.7f);
            }
            else
            {
                SetFeedback("Cannot afford Mana Potion. No currency spent.");
            }

            RefreshShop();
        }

        private void BuyLandmine()
        {
            if (resourceShop == null)
            {
                return;
            }

            if (resourceShop.TryBuyLandmine())
            {
                SetFeedback(
                    "Oblivion Disk capacity " + resourceShop.DiskCapacity + " unlocked. Place with "
                    + GameInput.Bindings.GetBinding(GameAction.PlaceLandmine).Primary + ".");
                CaveSfx.Play(CaveSfxCue.Bonus, 0.7f);
            }
            else
            {
                SetFeedback(resourceShop.IsDiskAtMaximumCapacity
                    ? "Oblivion Disk capacity is already MAX."
                    : "Cannot afford Oblivion Disk upgrade. No currency spent.");
            }

            RefreshShop();
        }

        private void ToggleSelectionPanel()
        {
            SetModalVisible(selectionPanel == null || !selectionPanel.activeSelf);
        }

        private void SetModalVisible(bool visible)
        {
            SetUnderlyingHudVisible(!visible);

            if (modalBackdrop != null)
            {
                modalBackdrop.SetActive(visible);
                if (visible) modalBackdrop.transform.SetAsLastSibling();
            }

            if (selectionPanel != null)
            {
                selectionPanel.SetActive(visible);
                if (visible) selectionPanel.transform.SetAsLastSibling();
            }
        }

        public void ToggleSelectionPanelFromUi()
        {
            ToggleSelectionPanel();
        }

        private void ShowTab(SelectionTab tab)
        {
            bool showModes = tab == SelectionTab.Modes;
            bool showShop = tab == SelectionTab.Shop;
            bool showDomain = tab == SelectionTab.Domain;
            if (modesContent != null)
            {
                modesContent.SetActive(showModes);
            }

            if (shopContent != null)
            {
                shopContent.SetActive(showShop);
            }

            if (domainContent != null)
            {
                domainContent.SetActive(showDomain);
            }

            SetModeHeaderVisible(!showDomain);
            SetDomainFooterVisible(!showDomain);

            SetTabVisual(modesTabButton, showModes);
            SetTabVisual(shopTabButton, showShop);
            SetTabVisual(domainTabButton, showDomain);
            CaveUiArt.ApplySkillTab(modesTabButton, showModes, false);
            CaveUiArt.ApplySkillTab(shopTabButton, showShop, true);
            CaveUiArt.ApplySkillTab(domainTabButton, showDomain, false);
            ApplyDomainTabAccent(showDomain);
            if (showDomain)
            {
                RefreshDomain();
            }
        }

        private static void SetTabVisual(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = selected ? new Color(0.12f, 0.42f, 0.56f, 1f) : CaveUiTheme.SurfaceRaised;
            button.colors = colors;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = selected ? CaveUiTheme.BorderBright : CaveUiTheme.PrimaryText;
                label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void ApplyDomainTabAccent(bool selected)
        {
            if (domainTabButton == null) return;

            Image image = domainTabButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected ? CaveUiTheme.Gold : Color.white;
            }

            Text label = domainTabButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = selected ? CaveUiTheme.PrimaryText : CaveUiTheme.SecondaryText;
            }
        }

        private void RefreshAll()
        {
            if (specialMode != null)
            {
                UpdateMode(specialMode.CurrentMode);
                RefreshModeButtons();
            }

            if (playerCurrency != null)
            {
                UpdateCurrency(playerCurrency.CurrentCurrency);
            }

            RefreshShop();
            RefreshDomain();
        }

        private void RefreshModeButtons()
        {
            if (specialMode == null || modeButtonLabels == null)
            {
                return;
            }

            for (int index = 0; index < Modes.Length; index++)
            {
                SpecialMode mode = Modes[index];
                bool active = mode == specialMode.CurrentMode;
                bool tier2Owned = upgradeState != null && upgradeState.IsTier2Owned(mode);
                bool tier3Owned = upgradeState != null && upgradeState.IsTier3Owned(mode);
                string tier2Ability = upgradeState != null
                    ? upgradeState.GetTier2Description(mode)
                    : "Tier 2 ability";
                string tier3Ability = upgradeState != null
                    ? upgradeState.GetTier3Description(mode)
                    : "Tier 3 ability";

                if (index < modeButtonLabels.Length && modeButtonLabels[index] != null)
                {
                    modeButtonLabels[index].text = active
                        ? "ACTIVE"
                        : "SWITCH  " + specialMode.GetSwitchCost(mode);
                }

                if (modeInfoTexts != null
                    && index < modeInfoTexts.Length
                    && modeInfoTexts[index] != null)
                {
                    modeInfoTexts[index].text = FormatMode(mode).ToUpperInvariant()
                        + (active ? "  • ACTIVE" : string.Empty)
                        + "\n"
                        + (tier2Owned ? "Tier 2 — " : "Tier 2: ")
                        + tier2Ability
                        + (!tier2Owned
                            ? "  |  Cost: " + (upgradeState != null ? upgradeState.GetTier2Cost(mode) : 0)
                            : string.Empty)
                        + "\n"
                        + (tier3Owned ? "Tier 3 — " : "Tier 3: ")
                        + tier3Ability
                        + (!tier3Owned
                            ? "  |  " + (tier2Owned ? "Cost: " + upgradeState.GetTier3Cost(mode) : "Requires Tier 2")
                            : string.Empty);
                }

                if (upgradeButtons != null
                    && index < upgradeButtons.Length
                    && upgradeButtons[index] != null)
                {
                    upgradeButtons[index].gameObject.SetActive(upgradeState != null && !tier3Owned);
                    Text label = upgradeButtons[index].GetComponentInChildren<Text>();
                    if (label != null && upgradeState != null)
                    {
                        label.text = !tier2Owned
                            ? "TIER 2  " + upgradeState.GetTier2Cost(mode)
                            : "TIER 3  " + upgradeState.GetTier3Cost(mode);
                    }
                }
            }
        }

        private void RefreshShop()
        {
            if (resourceShop == null)
            {
                return;
            }

            if (healthPotionText != null && playerHealth != null)
            {
                healthPotionText.text = "HEALTH POTION\nRestore: "
                    + Mathf.RoundToInt(resourceShop.HealthPotionRestorePercent * 100f)
                    + "%  |  Cost: " + resourceShop.HealthPotionCost
                    + "\nOwned: " + resourceShop.OwnedHealthPotions + "  |  Use: "
                    + GameInput.Bindings.GetBinding(GameAction.UseHealthPotion).Primary;
            }

            if (manaPotionText != null && playerMana != null)
            {
                manaPotionText.text = "MANA POTION\nRestore: "
                    + Mathf.RoundToInt(resourceShop.ManaPotionRestorePercent * 100f)
                    + "%  |  Cost: " + resourceShop.ManaPotionCost
                    + "\nOwned: " + resourceShop.OwnedManaPotions + "  |  Use: "
                    + GameInput.Bindings.GetBinding(GameAction.UseManaPotion).Primary;
            }

            if (landmineText != null)
            {
                string cost = resourceShop.IsDiskAtMaximumCapacity
                    ? "MAX"
                    : resourceShop.LandmineCost.ToString();
                landmineText.text = resourceShop.NextDiskCapacityLabel
                    + "\nCharges: " + FormatDiskPips(resourceShop.StoredDiskCharges, resourceShop.DiskCapacity)
                    + "  |  Cost: " + cost
                    + "\nRecharge: " + resourceShop.DiskRechargeSeconds.ToString("0") + "s  |  Place: "
                    + GameInput.Bindings.GetBinding(GameAction.PlaceLandmine).Primary;
            }
        }

        private void RefreshDomain()
        {
            // The Domain page can be configured before the player owner's Awake
            // path has installed its current-run authoring components. Reconcile
            // that normal lifecycle ordering here instead of leaving the page in
            // its null-context fallback state.
            if (specialMode != null)
            {
                PlayerMasteryEvidenceRuntime evidence = PlayerMasteryEvidenceRuntime.EnsureOn(specialMode.gameObject);
                PlayerDomainLawCollection laws = PlayerDomainLawCollection.EnsureOn(specialMode.gameObject);
                if (domainEvidence != evidence || domainLaws != laws)
                {
                    BindDomainProduction(evidence, laws);
                }
            }

            if (domainPage != null)
            {
                domainPage.Refresh(domainMastery);
            }
        }

        private void SetModeHeaderVisible(bool visible)
        {
            if (selectionPanel == null) return;

            SetChildVisible("Active Mode Header", visible);
            SetChildVisible("Selected Mode", visible);
            SetChildVisible("Mode Currency", visible);
        }

        private void SetDomainFooterVisible(bool visible)
        {
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(visible);
            }

            if (generalShardSummaryMount != null)
            {
                generalShardSummaryMount.gameObject.SetActive(visible);
            }
        }

        private void SetUnderlyingHudVisible(bool visible)
        {
            if (visible)
            {
                foreach (KeyValuePair<CanvasGroup, HudGroupState> entry in suppressedHudGroups)
                {
                    CanvasGroup group = entry.Key;
                    if (group == null) continue;

                    HudGroupState state = entry.Value;
                    group.alpha = state.Alpha;
                    group.interactable = state.Interactable;
                    group.blocksRaycasts = state.BlocksRaycasts;
                    group.ignoreParentGroups = state.IgnoreParentGroups;
                }

                suppressedHudGroups.Clear();
                return;
            }

            if (selectionPanel == null) return;

            Transform hudRoot = selectionPanel.transform.parent;
            if (hudRoot == null) return;

            for (int index = 0; index < hudRoot.childCount; index++)
            {
                Transform child = hudRoot.GetChild(index);
                if (child.gameObject == selectionPanel || child.gameObject == modalBackdrop) continue;

                CanvasGroup group = child.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = child.gameObject.AddComponent<CanvasGroup>();
                }

                if (!suppressedHudGroups.ContainsKey(group))
                {
                    suppressedHudGroups.Add(group, new HudGroupState
                    {
                        Alpha = group.alpha,
                        Interactable = group.interactable,
                        BlocksRaycasts = group.blocksRaycasts,
                        IgnoreParentGroups = group.ignoreParentGroups
                    });
                }

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private void SetChildVisible(string name, bool visible)
        {
            Transform child = selectionPanel.transform.Find(name);
            if (child != null)
            {
                child.gameObject.SetActive(visible);
            }
        }

        private void HandleModeChanged(SpecialMode mode)
        {
            UpdateMode(mode);
            RefreshModeButtons();
        }

        private void HandleTier2Changed(SpecialMode mode)
        {
            RefreshModeButtons();
        }

        private void HandleHealthChanged(int current, int maximum)
        {
            RefreshShop();
        }

        private void HandleManaChanged(float current, float maximum)
        {
            RefreshShop();
        }

        private void HandleDomainSeedGranted()
        {
            RefreshDomain();
            SetFeedback("Domain Seed awakened. The Domain foundation is now available.");
        }

        private void HandleDomainMasteryChanged(MasteryDomain phenomenon, float value)
        {
            RefreshDomain();
        }

        private void BindDomainMastery(AxiomMasteryState mastery)
        {
            if (domainMastery == mastery)
            {
                return;
            }

            if (domainMastery != null)
            {
                domainMastery.MasteryChanged -= HandleDomainMasteryChanged;
            }

            domainMastery = mastery;
            if (domainMastery != null)
            {
                domainMastery.MasteryChanged += HandleDomainMasteryChanged;
            }

            RefreshDomain();
        }

        private void BindDomainProduction(PlayerMasteryEvidenceRuntime evidence, PlayerDomainLawCollection laws)
        {
            domainEvidence = evidence;
            domainLaws = laws;
            if (domainPage != null) domainPage.BindProduction(domainEvidence, domainLaws);
        }

        private void SubscribeDomainSeed()
        {
            if (domainSeedSubscribed)
            {
                return;
            }

            DomainProgression.DomainSeedGranted += HandleDomainSeedGranted;
            domainSeedSubscribed = true;
        }

        private void UnsubscribeDomainSeed()
        {
            if (!domainSeedSubscribed)
            {
                return;
            }

            DomainProgression.DomainSeedGranted -= HandleDomainSeedGranted;
            domainSeedSubscribed = false;
        }

        private void UpdateMode(SpecialMode mode)
        {
            if (currentModeText != null)
            {
                currentModeText.text = "SKILL PATH\n" + FormatMode(mode).ToUpperInvariant();
            }

            if (selectionCurrentModeText != null)
            {
                selectionCurrentModeText.text = "ACTIVE PATH  •  " + FormatMode(mode).ToUpperInvariant();
            }
        }

        private void UpdateCurrency(int value)
        {
            if (currencyText != null)
            {
                currencyText.text = "◇ " + value;
            }

            RefreshModeButtons();
        }

        private void BindLauncher(PlayerProjectileLauncher launcher)
        {
            if (projectileLauncher == launcher)
            {
                return;
            }

            if (projectileLauncher != null)
            {
                projectileLauncher.FeedbackRequested -= SetFeedback;
            }

            projectileLauncher = launcher;
            if (projectileLauncher != null)
            {
                projectileLauncher.FeedbackRequested += SetFeedback;
            }
        }

        private void SubscribeMode()
        {
            if (specialMode != null)
            {
                specialMode.SpecialModeChanged += HandleModeChanged;
            }
        }

        private void UnsubscribeMode()
        {
            if (specialMode != null)
            {
                specialMode.SpecialModeChanged -= HandleModeChanged;
            }
        }

        private void SubscribeCurrency()
        {
            if (playerCurrency != null)
            {
                playerCurrency.CurrencyChanged += UpdateCurrency;
            }
        }

        private void UnsubscribeCurrency()
        {
            if (playerCurrency != null)
            {
                playerCurrency.CurrencyChanged -= UpdateCurrency;
            }
        }

        private void SubscribeProgression()
        {
            if (upgradeState != null)
            {
                upgradeState.Tier2OwnershipChanged += HandleTier2Changed;
                upgradeState.Tier3OwnershipChanged += HandleTier2Changed;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged += HandleHealthChanged;
            }

            if (playerMana != null)
            {
                playerMana.ManaChanged += HandleManaChanged;
            }

            if (landmines != null)
            {
                landmines.ConsumableQuantityChanged += HandleConsumableQuantityChanged;
            }
        }

        private void UnsubscribeProgression()
        {
            if (upgradeState != null)
            {
                upgradeState.Tier2OwnershipChanged -= HandleTier2Changed;
                upgradeState.Tier3OwnershipChanged -= HandleTier2Changed;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
            }

            if (playerMana != null)
            {
                playerMana.ManaChanged -= HandleManaChanged;
            }

            if (landmines != null)
            {
                landmines.ConsumableQuantityChanged -= HandleConsumableQuantityChanged;
            }
        }

        private void HandleConsumableQuantityChanged(PlayerConsumableType type, int owned)
        {
            RefreshShop();
        }

        private static string FormatDiskPips(int stored, int capacity)
        {
            if (capacity <= 0) return "LOCKED";
            string pips = string.Empty;
            for (int index = 0; index < capacity; index++)
            {
                if (index > 0) pips += " ";
                pips += index < stored ? "◆" : "◇";
            }
            return pips;
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
            }
        }

        private static string FormatMode(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return "Slow Shot";
                case SpecialMode.BurnShot:
                    return "Burn Shot";
                case SpecialMode.Flight:
                    return "Flight";
                case SpecialMode.DamageBoost:
                    return "Strength";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void OnDestroy()
        {
            SetUnderlyingHudVisible(true);
            UnsubscribeMode();
            UnsubscribeCurrency();
            UnsubscribeProgression();
            BindDomainMastery(null);
            UnsubscribeDomainSeed();
            BindLauncher(null);
        }
    }
}
