#if UNITY_EDITOR
using System.IO;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Idempotently applies only the sprint's high-confidence role assignments.</summary>
    public static class StrategicCombatDeploymentInstaller
    {
        private const string EncounterPath = "Assets/Cave/Prefabs/Encounters/StrategicMixedEncounter.prefab";

        [MenuItem("Tools/Cave/Enemies/Deploy Strategic Combat Roles")]
        public static void Deploy()
        {
            EnsureComponent<EnemyTank>("Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab");
            EnsureComponent<EnemyTank>("Assets/Cave/Prefabs/Mobs/Melee/Corrupt Brute .prefab");
            EnsureComponent<EnemySupport>("Assets/Cave/Prefabs/Mobs/Support/DarkWizard.prefab");
            EnsureComponent<EnemySwarm>("Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab");
            BuildMixedEncounter();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Deployed Brute=Tank, Necromancer=Support, Lesser Skeleton=Swarm and rebuilt the mixed test encounter prefab.");
        }

        private static void EnsureComponent<T>(string path) where T : Component
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;
            if (root.GetComponent<T>() == null) root.AddComponent<T>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void BuildMixedEncounter()
        {
            EnsureFolder(Path.GetDirectoryName(EncounterPath));
            GameObject root = new GameObject("Strategic Mixed Encounter");
            Add(root.transform, "Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab", new Vector3(-3f, 0f));
            Add(root.transform, "Assets/Cave/Prefabs/Mobs/Support/DarkWizard.prefab", new Vector3(3.5f, 0f));
            Add(root.transform, "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab", new Vector3(1f, 0f));
            Add(root.transform, "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab", new Vector3(2f, 0f));
            Add(root.transform, "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab", new Vector3(3f, 0f));
            PrefabUtility.SaveAsPrefabAsset(root, EncounterPath);
            Object.DestroyImmediate(root);
        }

        private static void Add(Transform parent, string path, Vector3 localPosition)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
