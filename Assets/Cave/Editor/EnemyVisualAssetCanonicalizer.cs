#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// One-time, Unity-safe migration for retired enemy visual generations.
    /// It only moves assets through AssetDatabase so their GUIDs are preserved;
    /// it never deletes source art or gameplay assets. The two dedicated
    /// processors remain the only producers of active Detective and Anti-Pyre
    /// runtime frames.
    /// </summary>
    public static class EnemyVisualAssetCanonicalizer
    {
        private const string GothRoot = "Assets/Cave/Art/Enemies/Goth";
        private const string GothSourceRoot = GothRoot + "/Source";
        private const string GothRuntimeRoot = GothRoot + "/Runtime";
        private const string GothLegacyRoot = GothRoot + "/Legacy";
        private const string DetectiveRoot = "Assets/Cave/Art/Enemies/Detective";
        private const string DetectiveRuntimeRoot = DetectiveRoot + "/Runtime";
        private const string DetectiveLegacyRoot = DetectiveRoot + "/Legacy";
        private const string GothVfxSourceRoot = "Assets/Cave/Art/VFX/Goth/Source";
        private const string GothVfxLegacyRoot = "Assets/Cave/Art/VFX/Goth/Legacy";

        [MenuItem("Tools/Cave/Enemies/Canonicalize Approved Enemy Visual Assets", priority = 210)]
        public static void CanonicalizeAndRebuild()
        {
            if (!ArchiveSupersededGothAssets() || !ArchiveSupersededDetectiveAssets()) return;

            EnsureAssetFolder(GothAssetProcessor.RegularRuntimeRoot);
            EnsureAssetFolder(GothAssetProcessor.CorruptRuntimeRoot);
            EnsureAssetFolder(DetectiveApprovedVisualProcessor.RegularRuntimeRoot);
            EnsureAssetFolder(DetectiveApprovedVisualProcessor.CorruptRuntimeRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            GothAssetProcessor.Build();
            DetectiveApprovedVisualProcessor.BuildApprovedDetectiveVisuals();
            ApprovedEnemyPresentationInstaller.BuildGothVisuals();

            bool outputsValid = ValidateRuntimeNames(GothAssetProcessor.RegularRuntimeRoot, "AntiPyre_Regular_") &&
                                ValidateRuntimeNames(GothAssetProcessor.CorruptRuntimeRoot, "AntiPyre_Corrupt_") &&
                                ValidateRuntimeNames(DetectiveApprovedVisualProcessor.RegularRuntimeRoot, "Detective_Regular_") &&
                                ValidateRuntimeNames(DetectiveApprovedVisualProcessor.CorruptRuntimeRoot, "Detective_Corrupt_");
            bool noLegacyReferences = ValidateNoLegacyPresentationReferences();
            AssetDatabase.SaveAssets();

            if (outputsValid && noLegacyReferences)
            {
                Debug.Log("[Cave] Canonical enemy visual migration completed. Active Runtime folders now contain only approved Anti-Pyre and Detective generations.");
            }
            else
            {
                Debug.LogError("[Cave] Canonical enemy visual migration stopped with validation errors. Legacy assets were preserved; inspect the earlier errors before using the rebuilt presentation.");
            }
        }

        private static bool ArchiveSupersededGothAssets()
        {
            bool valid = true;
            // The current clean actions become the approved source family. The
            // older two-page boards are retained for provenance under Legacy.
            valid &= MoveIfPresent(GothSourceRoot + "/CleanActions", GothSourceRoot + "/Approved", false);
            valid &= MoveIfPresent(GothSourceRoot + "/Original_Goth", GothLegacyRoot + "/OriginalGothPages", true);
            valid &= MoveIfPresent(GothSourceRoot + "/Corrupted_Goth", GothLegacyRoot + "/CorruptedGothPages", true);

            // Archive every ambiguous active runtime generation before the two
            // canonical folders are recreated and populated by GothAssetProcessor.
            valid &= MoveIfPresent(GothRuntimeRoot + "/Normal", GothLegacyRoot + "/OldGothNormal", true);
            valid &= MoveIfPresent(GothRuntimeRoot + "/Corrupt", GothLegacyRoot + "/OldGothCorrupt", true);
            valid &= MoveIfPresent(GothRuntimeRoot + "/AntiPyre", GothLegacyRoot + "/PreviousAntiPyreRuntime", true);

            // The old composite VFX board has no consumer after the clean action
            // processor was introduced. Preserve it outside active source paths.
            valid &= MoveIfPresent(GothVfxSourceRoot + "/GothAttackVfxSheet.png", GothVfxLegacyRoot + "/RetiredCompositeGothVfx.png", true);
            return valid;
        }

        private static bool ArchiveSupersededDetectiveAssets()
        {
            bool valid = true;
            // This older composite is not a canonical Detective source and its
            // importer is intentionally retired. Keep its GUID/history in Legacy.
            valid &= MoveIfPresent(DetectiveRoot + "/noir_detective_pixel_sprite_sheet.png",
                DetectiveLegacyRoot + "/RetiredCompositeNoirSheet.png", true);

            // Archive the pre-canonical generated frames only once. On subsequent
            // runs this source no longer exists, so freshly rebuilt output remains
            // in place and the command is idempotent.
            valid &= MoveIfFirstCanonicalization(DetectiveRuntimeRoot + "/Regular", DetectiveLegacyRoot + "/PreviousGeneratedRegular");
            valid &= MoveIfFirstCanonicalization(DetectiveRuntimeRoot + "/Corrupt", DetectiveLegacyRoot + "/PreviousGeneratedCorrupt");
            return valid;
        }

        private static bool MoveIfFirstCanonicalization(string sourcePath, string targetPath)
        {
            // The presence of the archived set is our one-time migration marker.
            // Do not archive the freshly generated canonical set on later runs.
            return AssetExists(targetPath) ? true : MoveIfPresent(sourcePath, targetPath, false);
        }

        private static bool MoveIfPresent(string sourcePath, string targetPath, bool uniqueTarget)
        {
            if (!AssetExists(sourcePath)) return true;
            string parent = Path.GetDirectoryName(targetPath).Replace('\\', '/');
            if (!EnsureAssetFolder(parent)) return false;
            string destination = targetPath;
            if (AssetExists(destination))
            {
                if (!uniqueTarget)
                {
                    Debug.LogError("[Cave] Enemy visual canonicalization stopped: destination already exists: " + destination);
                    return false;
                }

                destination = AssetDatabase.GenerateUniqueAssetPath(destination);
            }

            string error = AssetDatabase.MoveAsset(sourcePath, destination);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("[Cave] Enemy visual canonicalization could not move '" + sourcePath + "' to '" + destination + "': " + error);
                return false;
            }

            Debug.Log("[Cave] Archived superseded enemy visual asset: " + sourcePath + " -> " + destination);
            return true;
        }

        private static bool AssetExists(string path)
        {
            return AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null;
        }

        private static bool EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return true;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return false;
            if (!EnsureAssetFolder(parent)) return false;
            AssetDatabase.CreateFolder(parent, name);
            return AssetDatabase.IsValidFolder(path);
        }

        private static bool ValidateRuntimeNames(string folder, string requiredPrefix)
        {
            string[] ids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            if (ids.Length == 0)
            {
                Debug.LogError("[Cave] Canonical runtime validation failed: no generated sprites in " + folder);
                return false;
            }

            bool valid = true;
            for (int index = 0; index < ids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(ids[index]);
                string filename = Path.GetFileNameWithoutExtension(path);
                if (filename.StartsWith(requiredPrefix, StringComparison.Ordinal)) continue;
                Debug.LogError("[Cave] Canonical runtime validation failed: unexpected asset '" + path + "' in " + folder);
                valid = false;
            }
            return valid;
        }

        private static bool ValidateNoLegacyPresentationReferences()
        {
            string[] prefabIds = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Cave/Prefabs" });
            string[] sceneIds = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var assets = new List<string>(prefabIds.Length + sceneIds.Length);
            AddPaths(prefabIds, assets);
            AddPaths(sceneIds, assets);
            bool valid = true;
            for (int index = 0; index < assets.Count; index++)
            {
                List<string> chain;
                if (!EnemyVisualDependencyAudit.TryFindLegacyDependencyChain(assets[index], out chain)) continue;
                string relation = chain.Count <= 2 ? "DIRECT" : "TRANSITIVE";
                Debug.LogError("[Cave] " + relation + " legacy presentation dependency remains: " + string.Join(" -> ", chain.ToArray()));
                valid = false;
            }
            return valid;
        }

        private static void AddPaths(string[] ids, List<string> paths)
        {
            for (int index = 0; index < ids.Length; index++) paths.Add(AssetDatabase.GUIDToAssetPath(ids[index]));
        }
    }
}
#endif
