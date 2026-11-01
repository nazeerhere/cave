using Cave.Player;
using UnityEngine;

namespace Cave.Projectiles
{
    /// <summary>Pure checks for the standard-fire and related shop economics seams.</summary>
    public static class FirePatternAndProgressionVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyPatternVocabulary(out failure)
                || !VerifyImpulseScaling(out failure)
                || !VerifyProgressionPriceRules(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyPatternVocabulary(out string failure)
        {
            bool valid = (int)FirePattern.Precision == 0
                && (int)FirePattern.Automatic == 1
                && (int)FirePattern.Burst == 2
                && (int)FirePattern.Volley == 3
                && (int)FirePattern.SpoolUp == 4;
            failure = valid ? null : "Fire Pattern selection vocabulary is not stable.";
            return valid;
        }

        private static bool VerifyImpulseScaling(out string failure)
        {
            float slower = StandardProjectileImpulsePolicy.Resolve(8f, 10f, 20f);
            float normal = StandardProjectileImpulsePolicy.Resolve(8f, 20f, 20f);
            float faster = StandardProjectileImpulsePolicy.Resolve(8f, 30f, 20f);
            bool valid = Mathf.Approximately(slower, 4f)
                && Mathf.Approximately(normal, 8f)
                && Mathf.Approximately(faster, 12f)
                && Mathf.Approximately(StandardProjectileImpulsePolicy.DestructibleResistanceMultiplier, .5f);
            failure = valid ? null : "Pattern impulse did not scale with projectile speed or destructible resistance.";
            return valid;
        }

        private static bool VerifyProgressionPriceRules(out string failure)
        {
            int basePrice = PotionEconomyPolicy.ResolvePrice(8, .25f, .25f, 0);
            int oneRank = PotionEconomyPolicy.ResolvePrice(8, .25f, .25f, 1);
            int twoRanks = PotionEconomyPolicy.ResolvePrice(8, .25f, .25f, 2);
            bool valid = basePrice == 8 && oneRank == 10 && twoRanks == 12
                && PlayerLandmineInventory.ResolveDiskCapacityPrice(18, 0) == 18
                && PlayerLandmineInventory.ResolveDiskCapacityPrice(18, 2) == 27
                && PlayerLandmineInventory.ResolveDiskCapacityPrice(36, 2) == 54;
            failure = valid ? null : "Potion progression price no longer adds 25% of original price per associated rank.";
            return valid;
        }
    }
}
