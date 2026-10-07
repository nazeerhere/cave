using UnityEngine;

namespace Cave.Player
{
    /// <summary>Pure policy verification for the upgraded-sword multiplier.</summary>
    public static class PlayerSwordUpgradeVerification
    {
        public static bool TryRunAll(out string failure)
        {
            const float currentUpgradedVisualScale = 0.72f;
            const float currentUpgradedReach = 1.1f;
            float upgradedVisual = currentUpgradedVisualScale * PlayerSwordCosmetics.UpgradedSwordMultiplier;
            float upgradedReach = currentUpgradedReach
                * PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Sword1);
            float baseReach = currentUpgradedReach
                * PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Default);
            bool upgradedVariants = PlayerSwordCosmetics.IsUpgraded(SwordCosmeticSelection.Sword1)
                && PlayerSwordCosmetics.IsUpgraded(SwordCosmeticSelection.Sword2)
                && PlayerSwordCosmetics.IsUpgraded(SwordCosmeticSelection.Sword3);
            bool allSwordsAvailableWithOverride = true;
            for (int index = (int)SwordCosmeticSelection.Default;
                index <= (int)SwordCosmeticSelection.Sword3;
                index++)
            {
                allSwordsAvailableWithOverride &= CosmeticAvailability.IsAvailable(false, true);
            }
            bool valid = upgradedVariants
                && !CosmeticAvailability.IsAvailable(false, false)
                && CosmeticAvailability.IsAvailable(false, true)
                && CosmeticAvailability.IsAvailable(true, false)
                && allSwordsAvailableWithOverride
                && Mathf.Approximately(upgradedVisual, 0.864f)
                && Mathf.Approximately(upgradedReach, 1.32f)
                && Mathf.Approximately(baseReach, currentUpgradedReach)
                && Mathf.Approximately(
                    PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Sword1),
                    PlayerSwordCosmetics.UpgradedSwordMultiplier);
            failure = valid ? null : "Sword upgrade or cosmetic availability policy was invalid.";
            return valid;
        }
    }
}
