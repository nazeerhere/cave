using Cave.Combat;
using Cave.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyLocationBarHud : MonoBehaviour
    {
        private const int RegistryCapacity = 128;
        private const float RefreshInterval = 0.1f;

        private readonly Damageable[] registryBuffer = new Damageable[RegistryCapacity];
        private readonly Damageable[] selectedThreats = new Damageable[8];
        private readonly float[] selectedDistances = new float[8];
        private RectTransform track;
        private Image trackImage;
        private CanvasGroup canvasGroup;
        private Text[] markers;
        private Transform player;
        private float horizontalRange;
        private float nextRefreshAt;

        public void Configure(
            RectTransform configuredTrack,
            Image configuredTrackImage,
            CanvasGroup configuredCanvasGroup,
            Text[] configuredMarkers,
            Transform playerTransform,
            float visibleHorizontalRange)
        {
            track = configuredTrack;
            trackImage = configuredTrackImage;
            canvasGroup = configuredCanvasGroup;
            markers = configuredMarkers;
            player = playerTransform;
            horizontalRange = Mathf.Max(1f, visibleHorizontalRange);
            RefreshNow();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.unscaledTime + RefreshInterval;
            RefreshNow();
        }

        private void OnEnable()
        {
            nextRefreshAt = 0f;
        }

        private void OnDisable()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void RefreshNow()
        {
            if (track == null || trackImage == null || markers == null || player == null)
            {
                return;
            }

            int selectedCount = SelectRelevantThreats();
            trackImage.color = new Color(
                CaveUiTheme.Surface.r,
                CaveUiTheme.Surface.g,
                CaveUiTheme.Surface.b,
                selectedCount > 0 ? 0.82f : 0f);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = selectedCount > 0 ? 1f : 0f;
            }

            float usableWidth = Mathf.Max(1f, track.rect.width - 22f);
            for (int index = 0; index < markers.Length; index++)
            {
                Text marker = markers[index];
                bool active = index < selectedCount;
                marker.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float delta = selectedThreats[index].transform.position.x - player.position.x;
                bool offscreen = Mathf.Abs(delta) > horizontalRange;
                float normalized = Mathf.Clamp(delta / horizontalRange, -1f, 1f);
                marker.rectTransform.anchoredPosition = new Vector2(normalized * usableWidth * 0.5f, 0f);
                marker.text = offscreen ? delta < 0f ? "◀" : "▶" : "◆";
                marker.color = offscreen ? CaveUiTheme.Gold : CaveUiTheme.BorderBright;
            }
        }

        private int SelectRelevantThreats()
        {
            int activeCount = EnemyThreatRegistry.CopyActive(registryBuffer);
            int selectedCount = 0;
            for (int index = 0; index < activeCount; index++)
            {
                Damageable candidate = registryBuffer[index];
                registryBuffer[index] = null;
                if (!HostileMobQuery.IsRealHostile(candidate))
                {
                    continue;
                }

                float distance = Mathf.Abs(candidate.transform.position.x - player.position.x);
                int insertion = selectedCount;
                while (insertion > 0 && selectedDistances[insertion - 1] > distance)
                {
                    if (insertion < selectedThreats.Length)
                    {
                        selectedThreats[insertion] = selectedThreats[insertion - 1];
                        selectedDistances[insertion] = selectedDistances[insertion - 1];
                    }
                    insertion--;
                }

                if (insertion < selectedThreats.Length)
                {
                    selectedThreats[insertion] = candidate;
                    selectedDistances[insertion] = distance;
                    selectedCount = Mathf.Min(selectedCount + 1, selectedThreats.Length);
                }
            }

            for (int index = selectedCount; index < selectedThreats.Length; index++)
            {
                selectedThreats[index] = null;
                selectedDistances[index] = 0f;
            }

            return Mathf.Min(selectedCount, markers.Length);
        }
    }
}
