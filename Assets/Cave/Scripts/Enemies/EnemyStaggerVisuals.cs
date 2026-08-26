using UnityEngine;
using Cave.Combat;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyStaggerVisuals : MonoBehaviour
    {
        [Header("Impact Colors")]
        [SerializeField] private Color minorColor = new Color(1f, 0.82f, 0.25f, 0.9f);
        [SerializeField] private Color normalColor = new Color(1f, 0.58f, 0.12f, 0.95f);
        [SerializeField] private Color heavyColor = new Color(1f, 0.25f, 0.12f, 1f);
        [SerializeField] private Color resistedColor = new Color(0.45f, 0.78f, 1f, 0.95f);

        [Header("Effect Shape")]
        [SerializeField, Min(0.1f)] private float minorRadius = 0.3f;
        [SerializeField, Min(0.1f)] private float normalRadius = 0.42f;
        [SerializeField, Min(0.1f)] private float heavyRadius = 0.56f;

        [Header("Mob-Relative Placement")]
        [SerializeField, Range(0f, 1f)] private float torsoHeight = 0.55f;
        [SerializeField, Min(0f)] private float fallbackStatusHeight = 0.3f;
        [SerializeField] private Vector3 impactVfxLocalOffset = new Vector3(0f, 0.02f, 0.01f);
        [SerializeField, Min(0.01f)] private float impactVfxScale = 0.42f;
        [SerializeField] private Vector3 statusVfxLocalOffset = new Vector3(0f, 0.04f, 0.01f);
        [SerializeField, Min(0.01f)] private float statusVfxScale = 0.38f;
        [SerializeField] private int sortingOrderOffset = 3;

        [Header("Optional Impact Prefabs")]
        [SerializeField] private GameObject minorStaggerVfxPrefab;
        [SerializeField] private GameObject normalStaggerVfxPrefab;
        [SerializeField] private GameObject heavyStaggerVfxPrefab;
        [SerializeField] private GameObject resistedStaggerVfxPrefab;

        [Header("Optional Continuous Stagger Prefabs")]
        [SerializeField] private GameObject minorStatusVfxPrefab;
        [SerializeField] private GameObject normalStatusVfxPrefab;
        [SerializeField] private GameObject heavyStatusVfxPrefab;
        [SerializeField] private GameObject resistedStatusVfxPrefab;

        private ParticleSystem statusParticles;
        private Material statusMaterial;
        private GameObject activeStatusVfx;
        private SpriteRenderer primarySpriteRenderer;
        private float stopStatusAt;

        private void Awake()
        {
            primarySpriteRenderer = FindPrimarySpriteRenderer();
        }

        public void Show(StaggerStrength strength, float duration, bool resisted)
        {
            Color color = resisted ? resistedColor : ResolveColor(strength);
            float radius = ResolveRadius(strength) * (resisted ? 0.78f : 1f);
            GameObject effectPrefab = ResolveEffectPrefab(strength, resisted);

            // One-shot impact VFX.
            if (effectPrefab != null)
            {
                GameObject instance = Instantiate(effectPrefab, transform);
                instance.transform.localPosition = ResolveTorsoAnchor() + impactVfxLocalOffset;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = effectPrefab.transform.localScale * impactVfxScale;
                ConfigureAttachedVfx(instance);

                Destroy(instance, Mathf.Max(0.3f, duration + 0.15f));
            }
            else
            {
                CombatShapeEffect.Create(
                    transform.TransformPoint(ResolveTorsoAnchor() + impactVfxLocalOffset),
                    ResolveShape(strength, resisted),
                    radius,
                    color,
                    0.22f,
                    strength == StaggerStrength.Minor ? -25f : 0f);
            }

            // Lingering VFX that lasts for the stagger duration.
            ShowContinuousStaggerVfx(strength, color, duration, resisted);
        }

        private void Update()
        {
            if (Time.time < stopStatusAt)
            {
                return;
            }

            StopContinuousStaggerVfx();
        }

        private void ShowContinuousStaggerVfx(
            StaggerStrength strength,
            Color color,
            float duration,
            bool resisted)
        {
            stopStatusAt = Mathf.Max(stopStatusAt, Time.time + Mathf.Max(0f, duration));

            GameObject statusPrefab = ResolveStatusEffectPrefab(strength, resisted);

            if (statusPrefab != null)
            {
                if (statusParticles != null && statusParticles.isPlaying)
                {
                    statusParticles.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                if (activeStatusVfx != null)
                {
                    Destroy(activeStatusVfx);
                    activeStatusVfx = null;
                }

                activeStatusVfx = Instantiate(statusPrefab, transform);
                activeStatusVfx.transform.localPosition = ResolveTorsoAnchor() + statusVfxLocalOffset;
                activeStatusVfx.transform.localRotation = Quaternion.identity;
                activeStatusVfx.transform.localScale = statusPrefab.transform.localScale * statusVfxScale;
                ConfigureAttachedVfx(activeStatusVfx);

                return;
            }

            // Fall back to the original procedural lingering particles when
            // no custom continuous prefab has been assigned.
            if (activeStatusVfx != null)
            {
                Destroy(activeStatusVfx);
                activeStatusVfx = null;
            }

            EnsureStatusParticles();
            ConfigureParticles(strength, color, resisted);

            if (!statusParticles.isPlaying)
            {
                statusParticles.Play();
            }
        }

        private void StopContinuousStaggerVfx()
        {
            if (statusParticles != null && statusParticles.isPlaying)
            {
                statusParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (activeStatusVfx != null)
            {
                Destroy(activeStatusVfx);
                activeStatusVfx = null;
            }
        }

        private void EnsureStatusParticles()
        {
            if (statusParticles != null)
            {
                return;
            }

            GameObject visual = new GameObject("Stagger Status VFX");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = ResolveTorsoAnchor() + statusVfxLocalOffset;
            statusParticles = visual.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = statusParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 10;
            main.startLifetime = 0.45f;
            main.startSpeed = 0.08f;
            main.startSize = 0.06f;

            ParticleSystem.ShapeModule shape = statusParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.24f;

            ParticleSystem.VelocityOverLifetimeModule velocity = statusParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalZ = 1.5f;

            ParticleSystemRenderer particleRenderer =
                statusParticles.GetComponent<ParticleSystemRenderer>();

            statusMaterial = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.material = statusMaterial;
            ApplyTargetSorting(particleRenderer);
        }

        private void ConfigureParticles(
            StaggerStrength strength,
            Color color,
            bool resisted)
        {
            ParticleSystem.MainModule main = statusParticles.main;
            main.startColor = color;
            main.startSize = resisted
                ? new ParticleSystem.MinMaxCurve(0.035f, 0.06f)
                : strength == StaggerStrength.Heavy
                    ? new ParticleSystem.MinMaxCurve(0.06f, 0.105f)
                    : new ParticleSystem.MinMaxCurve(0.04f, 0.075f);

            ParticleSystem.EmissionModule emission = statusParticles.emission;
            emission.rateOverTime = resisted
                ? 5f
                : strength == StaggerStrength.Heavy
                    ? 13f
                    : strength == StaggerStrength.Normal
                        ? 9f
                        : 5f;
        }

        private Color ResolveColor(StaggerStrength strength)
        {
            switch (strength)
            {
                case StaggerStrength.Heavy:
                    return heavyColor;
                case StaggerStrength.Normal:
                    return normalColor;
                default:
                    return minorColor;
            }
        }

        private GameObject ResolveEffectPrefab(
            StaggerStrength strength,
            bool resisted)
        {
            if (resisted)
            {
                return resistedStaggerVfxPrefab;
            }

            switch (strength)
            {
                case StaggerStrength.Heavy:
                    return heavyStaggerVfxPrefab;
                case StaggerStrength.Normal:
                    return normalStaggerVfxPrefab;
                default:
                    return minorStaggerVfxPrefab;
            }
        }

        private GameObject ResolveStatusEffectPrefab(
            StaggerStrength strength,
            bool resisted)
        {
            if (resisted)
            {
                return resistedStatusVfxPrefab;
            }

            switch (strength)
            {
                case StaggerStrength.Heavy:
                    return heavyStatusVfxPrefab;
                case StaggerStrength.Normal:
                    return normalStatusVfxPrefab;
                default:
                    return minorStatusVfxPrefab;
            }
        }

        private static CombatShape ResolveShape(
            StaggerStrength strength,
            bool resisted)
        {
            if (resisted)
            {
                return CombatShape.Hexagon;
            }

            return strength == StaggerStrength.Minor
                ? CombatShape.Slash
                : CombatShape.Diamond;
        }

        private float ResolveRadius(StaggerStrength strength)
        {
            switch (strength)
            {
                case StaggerStrength.Heavy:
                    return heavyRadius;
                case StaggerStrength.Normal:
                    return normalRadius;
                default:
                    return minorRadius;
            }
        }

        private Vector3 ResolveTorsoAnchor()
        {
            if (primarySpriteRenderer == null)
            {
                primarySpriteRenderer = FindPrimarySpriteRenderer();
            }

            if (primarySpriteRenderer == null)
            {
                return Vector3.up * fallbackStatusHeight;
            }

            Bounds bounds = primarySpriteRenderer.bounds;
            Vector3 worldAnchor = new Vector3(
                bounds.center.x,
                Mathf.Lerp(bounds.min.y, bounds.max.y, torsoHeight),
                transform.position.z);
            return transform.InverseTransformPoint(worldAnchor);
        }

        private SpriteRenderer FindPrimarySpriteRenderer()
        {
            SpriteRenderer primary = null;
            float largestArea = 0f;

            foreach (SpriteRenderer spriteRenderer
                     in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer.sprite == null)
                {
                    continue;
                }

                Vector2 size = spriteRenderer.bounds.size;
                float area = size.x * size.y;
                if (primary == null || area > largestArea)
                {
                    primary = spriteRenderer;
                    largestArea = area;
                }
            }

            return primary;
        }

        private void ConfigureAttachedVfx(GameObject instance)
        {
            foreach (ParticleSystem particleSystem
                     in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }

            foreach (ParticleSystemRenderer particleRenderer
                     in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                ApplyTargetSorting(particleRenderer);
            }

            foreach (SpriteRenderer spriteRenderer
                     in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                ApplyTargetSorting(spriteRenderer);
            }
        }

        private void ApplyTargetSorting(Renderer renderer)
        {
            if (primarySpriteRenderer == null)
            {
                primarySpriteRenderer = FindPrimarySpriteRenderer();
            }

            if (primarySpriteRenderer == null)
            {
                renderer.sortingOrder = sortingOrderOffset;
                return;
            }

            renderer.sortingLayerID = primarySpriteRenderer.sortingLayerID;
            renderer.sortingOrder = primarySpriteRenderer.sortingOrder + sortingOrderOffset;
        }

        private void OnDisable()
        {
            stopStatusAt = 0f;
            StopContinuousStaggerVfx();
        }

        private void OnDestroy()
        {
            if (activeStatusVfx != null)
            {
                Destroy(activeStatusVfx);
                activeStatusVfx = null;
            }

            if (statusMaterial != null)
            {
                Destroy(statusMaterial);
            }
        }
    }
}
