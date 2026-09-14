#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Editor-only dependency diagnostics for canonical enemy presentation.
    /// It forces the selected scene's database record to update before asking
    /// for direct and recursive dependencies, so stale import results cannot be
    /// mistaken for serialized references.
    /// </summary>
    public static class EnemyVisualDependencyAudit
    {
        public const string SprintFiveHazardsScene = "Assets/Cave/Scenes/Sprint5_Hazards.unity";

        [MenuItem("Tools/Cave/Enemies/Audit Legacy Goth Scene Dependencies", priority = 213)]
        public static void ForceReimportAndAuditSprintFiveHazards()
        {
            AssetDatabase.ImportAsset(SprintFiveHazardsScene, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ReportSceneDependencies(SprintFiveHazardsScene);
        }

        internal static bool ReportSceneDependencies(string scenePath)
        {
            string[] direct = AssetDatabase.GetDependencies(scenePath, false);
            string[] recursive = AssetDatabase.GetDependencies(scenePath, true);
            string directLegacy = FirstLegacyPath(direct);
            string recursiveLegacy = FirstLegacyPath(recursive);
            if (string.IsNullOrEmpty(recursiveLegacy))
            {
                Debug.Log("[Cave] Legacy dependency audit passed after ForceUpdate: " + scenePath + " has no Legacy, Goth_Normal_, or Goth_Corrupt_ dependency.");
                return true;
            }

            List<string> chain;
            bool foundChain = TryFindLegacyDependencyChain(scenePath, out chain);
            string relation = string.IsNullOrEmpty(directLegacy) ? "TRANSITIVE" : "DIRECT";
            string message = "[Cave] " + relation + " legacy presentation dependency after ForceUpdate | " + FormatChain(foundChain ? chain : null, scenePath, recursiveLegacy);
            Debug.LogError(message);
            LogSerializedOwners(recursiveLegacy);
            return false;
        }

        internal static bool TryFindLegacyDependencyChain(string rootPath, out List<string> chain)
        {
            chain = new List<string>();
            var parentByPath = new Dictionary<string, string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            parentByPath[rootPath] = null;
            pending.Enqueue(rootPath);
            while (pending.Count > 0)
            {
                string current = pending.Dequeue();
                string[] direct = AssetDatabase.GetDependencies(current, false);
                for (int index = 0; index < direct.Length; index++)
                {
                    string next = direct[index];
                    if (next == current || parentByPath.ContainsKey(next)) continue;
                    parentByPath[next] = current;
                    if (IsLegacyPresentationPath(next))
                    {
                        BuildChain(rootPath, next, parentByPath, chain);
                        return true;
                    }
                    pending.Enqueue(next);
                }
            }
            return false;
        }

        internal static bool IsLegacyPresentationPath(string path)
        {
            return path.IndexOf("/Legacy/", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("Goth_Normal_", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("Goth_Corrupt_", StringComparison.Ordinal) >= 0;
        }

        private static string FirstLegacyPath(string[] paths)
        {
            for (int index = 0; index < paths.Length; index++) if (IsLegacyPresentationPath(paths[index])) return paths[index];
            return null;
        }

        private static void BuildChain(string rootPath, string legacyPath, Dictionary<string, string> parentByPath, List<string> chain)
        {
            string current = legacyPath;
            while (!string.IsNullOrEmpty(current))
            {
                chain.Insert(0, current);
                if (current == rootPath) break;
                current = parentByPath[current];
            }
        }

        private static string FormatChain(List<string> chain, string rootPath, string legacyPath)
        {
            if (chain == null || chain.Count == 0) return rootPath + " -> " + legacyPath;
            return string.Join(" -> ", chain.ToArray());
        }

        private static void LogSerializedOwners(string legacyPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(legacyPath);
            if (string.IsNullOrEmpty(guid)) return;
            string[] files = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories);
            bool foundOwner = false;
            for (int index = 0; index < files.Length; index++)
            {
                string extension = Path.GetExtension(files[index]);
                if (extension != ".unity" && extension != ".prefab" && extension != ".asset" && extension != ".anim" && extension != ".controller") continue;
                string text = File.ReadAllText(files[index]);
                if (text.IndexOf(guid, StringComparison.Ordinal) < 0) continue;
                string assetPath = "Assets" + files[index].Substring(Application.dataPath.Length).Replace('\\', '/');
                Debug.LogError("[Cave] Serialized legacy GUID owner | " + assetPath + " -> " + legacyPath + " | guid=" + guid);
                foundOwner = true;
            }
            if (!foundOwner) Debug.Log("[Cave] No YAML serialized owner contains legacy GUID " + guid + "; the reported dependency is likely cached importer state.");
        }
    }
}
#endif
