using System.Collections.Generic;
using Cave.Combat;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSpecialMode), typeof(PlayerSpecialModeUpgradeState))]
    public sealed class PlayerSkillVisuals : MonoBehaviour
    {
        [SerializeField] private SpecialModeTier2Settings settings;

        private PlayerSpecialMode specialMode;
        private PlayerSpecialModeUpgradeState upgrades;
        private PlayerFlight flight;
        private PlayerStrengthShield strengthShield;
        private SpinSwordAttack spinSword;
        private LineRenderer strengthAura;
        private Material strengthAuraMaterial;
        private ParticleSystem flightParticles;
        private Material flightParticleMaterial;
        private readonly List<SwordGlowPair> swordGlowPairs = new List<SwordGlowPair>();

        private sealed class SwordGlowPair
        {
            public SpriteRenderer Source;
            public SpriteRenderer Glow;
        }

        private void Awake()
        {
            specialMode = GetComponent<PlayerSpecialMode>();
            upgrades = GetComponent<PlayerSpecialModeUpgradeState>();
            flight = GetComponent<PlayerFlight>();
            strengthShield = GetComponent<PlayerStrengthShield>();
            spinSword = GetComponent<SpinSwordAttack>();
        }

        internal void Configure(SpecialModeTier2Settings visualSettings)
        {
            settings = visualSettings;
            RefreshVisuals();
        }

        private void Update()
        {
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (specialMode == null || upgrades == null)
            {
                return;
            }

            RefreshFlightParticles();
            RefreshStrengthVisuals();
        }

        private void RefreshFlightParticles()
        {
            int tier = upgrades.GetCurrentTier(SpecialMode.Flight);
            bool shouldPlay = flight != null && flight.IsFlying && tier >= 2;
            if (!shouldPlay)
            {
                if (flightParticles != null && flightParticles.isEmitting)
                {
                    flightParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                return;
            }

            EnsureFlightParticles();
            bool tier3 = tier >= 3;
            Color color = settings != null
                ? tier3
                    ? settings.FlightTier3ParticleColor
                    : settings.FlightTier2ParticleColor
                : tier3
                    ? new Color(0.75f, 0.4f, 1f, 0.95f)
                    : new Color(0.25f, 0.82f, 1f, 0.85f);
            float rate = settings != null
                ? tier3 ? settings.FlightTier3ParticleRate : settings.FlightTier2ParticleRate
                : tier3 ? 28f : 14f;
            float lifetime = settings != null ? settings.FlightParticleLifetime : 0.55f;

            ParticleSystem.MainModule main = flightParticles.main;
            main.startLifetime = lifetime;
            main.startSize = tier3
                ? new ParticleSystem.MinMaxCurve(0.09f, 0.2f)
                : new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = color;
            ParticleSystem.EmissionModule emission = flightParticles.emission;
            emission.rateOverTime = rate;
            if (!flightParticles.isPlaying)
            {
                flightParticles.Play();
            }
        }

        private void EnsureFlightParticles()
        {
            if (flightParticles != null)
            {
                return;
            }

            GameObject effectObject = new GameObject("Flight Tier Particles");
            effectObject.transform.SetParent(transform, false);
            effectObject.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            flightParticles = effectObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = flightParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 40;

            ParticleSystem.ShapeModule shape = flightParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.75f, 0.16f, 0.1f);

            ParticleSystem.VelocityOverLifetimeModule velocity = flightParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-0.9f, -0.35f);

            ParticleSystemRenderer particleRenderer = flightParticles.GetComponent<ParticleSystemRenderer>();
            flightParticleMaterial = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.material = flightParticleMaterial;
            particleRenderer.sortingOrder = 12;
        }

        private void RefreshStrengthVisuals()
        {
            bool strengthActive = specialMode.CurrentMode == SpecialMode.DamageBoost;
            if (!strengthActive)
            {
                if (strengthAura != null)
                {
                    strengthAura.enabled = false;
                }

                SetSwordGlowActive(false, Color.clear);
                return;
            }

            int tier = upgrades.GetCurrentTier(SpecialMode.DamageBoost);
            EnsureStrengthAura();
            strengthAura.enabled = true;
            Color auraColor = ResolveStrengthAuraColor(tier);
            float pulse = 0.92f + Mathf.Sin(Time.time * 4.5f) * 0.08f;
            Color pulsed = auraColor;
            pulsed.a *= 0.82f + Mathf.Sin(Time.time * 4.5f) * 0.18f;
            strengthAura.startColor = pulsed;
            strengthAura.endColor = pulsed;
            strengthAura.transform.localScale = Vector3.one * pulse;

            Color swordColor = settings != null
                ? settings.StrengthTier3SwordGlowColor
                : new Color(1f, 0.9f, 0.3f, 0.62f);
            SetSwordGlowActive(tier >= 3, swordColor);
        }

        private Color ResolveStrengthAuraColor(int tier)
        {
            Color baseColor = settings != null
                ? settings.StrengthAuraColor
                : new Color(1f, 0.7f, 0.16f, 0.75f);
            if (tier < 2 || strengthShield == null)
            {
                return baseColor;
            }

            switch (strengthShield.CurrentState)
            {
                case StrengthShieldState.Ready:
                    return settings != null
                        ? settings.StrengthShieldReadyAuraColor
                        : new Color(0.25f, 0.9f, 1f, 0.85f);
                case StrengthShieldState.Broken:
                    return settings != null
                        ? settings.StrengthShieldBrokenAuraColor
                        : new Color(0.35f, 0.22f, 0.18f, 0.55f);
                case StrengthShieldState.Recharging:
                    return settings != null
                        ? settings.StrengthShieldRechargingAuraColor
                        : new Color(0.38f, 0.5f, 0.95f, 0.7f);
                default:
                    return baseColor;
            }
        }

        private void EnsureStrengthAura()
        {
            if (strengthAura != null)
            {
                return;
            }

            GameObject auraObject = new GameObject("Strength Tier Aura");
            auraObject.transform.SetParent(transform, false);
            strengthAura = auraObject.AddComponent<LineRenderer>();
            strengthAuraMaterial = new Material(Shader.Find("Sprites/Default"));
            strengthAura.material = strengthAuraMaterial;
            strengthAura.useWorldSpace = false;
            strengthAura.loop = true;
            strengthAura.positionCount = 48;
            strengthAura.startWidth = 0.07f;
            strengthAura.endWidth = 0.07f;
            strengthAura.sortingOrder = 9;
            float radius = settings != null ? settings.StrengthAuraRadius : 0.85f;
            for (int index = 0; index < strengthAura.positionCount; index++)
            {
                float angle = index / (float)strengthAura.positionCount * Mathf.PI * 2f;
                strengthAura.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void SetSwordGlowActive(bool active, Color color)
        {
            if (active && swordGlowPairs.Count == 0)
            {
                CreateSwordGlowOverlays();
            }

            foreach (SwordGlowPair pair in swordGlowPairs)
            {
                if (pair.Source == null || pair.Glow == null)
                {
                    continue;
                }

                pair.Glow.sprite = pair.Source.sprite;
                pair.Glow.flipX = pair.Source.flipX;
                pair.Glow.flipY = pair.Source.flipY;
                pair.Glow.drawMode = pair.Source.drawMode;
                pair.Glow.size = pair.Source.size;
                pair.Glow.color = color;
                pair.Glow.enabled = active && pair.Source.enabled;
            }
        }

        private void CreateSwordGlowOverlays()
        {
            if (spinSword == null || spinSword.SwordVisualObject == null)
            {
                return;
            }

            SpriteRenderer[] swordRenderers = spinSword.SwordVisualObject
                .GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer source in swordRenderers)
            {
                GameObject glowObject = new GameObject("Strength Tier 3 Sword Glow");
                glowObject.transform.SetParent(source.transform, false);
                SpriteRenderer glow = glowObject.AddComponent<SpriteRenderer>();
                glow.sprite = source.sprite;
                glow.sharedMaterial = source.sharedMaterial;
                glow.sortingLayerID = source.sortingLayerID;
                glow.sortingOrder = source.sortingOrder + 1;
                glow.maskInteraction = source.maskInteraction;
                glow.enabled = false;
                swordGlowPairs.Add(new SwordGlowPair { Source = source, Glow = glow });
            }
        }

        private void OnDisable()
        {
            if (strengthAura != null)
            {
                strengthAura.enabled = false;
            }

            if (flightParticles != null)
            {
                flightParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            SetSwordGlowActive(false, Color.clear);
        }

        private void OnDestroy()
        {
            if (strengthAuraMaterial != null)
            {
                Destroy(strengthAuraMaterial);
            }

            if (flightParticleMaterial != null)
            {
                Destroy(flightParticleMaterial);
            }
        }
    }
}
