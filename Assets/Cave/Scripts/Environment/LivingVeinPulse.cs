using UnityEngine;

namespace Cave.EnvironmentFx
{
    /// <summary>Subtle palette pulse for a fixed small set of living-cave vein renderers.</summary>
    [DisallowMultipleComponent]
    public sealed class LivingVeinPulse : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] veinRenderers;
        [SerializeField, Min(0.01f)] private float pulseSpeed = 0.22f;
        [SerializeField, Range(0f, 0.35f)] private float brightnessRange = 0.12f;

        private Color[] baseColors;
        private float elapsed;

        public void Configure(SpriteRenderer[] renderers, float speed, float range)
        {
            veinRenderers = renderers;
            pulseSpeed = speed;
            brightnessRange = range;
            CacheColors();
        }

        private void Awake()
        {
            CacheColors();
        }

        private void OnEnable()
        {
            if (baseColors == null)
            {
                CacheColors();
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float multiplier = 1f + Mathf.Sin(elapsed * pulseSpeed) * brightnessRange;
            for (int index = 0; index < veinRenderers.Length; index++)
            {
                SpriteRenderer vein = veinRenderers[index];
                if (vein == null)
                {
                    continue;
                }

                Color color = baseColors[index];
                color.r *= multiplier;
                color.g *= multiplier;
                color.b *= multiplier;
                vein.color = color;
            }
        }

        private void CacheColors()
        {
            int count = veinRenderers == null ? 0 : veinRenderers.Length;
            baseColors = new Color[count];
            for (int index = 0; index < count; index++)
            {
                if (veinRenderers[index] != null)
                {
                    baseColors[index] = veinRenderers[index].color;
                }
            }
        }
    }
}
