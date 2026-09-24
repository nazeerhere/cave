using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Projectiles
{
    public sealed class SlowFieldEffect : MonoBehaviour
    {
        private const int VisualSegments = 48;
        private static readonly List<SlowFieldEffect> ActiveFields = new List<SlowFieldEffect>();
        private static int nextCreationSequence;

        private float radius;
        private float maximumRadius;
        private float growthPerMerge;
        private float movementMultiplier;
        private float entryPinDuration;
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
        private int creationSequence;
        private bool retired;
        private readonly HashSet<Damageable> occupants = new HashSet<Damageable>();

        public float Radius => radius;
        public bool IsAuthoritative => !retired;
        public int CreationSequence => creationSequence;

        public static SlowFieldEffect Create(
            Vector2 position,
            float fieldRadius,
            float duration,
            float slowMultiplier,
            LayerMask affectedLayers,
            Color fieldOutlineColor,
            Color fieldFillColor,
            float tier2EntryPinDuration = 0f,
            float mergeGrowth = 0f,
            float maximumFieldRadius = 0f)
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
                fieldFillColor,
                tier2EntryPinDuration,
                mergeGrowth,
                maximumFieldRadius);
            return field.MergeOverlaps();
        }

        private void Initialize(
            float fieldRadius,
            float duration,
            float slowMultiplier,
            LayerMask affectedLayers,
            Color fieldOutlineColor,
            Color fieldFillColor,
            float tier2EntryPinDuration,
            float mergeGrowth,
            float maximumFieldRadius)
        {
            radius = Mathf.Max(0.1f, fieldRadius);
            maximumRadius = Mathf.Max(radius, maximumFieldRadius > 0f ? maximumFieldRadius : radius);
            growthPerMerge = Mathf.Max(0f, mergeGrowth);
            movementMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
            entryPinDuration = Mathf.Max(0f, tier2EntryPinDuration);
            expiresAt = Time.time + Mathf.Max(0.1f, duration);
            createdAt = Time.time;
            damageableLayers = affectedLayers;
            outlineColor = fieldOutlineColor;
            fillColor = fieldFillColor;
            creationSequence = nextCreationSequence++;
            ActiveFields.Add(this);
            CreateVisual();
            ApplySlow();
        }

        private void Update()
        {
            if (retired)
            {
                return;
            }

            if (Time.time >= expiresAt)
            {
                Retire();
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

        private SlowFieldEffect MergeOverlaps()
        {
            SlowFieldEffect survivor = this;
            bool merged;
            do
            {
                merged = false;
                for (int index = ActiveFields.Count - 1; index >= 0; index--)
                {
                    SlowFieldEffect candidate = ActiveFields[index];
                    if (candidate == null || candidate.retired || candidate == survivor
                        || !Overlaps(survivor, candidate))
                    {
                        continue;
                    }

                    SlowFieldEffect selected = SelectSurvivor(survivor, candidate);
                    SlowFieldEffect consumed = selected == survivor ? candidate : survivor;
                    selected.Absorb(consumed);
                    survivor = selected;
                    merged = true;
                    break;
                }
            }
            while (merged && survivor != null && !survivor.retired);

            return survivor;
        }

        private static bool Overlaps(SlowFieldEffect left, SlowFieldEffect right)
        {
            return SlowFieldMergePolicy.Overlaps(
                left.transform.position,
                left.radius,
                right.transform.position,
                right.radius);
        }

        private static SlowFieldEffect SelectSurvivor(SlowFieldEffect left, SlowFieldEffect right)
        {
            return left.creationSequence < right.creationSequence
                || (left.creationSequence == right.creationSequence
                    && left.GetInstanceID() <= right.GetInstanceID())
                ? left
                : right;
        }

        private void Absorb(SlowFieldEffect consumed)
        {
            if (consumed == null || consumed == this || consumed.retired)
            {
                return;
            }

            radius = SlowFieldMergePolicy.ResolveMergedRadius(
                radius,
                growthPerMerge,
                maximumRadius);
            movementMultiplier = Mathf.Min(movementMultiplier, consumed.movementMultiplier);
            entryPinDuration = Mathf.Max(entryPinDuration, consumed.entryPinDuration);
            expiresAt = Mathf.Max(expiresAt, consumed.expiresAt);
            occupants.UnionWith(consumed.occupants);
            UpdateVisualGeometry();
            consumed.Retire();
            ApplySlow();
        }

        private void ApplySlow()
        {
            if (retired)
            {
                return;
            }

            nextApplicationTime = Time.time + 0.2f;
            float remainingDuration = Mathf.Max(0.2f, expiresAt - Time.time);
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, radius, damageableLayers);
            HashSet<Damageable> currentOccupants = new HashSet<Damageable>();
            foreach (Collider2D overlap in overlaps)
            {
                Damageable damageable = overlap.GetComponentInParent<Damageable>();
                if (damageable == null || !currentOccupants.Add(damageable))
                {
                    continue;
                }

                EnemyStatusEffects statusEffects = damageable.GetComponent<EnemyStatusEffects>();
                if (statusEffects == null)
                {
                    continue;
                }

                bool entered = occupants.Add(damageable);
                statusEffects.ApplySlow(movementMultiplier, remainingDuration);
                if (entered && entryPinDuration > 0f)
                {
                    // Reuse the exact Tier 2 immobilize status, but only when
                    // an occupant first enters this authoritative field.
                    statusEffects.ApplyImmobilize(entryPinDuration);
                }
            }

            occupants.IntersectWith(currentOccupants);
        }

        private void Retire()
        {
            if (retired)
            {
                return;
            }

            retired = true;
            occupants.Clear();
            ActiveFields.Remove(this);
            Destroy(gameObject);
        }

        private void CreateVisual()
        {
            areaRenderer = gameObject.AddComponent<LineRenderer>();
            areaMaterial = new Material(Shader.Find("Sprites/Default"));
            areaRenderer.material = areaMaterial;
            areaRenderer.useWorldSpace = false;
            areaRenderer.loop = true;
            areaRenderer.positionCount = VisualSegments;
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
            UpdateVisualGeometry();
        }

        private void CreateFillVisual()
        {
            Vector3[] vertices = new Vector3[VisualSegments + 1];
            int[] triangles = new int[VisualSegments * 3];
            vertices[0] = Vector3.zero;
            for (int index = 0; index < VisualSegments; index++)
            {
                float angle = index / (float)VisualSegments * Mathf.PI * 2f;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                int triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = index + 1;
                triangles[triangleIndex + 2] = (index + 1) % VisualSegments + 1;
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

        private void UpdateVisualGeometry()
        {
            if (areaRenderer == null || areaMesh == null)
            {
                return;
            }

            Vector3[] vertices = new Vector3[VisualSegments + 1];
            int[] triangles = new int[VisualSegments * 3];
            vertices[0] = Vector3.zero;
            for (int index = 0; index < VisualSegments; index++)
            {
                float angle = index / (float)VisualSegments * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                areaRenderer.SetPosition(index, point);
                vertices[index + 1] = point;
                int triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = index + 1;
                triangles[triangleIndex + 2] = (index + 1) % VisualSegments + 1;
            }

            areaMesh.Clear();
            areaMesh.vertices = vertices;
            areaMesh.triangles = triangles;
            areaMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            ActiveFields.Remove(this);
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

    /// <summary>Pure geometry and cap policy for deterministic Frost-field coverage.</summary>
    public static class SlowFieldMergePolicy
    {
        public static bool Overlaps(Vector2 leftPosition, float leftRadius, Vector2 rightPosition, float rightRadius)
        {
            float combinedRadius = Mathf.Max(0f, leftRadius) + Mathf.Max(0f, rightRadius);
            return (leftPosition - rightPosition).sqrMagnitude <= combinedRadius * combinedRadius;
        }

        public static float ResolveMergedRadius(float currentRadius, float growthPerMerge, float maximumRadius)
        {
            float current = Mathf.Max(0.1f, currentRadius);
            float maximum = Mathf.Max(current, maximumRadius);
            return Mathf.Min(maximum, current + Mathf.Max(0f, growthPerMerge));
        }
    }
}
