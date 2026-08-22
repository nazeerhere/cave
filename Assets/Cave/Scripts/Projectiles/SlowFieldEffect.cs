using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Projectiles
{
    public sealed class SlowFieldEffect : MonoBehaviour
    {
        private float radius;
        private float movementMultiplier;
        private float expiresAt;
        private LayerMask damageableLayers;
        private LineRenderer areaRenderer;
        private Material areaMaterial;
        private Mesh areaMesh;
        private Material fillMaterial;
        private Color outlineColor;
        private Color fillColor;
        private float createdAt;
        private float nextApplicationTime;

        public static void Create(
            Vector2 position,
            float fieldRadius,
            float duration,
            float slowMultiplier,
            LayerMask affectedLayers,
            Color fieldOutlineColor,
            Color fieldFillColor)
        {
            GameObject fieldObject = new GameObject("Slow Tier 3 Field");
            fieldObject.transform.position = position;
            SlowFieldEffect field = fieldObject.AddComponent<SlowFieldEffect>();
            field.Initialize(
                fieldRadius,
                duration,
                slowMultiplier,
                affectedLayers,
                fieldOutlineColor,
                fieldFillColor);
        }

        private void Initialize(
            float fieldRadius,
            float duration,
            float slowMultiplier,
            LayerMask affectedLayers,
            Color fieldOutlineColor,
            Color fieldFillColor)
        {
            radius = Mathf.Max(0.1f, fieldRadius);
            movementMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
            expiresAt = Time.time + Mathf.Max(0.1f, duration);
            createdAt = Time.time;
            damageableLayers = affectedLayers;
            outlineColor = fieldOutlineColor;
            fillColor = fieldFillColor;
            CreateVisual();
            ApplySlow();
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            float pulse = 0.72f + Mathf.Sin((Time.time - createdAt) * 5f) * 0.18f;
            Color pulsedOutline = outlineColor;
            pulsedOutline.a *= pulse;
            areaRenderer.startColor = pulsedOutline;
            areaRenderer.endColor = pulsedOutline;

            Color pulsedFill = fillColor;
            pulsedFill.a *= 0.82f + Mathf.Sin((Time.time - createdAt) * 3f) * 0.18f;
            fillMaterial.color = pulsedFill;

            if (Time.time >= nextApplicationTime)
            {
                ApplySlow();
            }
        }

        private void ApplySlow()
        {
            nextApplicationTime = Time.time + 0.2f;
            float remainingDuration = Mathf.Max(0.2f, expiresAt - Time.time);
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, radius, damageableLayers);
            HashSet<Damageable> affected = new HashSet<Damageable>();
            foreach (Collider2D overlap in overlaps)
            {
                Damageable damageable = overlap.GetComponentInParent<Damageable>();
                if (damageable == null || !affected.Add(damageable))
                {
                    continue;
                }

                EnemyStatusEffects statusEffects = damageable.GetComponent<EnemyStatusEffects>();
                statusEffects?.ApplySlow(movementMultiplier, remainingDuration);
            }
        }

        private void CreateVisual()
        {
            areaRenderer = gameObject.AddComponent<LineRenderer>();
            areaMaterial = new Material(Shader.Find("Sprites/Default"));
            areaRenderer.material = areaMaterial;
            areaRenderer.useWorldSpace = false;
            areaRenderer.loop = true;
            areaRenderer.positionCount = 48;
            areaRenderer.startWidth = 0.08f;
            areaRenderer.endWidth = 0.08f;
            areaRenderer.startColor = outlineColor;
            areaRenderer.endColor = outlineColor;
            areaRenderer.sortingOrder = 4;
            for (int index = 0; index < areaRenderer.positionCount; index++)
            {
                float angle = index / (float)areaRenderer.positionCount * Mathf.PI * 2f;
                areaRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            CreateFillVisual();
        }

        private void CreateFillVisual()
        {
            const int segmentCount = 48;
            Vector3[] vertices = new Vector3[segmentCount + 1];
            int[] triangles = new int[segmentCount * 3];
            vertices[0] = Vector3.zero;
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                int triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = index + 1;
                triangles[triangleIndex + 2] = (index + 1) % segmentCount + 1;
            }

            areaMesh = new Mesh { name = "Frost Field Runtime Mesh" };
            areaMesh.vertices = vertices;
            areaMesh.triangles = triangles;
            areaMesh.RecalculateBounds();

            GameObject fillObject = new GameObject("Frost Field Overlay");
            fillObject.transform.SetParent(transform, false);
            MeshFilter meshFilter = fillObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = areaMesh;
            MeshRenderer meshRenderer = fillObject.AddComponent<MeshRenderer>();
            fillMaterial = new Material(Shader.Find("Sprites/Default"));
            fillMaterial.color = fillColor;
            meshRenderer.material = fillMaterial;
            meshRenderer.sortingOrder = 3;
        }

        private void OnDestroy()
        {
            if (areaMaterial != null)
            {
                Destroy(areaMaterial);
            }

            if (fillMaterial != null)
            {
                Destroy(fillMaterial);
            }

            if (areaMesh != null)
            {
                Destroy(areaMesh);
            }
        }
    }
}
