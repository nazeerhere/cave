using Cave.Combat;
using Cave.Progression;
using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class ManifestationOriginVerificationMenu
    {
        [MenuItem("Cave/Verification/Manifestation Origin")]
        public static void Run()
        {
            string failure;
            if (!ManifestationOriginVerification.TryRunAll(out failure))
                throw new System.InvalidOperationException(failure);

            SpecialModeTier2Settings settings = AssetDatabase.LoadAssetAtPath<SpecialModeTier2Settings>(
                "Assets/Cave/Resources/SpecialModeTier2Settings.asset");
            if (settings == null
                || settings.GetTier2Cost(SpecialMode.SlowShot) != 25
                || settings.GetTier2Cost(SpecialMode.BurnShot) != 30
                || settings.GetTier2Cost(SpecialMode.Flight) != 35
                || settings.GetTier2Cost(SpecialMode.DamageBoost) != 40
                || settings.GetTier3Cost(SpecialMode.SlowShot) != 60
                || settings.GetTier3Cost(SpecialMode.BurnShot) != 70
                || settings.GetTier3Cost(SpecialMode.Flight) != 80
                || settings.GetTier3Cost(SpecialMode.DamageBoost) != 90)
                throw new System.InvalidOperationException(
                    "Skill Path purchase prices changed from the audited 25/30/35/40 and 60/70/80/90 baseline.");

            Debug.Log("[Cave] Manifestation Origin verification passed; Skill Path purchase price baseline preserved.");
        }
    }
}
