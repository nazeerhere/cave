using System.Collections;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [Header("Fixed Ratio Display")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;

        [Header("Damage Feedback")]
        [SerializeField, Min(0.01f)] private float feedbackDuration = 0.15f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.3f, 0.2f, 0.9f);

        private PlayerHealth playerHealth;
        private Image background;
        private Color normalBackgroundColor;
        private Coroutine feedbackRoutine;

        public void Configure(Image healthFillImage, Text healthValueText, Image panelBackground)
        {
            fillImage = healthFillImage;
            valueText = healthValueText;
            background = panelBackground;
            if (background != null)
            {
                normalBackgroundColor = background.color;
            }
        }

        public void Configure(RectTransform healthBarContainer, Image panelBackground)
        {
            Configure(
                healthBarContainer != null ? healthBarContainer.GetComponentInChildren<Image>() : null,
                null,
                panelBackground);
        }

        public void Bind(PlayerHealth health)
        {
            if (playerHealth != health)
            {
                Unsubscribe();
                playerHealth = health;
                Subscribe();
            }

            if (playerHealth != null)
            {
                UpdateHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
        }

        private void Start()
        {
            if (playerHealth == null)
            {
                Bind(FindObjectOfType<PlayerHealth>());
            }
        }

        private void Subscribe()
        {
            if (playerHealth == null)
            {
                return;
            }

            playerHealth.HealthChanged += UpdateHealth;
            playerHealth.DamageTaken += PlayDamageFeedback;
        }

        private void Unsubscribe()
        {
            if (playerHealth == null)
            {
                return;
            }

            playerHealth.HealthChanged -= UpdateHealth;
            playerHealth.DamageTaken -= PlayDamageFeedback;
        }

        private void UpdateHealth(int currentHealth, int maximumHealth)
        {
            if (fillImage != null)
            {
                float fill = maximumHealth > 0
                    ? Mathf.Clamp01(currentHealth / (float)maximumHealth)
                    : 0f;
                fillImage.rectTransform.localScale = new Vector3(fill, 1f, 1f);
            }

            if (valueText != null)
            {
                valueText.text = currentHealth + " / " + maximumHealth;
            }
        }

        private void PlayDamageFeedback()
        {
            if (background == null)
            {
                return;
            }

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(DamageFeedback());
        }

        private IEnumerator DamageFeedback()
        {
            background.color = damageFlashColor;
            yield return new WaitForSecondsRealtime(feedbackDuration);
            background.color = normalBackgroundColor;
            feedbackRoutine = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
