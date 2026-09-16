using Cave.Enemies;
using Cave.Missions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    /// <summary>Idempotently converts Breach's reference markers into reusable mission spawn authoring.</summary>
    internal static class BreachMissionSpawnAuthoringInstaller
    {
        private const string ScenePath = "Assets/Cave/Scenes/01_UpperCave.unity";

        [MenuItem("Tools/Cave/Missions/Author Breach Spawn Architecture")]
        public static void AuthorBreach()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject bootstrapObject = FindRequired("Mission Map Bootstrap");
            GameObject enemySpawns = FindRequired("EnemySpawns");
            Transform authoringRoot = FindOrCreate("Breach Mission Spawn Authoring", enemySpawns.transform).transform;

            EnemyEncounterZone entry = EnsureZone(authoringRoot, "Breach_Entry", new Vector2(-14f, -4.5f), 1, 2, false);
            EnemyEncounterZone alcove = EnsureZone(authoringRoot, "Breach_UpperAlcove", new Vector2(-18f, 4.6f), 1, 2, false);
            EnemyEncounterZone camp = EnsureZone(authoringRoot, "Breach_Camp", new Vector2(-5f, -2f), 1, 3, true);
            EnemyEncounterZone bridge = EnsureZone(authoringRoot, "Breach_Bridge", new Vector2(6f, -1f), 1, 2, false);
            EnemyEncounterZone fortress = EnsureZone(authoringRoot, "Breach_Fortress", new Vector2(16f, 0f), 1, 3, true);
            EnemyEncounterZone lowerMine = EnsureZone(authoringRoot, "Breach_LowerMine", new Vector2(19f, -4.5f), 1, 3, true);

            ConfigureSocket(FindOrCreate("Breach Entry Socket", entry.transform, new Vector2(-14f, -4.5f)), entry, EnemyMobSize.Normal, EnemyLocomotion.Ground, EnemyArchetype.Melee | EnemyArchetype.Ranged, MissionSpawnUsage.Normal | MissionSpawnUsage.Bounty, true, false);
            ConfigureSocket(FindRequired("Future Detective Alcove Spawn 01"), alcove, EnemyMobSize.Small, EnemyLocomotion.Ground, EnemyArchetype.Ranged, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty, true, true);
            ConfigureSocket(FindRequired("Future Detective Alcove Spawn 02"), alcove, EnemyMobSize.Small, EnemyLocomotion.Ground, EnemyArchetype.Ranged, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty, true, true);
            ConfigureSocket(FindOrCreate("Breach Camp Socket A", camp.transform, new Vector2(-6f, -2f)), camp, EnemyMobSize.Normal, EnemyLocomotion.Ground, EnemyArchetype.Melee | EnemyArchetype.Ranged, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty, true, true);
            ConfigureSocket(FindOrCreate("Breach Camp Socket B", camp.transform, new Vector2(-3f, -2f)), camp, EnemyMobSize.Normal, EnemyLocomotion.Ground, EnemyArchetype.Melee | EnemyArchetype.Ranged, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty, true, true);
            ConfigureSocket(FindOrCreate("Breach Bridge Socket", bridge.transform, new Vector2(6f, -1f)), bridge, EnemyMobSize.Normal, EnemyLocomotion.Ground, EnemyArchetype.Melee | EnemyArchetype.Ranged, MissionSpawnUsage.Normal, false, false);
            ConfigureSocket(FindOrCreate("Breach Fortress Socket", fortress.transform, new Vector2(16f, 0f)), fortress, EnemyMobSize.Normal, EnemyLocomotion.Ground, EnemyArchetype.Melee | EnemyArchetype.Ranged | EnemyArchetype.Tank, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty | MissionSpawnUsage.Containment, true, true);
            ConfigureSocket(FindRequired("Future Brute Mining Spawn 01"), lowerMine, EnemyMobSize.Large, EnemyLocomotion.Ground, EnemyArchetype.Melee | EnemyArchetype.Tank, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty | MissionSpawnUsage.Containment, true, true);
            ConfigureSocket(FindRequired("Future Brute Mining Spawn 02"), lowerMine, EnemyMobSize.Large, EnemyLocomotion.Ground, EnemyArchetype.Melee | EnemyArchetype.Tank, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty | MissionSpawnUsage.Containment, true, true);
            ConfigureSocket(FindRequired("Future Wizard Lead Spawn"), lowerMine, EnemyMobSize.Normal, EnemyLocomotion.Ground, EnemyArchetype.Ranged | EnemyArchetype.Support, MissionSpawnUsage.Normal | MissionSpawnUsage.CorruptionDefense | MissionSpawnUsage.Bounty | MissionSpawnUsage.Containment, true, true);

            MissionSpawnDirector director = bootstrapObject.GetComponent<MissionSpawnDirector>();
            if (director == null) director = bootstrapObject.AddComponent<MissionSpawnDirector>();
            director.Configure(
                new[] { entry, alcove, camp, bridge, fortress, lowerMine },
                CreateCatalog());

            EditorUtility.SetDirty(bootstrapObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Cave] Breach mission spawn authoring installed: six zones, converted legacy markers, and one MissionSpawnDirector.");
        }

        private static MissionEnemyCatalogEntry[] CreateCatalog()
        {
            return new[]
            {
                CreateEntry("Assets/Cave/Prefabs/Mobs/Melee/LightBandit Variant.prefab", EnemyArchetype.Melee, EnemyMobSize.Normal, 0, false, false),
                CreateEntry("Assets/Cave/Prefabs/Mobs/Range/Detective.prefab", EnemyArchetype.Ranged, EnemyMobSize.Small, 0, false, false),
                CreateEntry("Assets/Cave/Prefabs/Mobs/Range/Corrupt Detective.prefab", EnemyArchetype.Ranged, EnemyMobSize.Small, 0, true, true),
                CreateEntry("Assets/Cave/Prefabs/Mobs/Support/Wizard.prefab", EnemyArchetype.Ranged | EnemyArchetype.Support, EnemyMobSize.Normal, 4, false, false),
                CreateEntry("Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab", EnemyArchetype.Melee | EnemyArchetype.Tank, EnemyMobSize.Large, 5, false, false),
                CreateEntry("Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab", EnemyArchetype.Swarm, EnemyMobSize.Small, 6, false, false)
            };
        }

        private static MissionEnemyCatalogEntry CreateEntry(string path, EnemyArchetype roles, EnemyMobSize size, int level, bool bountyEligible, bool corruptedVariant)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.InvalidOperationException("Missing Breach mission enemy prefab: " + path);
            MissionEnemyCatalogEntry entry = new MissionEnemyCatalogEntry();
            entry.Configure(prefab, roles, size, EnemyLocomotion.Ground, level, bountyEligible, corruptedVariant);
            return entry;
        }

        private static EnemyEncounterZone EnsureZone(Transform root, string id, Vector2 position, int initialPopulation, int activeCap, bool reinforcement)
        {
            GameObject zoneObject = FindOrCreate(id, root, position);
            EnemyEncounterZone zone = zoneObject.GetComponent<EnemyEncounterZone>();
            if (zone == null) zone = zoneObject.AddComponent<EnemyEncounterZone>();
            zone.Configure(id, initialPopulation, activeCap, reinforcement);
            return zone;
        }

        private static void ConfigureSocket(GameObject socketObject, EnemyEncounterZone zone, EnemyMobSize size, EnemyLocomotion locomotion, EnemyArchetype roles, MissionSpawnUsage usage, bool bounty, bool reinforcement)
        {
            socketObject.transform.SetParent(zone.transform, true);
            EnemySpawnSocket socket = socketObject.GetComponent<EnemySpawnSocket>();
            if (socket == null) socket = socketObject.AddComponent<EnemySpawnSocket>();
            socket.Configure(zone, size, locomotion, roles, usage, bounty, reinforcement, 5f, true);
            EditorUtility.SetDirty(socketObject);
        }

        private static GameObject FindRequired(string objectName)
        {
            GameObject result = GameObject.Find(objectName);
            if (result == null) throw new System.InvalidOperationException("Breach authoring requires '" + objectName + "'.");
            return result;
        }

        private static GameObject FindOrCreate(string objectName, Transform parent, Vector2 position)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null) return existing.gameObject;
            GameObject created = new GameObject(objectName);
            created.transform.SetParent(parent, false);
            created.transform.position = position;
            return created;
        }

        private static GameObject FindOrCreate(string objectName, Transform parent)
        {
            return FindOrCreate(objectName, parent, Vector2.zero);
        }
    }
}
