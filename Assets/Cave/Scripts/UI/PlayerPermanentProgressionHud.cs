using System.Collections;
using Cave.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerPermanentProgression))]
    public sealed class PlayerPermanentProgressionHud : MonoBehaviour
    {
        private PlayerPermanentProgression progression;
        private Text shardText;
        private Text healthText;
        private Text manaText;
        private Text speedText;
        private Text powerText;
        private Text healthPotionText;
        private Text manaPotionText;
        private Text healthPotionRegenerationText;
        private Text manaPotionRegenerationText;
        private Text diskDamageText;
        private Text diskAreaText;
        private Text diskPullText;
        private Text structuralCarapaceText;
        private Text regenerationCarapaceText;
        private Text latticeCarapaceText;
        private Text feedbackText;
        private Text shardGainPopupText;
        private Button healthButton;
        private Button manaButton;
        private Button speedButton;
        private Button healthPotionButton;
        private Button manaPotionButton;
        private Button healthPotionRegenerationButton;
        private Button manaPotionRegenerationButton;
        private Button diskDamageButton;
        private Button diskAreaButton;
        private Button diskPullButton;
        private Button structuralCarapaceButton;
        private Button regenerationCarapaceButton;
        private Button latticeCarapaceButton;
        private Coroutine feedbackRoutine;
        private Coroutine shardPopupRoutine;

        private void Awake()
        {
            progression = GetComponent<PlayerPermanentProgression>();
        }

        private IEnumerator Start()
        {
            Canvas canvas = null;
            PlayerSpecialModeHud modeHud = null;
            for (int attempt = 0; attempt < 10
                && (canvas == null
                    || modeHud == null
                    || modeHud.PermanentProgressionMount == null
                    || modeHud.GeneralShardSummaryMount == null);
                attempt++)
            {
                canvas = FindObjectOfType<Canvas>();
                modeHud = FindObjectOfType<PlayerSpecialModeHud>(true);
                if (canvas == null
                    || modeHud == null
                    || modeHud.PermanentProgressionMount == null
                    || modeHud.GeneralShardSummaryMount == null)
                {
                    yield return null;
                }
            }

            if (canvas == null
                || modeHud == null
                || modeHud.PermanentProgressionMount == null
                || modeHud.GeneralShardSummaryMount == null)
            {
                yield break;
            }

            Build(
                canvas.transform,
                modeHud.PermanentProgressionMount,
                modeHud.GeneralShardSummaryMount);
            progression.GeneralShardsChanged += HandleShardsChanged;
            progression.UpgradesChanged += Refresh;
            Refresh();
        }

        private void Build(
            Transform canvas,
            RectTransform mount,
            RectTransform shardSummaryMount)
        {
            if (mount.Find("Player Permanent Progression") != null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            GameObject root = CreateUiObject("Player Permanent Progression", mount);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            shardText = CreateText(shardSummaryMount, font, "SHARDS  ◆  0", 13);
            SetRect(shardText.rectTransform, Vector2.zero, new Vector2(156f, 28f));
            shardText.fontStyle = FontStyle.Bold;
            shardText.color = CaveUiTheme.GeneralShard;

            healthPotionButton = CreateUpgradeButton(root.transform, font, new Vector2(-225f, 84f));
            healthPotionText = healthPotionButton.GetComponentInChildren<Text>();
            healthPotionButton.onClick.AddListener(() => Purchase(
                progression.TryPurchaseHealthPotionEfficiency(), "Health Potion Efficiency"));

            manaPotionButton = CreateUpgradeButton(root.transform, font, new Vector2(225f, 84f));
            manaPotionText = manaPotionButton.GetComponentInChildren<Text>();
            manaPotionButton.onClick.AddListener(() => Purchase(
                progression.TryPurchaseManaPotionEfficiency(), "Mana Potion Efficiency"));

            healthButton = CreateUpgradeButton(root.transform, font, new Vector2(-225f, 50f));
            healthText = healthButton.GetComponentInChildren<Text>();
            healthButton.onClick.AddListener(() => Purchase(progression.TryPurchaseHealthRegeneration(), "Health Regeneration"));

            manaButton = CreateUpgradeButton(root.transform, font, new Vector2(225f, 50f));
            manaText = manaButton.GetComponentInChildren<Text>();
            manaButton.onClick.AddListener(() => Purchase(progression.TryPurchaseManaRegeneration(), "Mana Regeneration"));

            speedButton = CreateUpgradeButton(root.transform, font, new Vector2(-225f, 16f));
            speedText = speedButton.GetComponentInChildren<Text>();
            speedButton.onClick.AddListener(() => Purchase(progression.TryPurchaseSpeedBurst(), "Speed Burst"));

            Button power = CreateUpgradeButton(root.transform, font, new Vector2(225f, 16f));
            powerText = power.GetComponentInChildren<Text>();
            HoldRepeatButton repeatPurchase = power.gameObject.AddComponent<HoldRepeatButton>();
            repeatPurchase.Configure(
                progression.TryPurchasePower,
                succeeded => Purchase(succeeded, "Power"));

            healthPotionRegenerationButton = CreateUpgradeButton(root.transform, font, new Vector2(-225f, -18f));
            healthPotionRegenerationText = healthPotionRegenerationButton.GetComponentInChildren<Text>();
            healthPotionRegenerationButton.onClick.AddListener(() => Purchase(
                progression.TryPurchaseHealthPotionRegeneration(), "Health Potion Regeneration"));

            manaPotionRegenerationButton = CreateUpgradeButton(root.transform, font, new Vector2(225f, -18f));
            manaPotionRegenerationText = manaPotionRegenerationButton.GetComponentInChildren<Text>();
            manaPotionRegenerationButton.onClick.AddListener(() => Purchase(
                progression.TryPurchaseManaPotionRegeneration(), "Mana Potion Regeneration"));

            diskDamageButton = CreateUpgradeButton(root.transform, font, new Vector2(-225f, -52f));
            diskDamageText = diskDamageButton.GetComponentInChildren<Text>();
            diskDamageButton.onClick.AddListener(() => Purchase(
                progression.TryPurchaseDiskDamage(), "Oblivion Disk Damage"));

            diskAreaButton = CreateUpgradeButton(root.transform, font, new Vector2(225f, -52f));
            diskAreaText = diskAreaButton.GetComponentInChildren<Text>();
            diskAreaButton.onClick.AddListener(() => Purchase(
                progression.TryPurchaseDiskArea(), "Oblivion Disk Area"));

            diskPullButton = CreateUpgradeButton(root.transform, font, new Vector2(-225f, -86f));
            diskPullText = diskPullButton.GetComponentInChildren<Text>();
            diskPullButton.onClick.AddListener(() => Purchase(
                progression.TryPurchaseDiskPullStrength(), "Oblivion Disk Pull"));

            structuralCarapaceButton = CreateUpgradeButton(root.transform, font, new Vector2(-225f, -120f));
            structuralCarapaceText = structuralCarapaceButton.GetComponentInChildren<Text>();
            structuralCarapaceButton.onClick.AddListener(() => Purchase(progression.TryPurchaseStructuralReinforcement(), "Structural Reinforcement"));
            regenerationCarapaceButton = CreateUpgradeButton(root.transform, font, new Vector2(225f, -120f));
            regenerationCarapaceText = regenerationCarapaceButton.GetComponentInChildren<Text>();
            regenerationCarapaceButton.onClick.AddListener(() => Purchase(progression.TryPurchaseLivingRegeneration(), "Living Regeneration"));
            latticeCarapaceButton = CreateUpgradeButton(root.transform, font, new Vector2(0f, -154f));
            latticeCarapaceText = latticeCarapaceButton.GetComponentInChildren<Text>();
            latticeCarapaceButton.onClick.AddListener(() => Purchase(progression.TryPurchaseAnchoredLattice(), "Anchored Lattice"));

            feedbackText = CreateText(root.transform, font, string.Empty, 11);
            SetRect(feedbackText.rectTransform, new Vector2(0f, -188f), new Vector2(860f, 18f));
            feedbackText.color = CaveUiTheme.BorderBright;

            shardGainPopupText = CreateText(canvas, font, string.Empty, 22);
            shardGainPopupText.gameObject.name = "General Shard Gain Popup";
            SetRect(
                shardGainPopupText.rectTransform,
                new Vector2(0f, 92f),
                new Vector2(360f, 48f));
            shardGainPopupText.color = CaveUiTheme.GeneralShard;
            Outline popupOutline = shardGainPopupText.gameObject.AddComponent<Outline>();
            popupOutline.effectColor = Color.black;
        }

        private void Purchase(bool succeeded, string upgradeName)
        {
            ShowFeedback(succeeded
                ? upgradeName + " purchased."
                : "Purchase unavailable or not enough Shards.");
            Refresh();
        }

        private void HandleShardsChanged(int current, int delta)
        {
            Refresh();
            if (delta > 0)
            {
                ShowFeedback("+" + delta + " GENERAL SHARDS");
                if (shardPopupRoutine != null)
                {
                    StopCoroutine(shardPopupRoutine);
                }

                shardPopupRoutine = StartCoroutine(ShardPopupRoutine(delta));
            }
        }

        private void Refresh()
        {
            if (shardText == null)
            {
                return;
            }

            shardText.text = "SHARDS  ◆  " + progression.GeneralShards;
            healthPotionText.text = "HEALTH POTION EFFICIENCY L" + progression.HealthPotionEfficiencyLevel
                + "  •  +" + FormatPercent(progression.PotionEfficiencyIncreasePerLevel)
                + " NEW POTENCY\nNEXT " + progression.HealthPotionEfficiencyCost + " ◆";
            manaPotionText.text = "MANA POTION EFFICIENCY L" + progression.ManaPotionEfficiencyLevel
                + "  •  +" + FormatPercent(progression.PotionEfficiencyIncreasePerLevel)
                + " NEW POTENCY\nNEXT " + progression.ManaPotionEfficiencyCost + " ◆";
            healthText.text = "HEALTH REGEN L" + progression.HealthRegenerationLevel
                + "  •  "
                + FormatPercent(progression.HealthRegenerationPercentPerTick)
                + " / " + FormatSeconds(progression.RegenerationTickInterval)
                + "s\nNEXT " + progression.HealthRegenerationCost + " ◆";
            manaText.text = "MANA REGEN L" + progression.ManaRegenerationLevel
                + "  •  "
                + FormatPercent(progression.ManaRegenerationPercentPerTick)
                + " / " + FormatSeconds(progression.RegenerationTickInterval)
                + "s\nNEXT " + progression.ManaRegenerationCost + " ◆";
            speedText.text = "SPEED BURST  •  " + (progression.SpeedBurstOwned
                ? "OWNED"
                : progression.SpeedBurstCost + " ◆");
            powerText.text = "POWER +1%  •  " + progression.PowerCostPerPurchase
                + " ◆  (TOTAL +"
                + Mathf.RoundToInt(progression.PermanentDamagePercent * 100f) + "%)";
            healthPotionRegenerationText.text = "HEALTH POTION REGEN L"
                + progression.HealthPotionRegenerationLevel + "  •  "
                + FormatPercent(progression.HealthPotionRegenerationPercent) + " / "
                + FormatSeconds(progression.PotionRegenerationDuration) + "s\nNEXT "
                + progression.HealthPotionRegenerationCost + " ◆";
            manaPotionRegenerationText.text = "MANA POTION REGEN L"
                + progression.ManaPotionRegenerationLevel + "  •  "
                + FormatPercent(progression.ManaPotionRegenerationPercent) + " / "
                + FormatSeconds(progression.PotionRegenerationDuration) + "s\nNEXT "
                + progression.ManaPotionRegenerationCost + " ◆";
            diskDamageText.text = "DISK DAMAGE L" + progression.DiskDamageLevel
                + "  •  +" + progression.DiskDamageBonus + "\nNEXT "
                + progression.DiskDamageCost + " ◆";
            diskAreaText.text = "DISK AREA L" + progression.DiskAreaLevel
                + "  •  x" + progression.DiskAreaMultiplier.ToString("0.##") + "\nNEXT "
                + progression.DiskAreaCost + " ◆";
            structuralCarapaceText.text = "STRUCTURAL REINFORCEMENT L" + progression.StructuralReinforcementLevel
                + "  •  +" + progression.StructuralReinforcementDurabilityPerLevel + " DURABILITY\nNEXT " + progression.StructuralReinforcementCost + " ◆";
            regenerationCarapaceText.text = "LIVING REGENERATION L" + progression.LivingRegenerationLevel
                + "  •  +" + FormatPercent(progression.LivingRegenerationEfficiencyPerLevel) + " REPAIR\nNEXT " + progression.LivingRegenerationCost + " ◆";
            latticeCarapaceText.text = "ANCHORED LATTICE L" + progression.AnchoredLatticeLevel + " / 5"
                + "  •  +8% CONTROL RESIST\n" + (progression.AnchoredLatticeCost > 0 ? "NEXT " + progression.AnchoredLatticeCost + " ◆" : "MAX" );
            diskPullText.text = "DISK PULL L" + progression.DiskPullStrengthLevel
                + "  •  x" + progression.DiskPullStrengthMultiplier.ToString("0.##") + "\nNEXT "
                + progression.DiskPullStrengthCost + " ◆";
            healthButton.interactable = true;
            manaButton.interactable = true;
            healthPotionButton.interactable = true;
            manaPotionButton.interactable = true;
            speedButton.interactable = !progression.SpeedBurstOwned;
            healthPotionRegenerationButton.interactable = true;
            manaPotionRegenerationButton.interactable = true;
            diskDamageButton.interactable = true;
            diskAreaButton.interactable = true;
            diskPullButton.interactable = true;
            structuralCarapaceButton.interactable = true;
            regenerationCarapaceButton.interactable = true;
            latticeCarapaceButton.interactable = progression.AnchoredLatticeCost > 0;
        }

        private static string FormatPercent(float normalizedPercent)
        {
            return (normalizedPercent * 100f).ToString("0.#") + "%";
        }

        private static string FormatSeconds(float seconds)
        {
            return seconds.ToString("0.#");
        }

        private void ShowFeedback(string message)
        {
            if (feedbackText == null)
            {
                return;
            }

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(FeedbackRoutine(message));
        }

        private IEnumerator FeedbackRoutine(string message)
        {
            feedbackText.text = message;
            yield return new WaitForSecondsRealtime(1.5f);
            feedbackText.text = string.Empty;
            feedbackRoutine = null;
        }

        private IEnumerator ShardPopupRoutine(int amount)
        {
            if (shardGainPopupText == null)
            {
                yield break;
            }

            shardGainPopupText.text = "+" + amount + " GENERAL SHARDS";
            yield return new WaitForSecondsRealtime(1.2f);
            shardGainPopupText.text = string.Empty;
            shardPopupRoutine = null;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static Text CreateText(Transform parent, Font font, string value, int size)
        {
            GameObject textObject = CreateUiObject("Text", parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            Font font,
            string label,
            Vector2 position)
        {
            GameObject buttonObject = CreateUiObject("Upgrade Button", parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = CaveUiTheme.IronLight;
            outline.effectDistance = new Vector2(2f, -2f);
            Button button = buttonObject.AddComponent<Button>();
            SetRect(buttonObject.GetComponent<RectTransform>(), position, new Vector2(880f, 44f));
            Text text = CreateText(buttonObject.transform, font, label, 13);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-8f, -2f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 13;
            return button;
        }

        private static Button CreateUpgradeButton(Transform parent, Font font, Vector2 position)
        {
            Button button = CreateButton(parent, font, string.Empty, position);
            SetRect(button.GetComponent<RectTransform>(), position, new Vector2(430f, 30f));
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void OnDestroy()
        {
            if (progression != null)
            {
                progression.GeneralShardsChanged -= HandleShardsChanged;
                progression.UpgradesChanged -= Refresh;
            }
        }
    }
}
