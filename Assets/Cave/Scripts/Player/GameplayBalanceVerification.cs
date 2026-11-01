using Cave.Axioms.Phase;
using Cave.Domain;
using Cave.Projectiles;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>Focused deterministic checks for the balance/persistence patch's pure contracts.</summary>
    public static class GameplayBalanceVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyPotionEconomy(out failure)
                || !VerifyFlightDefaults(out failure)
                || !VerifyDomainRadius(out failure)
                || !VerifyImaginaryDuration(out failure)
                || !VerifyHeavyPresentationScale(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyPotionEconomy(out string failure)
        {
            float upgraded = PotionEconomyPolicy.ResolvePotency(.25f, 1, .05f);
            bool valid = Mathf.Approximately(upgraded, .30f)
                && PotionEconomyPolicy.ResolvePrice(5, .25f, upgraded) == 6
                && PotionEconomyPolicy.ResolvePrice(4, .25f, upgraded) == 5
                && PotionEconomyPolicy.ResolvePrice(5, .25f, .25f) == 5;
            failure = valid ? null : "Potion potency or deterministic proportional price rounding changed.";
            return valid;
        }

        private static bool VerifyFlightDefaults(out string failure)
        {
            SpecialModeTier2Settings settings = ScriptableObject.CreateInstance<SpecialModeTier2Settings>();
            bool valid = Mathf.Approximately(settings.FlightTier2EfficiencyBonus, .30f)
                && Mathf.Approximately(settings.FlightTier3EfficiencyBonus, .70f);
            Object.DestroyImmediate(settings);
            failure = valid ? null : "Flight tier drain reductions were not 30% and 70% from base.";
            return valid;
        }

        private static bool VerifyDomainRadius(out string failure)
        {
            GameObject owner = new GameObject("Balance Verification Domain Owner");
            PlayerDomainManifestation manifestation = owner.AddComponent<PlayerDomainManifestation>();
            bool valid = Mathf.Approximately(
                manifestation.CalculateRadius(Cave.Axioms.Mastery.PlayerMasteryEvidenceState.Empty), 6f);
            Object.DestroyImmediate(owner);
            failure = valid ? null : "Base Domain radius was not increased from 2.4 to 6.0.";
            return valid;
        }

        private static bool VerifyImaginaryDuration(out string failure)
        {
            GameObject owner = new GameObject("Balance Verification Phase Owner");
            PhaseCombatState phase = owner.AddComponent<PhaseCombatState>();
            bool valid = Mathf.Approximately(phase.CurrentImaginaryDuration, 7.35f);
            Object.DestroyImmediate(owner);
            failure = valid ? null : "Imaginary base duration was not increased from 4.2 to 7.35 seconds.";
            return valid;
        }

        private static bool VerifyHeavyPresentationScale(out string failure)
        {
            bool valid = Mathf.Approximately(
                    ChargedProjectilePolicy.HeavyVisualScale(0f, 1.4f, 2.2f), 1.4f)
                && Mathf.Approximately(
                    ChargedProjectilePolicy.HeavyVisualScale(1f, 1.4f, 2.2f), 2.2f)
                && Mathf.Approximately(ChargedProjectilePolicy.DefaultHeavyExplosionRadius, 3.5f);
            failure = valid ? null : "Heavy presentation scale or terminal radius changed unexpectedly.";
            return valid;
        }
    }
}
