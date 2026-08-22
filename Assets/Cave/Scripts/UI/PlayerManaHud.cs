using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerManaHud : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;

        private PlayerMana playerMana;

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
            float fillAmount = maximumMana > 0f ? Mathf.Clamp01(currentMana / maximumMana) : 0f;
            if (fillImage != null)
            {
                fillImage.rectTransform.localScale = new Vector3(fillAmount, 1f, 1f);
            }

            if (valueText != null)
            {
                valueText.text = Mathf.RoundToInt(currentMana) + " / " + Mathf.RoundToInt(maximumMana);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
