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
        private Text feedbackText;
        private Text shardGainPopupText;
        private Button healthButton;
        private Button manaButton;
        private Button speedButton;
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

            healthButton = CreateButton(root.transform, font, string.Empty, new Vector2(-101f, 29f));
            SetRect(healthButton.GetComponent<RectTransform>(), new Vector2(-101f, 29f), new Vector2(194f, 34f));
            healthText = healthButton.GetComponentInChildren<Text>();
            healthButton.onClick.AddListener(() => Purchase(progression.TryPurchaseHealthRegeneration(), "Health Regeneration"));

            manaButton = CreateButton(root.transform, font, string.Empty, new Vector2(101f, 29f));
            SetRect(manaButton.GetComponent<RectTransform>(), new Vector2(101f, 29f), new Vector2(194f, 34f));
            manaText = manaButton.GetComponentInChildren<Text>();
            manaButton.onClick.AddListener(() => Purchase(progression.TryPurchaseManaRegeneration(), "Mana Regeneration"));

            speedButton = CreateButton(root.transform, font, string.Empty, new Vector2(-101f, -9f));
            SetRect(speedButton.GetComponent<RectTransform>(), new Vector2(-101f, -9f), new Vector2(194f, 34f));
            speedText = speedButton.GetComponentInChildren<Text>();
            speedButton.onClick.AddListener(() => Purchase(progression.TryPurchaseSpeedBurst(), "Speed Burst"));

            Button power = CreateButton(root.transform, font, string.Empty, new Vector2(101f, -9f));
            SetRect(power.GetComponent<RectTransform>(), new Vector2(101f, -9f), new Vector2(194f, 34f));
            powerText = power.GetComponentInChildren<Text>();
            HoldRepeatButton repeatPurchase = power.gameObject.AddComponent<HoldRepeatButton>();
            repeatPurchase.Configure(
                progression.TryPurchasePower,
                succeeded => Purchase(succeeded, "Power"));

            feedbackText = CreateText(root.transform, font, string.Empty, 11);
            SetRect(feedbackText.rectTransform, new Vector2(0f, -49f), new Vector2(398f, 18f));
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
            healthButton.interactable = true;
            manaButton.interactable = true;
            speedButton.interactable = !progression.SpeedBurstOwned;
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
            SetRect(buttonObject.GetComponent<RectTransform>(), position, new Vector2(286f, 44f));
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
