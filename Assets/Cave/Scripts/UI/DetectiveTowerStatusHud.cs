using Cave.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class DetectiveTowerStatusHud : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.12f;
        [SerializeField, Min(0f)] private float highAlertPulseSpeed = 3f;
        [SerializeField, Range(0f, 0.35f)] private float highAlertPulseAmount = 0.12f;

        private CanvasGroup canvasGroup;
        private Image panelImage;
        private Text titleText;
        private Text recoveryText;
        private Text radiusText;
        private Text threatText;
        private DetectiveEncounterCoordinator coordinator;
        private float nextRefreshTime;
        private Color stageColor = CaveUiTheme.BronzeLight;
        private int visibleStage;

        public void Configure(
            CanvasGroup group,
            Image background,
            Text title,
            Text recovery,
            Text radius,
            Text threat)
        {
            canvasGroup = group;
            panelImage = background;
            titleText = title;
            recoveryText = recovery;
            radiusText = radius;
            threatText = threat;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            BindCoordinator(FindObjectOfType<DetectiveEncounterCoordinator>());
            RefreshDisplay();
        }

        private void Update()
        {
            if (coordinator == null && Time.unscaledTime >= nextRefreshTime)
            {
                BindCoordinator(FindObjectOfType<DetectiveEncounterCoordinator>());
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + refreshInterval;
                RefreshDisplay();
            }

            AnimateStageFrame();
        }

        private void BindCoordinator(DetectiveEncounterCoordinator value)
        {
            if (coordinator == value)
            {
                return;
            }

            if (coordinator != null)
            {
                coordinator.TowerStatusChanged -= RefreshDisplay;
            }

            coordinator = value;
            if (coordinator != null)
            {
                coordinator.TowerStatusChanged -= RefreshDisplay;
                coordinator.TowerStatusChanged += RefreshDisplay;
            }
        }

        private void RefreshDisplay()
        {
            DetectiveTower tower = coordinator != null ? coordinator.ActiveTower : null;
            bool visible = tower != null && tower.gameObject.activeInHierarchy;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
            }

            if (!visible)
            {
                visibleStage = 0;
                return;
            }

            visibleStage = Mathf.Max(0, tower.ResearchStage);
            stageColor = ResolveStageColor(visibleStage);
            string stageMarker = FormatStage(visibleStage);
            if (titleText != null)
            {
                titleText.text = "TOWER  •  STAGE " + stageMarker;
                titleText.color = stageColor;
            }

            DetectiveTowerDebuffStage debuff = tower.CurrentDebuffStage;
            if (recoveryText != null)
            {
                recoveryText.text = string.Format(
                    "HP REGEN  {0:0}%  •  STA REGEN  {1:0}%",
                    debuff.HealthRegenerationMultiplier * 100f,
                    debuff.StaminaRegenerationMultiplier * 100f);
            }

            if (radiusText != null)
            {
                radiusText.text = string.Format(
                    "MANA COST  +{0:0}%  •  DROPS  {1:0}%",
                    (tower.CurrentManaCostMultiplier - 1f) * 100f,
                    tower.CurrentDropRateMultiplier * 100f);
            }

            if (threatText != null)
            {
                threatText.text = string.Format(
                    "FIELD {0:0.0}/{1:0.0}  {2}  {3}",
                    tower.CurrentRadius,
                    tower.MaximumRadius,
                    tower.IsAtMaximumRadius ? "MAX" : "EXPANDING",
                    BuildThreatMarks(visibleStage));
                threatText.color = stageColor;
            }
        }

        private void AnimateStageFrame()
        {
            if (panelImage == null || canvasGroup == null || canvasGroup.alpha <= 0f)
            {
                return;
            }

            float pulse = visibleStage >= 3
                ? (Mathf.Sin(Time.unscaledTime * highAlertPulseSpeed) * 0.5f + 0.5f)
                    * highAlertPulseAmount
                : 0f;
            panelImage.color = Color.Lerp(CaveUiTheme.Surface, stageColor, 0.07f + pulse);
        }

        private static Color ResolveStageColor(int stage)
        {
            switch (stage)
            {
                case 1:
                    return CaveUiTheme.BorderBright;
                case 2:
                    return CaveUiTheme.Gold;
                case 3:
                    return new Color(1f, 0.34f, 0.2f, 1f);
                default:
                    return CaveUiTheme.BronzeLight;
            }
        }

        private static string FormatStage(int stage)
        {
            switch (stage)
            {
                case 1:
                    return "I";
                case 2:
                    return "II";
                case 3:
                    return "III";
                default:
                    return stage <= 0 ? "0" : stage.ToString();
            }
        }

        private static string BuildThreatMarks(int stage)
        {
            int count = Mathf.Clamp(stage + 1, 1, 4);
            return new string('◆', count);
        }

        private void OnDestroy()
        {
            if (coordinator != null)
            {
                coordinator.TowerStatusChanged -= RefreshDisplay;
            }
        }
    }
}
