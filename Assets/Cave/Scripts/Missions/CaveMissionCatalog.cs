using UnityEngine;

namespace Cave.Missions
{
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
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] != null && modes[i].mode == mode) return modes[i];
            }
            return null;
        }

        public CaveMapDefinition FindMap(string id)
        {
            if (maps == null) return null;
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && maps[i].mapId == id) return maps[i];
            }
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
                Map("upper_cave", "BREACH", "01_UpperCave", "Cross the fractured mining breach, fortified cavern approaches, and lower excavation works.", true, 0),
                Map("stronghold", "STRONGHOLD", "02_Stronghold", false, 1),
                Map("hollow_districts", "HOLLOW DISTRICTS", "03_HollowDistricts", false, 2),
                Map("deep_veins", "DEEP VEINS", "04_DeepVeins", false, 3),
                Map("heart_chamber", "HEART CHAMBER", "05_HeartChamber", false, 4)
            };
            return catalog;
        }

        private static CaveModeDefinition Mode(CaveGameMode mode, string name, string objective, bool extraction)
        {
            return new CaveModeDefinition
            {
                mode = mode,
                displayName = name,
                description = objective,
                objectiveSummary = objective,
                expectsExtraction = extraction
            };
        }

        private static CaveMapDefinition Map(string id, string name, string scene, bool unlocked, int order)
        {
            return Map(id, name, scene, "Mission area: " + name + ".", unlocked, order);
        }

        private static CaveMapDefinition Map(string id, string name, string scene, string description, bool unlocked, int order)
        {
            return new CaveMapDefinition
            {
                mapId = id,
                displayName = name,
                sceneName = scene,
                description = description,
                initiallyUnlocked = unlocked,
                displayOrder = order
            };
        }
    }
}
