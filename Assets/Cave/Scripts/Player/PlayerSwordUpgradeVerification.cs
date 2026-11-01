using Cave.Combat;
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
            bool legendaryVariants = PlayerSwordCosmetics.IsLegendaryWeapon(SwordCosmeticSelection.AstralNocturne)
                && PlayerSwordCosmetics.IsLegendaryWeapon(SwordCosmeticSelection.HeavensDefiance)
                && PlayerSwordCosmetics.IsLegendaryWeapon(SwordCosmeticSelection.AbyssalOssuary)
                && PlayerSwordCosmetics.IsTwoHandedWeapon(SwordCosmeticSelection.AstralNocturne)
                && PlayerSwordCosmetics.IsTwoHandedWeapon(SwordCosmeticSelection.HeavensDefiance)
                && PlayerSwordCosmetics.IsTwoHandedWeapon(SwordCosmeticSelection.AbyssalOssuary)
                && !PlayerSwordCosmetics.IsUpgraded(SwordCosmeticSelection.AstralNocturne)
                && !PlayerSwordCosmetics.IsUpgraded(SwordCosmeticSelection.HeavensDefiance)
                && !PlayerSwordCosmetics.IsUpgraded(SwordCosmeticSelection.AbyssalOssuary)
                && Mathf.Approximately(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.AstralNocturne), 1f)
                && Mathf.Approximately(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.HeavensDefiance), 1f)
                && Mathf.Approximately(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.AbyssalOssuary), 1f)
                && PlayerSwordCosmetics.SelectionCount == 7;
            bool selectionAvailability = PlayerSwordCosmetics.IsAvailableForPolicy(
                SwordCosmeticSelection.Default, true, false);
            for (int index = (int)SwordCosmeticSelection.Sword1;
                index < PlayerSwordCosmetics.SelectionCount;
                index++)
            {
                SwordCosmeticSelection selection = (SwordCosmeticSelection)index;
                selectionAvailability &= !PlayerSwordCosmetics.IsAvailableForPolicy(selection, false, false)
                    && PlayerSwordCosmetics.IsAvailableForPolicy(selection, false, true)
                    && PlayerSwordCosmetics.IsAvailableForPolicy(selection, true, false);
            }
            bool valid = upgradedVariants
                && legendaryVariants
                && !CosmeticAvailability.IsAvailable(false, false)
                && CosmeticAvailability.IsAvailable(false, true)
                && CosmeticAvailability.IsAvailable(true, false)
                && selectionAvailability
                && Mathf.Approximately(upgradedVisual, 0.864f)
                && Mathf.Approximately(upgradedReach, 1.32f)
                && Mathf.Approximately(baseReach, currentUpgradedReach)
                && Mathf.Approximately(
                    PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Sword1),
                    PlayerSwordCosmetics.UpgradedSwordMultiplier)
                && PlayerSwordCosmetics.ResourcePathFor(SwordCosmeticSelection.AstralNocturne)
                    == "Cosmetics/Weapons/Astral_Nocturne"
                && PlayerSwordCosmetics.ResourcePathFor(SwordCosmeticSelection.HeavensDefiance)
                    == "Cosmetics/Weapons/Heavens_Defiance"
                && PlayerSwordCosmetics.ResourcePathFor(SwordCosmeticSelection.AbyssalOssuary)
                    == "Cosmetics/Weapons/Abyssal_Ossuary"
                && ChargedAttack.ShouldSuppressLegacyAttackPreview(true, true)
                && !ChargedAttack.ShouldSuppressLegacyAttackPreview(true, false)
                && !ChargedAttack.ShouldSuppressLegacyAttackPreview(false, true);
            failure = valid ? null : "Sword upgrade or cosmetic availability policy was invalid.";
            return valid;
        }
    }
}
