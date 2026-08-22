using UnityEngine;

namespace Cave.Combat
{
    public sealed class AreaPulseEffect : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Material pulseMaterial;
        private Color color;
        private float duration;
        private float startedAt;

        public static void Create(Vector2 position, float radius, Color pulseColor, float lifetime = 0.2f)
        {
            GameObject pulseObject = new GameObject("Combat Area Pulse");
            pulseObject.transform.position = position;
            AreaPulseEffect pulse = pulseObject.AddComponent<AreaPulseEffect>();
            pulse.Initialize(radius, pulseColor, lifetime);
        }

        private void Initialize(float radius, Color pulseColor, float lifetime)
        {
            color = pulseColor;
            duration = Mathf.Max(0.01f, lifetime);
            startedAt = Time.time;
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            pulseMaterial = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.material = pulseMaterial;
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = 40;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.sortingOrder = 8;
            for (int index = 0; index < lineRenderer.positionCount; index++)
            {
                float angle = index / (float)lineRenderer.positionCount * Mathf.PI * 2f;
                lineRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            transform.localScale = Vector3.one * 0.2f;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, progress);
            Color faded = color;
            faded.a *= 1f - progress;
            lineRenderer.startColor = faded;
            lineRenderer.endColor = faded;
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (pulseMaterial != null)
            {
                Destroy(pulseMaterial);
            }
        }
    }
}
