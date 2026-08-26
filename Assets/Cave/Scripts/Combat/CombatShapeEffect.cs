using UnityEngine;

namespace Cave.Combat
{
    public enum CombatShape
    {
        Ring,
        Hexagon,
        Diamond,
        Arrow,
        Chevron,
        Wedge,
        Line,
        Slash,
        Arc
    }

    /// <summary>
    /// Small procedural combat cue used where a full circular area indicator would
    /// communicate the wrong thing. It owns and cleans up all of its runtime data.
    /// </summary>
    public sealed class CombatShapeEffect : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Material effectMaterial;
        private Color color;
        private float duration;
        private float startedAt;

        public static void Create(
            Vector2 position,
            CombatShape shape,
            float size,
            Color effectColor,
            float lifetime = 0.2f,
            float rotationDegrees = 0f,
            float lineWidth = 0.09f)
        {
            GameObject effectObject = new GameObject("Combat " + shape + " Cue");
            effectObject.transform.position = position;
            effectObject.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            CombatShapeEffect effect = effectObject.AddComponent<CombatShapeEffect>();
            effect.Initialize(shape, size, effectColor, lifetime, lineWidth);
        }

        private void Initialize(
            CombatShape shape,
            float size,
            Color effectColor,
            float lifetime,
            float lineWidth)
        {
            color = effectColor;
            duration = Mathf.Max(0.01f, lifetime);
            startedAt = Time.time;
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            effectMaterial = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.material = effectMaterial;
            lineRenderer.useWorldSpace = false;
            lineRenderer.startWidth = Mathf.Max(0.01f, lineWidth);
            lineRenderer.endWidth = Mathf.Max(0.01f, lineWidth);
            lineRenderer.sortingOrder = 8;

            Vector3[] points = BuildPoints(shape, Mathf.Max(0.05f, size), out bool loop);
            lineRenderer.loop = loop;
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
            transform.localScale = Vector3.one * 0.72f;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, progress);
            Color faded = color;
            faded.a *= 1f - progress;
            lineRenderer.startColor = faded;
            lineRenderer.endColor = faded;
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private static Vector3[] BuildPoints(CombatShape shape, float size, out bool loop)
        {
            loop = false;
            switch (shape)
            {
                case CombatShape.Ring:
                    loop = true;
                    return BuildRegularPolygon(28, size);
                case CombatShape.Hexagon:
                    loop = true;
                    return BuildRegularPolygon(6, size);
                case CombatShape.Diamond:
                    loop = true;
                    return new[]
                    {
                        new Vector3(0f, size),
                        new Vector3(size, 0f),
                        new Vector3(0f, -size),
                        new Vector3(-size, 0f)
                    };
                case CombatShape.Arrow:
                    return new[]
                    {
                        new Vector3(-size, 0f),
                        new Vector3(size * 0.55f, 0f),
                        new Vector3(size * 0.15f, size * 0.45f),
                        new Vector3(size, 0f),
                        new Vector3(size * 0.15f, -size * 0.45f)
                    };
                case CombatShape.Chevron:
                    return new[]
                    {
                        new Vector3(-size * 0.7f, size * 0.65f),
                        new Vector3(size * 0.45f, 0f),
                        new Vector3(-size * 0.7f, -size * 0.65f)
                    };
                case CombatShape.Wedge:
                    return new[]
                    {
                        Vector3.zero,
                        new Vector3(size, size * 0.55f),
                        new Vector3(size * 0.72f, 0f),
                        new Vector3(size, -size * 0.55f),
                        Vector3.zero
                    };
                case CombatShape.Line:
                    return new[] { new Vector3(-size, 0f), new Vector3(size, 0f) };
                case CombatShape.Slash:
                    return new[]
                    {
                        new Vector3(-size * 0.72f, -size),
                        new Vector3(size * 0.72f, size)
                    };
                default:
                    return BuildArc(size, shape == CombatShape.Arc ? 120f : 90f);
            }
        }

        private static Vector3[] BuildRegularPolygon(int sides, float radius)
        {
            Vector3[] points = new Vector3[sides];
            for (int index = 0; index < sides; index++)
            {
                float angle = index / (float)sides * Mathf.PI * 2f;
                points[index] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return points;
        }

        private static Vector3[] BuildArc(float radius, float degrees)
        {
            const int segments = 14;
            Vector3[] points = new Vector3[segments];
            float halfRadians = degrees * Mathf.Deg2Rad * 0.5f;
            for (int index = 0; index < segments; index++)
            {
                float progress = index / (segments - 1f);
                float angle = Mathf.Lerp(-halfRadians, halfRadians, progress);
                points[index] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return points;
        }

        private void OnDestroy()
        {
            if (effectMaterial != null)
            {
                Destroy(effectMaterial);
            }
        }
    }
}
