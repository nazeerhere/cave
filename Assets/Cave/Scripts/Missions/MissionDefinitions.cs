using System;
using Cave.World;
using UnityEngine;

namespace Cave.Missions
{
    public enum CaveGameMode { ClassicSweep, CorruptionPurge, CorruptBounty, Containment }
    public enum MissionLifecycleState { None, Setup, Active, PrimaryObjectiveComplete, Extraction, Success, Failure }

    [Serializable]
    public sealed class CaveModeDefinition
    {
        public CaveGameMode mode;
        public string displayName;
        [TextArea] public string description;
        [TextArea] public string objectiveSummary;
        public Sprite icon;
        public bool expectsExtraction;
    }

    [Serializable]
    public sealed class CaveMapDefinition
    {
        public string mapId;
        public string displayName;
        public string sceneName;
        [TextArea] public string description;
        public Sprite thumbnail;
        public int displayOrder;
        public bool initiallyUnlocked;
        [Min(0)] public int requiredWorldLevel;
        public string requiredProgressionFlag;

        public bool IsUnlocked
        {
            get
            {
                if (!string.IsNullOrEmpty(requiredProgressionFlag)
                    && PlayerPrefs.GetInt(requiredProgressionFlag, 0) == 0) return false;
                if (requiredWorldLevel > 0
                    && WorldDifficultyManager.CurrentDifficultyTier < requiredWorldLevel) return false;
                return initiallyUnlocked || requiredWorldLevel > 0 || !string.IsNullOrEmpty(requiredProgressionFlag);
            }
        }

        public string LockReason
        {
            get
            {
                if (IsUnlocked) return string.Empty;
                if (requiredWorldLevel > 0) return "REQUIRES WORLD LEVEL " + requiredWorldLevel;
                return "LOCKED";
            }
        }
    }

    [CreateAssetMenu(menuName = "Cave/Missions/Mission Catalog", fileName = "CaveMissionCatalog")]
    public sealed class CaveMissionCatalog : ScriptableObject
    {
        [SerializeField] private CaveModeDefinition[] modes;
        [SerializeField] private CaveMapDefinition[] maps;
        public CaveModeDefinition[] Modes => modes;
        public CaveMapDefinition[] Maps => maps;
        public void Configure(CaveModeDefinition[] modeDefinitions, CaveMapDefinition[] mapDefinitions)
        {
            modes = modeDefinitions;
            maps = mapDefinitions;
        }

        public CaveModeDefinition FindMode(CaveGameMode mode)
        {
            if (modes == null) return null;
            for (int i = 0; i < modes.Length; i++) if (modes[i] != null && modes[i].mode == mode) return modes[i];
            return null;
        }

        public CaveMapDefinition FindMap(string id)
        {
            if (maps == null) return null;
            for (int i = 0; i < maps.Length; i++) if (maps[i] != null && maps[i].mapId == id) return maps[i];
            return null;
        }

        public static CaveMissionCatalog CreateRuntimeDefaults()
        {
            CaveMissionCatalog catalog = CreateInstance<CaveMissionCatalog>();
            catalog.modes = new[]
            {
                Mode(CaveGameMode.ClassicSweep, "CLASSIC SWEEP", "Fight through the map's normal rounds and clear the final encounter.", false),
                Mode(CaveGameMode.CorruptionPurge, "CORRUPTION PURGE", "Locate and destroy spreading corruption sources before time expires.", true),
                Mode(CaveGameMode.CorruptBounty, "CORRUPT BOUNTY", "Hunt a heavily empowered corrupted target, claim its bounty, and escape.", true),
                Mode(CaveGameMode.Containment, "CONTAINMENT", "Defend a designated location against escalating enemy pressure.", false)
            };
            catalog.maps = new[]
            {
                Map("upper_cave", "UPPER CAVE", "01_UpperCave", true, 0),
                Map("stronghold", "STRONGHOLD", "02_Stronghold", false, 1),
                Map("hollow_districts", "HOLLOW DISTRICTS", "03_HollowDistricts", false, 2),
                Map("deep_veins", "DEEP VEINS", "04_DeepVeins", false, 3),
                Map("heart_chamber", "HEART CHAMBER", "05_HeartChamber", false, 4)
            };
            return catalog;
        }

        private static CaveModeDefinition Mode(CaveGameMode mode, string name, string objective, bool extraction)
        {
            return new CaveModeDefinition { mode = mode, displayName = name, description = objective, objectiveSummary = objective, expectsExtraction = extraction };
        }

        private static CaveMapDefinition Map(string id, string name, string scene, bool unlocked, int order)
        {
            return new CaveMapDefinition { mapId = id, displayName = name, sceneName = scene, description = "Mission area: " + name + ".", initiallyUnlocked = unlocked, displayOrder = order };
        }
    }
}
