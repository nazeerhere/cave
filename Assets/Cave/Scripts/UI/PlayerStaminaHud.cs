using Cave.Combat;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerStaminaHud : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;

        private SpinSwordAttack spinSwordAttack;
        private PlayerCurseController curses;
        private CanvasGroup canvasGroup;

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
            if (curses == null)
            {
                curses = FindObjectOfType<PlayerCurseController>();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = curses != null && curses.IsBurnoutActive ? 0f : 1f;
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
            if (fillImage != null)
            {
                float fillAmount = maximumStamina > 0f
                    ? Mathf.Clamp01(currentStamina / maximumStamina)
                    : 0f;
                fillImage.rectTransform.localScale = new Vector3(fillAmount, 1f, 1f);
            }

            if (valueText != null)
            {
                valueText.text = Mathf.RoundToInt(currentStamina)
                    + " / "
                    + Mathf.RoundToInt(maximumStamina);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
