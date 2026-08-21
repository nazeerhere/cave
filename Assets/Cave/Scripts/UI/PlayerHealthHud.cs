using System.Collections;
using System.Collections.Generic;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [Header("Segments")]
        [SerializeField] private Color filledColor = new Color(0.9f, 0.16f, 0.12f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.12f, 0.12f, 0.9f);

        [Header("Damage Feedback")]
        [SerializeField, Min(0.01f)] private float feedbackDuration = 0.15f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.3f, 0.2f, 0.9f);

        private readonly List<Image> segments = new List<Image>();
        private PlayerHealth playerHealth;
        private RectTransform segmentContainer;
        private Image background;
        private Color normalBackgroundColor;
        private Coroutine feedbackRoutine;

        public void Configure(RectTransform healthSegmentContainer, Image panelBackground)
        {
            segmentContainer = healthSegmentContainer;
            background = panelBackground;
            normalBackgroundColor = background.color;
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
            if (segmentContainer == null)
            {
                return;
            }

            if (segments.Count != maximumHealth)
            {
                RebuildSegments(maximumHealth);
            }

            for (int index = 0; index < segments.Count; index++)
            {
                segments[index].color = index < currentHealth ? filledColor : emptyColor;
            }
        }

        private void RebuildSegments(int maximumHealth)
        {
            foreach (Image segment in segments)
            {
                if (segment != null)
                {
                    Destroy(segment.gameObject);
                }
            }

            segments.Clear();

            for (int index = 0; index < maximumHealth; index++)
            {
                GameObject segmentObject = new GameObject("Health Segment " + (index + 1));
                segmentObject.layer = gameObject.layer;
                segmentObject.transform.SetParent(segmentContainer, false);

                Image image = segmentObject.AddComponent<Image>();
                image.color = filledColor;

                LayoutElement layout = segmentObject.AddComponent<LayoutElement>();
                layout.preferredWidth = .01f;
                layout.preferredHeight = 1f;
                segments.Add(image);
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
