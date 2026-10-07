using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// Immutable presentation catalogue for player appearances.  It deliberately contains
    /// no combat, progression, or collider data: a skin is only a label, unlock policy and
    /// location of its artwork.
    /// </summary>
    public static class PlayerSkinLibrary
    {
        public enum UnlockPolicy
        {
            AlwaysAvailable,
            PlayerLevel15,
            SovereignGambitVictory,
            RequirementToBeDetermined
        }

        public readonly struct Definition
        {
            public Definition(PlayerSkinSelection selection, string displayName, string description,
                string resourceRoot, UnlockPolicy unlockPolicy, string unlockKey)
            {
                Selection = selection;
                DisplayName = displayName;
                Description = description;
                ResourceRoot = resourceRoot;
                Policy = unlockPolicy;
                UnlockKey = unlockKey;
            }

            public PlayerSkinSelection Selection { get; }
            public string DisplayName { get; }
            public string Description { get; }
            public string ResourceRoot { get; }
            public UnlockPolicy Policy { get; }
            public string UnlockKey { get; }
            public bool IsCosmeticOnly => true;
            public string UnlockDescription => Policy == UnlockPolicy.PlayerLevel15
                ? "REACH PLAYER LEVEL 15"
                : Policy == UnlockPolicy.SovereignGambitVictory
                    ? "BEAT GAMBIT MODE"
                    : Policy == UnlockPolicy.RequirementToBeDetermined
                        ? "REQUIREMENT TBD"
                        : "AVAILABLE";
        }

        private const string SkinResourceRoot = "Player/Skins/";
        private static readonly Definition[] Definitions =
        {
            new Definition(PlayerSkinSelection.DefaultMiner, "DEFAULT MINER",
                "The faithful explorer of the endless depths.", null, UnlockPolicy.AlwaysAvailable, null),
            new Definition(PlayerSkinSelection.IvorySovereign, "IVORY SOVEREIGN",
                "A ruler in silence, beneath the earth.", "IvorySovereign", UnlockPolicy.PlayerLevel15,
                "Cave.CosmeticSkin.IvorySovereign.Unlocked"),
            new Definition(PlayerSkinSelection.FrostboundWanderer, "FROSTBOUND WANDERER",
                "Drifts through the dark like winter itself.", "FrostboundWanderer", UnlockPolicy.RequirementToBeDetermined, null),
            new Definition(PlayerSkinSelection.CrimsonExile, "CRIMSON EXILE",
                "Blood remembers even in stone.", "CrimsonExile", UnlockPolicy.SovereignGambitVictory,
                "Cave.CosmeticSkin.CrimsonExile.Unlocked"),
            new Definition(PlayerSkinSelection.AshenMonarch, "ASHEN MONARCH",
                "A fallen crown still commands the cinders.", "AshenMonarch", UnlockPolicy.RequirementToBeDetermined, null),
            new Definition(PlayerSkinSelection.FrostboundThornQueen, "FROSTBOUND THORN QUEEN",
                "Feral regality, thorned by winter.", "FrostboundThornQueen", UnlockPolicy.RequirementToBeDetermined, null),
            new Definition(PlayerSkinSelection.CyberpunkStarlet, "CYBERPUNK STARLET",
                "A bright signal from a stranger future.", "CyberpunkStarlet", UnlockPolicy.RequirementToBeDetermined, null),
            new Definition(PlayerSkinSelection.OrnateRoseWitch, "ORNATE ROSE WITCH",
                "Gold filigree and roses beneath the stone.", "OrnateRoseWitch", UnlockPolicy.RequirementToBeDetermined, null)
        };

        private static readonly Dictionary<string, Sprite[]> CachedFrames =
            new Dictionary<string, Sprite[]>(StringComparer.Ordinal);

        public static int Count => Definitions.Length;

        public static Definition GetDefinition(PlayerSkinSelection selection)
        {
            int index = (int)selection;
            return index >= 0 && index < Definitions.Length ? Definitions[index] : Definitions[0];
        }

        public static bool IsRegistered(PlayerSkinSelection selection)
        {
            int index = (int)selection;
            return index >= 0 && index < Definitions.Length;
        }

        public static Sprite[] LoadFrames(PlayerSkinSelection selection, string animationName)
        {
            Definition definition = GetDefinition(selection);
            if (selection == PlayerSkinSelection.DefaultMiner || string.IsNullOrEmpty(definition.ResourceRoot)
                || string.IsNullOrEmpty(animationName))
            {
                return Array.Empty<Sprite>();
            }

            string path = SkinResourceRoot + definition.ResourceRoot + "/" + animationName;
            Sprite[] frames;
            if (CachedFrames.TryGetValue(path, out frames))
            {
                return frames;
            }

            frames = Resources.LoadAll<Sprite>(path);
            Array.Sort(frames, CompareFrameNames);
            CachedFrames[path] = frames;
            return frames;
        }

        public static Sprite LoadPreview(PlayerSkinSelection selection)
        {
            Sprite[] frames = LoadFrames(selection, "Idle");
            return frames.Length > 0 ? frames[0] : null;
        }

        private static int CompareFrameNames(Sprite left, Sprite right)
        {
            int leftNumber = TrailingNumber(left != null ? left.name : null);
            int rightNumber = TrailingNumber(right != null ? right.name : null);
            int byNumber = leftNumber.CompareTo(rightNumber);
            return byNumber != 0 ? byNumber : string.CompareOrdinal(left != null ? left.name : string.Empty,
                right != null ? right.name : string.Empty);
        }

        private static int TrailingNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return int.MinValue;
            int start = value.Length;
            while (start > 0 && char.IsDigit(value[start - 1])) start--;
            int number;
            return start < value.Length && int.TryParse(value.Substring(start), out number) ? number : int.MinValue;
        }
    }
}
