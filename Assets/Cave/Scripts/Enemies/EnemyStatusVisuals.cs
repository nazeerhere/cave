using Cave.Progression;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyStatusVisuals : MonoBehaviour
    {
        private ParticleSystem burnParticles;
        private ParticleSystem frostParticles;
        private Material burnMaterial;
        private Material frostMaterial;
        private SpecialModeTier2Settings settings;

        public void Configure(SpecialModeTier2Settings visualSettings)
        {
            settings = visualSettings;
            if (burnParticles != null)
            {
                ConfigureBurnParticles();
            }

            if (frostParticles != null)
            {
                ConfigureFrostParticles(false);
            }
        }

        public void SetBurnActive(bool active)
        {
            if (active)
            {
                EnsureBurnParticles();
                if (!burnParticles.isPlaying)
                {
                    burnParticles.Play();
                }
            }
            else if (burnParticles != null)
            {
                burnParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void SetFrostActive(bool active, bool intense)
        {
            if (active)
            {
                EnsureFrostParticles();
                ConfigureFrostParticles(intense);
                if (!frostParticles.isPlaying)
                {
                    frostParticles.Play();
                }
            }
            else if (frostParticles != null)
            {
                frostParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsureBurnParticles()
        {
            if (burnParticles != null)
            {
                return;
            }

            burnParticles = CreateParticleSystem("Burn Status VFX", out burnMaterial);
            ConfigureBurnParticles();
        }

        private void EnsureFrostParticles()
        {
            if (frostParticles != null)
            {
                return;
            }

            frostParticles = CreateParticleSystem("Frost Status VFX", out frostMaterial);
            ConfigureFrostParticles(false);
        }

        private ParticleSystem CreateParticleSystem(string objectName, out Material material)
        {
            GameObject effectObject = new GameObject(objectName);
            effectObject.transform.SetParent(transform, false);
            effectObject.transform.localPosition = Vector3.zero;

            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 32;
            main.gravityModifier = -0.08f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = ResolveEffectSize();

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.material = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = ResolveSortingOrder();
            return particles;
        }

        private void ConfigureBurnParticles()
        {
            Color baseColor = settings != null
                ? settings.BurnStatusColor
                : new Color(1f, 0.22f, 0.03f, 0.9f);
            Color highlight = settings != null
                ? settings.BurnStatusHighlightColor
                : new Color(1f, 0.78f, 0.12f, 1f);
            float lifetime = settings != null ? settings.BurnParticleLifetime : 0.55f;
            float rate = settings != null ? settings.BurnParticleRate : 18f;

            ParticleSystem.MainModule main = burnParticles.main;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(baseColor, highlight);

            ParticleSystem.EmissionModule emission = burnParticles.emission;
            emission.rateOverTime = rate;
        }

        private void ConfigureFrostParticles(bool intense)
        {
            Color baseColor = settings != null
                ? settings.FrostStatusColor
                : new Color(0.12f, 0.68f, 1f, 0.9f);
            Color highlight = settings != null
                ? settings.FrostStatusHighlightColor
                : new Color(0.75f, 0.95f, 1f, 1f);
            float lifetime = settings != null ? settings.FrostParticleLifetime : 0.7f;
            float rate = settings != null ? settings.FrostParticleRate : 12f;

            ParticleSystem.MainModule main = frostParticles.main;
            main.startLifetime = lifetime;
            main.startSpeed = intense
                ? new ParticleSystem.MinMaxCurve(0.12f, 0.35f)
                : new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = intense
                ? new ParticleSystem.MinMaxCurve(0.12f, 0.23f)
                : new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(baseColor, highlight);

            ParticleSystem.EmissionModule emission = frostParticles.emission;
            emission.rateOverTime = intense ? rate * 1.8f : rate;
        }

        private Vector3 ResolveEffectSize()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                return new Vector3(0.8f, 1f, 0.1f);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            return new Vector3(
                Mathf.Max(0.45f, Mathf.Abs(localSize.x) * 0.75f),
                Mathf.Max(0.65f, Mathf.Abs(localSize.y) * 0.75f),
                0.1f);
        }

        private int ResolveSortingOrder()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            int order = 0;
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                order = Mathf.Max(order, spriteRenderer.sortingOrder);
            }

            return order + 3;
        }

        private void OnDisable()
        {
            SetBurnActive(false);
            SetFrostActive(false, false);
        }

        private void OnDestroy()
        {
            if (burnMaterial != null)
            {
                Destroy(burnMaterial);
            }

            if (frostMaterial != null)
            {
                Destroy(frostMaterial);
            }
        }
    }
}
