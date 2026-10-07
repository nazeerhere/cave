using System;
using Cave.Diagnostics;

namespace Cave.Player
{
    /// <summary>Pure catalogue contracts for the presentation-only player skin system.</summary>
    public static class PlayerSkinCosmeticsVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (PlayerSkinLibrary.Count != 8)
            {
                failure = "The cosmetics catalogue must contain the Default Miner and seven alternate skins.";
                return false;
            }

            for (int index = 0; index < PlayerSkinLibrary.Count; index++)
            {
                PlayerSkinSelection selection = (PlayerSkinSelection)index;
                if (!PlayerSkinLibrary.IsRegistered(selection)
                    || PlayerSkinLibrary.GetDefinition(selection).Selection != selection)
                {
                    failure = "Skin registration is not stable for index " + index + ".";
                    return false;
                }
            }

            if (PlayerSkinLibrary.GetDefinition(PlayerSkinSelection.DefaultMiner).Policy
                != PlayerSkinLibrary.UnlockPolicy.AlwaysAvailable
                || PlayerSkinCosmetics.IvorySovereignRequiredPlayerLevel != 15)
            {
                failure = "Default availability or Ivory's player-level requirement changed.";
                return false;
            }

            if (PlayerSkinLibrary.GetDefinition(PlayerSkinSelection.CrimsonExile).Policy
                != PlayerSkinLibrary.UnlockPolicy.SovereignGambitVictory)
            {
                failure = "Crimson Exile must be gated by an authoritative Gambit victory.";
                return false;
            }

            if (CosmeticAvailability.IsAvailable(false, false)
                || !CosmeticAvailability.IsAvailable(false, true)
                || !CosmeticAvailability.IsAvailable(true, false))
            {
                failure = "Developer cosmetic availability must be an override, not a progression unlock.";
                return false;
            }

            bool writeOverride;
            bool writeMigration;
            bool cleanDevelopmentDefault = DeveloperDiagnosticsSettings.ResolveUnlockAllCosmeticsForLoad(
                true, false, false, out writeOverride, out writeMigration);
            if (!cleanDevelopmentDefault || !writeOverride || !writeMigration)
            {
                failure = "Development cosmetics must default on and persist the one-time migration.";
                return false;
            }

            bool oldSavedOffMigration = DeveloperDiagnosticsSettings.ResolveUnlockAllCosmeticsForLoad(
                true, false, false, out writeOverride, out writeMigration);
            if (!oldSavedOffMigration || !writeOverride || !writeMigration)
            {
                failure = "A pre-migration saved cosmetics override of OFF must migrate to ON once.";
                return false;
            }

            bool userOffAfterMigration = DeveloperDiagnosticsSettings.ResolveUnlockAllCosmeticsForLoad(
                true, true, false, out writeOverride, out writeMigration);
            if (userOffAfterMigration || writeOverride || writeMigration)
            {
                failure = "The cosmetics migration must not overwrite a later user choice of OFF.";
                return false;
            }

            bool releaseValue = DeveloperDiagnosticsSettings.ResolveUnlockAllCosmeticsForLoad(
                false, false, true, out writeOverride, out writeMigration);
            if (releaseValue || writeOverride || writeMigration)
            {
                failure = "The cosmetics override must remain unavailable outside editor/debug builds.";
                return false;
            }

            for (int index = 0; index < PlayerSkinLibrary.Count; index++)
            {
                if (!CosmeticAvailability.IsAvailable(false, true))
                {
                    failure = "Every registered skin must be effectively available under the developer override.";
                    return false;
                }
            }

            PlayerSkinSelection[] pending =
            {
                PlayerSkinSelection.FrostboundWanderer,
                PlayerSkinSelection.AshenMonarch,
                PlayerSkinSelection.FrostboundThornQueen,
                PlayerSkinSelection.CyberpunkStarlet,
                PlayerSkinSelection.OrnateRoseWitch
            };
            for (int index = 0; index < pending.Length; index++)
            {
                if (PlayerSkinLibrary.GetDefinition(pending[index]).Policy
                    != PlayerSkinLibrary.UnlockPolicy.RequirementToBeDetermined)
                {
                    failure = pending[index] + " must remain locked pending an approved requirement.";
                    return false;
                }
            }

            failure = null;
            return true;
        }
    }
}
