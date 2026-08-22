using System;
using Cave.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Progression
{
    [CreateAssetMenu(menuName = "Cave/Special Mode Tier 2 Settings", fileName = "SpecialModeTier2Settings")]
    public sealed class SpecialModeTier2Settings : ScriptableObject
    {
        [Header("Tier 2 Upgrade Costs")]
        [SerializeField, Min(0)] private int slowShotTier2Cost = 25;
        [SerializeField, Min(0)] private int burnShotTier2Cost = 30;
        [SerializeField, Min(0)] private int flightTier2Cost = 35;
        [SerializeField, Min(0)] private int strengthTier2Cost = 40;

        [Header("Tier 3 Upgrade Costs")]
        [SerializeField, Min(0)] private int slowShotTier3Cost = 60;
        [SerializeField, Min(0)] private int burnShotTier3Cost = 70;
        [SerializeField, Min(0)] private int flightTier3Cost = 80;
        [SerializeField, Min(0)] private int strengthTier3Cost = 90;

        [Header("Tier Damage Scaling")]
        [SerializeField, Min(0)] private int slowShotTier2DamageBonus = 1;
        [SerializeField, Min(0)] private int slowShotTier3DamageBonus = 2;
        [SerializeField, Min(0)] private int burnShotTier2DamageBonus = 1;
        [SerializeField, Min(0)] private int burnShotTier3DamageBonus = 2;
        [SerializeField, Min(1f)] private float strengthTier2DamageMultiplier = 1.75f;
        [SerializeField, Min(1f)] private float strengthTier3DamageMultiplier = 2f;
        [SerializeField, Min(0)] private int strengthTier2DamageBonus = 1;
        [SerializeField, Min(0)] private int strengthTier3DamageBonus = 2;

        [Header("Flight Tier Efficiency")]
        [SerializeField, Range(0f, 0.9f)] private float flightTier2EfficiencyBonus = 0.05f;
        [SerializeField, Range(0f, 0.9f)] private float flightTier3EfficiencyBonus = 0.10f;

        [Header("Projectile Pierce")]
        [FormerlySerializedAs("slowShotPierceCount")]
        [SerializeField, Min(1)] private int slowShotTier2PierceCount = 2;
        [SerializeField, Min(1)] private int slowShotTier3PierceCount = 2;
        [FormerlySerializedAs("burnShotPierceCount")]
        [SerializeField, Min(1)] private int burnShotTier2PierceCount = 3;
        [SerializeField, Min(1)] private int burnShotTier3PierceCount = 5;

        [Header("Frost Control")]
        [SerializeField, Min(0f)] private float frostTier2PinDuration = 1f;
        [SerializeField, Min(0f)] private float frostTier3FreezeDuration = 2f;

        [Header("Flight Bash")]
        [SerializeField, Min(0f)] private float bashManaCost = 20f;
        [SerializeField, Min(1)] private int bashDamage = 2;
        [SerializeField, Min(0f)] private float bashSpeed = 20f;
        [SerializeField, Min(0.01f)] private float bashDuration = 0.18f;
        [SerializeField, Min(0f)] private float bashCooldown = 0.35f;
        [SerializeField, Min(0f)] private float bashKnockback = 12f;

        [Header("Strength Shield")]
        [SerializeField, Min(0f)] private float shieldRechargeDelay = 8f;
        [SerializeField, Min(0.01f)] private float shieldRechargeDuration = 6f;
        [SerializeField, Min(0f)] private float shieldFullRechargeManaCost = 20f;

        [Header("Frost Tier 3 Field")]
        [SerializeField, Min(0.1f)] private float slowFieldRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float slowFieldDuration = 5f;
        [SerializeField, Range(0.05f, 1f)] private float slowFieldMovementMultiplier = 0.8f;

        [Header("Burn Tier 3 Spread")]
        [SerializeField, Min(0.1f)] private float burnSpreadRadius = 2.5f;
        [SerializeField, Min(1)] private int maximumBurnSpreadTargets = 3;

        [Header("Fire / Frost Status Visuals")]
        [SerializeField] private Color burnStatusColor = new Color(1f, 0.22f, 0.03f, 0.9f);
        [SerializeField] private Color burnStatusHighlightColor = new Color(1f, 0.78f, 0.12f, 1f);
        [SerializeField, Min(1f)] private float burnParticleRate = 18f;
        [SerializeField, Min(0.05f)] private float burnParticleLifetime = 0.55f;
        [SerializeField] private Color frostStatusColor = new Color(0.12f, 0.68f, 1f, 0.9f);
        [SerializeField] private Color frostStatusHighlightColor = new Color(0.75f, 0.95f, 1f, 1f);
        [SerializeField, Min(1f)] private float frostParticleRate = 12f;
        [SerializeField, Min(0.05f)] private float frostParticleLifetime = 0.7f;
        [SerializeField] private Color frostFieldOutlineColor = new Color(0.28f, 0.82f, 1f, 0.85f);
        [SerializeField] private Color frostFieldFillColor = new Color(0.15f, 0.55f, 1f, 0.16f);

        [Header("Projectile Tier Visuals")]
        [SerializeField] private Color fireTier1Color = new Color(1f, 0.35f, 0.08f, 1f);
        [SerializeField] private Color fireTier2Color = new Color(1f, 0.68f, 0.12f, 1f);
        [SerializeField] private Color fireTier3Color = new Color(1f, 0.92f, 0.38f, 1f);
        [SerializeField] private Color frostTier1Color = new Color(0.25f, 0.72f, 1f, 1f);
        [SerializeField] private Color frostTier2Color = new Color(0.55f, 0.92f, 1f, 1f);
        [SerializeField] private Color frostTier3Color = new Color(0.82f, 0.96f, 1f, 1f);
        [SerializeField, Min(1f)] private float tier2ProjectileScale = 1.15f;
        [SerializeField, Min(1f)] private float tier3ProjectileScale = 1.32f;
        [SerializeField, Min(0f)] private float tier2ProjectileTrailTime = 0.16f;
        [SerializeField, Min(0f)] private float tier3ProjectileTrailTime = 0.28f;

        [Header("Flight Tier Particles")]
        [SerializeField] private Color flightTier2ParticleColor = new Color(0.25f, 0.82f, 1f, 0.85f);
        [SerializeField] private Color flightTier3ParticleColor = new Color(0.75f, 0.4f, 1f, 0.95f);
        [SerializeField, Min(1f)] private float flightTier2ParticleRate = 14f;
        [SerializeField, Min(1f)] private float flightTier3ParticleRate = 28f;
        [SerializeField, Min(0.05f)] private float flightParticleLifetime = 0.55f;

        [Header("Strength Tier Visuals")]
        [SerializeField] private Color strengthAuraColor = new Color(1f, 0.7f, 0.16f, 0.75f);
        [SerializeField] private Color strengthShieldReadyAuraColor = new Color(0.25f, 0.9f, 1f, 0.85f);
        [SerializeField] private Color strengthShieldBrokenAuraColor = new Color(0.35f, 0.22f, 0.18f, 0.55f);
        [SerializeField] private Color strengthShieldRechargingAuraColor = new Color(0.38f, 0.5f, 0.95f, 0.7f);
        [SerializeField] private Color strengthTier3SwordGlowColor = new Color(1f, 0.9f, 0.3f, 0.62f);
        [SerializeField, Min(0.1f)] private float strengthAuraRadius = 0.85f;

        [Header("Flight Tier 3 Shockwave")]
        [SerializeField, Min(0f)] private float tier3BashAdditionalManaCost;
        [SerializeField, Min(0.1f)] private float bashShockwaveRadius = 2.5f;
        [SerializeField, Min(1)] private int bashShockwaveDamage = 2;
        [SerializeField, Min(0f)] private float bashShockwaveKnockback = 14f;

        [Header("Strength Tier 3 Deflection")]
        [SerializeField, Min(0.01f)] private float deflectionFlashDuration = 0.12f;
        [SerializeField] private Color deflectionFlashColor = new Color(1f, 0.9f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float deflectionKnockback = 6f;

        [Header("Immediate-Use Shop")]
        [SerializeField, Min(0)] private int healthPotionCost = 5;
        [SerializeField, Range(0.01f, 1f)] private float healthPotionRestorePercent = 0.25f;
        [SerializeField, Min(0)] private int manaPotionCost = 4;
        [SerializeField, Range(0.01f, 1f)] private float manaPotionRestorePercent = 0.25f;

        public int SlowShotPierceCount => slowShotTier2PierceCount;
        public int BurnShotPierceCount => burnShotTier2PierceCount;
        public float FrostTier2PinDuration => frostTier2PinDuration;
        public float FrostTier3FreezeDuration => frostTier3FreezeDuration;
        public float StrengthTier2DamageMultiplier => strengthTier2DamageMultiplier;
        public float StrengthTier3DamageMultiplier => strengthTier3DamageMultiplier;
        public int StrengthTier2DamageBonus => strengthTier2DamageBonus;
        public int StrengthTier3DamageBonus => strengthTier3DamageBonus;
        public float FlightTier2EfficiencyBonus => flightTier2EfficiencyBonus;
        public float FlightTier3EfficiencyBonus => flightTier3EfficiencyBonus;
        public float BashManaCost => bashManaCost;
        public int BashDamage => bashDamage;
        public float BashSpeed => bashSpeed;
        public float BashDuration => bashDuration;
        public float BashCooldown => bashCooldown;
        public float BashKnockback => bashKnockback;
        public float ShieldRechargeDelay => shieldRechargeDelay;
        public float ShieldRechargeDuration => shieldRechargeDuration;
        public float ShieldFullRechargeManaCost => shieldFullRechargeManaCost;
        public float SlowFieldRadius => slowFieldRadius;
        public float SlowFieldDuration => slowFieldDuration;
        public float SlowFieldMovementMultiplier => slowFieldMovementMultiplier;
        public float BurnSpreadRadius => burnSpreadRadius;
        public int MaximumBurnSpreadTargets => maximumBurnSpreadTargets;
        public Color BurnStatusColor => burnStatusColor;
        public Color BurnStatusHighlightColor => burnStatusHighlightColor;
        public float BurnParticleRate => burnParticleRate;
        public float BurnParticleLifetime => burnParticleLifetime;
        public Color FrostStatusColor => frostStatusColor;
        public Color FrostStatusHighlightColor => frostStatusHighlightColor;
        public float FrostParticleRate => frostParticleRate;
        public float FrostParticleLifetime => frostParticleLifetime;
        public Color FrostFieldOutlineColor => frostFieldOutlineColor;
        public Color FrostFieldFillColor => frostFieldFillColor;
        public float Tier2ProjectileScale => tier2ProjectileScale;
        public float Tier3ProjectileScale => tier3ProjectileScale;
        public float Tier2ProjectileTrailTime => tier2ProjectileTrailTime;
        public float Tier3ProjectileTrailTime => tier3ProjectileTrailTime;
        public Color FlightTier2ParticleColor => flightTier2ParticleColor;
        public Color FlightTier3ParticleColor => flightTier3ParticleColor;
        public float FlightTier2ParticleRate => flightTier2ParticleRate;
        public float FlightTier3ParticleRate => flightTier3ParticleRate;
        public float FlightParticleLifetime => flightParticleLifetime;
        public Color StrengthAuraColor => strengthAuraColor;
        public Color StrengthShieldReadyAuraColor => strengthShieldReadyAuraColor;
        public Color StrengthShieldBrokenAuraColor => strengthShieldBrokenAuraColor;
        public Color StrengthShieldRechargingAuraColor => strengthShieldRechargingAuraColor;
        public Color StrengthTier3SwordGlowColor => strengthTier3SwordGlowColor;
        public float StrengthAuraRadius => strengthAuraRadius;
        public float Tier3BashAdditionalManaCost => tier3BashAdditionalManaCost;
        public float BashShockwaveRadius => bashShockwaveRadius;
        public int BashShockwaveDamage => bashShockwaveDamage;
        public float BashShockwaveKnockback => bashShockwaveKnockback;
        public float DeflectionFlashDuration => deflectionFlashDuration;
        public Color DeflectionFlashColor => deflectionFlashColor;
        public float DeflectionKnockback => deflectionKnockback;
        public int HealthPotionCost => healthPotionCost;
        public float HealthPotionRestorePercent => healthPotionRestorePercent;
        public int ManaPotionCost => manaPotionCost;
        public float ManaPotionRestorePercent => manaPotionRestorePercent;

        public int GetTier2Cost(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return slowShotTier2Cost;
                case SpecialMode.BurnShot:
                    return burnShotTier2Cost;
                case SpecialMode.Flight:
                    return flightTier2Cost;
                case SpecialMode.DamageBoost:
                    return strengthTier2Cost;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public int GetTier3Cost(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return slowShotTier3Cost;
                case SpecialMode.BurnShot:
                    return burnShotTier3Cost;
                case SpecialMode.Flight:
                    return flightTier3Cost;
                case SpecialMode.DamageBoost:
                    return strengthTier3Cost;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public int GetProjectileDamageBonus(SpecialMode mode, int tier)
        {
            if (tier < 2)
            {
                return 0;
            }

            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return tier >= 3 ? slowShotTier3DamageBonus : slowShotTier2DamageBonus;
                case SpecialMode.BurnShot:
                    return tier >= 3 ? burnShotTier3DamageBonus : burnShotTier2DamageBonus;
                default:
                    return 0;
            }
        }

        public int GetProjectilePierceCount(SpecialMode mode, int tier)
        {
            if (tier < 2)
            {
                return 1;
            }

            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return tier >= 3 ? slowShotTier3PierceCount : slowShotTier2PierceCount;
                case SpecialMode.BurnShot:
                    return tier >= 3 ? burnShotTier3PierceCount : burnShotTier2PierceCount;
                default:
                    return 1;
            }
        }

        public Color GetProjectileTierColor(SpecialMode mode, int tier)
        {
            bool isBurn = mode == SpecialMode.BurnShot;
            if (tier >= 3)
            {
                return isBurn ? fireTier3Color : frostTier3Color;
            }

            if (tier >= 2)
            {
                return isBurn ? fireTier2Color : frostTier2Color;
            }

            return isBurn ? fireTier1Color : frostTier1Color;
        }

        public string GetTier2AbilityName(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                case SpecialMode.BurnShot:
                    return "Pierce";
                case SpecialMode.Flight:
                    return "Bash";
                case SpecialMode.DamageBoost:
                    return "Shield";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public string GetTier2Description(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return "+" + slowShotTier2DamageBonus + " damage; pierce "
                        + slowShotTier2PierceCount + "; pin " + frostTier2PinDuration.ToString("0.#") + "s";
                case SpecialMode.BurnShot:
                    return "+" + burnShotTier2DamageBonus + " damage; burn and pierce "
                        + burnShotTier2PierceCount;
                case SpecialMode.Flight:
                    return "Airborne Mana Bash; drain -" + Mathf.RoundToInt(flightTier2EfficiencyBonus * 100f) + "%";
                case SpecialMode.DamageBoost:
                    return strengthTier2DamageMultiplier.ToString("0.##") + "x +"
                        + strengthTier2DamageBonus + " damage; Shield";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public string GetTier3AbilityName(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return "Slow Field";
                case SpecialMode.BurnShot:
                    return "Spreading Burn";
                case SpecialMode.Flight:
                    return "Bash Shockwave";
                case SpecialMode.DamageBoost:
                    return "Aggressive Deflection";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public string GetTier3Description(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return "+" + slowShotTier3DamageBonus + " damage; freeze "
                        + frostTier3FreezeDuration.ToString("0.#") + "s; Frost Field";
                case SpecialMode.BurnShot:
                    return "+" + burnShotTier3DamageBonus + " damage; pierce "
                        + burnShotTier3PierceCount + "; Burn spreads to " + maximumBurnSpreadTargets;
                case SpecialMode.Flight:
                    return "Drain -" + Mathf.RoundToInt(flightTier3EfficiencyBonus * 100f) + "%; Bash Shockwave";
                case SpecialMode.DamageBoost:
                    return strengthTier3DamageMultiplier.ToString("0.##") + "x +"
                        + strengthTier3DamageBonus + " damage; deflects";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
