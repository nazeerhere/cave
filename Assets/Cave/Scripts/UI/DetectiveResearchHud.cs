using Cave.Enemies;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class DetectiveResearchHud : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float fadeSpeed = 8f;
        [SerializeField, Min(0.05f)] private float outcomePulseDuration = 0.5f;
        [SerializeField] private GameObject successfulResearchVfxPrefab;
        [SerializeField] private GameObject corruptedResearchVfxPrefab;

        private CanvasGroup canvasGroup;
        private Text studyingText;
        private Text[] historyTexts;
        private Image[] historyFrames;
        private Text predictedText;
        private Image predictedFrame;
        private Text outcomeText;
        private DetectiveEncounterCoordinator coordinator;
        private float targetAlpha;
        private Image outcomePulse;
        private DetectiveResearchOutcome displayedOutcome;
        private float pulseEndsAt;
        private Color pulseColor;

        public void Configure(
            CanvasGroup group,
            Text observationLabel,
            Text[] recordedHistory,
            Image[] recordedFrames,
            Text predicted,
            Image predictionFrame,
            Text outcome)
        {
            canvasGroup = group;
            studyingText = observationLabel;
            historyTexts = recordedHistory;
            historyFrames = recordedFrames;
            predictedText = predicted;
            predictedFrame = predictionFrame;
            outcomeText = outcome;
            targetAlpha = 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }


            EnsureOutcomePulse();

            Refresh();
        }

        private void Update()
        {
            EnsureCoordinator();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(
                    canvasGroup.alpha,
                    targetAlpha,
                    fadeSpeed * Time.unscaledDeltaTime);
            }


            UpdateOutcomePulse();
        }

        private void EnsureCoordinator()
        {
            if (coordinator != null)
            {
                return;
            }

            coordinator = FindObjectOfType<DetectiveEncounterCoordinator>();
            if (coordinator == null)
            {
                return;
            }

            coordinator.ResearchDisplayChanged -= Refresh;
            coordinator.ResearchDisplayChanged += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            bool visible = coordinator != null && coordinator.ResearchHudVisible;
            targetAlpha = visible ? 1f : 0f;
            if (coordinator == null)
            {
                return;
            }

            if (studyingText != null)
            {
                studyingText.text = coordinator.ResearchPhase == DetectiveResearchPhase.Predicting
                    ? "DETECTIVE STUDY  •  PREDICTED NEXT"
                    : coordinator.ResearchPhase == DetectiveResearchPhase.Outcome
                        ? "DETECTIVE STUDY  •  RESULT"
                        : "DETECTIVE STUDY  •  OBSERVING";
            }

            for (int index = 0; historyTexts != null && index < historyTexts.Length; index++)
            {
                bool populated = index < coordinator.CurrentObservedPattern.Count;
                PlayerActionCategory category = populated
                    ? coordinator.CurrentObservedPattern[index]
                    : default;
                historyTexts[index].text = populated ? FormatCategory(category) : "·";
                historyTexts[index].color = populated
                    ? ResolveCategoryColor(category)
                    : CaveUiTheme.SecondaryText;
                if (historyFrames != null
                    && index < historyFrames.Length
                    && historyFrames[index] != null)
                {
                    historyFrames[index].color = populated
                        ? new Color(0.08f, 0.12f, 0.17f, 0.98f)
                        : new Color(0.025f, 0.03f, 0.04f, 0.7f);
                }
            }

            bool predictionVisible = coordinator.ResearchPhase == DetectiveResearchPhase.Predicting
                || (coordinator.ResearchPhase == DetectiveResearchPhase.Outcome
                    && coordinator.LastResearchOutcome != DetectiveResearchOutcome.Expired);
            if (predictedText != null)
            {
                predictedText.text = predictionVisible
                    ? "NEXT\n" + FormatCategory(coordinator.PredictedNextAction)
                    : "NEXT\n?";
                predictedText.color = predictionVisible
                    ? CaveUiTheme.Gold
                    : CaveUiTheme.SecondaryText;
            }

            if (predictedFrame != null)
            {
                predictedFrame.color = predictionVisible
                    ? new Color(0.16f, 0.11f, 0.035f, 0.98f)
                    : new Color(0.025f, 0.03f, 0.04f, 0.7f);
            }

            if (outcomeText != null)
            {
                switch (coordinator.LastResearchOutcome)
                {
                    case DetectiveResearchOutcome.Success:
                        outcomeText.text = "PREDICTION CONFIRMED";
                        outcomeText.color = new Color(0.35f, 0.95f, 1f, 1f);
                        break;
                    case DetectiveResearchOutcome.Corrupted:
                        outcomeText.text = "FALSE DATA  •  NETWORK CORRUPTED";
                        outcomeText.color = new Color(0.8f, 0.35f, 1f, 1f);
                        break;
                    case DetectiveResearchOutcome.Expired:
                        outcomeText.text = "INCONCLUSIVE  •  STUDY LOST";
                        outcomeText.color = new Color(0.82f, 0.69f, 0.46f, 1f);
                        break;
                    default:
                        outcomeText.text = string.Empty;
                        break;
                }
            }


            if (coordinator.LastResearchOutcome != DetectiveResearchOutcome.None
                && coordinator.LastResearchOutcome != displayedOutcome)
            {
                displayedOutcome = coordinator.LastResearchOutcome;
                BeginOutcomePulse(displayedOutcome);
            }
            else if (coordinator.LastResearchOutcome == DetectiveResearchOutcome.None)
            {
                displayedOutcome = DetectiveResearchOutcome.None;
            }
        }

        private void EnsureOutcomePulse()
        {
            if (outcomePulse != null)
            {
                return;
            }

            GameObject pulseObject = new GameObject("Research Outcome Pulse", typeof(RectTransform));
            pulseObject.transform.SetParent(transform, false);
            RectTransform rect = pulseObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            outcomePulse = pulseObject.AddComponent<Image>();
            outcomePulse.raycastTarget = false;
            outcomePulse.color = Color.clear;
            pulseObject.transform.SetAsFirstSibling();
        }

        private void BeginOutcomePulse(DetectiveResearchOutcome outcome)
        {
            GameObject prefab;
            switch (outcome)
            {
                case DetectiveResearchOutcome.Success:
                    pulseColor = new Color(0.25f, 0.92f, 1f, 0.65f);
                    prefab = successfulResearchVfxPrefab;
                    break;
                case DetectiveResearchOutcome.Corrupted:
                    pulseColor = new Color(0.72f, 0.2f, 1f, 0.7f);
                    prefab = corruptedResearchVfxPrefab;
                    break;
                default:
                    pulseColor = new Color(0.72f, 0.58f, 0.32f, 0.45f);
                    prefab = null;
                    break;
            }

            pulseEndsAt = Time.unscaledTime + outcomePulseDuration;
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, transform.position, Quaternion.identity);
                Destroy(instance, Mathf.Max(0.5f, outcomePulseDuration));
            }
        }

        private void UpdateOutcomePulse()
        {
            if (outcomePulse == null)
            {
                return;
            }

            float remaining = pulseEndsAt - Time.unscaledTime;
            if (remaining <= 0f)
            {
                outcomePulse.color = Color.clear;
                return;
            }

            float normalized = Mathf.Clamp01(remaining / outcomePulseDuration);
            float flicker = displayedOutcome == DetectiveResearchOutcome.Corrupted
                ? (Mathf.Sin(Time.unscaledTime * 55f) > 0f ? 1f : 0.35f)
                : displayedOutcome == DetectiveResearchOutcome.Expired
                    ? Mathf.Lerp(0.55f, 1f, Mathf.Sin(Time.unscaledTime * 18f) * 0.5f + 0.5f)
                    : 1f;
            outcomePulse.color = new Color(
                pulseColor.r,
                pulseColor.g,
                pulseColor.b,
                pulseColor.a * normalized * flicker);
        }

        public static string FormatCategory(PlayerActionCategory category)
        {
            switch (category)
            {
                case PlayerActionCategory.Attack:
                    return "ATK";
                case PlayerActionCategory.Defend:
                    return "DEF";
                case PlayerActionCategory.Evade:
                    return "EVA";
                case PlayerActionCategory.Projectile:
                    return "SHOT";
                case PlayerActionCategory.Reposition:
                    return "MOVE";
                case PlayerActionCategory.Special:
                    return "SPEC";
                default:
                    return "?";
            }
        }

        private static Color ResolveCategoryColor(PlayerActionCategory category)
        {
            switch (category)
            {
                case PlayerActionCategory.Attack:
                    return new Color(1f, 0.38f, 0.28f, 1f);
                case PlayerActionCategory.Defend:
                    return new Color(0.3f, 0.82f, 1f, 1f);
                case PlayerActionCategory.Evade:
                    return new Color(0.35f, 1f, 0.72f, 1f);
                case PlayerActionCategory.Projectile:
                    return new Color(0.75f, 0.5f, 1f, 1f);
                case PlayerActionCategory.Reposition:
                    return new Color(0.94f, 0.86f, 0.58f, 1f);
                case PlayerActionCategory.Special:
                    return CaveUiTheme.Gold;
                default:
                    return CaveUiTheme.PrimaryText;
            }
        }

        private void OnDestroy()
        {
            if (coordinator != null)
            {
                coordinator.ResearchDisplayChanged -= Refresh;
            }
        }
    }
}
