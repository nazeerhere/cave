using Cave.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerStaminaHud : MonoBehaviour
    {
        [SerializeField] private Image fillImage;

        private SpinSwordAttack spinSwordAttack;

        public void Configure(Image staminaFillImage)
        {
            fillImage = staminaFillImage;
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
                fillImage.fillAmount = maximumStamina > 0f
                    ? Mathf.Clamp01(currentStamina / maximumStamina)
                    : 0f;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
