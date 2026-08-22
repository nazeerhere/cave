using System.Collections;
using Cave.Audio;
using Cave.World;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class WorldLevelHud : MonoBehaviour
    {
        [SerializeField] private Text levelText;
        [SerializeField] private Text announcementText;
        [SerializeField] private CanvasGroup announcementGroup;
        [SerializeField, Min(0.1f)] private float announcementLifetime = 1.4f;

        private WorldDifficultyManager difficultyManager;
        private int displayedTier = -1;
        private Coroutine announcementRoutine;

        public void Configure(Text currentLevelText, Text levelAnnouncement, CanvasGroup levelAnnouncementGroup)
        {
            levelText = currentLevelText;
            announcementText = levelAnnouncement;
            announcementGroup = levelAnnouncementGroup;
        }

        public void Bind(WorldDifficultyManager manager)
        {
            if (difficultyManager != manager)
            {
                Unsubscribe();
                difficultyManager = manager;
                Subscribe();
            }

            Refresh(false);
        }

        private void Start()
        {
            if (difficultyManager == null)
            {
                Bind(FindObjectOfType<WorldDifficultyManager>());
            }
        }

        private void Subscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged += HandleDifficultyChanged;
            }
        }

        private void Unsubscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= HandleDifficultyChanged;
            }
        }

        private void HandleDifficultyChanged()
        {
            Refresh(true);
        }

        private void Refresh(bool allowAnnouncement)
        {
            if (difficultyManager == null)
            {
                return;
            }

            int newTier = difficultyManager.DifficultyTier;
            if (levelText != null)
            {
                levelText.text = newTier.ToString();
            }

            if (allowAnnouncement && displayedTier >= 0 && newTier > displayedTier)
            {
                ShowAnnouncement(newTier);
            }

            displayedTier = newTier;
        }

        private void ShowAnnouncement(int tier)
        {
            if (announcementText == null || announcementGroup == null)
            {
                return;
            }

            if (announcementRoutine != null)
            {
                StopCoroutine(announcementRoutine);
            }

            announcementText.text = "WORLD LEVEL  " + tier;
            CaveSfx.Play(CaveSfxCue.Bonus, 1f);
            announcementRoutine = StartCoroutine(AnimateAnnouncement());
        }

        private IEnumerator AnimateAnnouncement()
        {
            announcementGroup.alpha = 1f;
            float elapsed = 0f;
            while (elapsed < announcementLifetime)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / announcementLifetime);
                announcementGroup.alpha = progress < 0.65f
                    ? 1f
                    : 1f - (progress - 0.65f) / 0.35f;
                yield return null;
            }

            announcementGroup.alpha = 0f;
            announcementRoutine = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
