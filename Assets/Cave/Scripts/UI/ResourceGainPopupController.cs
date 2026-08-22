using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class ResourceGainPopupController : MonoBehaviour
    {
        [Header("Popup Motion")]
        [SerializeField, Min(0.1f)] private float lifetime = 1f;
        [SerializeField, Min(0f)] private float verticalTravel = 42f;
        [SerializeField, Min(0f)] private float simultaneousSpacing = 34f;
        [SerializeField, Min(0f)] private float aggregationDelay = 0.12f;

        private readonly List<RectTransform> activePopups = new List<RectTransform>();
        private RectTransform popupLayer;
        private Font font;
        private PlayerHealth playerHealth;
        private SpinSwordAttack spinSwordAttack;
        private PlayerMana playerMana;
        private PlayerCurrency playerCurrency;
        private int previousHealth;
        private float previousStamina;
        private float previousMana;
        private int previousCurrency;
        private float pendingHealth;
        private float pendingStamina;
        private float pendingMana;
        private float pendingCurrency;
        private Coroutine healthAggregation;
        private Coroutine staminaAggregation;
        private Coroutine manaAggregation;
        private Coroutine currencyAggregation;

        public void Configure(
            RectTransform layer,
            Font popupFont,
            PlayerHealth health,
            SpinSwordAttack stamina,
            PlayerMana mana,
            PlayerCurrency currency)
        {
            popupLayer = layer;
            font = popupFont;
            Unsubscribe();
            playerHealth = health;
            spinSwordAttack = stamina;
            playerMana = mana;
            playerCurrency = currency;
            CaptureCurrentValues();
            Subscribe();
        }

        private void Start()
        {
            if (playerHealth == null)
            {
                Configure(
                    popupLayer,
                    font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"),
                    FindObjectOfType<PlayerHealth>(),
                    FindObjectOfType<SpinSwordAttack>(),
                    FindObjectOfType<PlayerMana>(),
                    FindObjectOfType<PlayerCurrency>());
            }
        }

        private void CaptureCurrentValues()
        {
            previousHealth = playerHealth != null ? playerHealth.CurrentHealth : 0;
            previousStamina = spinSwordAttack != null ? spinSwordAttack.CurrentStamina : 0f;
            previousMana = playerMana != null ? playerMana.CurrentMana : 0f;
            previousCurrency = playerCurrency != null ? playerCurrency.CurrentCurrency : 0;
        }

        private void Subscribe()
        {
            if (playerHealth != null) playerHealth.HealthChanged += HandleHealthChanged;
            if (spinSwordAttack != null) spinSwordAttack.StaminaChanged += HandleStaminaChanged;
            if (playerMana != null) playerMana.ManaChanged += HandleManaChanged;
            if (playerCurrency != null) playerCurrency.CurrencyChanged += HandleCurrencyChanged;
        }

        private void Unsubscribe()
        {
            if (playerHealth != null) playerHealth.HealthChanged -= HandleHealthChanged;
            if (spinSwordAttack != null) spinSwordAttack.StaminaChanged -= HandleStaminaChanged;
            if (playerMana != null) playerMana.ManaChanged -= HandleManaChanged;
            if (playerCurrency != null) playerCurrency.CurrencyChanged -= HandleCurrencyChanged;
        }

        private void HandleHealthChanged(int current, int maximum)
        {
            int delta = current - previousHealth;
            previousHealth = current;
            if (delta > 0)
            {
                pendingHealth += delta;
                RestartAggregation(ref healthAggregation, FlushHealth);
            }
        }

        private void HandleStaminaChanged(float current, float maximum)
        {
            float delta = current - previousStamina;
            previousStamina = current;
            if (delta > 0.001f)
            {
                pendingStamina += delta;
                RestartAggregation(ref staminaAggregation, FlushStamina);
            }
        }

        private void HandleManaChanged(float current, float maximum)
        {
            float delta = current - previousMana;
            previousMana = current;
            if (delta > 0.001f)
            {
                pendingMana += delta;
                RestartAggregation(ref manaAggregation, FlushMana);
            }
        }

        private void HandleCurrencyChanged(int current)
        {
            int delta = current - previousCurrency;
            previousCurrency = current;
            if (delta > 0)
            {
                pendingCurrency += delta;
                RestartAggregation(ref currencyAggregation, FlushCurrency);
            }
        }

        private void RestartAggregation(ref Coroutine routine, System.Action flush)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(FlushAfterQuietPeriod(flush));
        }

        private IEnumerator FlushAfterQuietPeriod(System.Action flush)
        {
            if (aggregationDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(aggregationDelay);
            }

            flush();
        }

        private void FlushHealth()
        {
            ShowGain(pendingHealth, "HEALTH", CaveUiTheme.Health);
            pendingHealth = 0f;
            healthAggregation = null;
        }

        private void FlushStamina()
        {
            ShowGain(pendingStamina, "STAMINA", CaveUiTheme.Stamina);
            pendingStamina = 0f;
            staminaAggregation = null;
        }

        private void FlushMana()
        {
            ShowGain(pendingMana, "MANA", CaveUiTheme.Mana);
            pendingMana = 0f;
            manaAggregation = null;
        }

        private void FlushCurrency()
        {
            ShowGain(pendingCurrency, "CURRENCY", CaveUiTheme.Currency);
            pendingCurrency = 0f;
            currencyAggregation = null;
        }

        private void ShowGain(float amount, string resourceName, Color color)
        {
            if (popupLayer == null || font == null)
            {
                return;
            }

            GameObject popupObject = new GameObject(resourceName + " Gain", typeof(RectTransform));
            popupObject.layer = gameObject.layer;
            RectTransform rect = popupObject.GetComponent<RectTransform>();
            rect.SetParent(popupLayer, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 32f);
            rect.anchoredPosition = new Vector2(0f, 54f - activePopups.Count * simultaneousSpacing);

            CanvasGroup group = popupObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            Text text = popupObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = "+" + FormatAmount(amount) + "  " + resourceName;

            Outline outline = popupObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            activePopups.Add(rect);
            StartCoroutine(AnimatePopup(rect, group));
        }

        private IEnumerator AnimatePopup(RectTransform popup, CanvasGroup group)
        {
            Vector2 start = popup.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < lifetime)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / lifetime);
                popup.anchoredPosition = start + Vector2.up * (verticalTravel * progress);
                group.alpha = 1f - progress * progress;
                yield return null;
            }

            activePopups.Remove(popup);
            if (popup != null)
            {
                Destroy(popup.gameObject);
            }
        }

        private static string FormatAmount(float amount)
        {
            float rounded = Mathf.Round(amount);
            return Mathf.Abs(amount - rounded) < 0.05f
                ? rounded.ToString("0")
                : amount.ToString("0.0");
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
