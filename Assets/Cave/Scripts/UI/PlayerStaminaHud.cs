using Cave.Combat;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerStaminaHud : MonoBehaviour
    {
        private const float VisualRefreshInterval = 0.1f;

        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;

        private SpinSwordAttack spinSwordAttack;
        private PlayerCurseController curses;
        private CanvasGroup canvasGroup;
        private float displayedAlpha = -1f;
        private float nextCurseSearchTime;
        private float displayedFill = -1f;
        private int displayedCurrent = int.MinValue;
        private int displayedMaximum = int.MinValue;
        private float nextVisualRefreshTime;

        public void Configure(Image staminaFillImage)
        {
            Configure(staminaFillImage, null);
        }

        public void Configure(Image staminaFillImage, Text staminaValueText)
        {
            fillImage = staminaFillImage;
            if (staminaValueText != null)
            {
                valueText = staminaValueText;
            }
        }

        public void UseValueTextIfMissing(Text staminaValueText)
        {
            if (valueText == null)
            {
                valueText = staminaValueText;
            }
        }

        public void Bind(SpinSwordAttack attack)
        {
            if (spinSwordAttack != attack)
            {
                Unsubscribe();
                spinSwordAttack = attack;
                Subscribe();
            }

            if (spinSwordAttack != null)
            {
                UpdateStamina(spinSwordAttack.CurrentStamina, spinSwordAttack.MaximumStamina);
            }
        }

        private void Start()
        {
            if (spinSwordAttack == null)
            {
                Bind(FindObjectOfType<SpinSwordAttack>());
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
            if (spinSwordAttack != null)
            {
                spinSwordAttack.StaminaChanged += UpdateStamina;
            }
        }

        private void Unsubscribe()
        {
            if (spinSwordAttack != null)
            {
                spinSwordAttack.StaminaChanged -= UpdateStamina;
            }
        }

        private void UpdateStamina(float currentStamina, float maximumStamina)
        {
            if (Time.unscaledTime < nextVisualRefreshTime
                && displayedCurrent != int.MinValue)
            {
                return;
            }

            nextVisualRefreshTime = Time.unscaledTime + VisualRefreshInterval;
            if (fillImage != null)
            {
                float fillAmount = maximumStamina > 0f
                    ? Mathf.Clamp01(currentStamina / maximumStamina)
                    : 0f;
                if (!Mathf.Approximately(displayedFill, fillAmount))
                {
                    displayedFill = fillAmount;
                    fillImage.rectTransform.localScale = new Vector3(fillAmount, 1f, 1f);
                }
            }

            if (valueText != null)
            {
                int current = Mathf.RoundToInt(currentStamina);
                int maximum = Mathf.RoundToInt(maximumStamina);
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
