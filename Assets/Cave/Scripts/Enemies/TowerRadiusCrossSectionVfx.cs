using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class TowerRadiusCrossSectionVfx : MonoBehaviour
    {
        private const string CatalogResourcePath = "VFX/TowerRadiusCrossSectionCatalog";
        private static TowerRadiusCrossSectionCatalog catalog;
        private static bool catalogLoadAttempted;

        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private Sprite[] growthFrames;
        [SerializeField] private float animationFramesPerSecond = 18f;
        [SerializeField, Min(0.01f)] private float visualRadiusScale = 1f;
        [SerializeField] private Vector3 visualOffset;
        [SerializeField, Min(0.01f)] private float fullFrameDiameterWorldUnits = 2f;

        private int frameIndex;
        private float nextFrameTime;
        private bool growing;
        private float appliedRadius = -1f;

        public static TowerRadiusCrossSectionVfx CreateFor(Transform towerRoot)
        {
            if (towerRoot == null) return null;
            TowerRadiusCrossSectionVfx existing = towerRoot.GetComponentInChildren<TowerRadiusCrossSectionVfx>(true);
            if (existing != null) return existing;

            if (!catalogLoadAttempted)
            {
                catalogLoadAttempted = true;
                catalog = Resources.Load<TowerRadiusCrossSectionCatalog>(CatalogResourcePath);
            }
            if (catalog == null || catalog.Prefab == null) return null;

            TowerRadiusCrossSectionVfx instance = Instantiate(catalog.Prefab, towerRoot, false);
            instance.name = "Tower Radius Cross Section VFX";
            return instance;
        }

        public void Activate(float radius)
        {
            frameIndex = 0;
            growing = growthFrames != null && growthFrames.Length > 1;
            nextFrameTime = Time.time;
            if (visualRenderer != null && growthFrames != null && growthFrames.Length > 0)
            {
                visualRenderer.sprite = growthFrames[0];
                visualRenderer.enabled = true;
            }
            SetRadius(radius);
        }

        public void SetRadius(float radius)
        {
            radius = Mathf.Max(0.01f, radius);
            if (Mathf.Abs(radius - appliedRadius) <= .001f) return;
            appliedRadius = radius;
            float scale = (2f * radius * visualRadiusScale) / Mathf.Max(.01f, fullFrameDiameterWorldUnits);
            transform.localPosition = visualOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Update()
        {
            if (!growing || growthFrames == null || frameIndex >= growthFrames.Length - 1 || Time.time < nextFrameTime) return;
            frameIndex++;
            if (visualRenderer != null) visualRenderer.sprite = growthFrames[frameIndex];
            nextFrameTime = Time.time + 1f / Mathf.Max(.01f, animationFramesPerSecond);
            if (frameIndex >= growthFrames.Length - 1) growing = false;
        }

        public void Configure(SpriteRenderer renderer, Sprite[] frames, float frameRate, float diameterWorldUnits)
        {
            visualRenderer = renderer;
            growthFrames = frames;
            animationFramesPerSecond = Mathf.Max(.01f, frameRate);
            fullFrameDiameterWorldUnits = Mathf.Max(.01f, diameterWorldUnits);
        }
    }
}
