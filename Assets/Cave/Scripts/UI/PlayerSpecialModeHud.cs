using System;
using Cave.Audio;
using Cave.Combat;
using Cave.Player;
using Cave.Projectiles;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerSpecialModeHud : MonoBehaviour
    {
        [SerializeField] private Text currentModeText;
        [SerializeField] private Text selectionCurrentModeText;
        [SerializeField] private Text currencyText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private GameObject selectionPanel;
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

        public void Configure(
            Text modeText,
            Text panelModeText,
            Text currentCurrencyText,
            Text statusText,
            GameObject modeSelectionPanel,
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
            Button manaBuyButton)
        {
            currentModeText = modeText;
            selectionCurrentModeText = panelModeText;
            currencyText = currentCurrencyText;
            feedbackText = statusText;
            selectionPanel = modeSelectionPanel;
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

            toggleButton.onClick.AddListener(ToggleSelectionPanel);
            modesTabButton.onClick.AddListener(() => ShowTab(true));
            shopTabButton.onClick.AddListener(() => ShowTab(false));
            healthPotionButton.onClick.AddListener(BuyHealthPotion);
            manaPotionButton.onClick.AddListener(BuyManaPotion);

            for (int index = 0; index < modeButtons.Length && index < Modes.Length; index++)
            {
                SpecialMode mode = Modes[index];
                modeButtons[index].onClick.AddListener(() => SelectMode(mode));
                if (index < upgradeButtons.Length)
                {
                    upgradeButtons[index].onClick.AddListener(() => UpgradeMode(mode));
                }
            }

            ShowTab(true);
            selectionPanel.SetActive(false);
        }

        public void Bind(PlayerSpecialMode modeState, PlayerCurrency currency)
        {
            if (specialMode != modeState)
            {
                UnsubscribeMode();
                specialMode = modeState;
                SubscribeMode();
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

            if (resourceShop.TryBuyHealthPotion(out int restored))
            {
                SetFeedback("Health Potion restored " + restored + " health.");
                CaveSfx.Play(CaveSfxCue.Bonus, 0.7f);
            }
            else
            {
                SetFeedback(playerHealth != null && playerHealth.CurrentHealth >= playerHealth.MaxHealth
                    ? "Health is already full. No currency spent."
                    : "Cannot afford Health Potion. No currency spent.");
            }
        }

        private void BuyManaPotion()
        {
            if (resourceShop == null)
            {
                return;
            }

            if (resourceShop.TryBuyManaPotion(out float restored))
            {
                SetFeedback("Mana Potion restored " + Mathf.RoundToInt(restored) + " mana.");
                CaveSfx.Play(CaveSfxCue.Bonus, 0.7f);
            }
            else
            {
                SetFeedback(playerMana != null && playerMana.CurrentMana >= playerMana.MaximumMana
                    ? "Mana is already full. No currency spent."
                    : "Cannot afford Mana Potion. No currency spent.");
            }
        }

        private void ToggleSelectionPanel()
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(!selectionPanel.activeSelf);
            }
        }

        private void ShowTab(bool showModes)
        {
            if (modesContent != null)
            {
                modesContent.SetActive(showModes);
            }

            if (shopContent != null)
            {
                shopContent.SetActive(!showModes);
            }

            SetTabVisual(modesTabButton, showModes);
            SetTabVisual(shopTabButton, !showModes);
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
                    + "\nHealth: " + playerHealth.CurrentHealth + " / " + playerHealth.MaxHealth;
            }

            if (manaPotionText != null && playerMana != null)
            {
                manaPotionText.text = "MANA POTION\nRestore: "
                    + Mathf.RoundToInt(resourceShop.ManaPotionRestorePercent * 100f)
                    + "%  |  Cost: " + resourceShop.ManaPotionCost
                    + "\nMana: " + Mathf.RoundToInt(playerMana.CurrentMana)
                    + " / " + Mathf.RoundToInt(playerMana.MaximumMana);
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

        private void UpdateMode(SpecialMode mode)
        {
            if (currentModeText != null)
            {
                currentModeText.text = "MODE\n" + FormatMode(mode).ToUpperInvariant();
            }

            if (selectionCurrentModeText != null)
            {
                selectionCurrentModeText.text = "ACTIVE MODE  •  " + FormatMode(mode).ToUpperInvariant();
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
            UnsubscribeMode();
            UnsubscribeCurrency();
            UnsubscribeProgression();
            BindLauncher(null);
        }
    }
}
