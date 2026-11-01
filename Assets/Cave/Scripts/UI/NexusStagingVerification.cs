using System;
using Cave.Domain;
using Cave.Player;

namespace Cave.UI
{
    /// <summary>Deterministic contract checks for Nexus presentation bindings; no PlayerPrefs or scene state is mutated.</summary>
    public static class NexusStagingVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (IvoryRequirement() != 15)
            {
                failure = "Ivory Sovereign must require player level 15.";
                return false;
            }

            if (!PlayerSkinCosmeticsVerification.TryRunAll(out failure))
            {
                return false;
            }

            if (Math.Abs(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Default) - 1f) > .0001f
                || Math.Abs(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Sword1) - 1.2f) > .0001f
                || Math.Abs(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Sword2) - 1.2f) > .0001f
                || Math.Abs(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.Sword3) - 1.2f) > .0001f
                || Math.Abs(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.AstralNocturne) - 1f) > .0001f
                || Math.Abs(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.HeavensDefiance) - 1f) > .0001f
                || Math.Abs(PlayerSwordCosmetics.ReachMultiplierFor(SwordCosmeticSelection.AbyssalOssuary) - 1f) > .0001f)
            {
                failure = "Nexus must preserve legacy sword reach and legendary cosmetic-only reach contracts.";
                return false;
            }

            if (PlayerSwordCosmetics.SelectionCount != 7
                || !PlayerSwordCosmetics.IsTwoHandedWeapon(SwordCosmeticSelection.AstralNocturne)
                || !PlayerSwordCosmetics.IsTwoHandedWeapon(SwordCosmeticSelection.HeavensDefiance)
                || !PlayerSwordCosmetics.IsTwoHandedWeapon(SwordCosmeticSelection.AbyssalOssuary))
            {
                failure = "Nexus must expose all three legendary two-handed cosmetic weapons.";
                return false;
            }

            if (Enum.GetValues(typeof(LawPhenomenon)).Length != 8
                || Enum.GetValues(typeof(LawExpression)).Length != 3)
            {
                failure = "Nexus Domain bindings must use the authoritative current vocabulary.";
                return false;
            }

            failure = null;
            return true;
        }

        private static int IvoryRequirement()
        {
            return PlayerSkinCosmetics.IvorySovereignRequiredPlayerLevel;
        }
    }
}
