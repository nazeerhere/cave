using System;
using System.Collections.Generic;
using Cave.Diagnostics;

namespace Cave.Player
{
    /// <summary>Pure catalogue contracts for the presentation-only player skin system.</summary>
    public static class PlayerSkinCosmeticsVerification
    {
        private static readonly Dictionary<string, int> ExpectedAnimationFrameCounts =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "Brace", 4 }, { "Cast", 4 }, { "Dash", 4 }, { "Death", 5 },
                { "Guard", 4 }, { "GuardBreak", 4 }, { "Heavy_1", 5 },
                { "Heavy_2", 5 }, { "Heavy_3", 5 }, { "Heavy_4", 5 },
                { "Heavy_5", 5 }, { "Heavy_Charge", 4 }, { "Hit", 4 },
                { "Idle", 4 }, { "Interact", 4 }, { "Jump", 5 }, { "Knockback", 5 },
                { "Landing", 4 }, { "Parry", 4 }, { "Run", 6 }, { "Spin", 6 }
            };

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

            if (!PlayerSkinCosmetics.IsAvailableForPolicy(PlayerSkinSelection.DefaultMiner, true, false)
                || PlayerSkinCosmetics.IsAvailableForPolicy(PlayerSkinSelection.DefaultMiner, false, false))
            {
                failure = "Default Miner availability must follow its real, always-unlocked state.";
                return false;
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

            if (MinerPlayerVisualAnimator.AlternateHeavyAnimationNameFor(true, 4) != "Heavy_Charge")
            {
                failure = "Alternate skins must use Heavy_Charge while the charged attack is charging.";
                return false;
            }

            for (int index = 0; index < 5; index++)
            {
                if (MinerPlayerVisualAnimator.AlternateHeavyAnimationNameFor(false, index)
                    != "Heavy_" + (index + 1))
                {
                    failure = "Alternate heavy attack visual source mapping is invalid for index " + index + ".";
                    return false;
                }
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

            bool releaseStoredValue = DeveloperDiagnosticsSettings.ResolveUnlockAllCosmeticsForLoad(
                false, false, true, out writeOverride, out writeMigration);
            if (!releaseStoredValue || writeOverride || writeMigration
                || DeveloperDiagnosticsSettings.IsCosmeticsOverrideEffective(false, releaseStoredValue))
            {
                failure = "A stored cosmetics override must remain inactive when development features are unavailable.";
                return false;
            }

            if (!DeveloperDiagnosticsSettings.IsFeatureAvailable(true, false, false)
                || !DeveloperDiagnosticsSettings.IsFeatureAvailable(false, true, false)
                || !DeveloperDiagnosticsSettings.IsFeatureAvailable(false, false, true)
                || DeveloperDiagnosticsSettings.IsFeatureAvailable(false, false, false)
                || !DeveloperDiagnosticsSettings.IsUnbrandedLocalDevelopmentBuild("DefaultCompany", "New Unity Project")
                || DeveloperDiagnosticsSettings.IsUnbrandedLocalDevelopmentBuild("Cave Studio", "Cave: Sovereign Depths"))
            {
                failure = "Developer feature policy does not distinguish editor, debug, local-development, and release environments.";
                return false;
            }

            for (int index = 0; index < PlayerSkinLibrary.Count; index++)
            {
                PlayerSkinSelection selection = (PlayerSkinSelection)index;
                PlayerSkinLibrary.Definition definition = PlayerSkinLibrary.GetDefinition(selection);
                if (!PlayerSkinCosmetics.IsAvailableForPolicy(selection, false, true))
                {
                    failure = selection + " must be effectively available through the live skin policy under the developer override.";
                    return false;
                }

                if (selection == PlayerSkinSelection.DefaultMiner) continue;
                if (string.IsNullOrEmpty(definition.ResourceRoot))
                {
                    failure = selection + " does not provide a skin resource root.";
                    return false;
                }

                foreach (KeyValuePair<string, int> expected in ExpectedAnimationFrameCounts)
                {
                    if (PlayerSkinLibrary.LoadFrames(selection, expected.Key).Length != expected.Value)
                    {
                        failure = selection + " has an invalid " + expected.Key + " resource sequence.";
                        return false;
                    }
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
