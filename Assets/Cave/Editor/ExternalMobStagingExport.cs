#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Cave.Editor.MobStaging
{
    internal static class ExternalMobStagingExport
    {
        [MenuItem("Tools/Cave/Staging/Mobs/Export Selected Mobs To External Staging", priority = 250)]
        private static void ExportSelected()
        {
            List<string> roots = Selection.GetFiltered<GameObject>(SelectionMode.Assets)
                .Select(AssetDatabase.GetAssetPath)
                .Where(MobStagingTransferPaths.IsProductionMobPath)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (roots.Count == 0)
            {
                EditorUtility.DisplayDialog("Mob staging export", "Select one or more prefab assets under Assets/Cave/Prefabs/Mobs/.", "OK");
                return;
            }
            Export(roots, false);
        }

        [MenuItem("Tools/Cave/Staging/Mobs/Export All Existing Mob Prefabs To External Staging", priority = 252)]
        private static void ExportAllExisting()
        {
            try
            {
                ExportEach(FindAllProductionMobPrefabs(), false);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Cave] External all-mob staging export blocked: " + exception.Message);
            }
        }

        [MenuItem("Tools/Cave/Staging/Mobs/Validate All Existing Mob Prefabs Export (Dry Run)", priority = 253)]
        private static void ValidateAllExisting()
        {
            List<string> roots = FindAllProductionMobPrefabs();
            var reports = new List<string>();
            bool valid = true;
            for (int index = 0; index < roots.Count; index++)
            {
                string report;
                bool rootValid = TryBuildManifest(new List<string> { roots[index] }, null, out report);
                valid &= rootValid;
                reports.Add((rootValid ? "PASS " : "FAIL ") + roots[index] + "\n" + report);
            }
            Debug.Log("[Cave] External all-mob staging export dry run " + (valid ? "passed" : "failed") + "\nPrefabs: " + roots.Count + "\n" + string.Join("\n\n", reports.ToArray()));
        }

        [MenuItem("Tools/Cave/Staging/Mobs/Validate Selected Mob Export (Dry Run)", priority = 251)]
        private static void ValidateSelected()
        {
            List<string> roots = Selection.GetFiltered<GameObject>(SelectionMode.Assets)
                .Select(AssetDatabase.GetAssetPath)
                .Where(MobStagingTransferPaths.IsProductionMobPath)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            string report;
            bool valid = TryBuildManifest(roots, null, out report);
            Debug.Log("[Cave] External mob staging export dry run " + (valid ? "passed" : "failed") + "\n" + report);
        }

        // Retained for the original focused validation path. All production mob prefabs are
        // exported through ExportAllExisting; no specific mob is otherwise special-cased.
        public static void ExportSkeletonValidationPackage()
        {
            Export(new List<string> { "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab" }, false);
        }

        // Batch-compatible seam for a project that is not already open in another Unity
        // editor. One package is emitted per prefab, so staging can accept new mobs while
        // fail-closing only the individual prefab that has unreturned staging changes.
        public static void ExportAllExistingMobPrefabsForBatch()
        {
            ExportEach(FindAllProductionMobPrefabs(), false);
        }

        internal static bool TryBuildManifest(List<string> roots, string packageFileName, out string report)
        {
            report = string.Empty;
            if (roots == null || roots.Count == 0)
            {
                report = "No selected mob roots.";
                return false;
            }
            for (int index = 0; index < roots.Count; index++)
            {
                string root = roots[index];
                if (!MobStagingTransferPaths.IsProductionMobPath(root) || AssetDatabase.LoadAssetAtPath<GameObject>(root) == null)
                {
                    report = "Invalid production mob root: " + root;
                    return false;
                }
            }
            var assets = CollectAssets(roots);
            if (assets.Count == 0)
            {
                report = "Dependency closure was empty.";
                return false;
            }
            var missing = new List<string>();
            for (int index = 0; index < roots.Count; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(roots[index]);
                foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) missing.Add(roots[index]);
                }
            }
            if (missing.Count > 0)
            {
                report = "Missing MonoBehaviours in: " + string.Join(", ", missing.Distinct().ToArray())
                    + " (null component count: " + missing.Count + ").";
                return false;
            }
            report = "Roots: " + roots.Count + "\nDependency assets: " + assets.Count + "\nMirrored scripts: " + assets.Count(IsMirroredScript);
            return true;
        }

        private static void Export(List<string> roots, bool dryRun)
        {
            string validation;
            if (!TryBuildManifest(roots, null, out validation))
            {
                Debug.LogError("[Cave] External mob staging export blocked: " + validation);
                return;
            }
            if (dryRun)
            {
                Debug.Log("[Cave] External mob staging export dry run passed.\n" + validation);
                return;
            }

            EnsureExchangeFolders();
            string packageId = CreatePackageId(roots);
            string packageFileName = packageId + ".unitypackage";
            string packagePath = Path.Combine(MobStagingTransferPaths.ToStagingFolder, packageFileName);
            List<string> assets = CollectAssets(roots);
            var manifest = BuildManifest(packageId, packageFileName, roots, assets);
            string manifestPath = Path.Combine(MobStagingTransferPaths.ToStagingFolder, packageId + ".manifest.json");

            AssetDatabase.ExportPackage(assets.ToArray(), packagePath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            File.Copy(manifestPath, Path.Combine(MobStagingTransferPaths.ManifestFolder, Path.GetFileName(manifestPath)), true);
            Debug.Log("[Cave] External mob package created.\nPackage: " + packagePath + "\nManifest: " + manifestPath
                + "\n" + validation);
        }

        private static void ExportEach(List<string> roots, bool dryRun)
        {
            if (roots == null || roots.Count == 0) throw new InvalidOperationException("No production mob prefabs were found under " + MobStagingTransferPaths.ProductionMobRoot);
            for (int index = 0; index < roots.Count; index++) Export(new List<string> { roots[index] }, dryRun);
        }

        private static List<string> FindAllProductionMobPrefabs()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { MobStagingTransferPaths.ProductionMobRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Where(MobStagingTransferPaths.IsProductionMobPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static string CreatePackageId(List<string> roots)
        {
            string suffix = roots.Count == 1
                ? Path.GetFileNameWithoutExtension(roots[0]).Replace(' ', '-').Replace(".", string.Empty)
                : "selection-" + roots.Count;
            return "mob-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + suffix;
        }

        private static List<string> CollectAssets(List<string> roots)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < roots.Count; index++)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(roots[index], true)) result.Add(dependency);
            }
            // Script references are serialized assets, but C# compile dependencies are not represented in GetDependencies.
            // Mirror the Cave runtime source tree read-only so imported Mob components deserialize and compile consistently.
            foreach (string script in Directory.GetFiles(Application.dataPath + "/Cave/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                result.Add("Assets" + script.Substring(Application.dataPath.Length).Replace('\\', '/'));
            }
            // Unity's asset dependency graph records MonoScripts but not every sibling source
            // file required to compile a third-party script. Mirror direct runtime siblings for
            // each dependency script (for example CFXR_Effect.CameraShake.cs beside CFXR_Effect.cs).
            string[] dependencyScripts = result.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();
            for (int index = 0; index < dependencyScripts.Length; index++)
            {
                string directory = Path.GetDirectoryName(dependencyScripts[index]);
                if (string.IsNullOrEmpty(directory) || directory.IndexOf("/Editor", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string absoluteDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, directory);
                if (!Directory.Exists(absoluteDirectory)) continue;
                foreach (string sibling in Directory.GetFiles(absoluteDirectory, "*.cs", SearchOption.TopDirectoryOnly))
                {
                    result.Add("Assets" + sibling.Substring(Application.dataPath.Length).Replace('\\', '/'));
                }
            }
            return result.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        private static MobTransferManifest BuildManifest(string packageId, string packageFileName, List<string> roots, List<string> assets)
        {
            var manifest = new MobTransferManifest
            {
                schemaVersion = MobStagingTransferPaths.SchemaVersion,
                packageId = packageId,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                direction = "MAIN_TO_STAGING",
                sourceProject = "New Unity Project",
                destinationProject = "Cave_Scene_Staging",
                sourceGitBranch = ReadGit("branch --show-current"),
                sourceGitHead = ReadGit("rev-parse HEAD"),
                packageFileName = packageFileName,
                prefabRoots = new List<string>(roots),
                protectedAuthoritativePaths = new List<string>(MobStagingTransferPaths.ProtectedPaths),
                stagingEditablePolicy = new List<string>(MobStagingTransferPaths.EditablePolicy)
            };
            for (int index = 0; index < assets.Count; index++)
            {
                string path = assets[index];
                manifest.includedAssets.Add(new MobTransferAsset
                {
                    path = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    type = AssetDatabase.GetMainAssetTypeAtPath(path) == null ? "Unknown" : AssetDatabase.GetMainAssetTypeAtPath(path).FullName,
                    mirroredReadOnly = IsMirroredScript(path)
                });
            }
            return manifest;
        }

        private static bool IsMirroredScript(string path)
        {
            return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadGit(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();
                    return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd().Trim() : "unavailable";
                }
            }
            catch { return "unavailable"; }
        }

        private static void EnsureExchangeFolders()
        {
            Directory.CreateDirectory(MobStagingTransferPaths.ToStagingFolder);
            Directory.CreateDirectory(MobStagingTransferPaths.FromStagingFolder);
            Directory.CreateDirectory(MobStagingTransferPaths.ArchiveFolder);
            Directory.CreateDirectory(MobStagingTransferPaths.ManifestFolder);
        }
    }
}
#endif
