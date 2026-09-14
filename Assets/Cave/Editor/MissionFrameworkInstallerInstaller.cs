#if UNITY_EDITOR
using System.Collections.Generic;
using Cave.Missions;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class MissionFrameworkInstaller
    {
        private const string CatalogPath = "Assets/Cave/Resources/Missions/CaveMissionCatalog.asset";
        private static readonly string[] ScenePaths =
        {
            "Assets/Cave/Scenes/00_StartingRoom.unity", "Assets/Cave/Scenes/01_UpperCave.unity",
            "Assets/Cave/Scenes/02_Stronghold.unity", "Assets/Cave/Scenes/03_HollowDistricts.unity",
            "Assets/Cave/Scenes/04_DeepVeins.unity", "Assets/Cave/Scenes/05_HeartChamber.unity"
        };

        [MenuItem("Tools/Cave/Missions/Install Mission Hub Framework")]
        public static void Install()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureCatalog();
            for (int i = 0; i < ScenePaths.Length; i++) AuthorScene(ScenePaths[i], i == 0);
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < ScenePaths.Length; i++) scenes.Add(new EditorBuildSettingsScene(ScenePaths[i], true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Installed mission hub framework for the start room and five canonical maps.");
        }

        private static void EnsureCatalog()
        {
            if (AssetDatabase.LoadAssetAtPath<CaveMissionCatalog>(CatalogPath) != null) return;
            EnsureFolder("Assets/Cave/Resources/Missions");
            CaveMissionCatalog defaults = CaveMissionCatalog.CreateRuntimeDefaults();
            defaults.name = "CaveMissionCatalog";
            AssetDatabase.CreateAsset(defaults, CatalogPath);
        }

        private static void AuthorScene(string path, bool hub)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (hub) AuthorHub(scene); else AuthorMap(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AuthorHub(Scene scene)
        {
            MissionTerminal existing = FindInScene<MissionTerminal>(scene);
            if (existing == null)
            {
                GameObject terminal = new GameObject("Expedition Mission Terminal");
                terminal.transform.position = new Vector3(0f, -1.2f, 0f);
                BoxCollider2D collider = terminal.AddComponent<BoxCollider2D>(); collider.size = new Vector2(2.2f, 2.4f); collider.isTrigger = true;
                terminal.AddComponent<MissionTerminal>();
            }
            LevelTransition legacy = FindInScene<LevelTransition>(scene);
            if (legacy != null) legacy.enabled = false;
        }

        private static void AuthorMap(Scene scene)
        {
            if (FindInScene<MissionMapBootstrap>(scene) == null) new GameObject("Mission Map Bootstrap").AddComponent<MissionMapBootstrap>();
            GameObject authoring = GameObject.Find("Mission Objective Authoring");
            if (authoring == null) authoring = new GameObject("Mission Objective Authoring");
            EnsureSocket<CorruptionSourceSocket>(authoring.transform, "Purge Socket A", new Vector3(-3f, -1f));
            EnsureSocket<CorruptionSourceSocket>(authoring.transform, "Purge Socket B", new Vector3(0f, -1f));
            EnsureSocket<CorruptionSourceSocket>(authoring.transform, "Purge Socket C", new Vector3(3f, -1f));
            EnsureSocket<ContainmentSocket>(authoring.transform, "Containment Socket", new Vector3(0f, -1f));
            GameObject extraction = EnsureSocket<ExtractionPoint>(authoring.transform, "Extraction Point", new Vector3(4f, -1f));
            CircleCollider2D trigger = extraction.GetComponent<CircleCollider2D>();
            if (trigger == null) trigger = extraction.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true; trigger.radius = .8f;
        }

        private static GameObject EnsureSocket<T>(Transform parent, string name, Vector3 position) where T : Component
        {
            Transform child = parent.Find(name);
            GameObject target = child != null ? child.gameObject : new GameObject(name);
            target.transform.SetParent(parent, true); target.transform.position = position;
            if (target.GetComponent<T>() == null) target.AddComponent<T>();
            return target;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true); if (found != null) return found;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash); string name = path.Substring(slash + 1);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
