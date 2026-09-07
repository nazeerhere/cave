using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerManaHud : MonoBehaviour
    {
        private const float VisualRefreshInterval = 0.1f;

        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;

        private PlayerMana playerMana;
        private PlayerCurseController curses;
        private CanvasGroup canvasGroup;
        private float displayedAlpha = -1f;
        private float nextCurseSearchTime;
        private float displayedFill = -1f;
        private int displayedCurrent = int.MinValue;
        private int displayedMaximum = int.MinValue;
        private float nextVisualRefreshTime;

        public void Configure(Image manaFillImage, Text manaValueText)
        {
            fillImage = manaFillImage;
            valueText = manaValueText;
        }

        public void Bind(PlayerMana mana)
        {
            if (playerMana != mana)
            {
                Unsubscribe();
                playerMana = mana;
                Subscribe();
            }

            if (playerMana != null)
            {
                UpdateMana(playerMana.CurrentMana, playerMana.MaximumMana);
            }
        }

        private void Start()
        {
            if (playerMana == null)
            {
                Bind(FindObjectOfType<PlayerMana>());
            }

            curses = FindObjectOfType<PlayerCurseController>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            if (curses == null && Time.unscaledTime >= nextCurseSearchTime)
            {
                nextCurseSearchTime = Time.unscaledTime + 1f;
                curses = FindObjectOfType<PlayerCurseController>();
            }

            if (canvasGroup != null)
            {
                float nextAlpha = curses != null && curses.IsBurnoutActive ? 0f : 1f;
                if (!Mathf.Approximately(displayedAlpha, nextAlpha))
                {
                    displayedAlpha = nextAlpha;
                    canvasGroup.alpha = nextAlpha;
                }
            }
        }

        private void Subscribe()
        {
            if (playerMana != null)
            {
                playerMana.ManaChanged += UpdateMana;
            }
        }

        private void Unsubscribe()
        {
            if (playerMana != null)
            {
                playerMana.ManaChanged -= UpdateMana;
            }
        }

        private void UpdateMana(float currentMana, float maximumMana)
        {
            if (Time.unscaledTime < nextVisualRefreshTime
                && displayedCurrent != int.MinValue)
            {
                return;
            }

            nextVisualRefreshTime = Time.unscaledTime + VisualRefreshInterval;
            float fillAmount = maximumMana > 0f ? Mathf.Clamp01(currentMana / maximumMana) : 0f;
            if (fillImage != null)
            {
                if (!Mathf.Approximately(displayedFill, fillAmount))
                {
                    displayedFill = fillAmount;
                    fillImage.rectTransform.localScale = new Vector3(fillAmount, 1f, 1f);
                }
            }

            if (valueText != null)
            {
                int current = Mathf.RoundToInt(currentMana);
                int maximum = Mathf.RoundToInt(maximumMana);
                if (displayedCurrent != current || displayedMaximum != maximum)
                {
                    displayedCurrent = current;
                    displayedMaximum = maximum;
                    valueText.text = current + " / " + maximum;
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
