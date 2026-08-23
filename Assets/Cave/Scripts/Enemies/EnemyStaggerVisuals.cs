using UnityEngine;

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
        [SerializeField, Min(0.1f)] private float minorRadius = 0.45f;
        [SerializeField, Min(0.1f)] private float normalRadius = 0.62f;
        [SerializeField, Min(0.1f)] private float heavyRadius = 0.82f;
        [SerializeField, Min(0f)] private float statusHeight = 0.75f;

        private ParticleSystem statusParticles;
        private Material statusMaterial;
        private float stopStatusAt;

        public void Show(StaggerStrength strength, float duration, bool resisted)
        {
            Color color = resisted ? resistedColor : ResolveColor(strength);
            float radius = ResolveRadius(strength) * (resisted ? 0.78f : 1f);
            Cave.Combat.AreaPulseEffect.Create(transform.position, radius, color, 0.2f);

            EnsureStatusParticles();
            ConfigureParticles(strength, color, resisted);
            stopStatusAt = Mathf.Max(stopStatusAt, Time.time + duration);
            if (!statusParticles.isPlaying)
            {
                statusParticles.Play();
            }
        }

        private void Update()
        {
            if (statusParticles != null && statusParticles.isPlaying && Time.time >= stopStatusAt)
            {
                statusParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            visual.transform.localPosition = Vector3.up * statusHeight;
            statusParticles = visual.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = statusParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 14;
            main.startLifetime = 0.45f;
            main.startSpeed = 0.12f;
            main.startSize = 0.09f;

            ParticleSystem.ShapeModule shape = statusParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.38f;

            ParticleSystem.VelocityOverLifetimeModule velocity = statusParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalZ = 1.5f;

            ParticleSystemRenderer particleRenderer = statusParticles.GetComponent<ParticleSystemRenderer>();
            statusMaterial = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.material = statusMaterial;
            particleRenderer.sortingOrder = ResolveSortingOrder();
        }

        private void ConfigureParticles(StaggerStrength strength, Color color, bool resisted)
        {
            ParticleSystem.MainModule main = statusParticles.main;
            main.startColor = color;
            main.startSize = resisted
                ? new ParticleSystem.MinMaxCurve(0.05f, 0.09f)
                : strength == StaggerStrength.Heavy
                    ? new ParticleSystem.MinMaxCurve(0.09f, 0.16f)
                    : new ParticleSystem.MinMaxCurve(0.06f, 0.11f);

            ParticleSystem.EmissionModule emission = statusParticles.emission;
            emission.rateOverTime = resisted
                ? 7f
                : strength == StaggerStrength.Heavy ? 18f : strength == StaggerStrength.Normal ? 12f : 7f;
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

        private int ResolveSortingOrder()
        {
            int sortingOrder = 0;
            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                sortingOrder = Mathf.Max(sortingOrder, spriteRenderer.sortingOrder);
            }

            return sortingOrder + 5;
        }

        private void OnDisable()
        {
            stopStatusAt = 0f;
            if (statusParticles != null)
            {
                statusParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnDestroy()
        {
            if (statusMaterial != null)
            {
                Destroy(statusMaterial);
            }
        }
    }
}
