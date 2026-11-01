#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cave.Editor.MobStaging
{
    internal static class ExternalMobStagingReturnImporter
    {
        public static void DryRunLatestReturnPatchForBatch()
        {
            string path = FindLatestReturnPatchPath();
            string report;
            if (!TryValidate(path, out report)) throw new InvalidOperationException("Mob staging return dry run blocked.\n" + report);
            Debug.Log("[Cave] Mob staging return dry run passed.\n" + report);
        }

        public static void ApplyLatestReturnPatchForBatch()
        {
            string path = FindLatestReturnPatchPath();
            string report;
            if (!TryValidate(path, out report)) throw new InvalidOperationException("Mob staging return apply blocked.\n" + report);

            MobReturnPatch patch = JsonUtility.FromJson<MobReturnPatch>(File.ReadAllText(path));
            Directory.CreateDirectory(MobStagingTransferPaths.ArchiveFolder);
            File.Copy(path, Path.Combine(MobStagingTransferPaths.ArchiveFolder, Path.GetFileName(path)), true);
            int nodesApplied = 0;
            for (int index = 0; index < patch.prefabPatches.Count; index++)
            {
                MobPrefabPatch prefabPatch = patch.prefabPatches[index];
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPatch.targetPrefabPath);
                try
                {
                    for (int nodeIndex = 0; nodeIndex < prefabPatch.changedNodes.Count; nodeIndex++)
                    {
                        ApplyNode(contents.transform, prefabPatch.changedNodes[nodeIndex]);
                        nodesApplied++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPatch.targetPrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Cave] Applied controlled staging return patch " + patch.packageId + ". Changed nodes: " + nodesApplied + ".");
        }

        private static string FindLatestReturnPatchPath()
        {
            if (!Directory.Exists(MobStagingTransferPaths.FromStagingFolder))
            {
                throw new InvalidOperationException("No mob-staging return folder exists.");
            }

            string[] paths = Directory.GetFiles(MobStagingTransferPaths.FromStagingFolder, "return-*.return.json", SearchOption.TopDirectoryOnly);
            if (paths.Length == 0) throw new InvalidOperationException("No controlled staging return patch exists.");
            Array.Sort(paths, StringComparer.Ordinal);
            return paths[paths.Length - 1];
        }

        // Validation-only probes: write no production assets and prove the importer keeps
        // protected paths and visual asset replacement review requirements fail-closed.
        public static void RunValidationSafetyProbes()
        {
            Directory.CreateDirectory(MobStagingTransferPaths.FromStagingFolder);
            var protectedPatch = CreateProbePatch("probe-protected-script");
            protectedPatch.prefabPatches.Add(new MobPrefabPatch
            {
                targetPrefabPath = "Assets/Cave/Scripts/Enemies/EnemyArchetype.cs",
                targetPrefabGuid = "not-a-prefab",
                changedNodes = new List<MobNodeState> { new MobNodeState { path = "$" } }
            });
            string protectedPath = Path.Combine(MobStagingTransferPaths.FromStagingFolder, "probe-protected-script.return.json");
            File.WriteAllText(protectedPath, JsonUtility.ToJson(protectedPatch, true));
            string protectedReport;
            bool protectedAccepted = TryValidate(protectedPath, out protectedReport);

            var referencePatch = CreateProbePatch("probe-visual-reference");
            referencePatch.reviewRequiredAssets.Add("Sprite replacement: validation-only");
            referencePatch.prefabPatches.Add(new MobPrefabPatch
            {
                targetPrefabPath = "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab",
                targetPrefabGuid = AssetDatabase.AssetPathToGUID("Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab")
            });
            string referencePath = Path.Combine(MobStagingTransferPaths.FromStagingFolder, "probe-visual-reference.return.json");
            File.WriteAllText(referencePath, JsonUtility.ToJson(referencePatch, true));
            string referenceReport;
            bool referenceAccepted = TryValidate(referencePath, out referenceReport);

            if (protectedAccepted || referenceAccepted) throw new InvalidOperationException("A validation-only mob staging safety probe was unexpectedly accepted.");
            Debug.Log("[Cave] Mob staging safety probes passed.\nProtected-path result:\n" + protectedReport
                + "\nVisual-reference result:\n" + referenceReport);
        }

        private static MobReturnPatch CreateProbePatch(string packageId)
        {
            return new MobReturnPatch
            {
                schemaVersion = MobStagingTransferPaths.SchemaVersion,
                packageId = packageId,
                direction = "STAGING_TO_MAIN",
                protectedAuthoritativePaths = new List<string>(MobStagingTransferPaths.ProtectedPaths)
            };
        }

        [MenuItem("Tools/Cave/Staging/Mobs/Validate Return Patch (Dry Run)", priority = 260)]
        private static void SelectAndDryRun()
        {
            string path = EditorUtility.OpenFilePanel("Select staging return patch", MobStagingTransferPaths.FromStagingFolder, "json");
            if (string.IsNullOrEmpty(path)) return;
            string report;
            bool valid = TryValidate(path, out report);
            Debug.Log("[Cave] Mob staging return dry run " + (valid ? "passed" : "blocked") + "\n" + report);
            EditorUtility.DisplayDialog("Mob staging return dry run", report, "OK");
        }

        [MenuItem("Tools/Cave/Staging/Mobs/Apply Return Patch (Validated)", priority = 261)]
        private static void SelectAndApply()
        {
            string path = EditorUtility.OpenFilePanel("Select staging return patch", MobStagingTransferPaths.FromStagingFolder, "json");
            if (string.IsNullOrEmpty(path)) return;
            string report;
            if (!TryValidate(path, out report))
            {
                Debug.LogError("[Cave] Mob staging return blocked.\n" + report);
                EditorUtility.DisplayDialog("Mob staging return blocked", report, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Apply controlled mob staging patch", report + "\n\nOnly approved serialized presentation/physics fields will be applied. Gameplay scripts and protected assets are excluded.", "Apply", "Cancel")) return;

            MobReturnPatch patch = JsonUtility.FromJson<MobReturnPatch>(File.ReadAllText(path));
            Directory.CreateDirectory(MobStagingTransferPaths.ArchiveFolder);
            File.Copy(path, Path.Combine(MobStagingTransferPaths.ArchiveFolder, Path.GetFileName(path)), true);
            int nodesApplied = 0;
            for (int index = 0; index < patch.prefabPatches.Count; index++)
            {
                MobPrefabPatch prefabPatch = patch.prefabPatches[index];
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPatch.targetPrefabPath);
                try
                {
                    for (int nodeIndex = 0; nodeIndex < prefabPatch.changedNodes.Count; nodeIndex++)
                    {
                        ApplyNode(contents.transform, prefabPatch.changedNodes[nodeIndex]);
                        nodesApplied++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPatch.targetPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Cave] Applied controlled staging return patch " + patch.packageId + ". Changed nodes: " + nodesApplied + ".");
        }

        internal static bool TryValidate(string patchPath, out string report)
        {
            var messages = new List<string>();
            if (!File.Exists(patchPath))
            {
                report = "Return patch does not exist: " + patchPath;
                return false;
            }
            MobReturnPatch patch;
            try { patch = JsonUtility.FromJson<MobReturnPatch>(File.ReadAllText(patchPath)); }
            catch (Exception exception)
            {
                report = "Return patch JSON could not be read: " + exception.Message;
                return false;
            }
            if (patch == null || patch.schemaVersion != MobStagingTransferPaths.SchemaVersion || patch.direction != "STAGING_TO_MAIN")
            {
                report = "Unsupported return patch schema or direction.";
                return false;
            }
            if (patch.prefabPatches == null || patch.prefabPatches.Count == 0)
            {
                report = "Return patch contains no approved prefab changes.";
                return false;
            }
            if (patch.reviewRequiredAssets != null && patch.reviewRequiredAssets.Count > 0)
            {
                messages.Add("FAIL CLOSED: review-required asset replacements are present:\n- " + string.Join("\n- ", patch.reviewRequiredAssets.ToArray()));
            }
            if (patch.protectedAuthoritativePaths == null || patch.protectedAuthoritativePaths.Count != MobStagingTransferPaths.ProtectedPaths.Length)
            {
                messages.Add("FAIL CLOSED: return patch has an incomplete protected-path contract.");
            }
            else
            {
                for (int index = 0; index < patch.protectedAuthoritativePaths.Count; index++)
                {
                    if (!MobStagingTransferPaths.ProtectedPaths.Contains(patch.protectedAuthoritativePaths[index]))
                    {
                        messages.Add("FAIL CLOSED: return patch altered the protected-path contract.");
                        break;
                    }
                }
            }
            int nodeCount = 0;
            for (int index = 0; index < patch.prefabPatches.Count; index++)
            {
                MobPrefabPatch prefabPatch = patch.prefabPatches[index];
                if (!MobStagingTransferPaths.IsProductionMobPath(prefabPatch.targetPrefabPath))
                {
                    messages.Add("FAIL CLOSED: target is not a production mob prefab: " + prefabPatch.targetPrefabPath);
                    continue;
                }
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPatch.targetPrefabPath);
                if (prefab == null)
                {
                    messages.Add("Missing target prefab: " + prefabPatch.targetPrefabPath);
                    continue;
                }
                if (AssetDatabase.AssetPathToGUID(prefabPatch.targetPrefabPath) != prefabPatch.targetPrefabGuid)
                {
                    messages.Add("FAIL CLOSED: target GUID no longer matches baseline: " + prefabPatch.targetPrefabPath);
                    continue;
                }
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPatch.targetPrefabPath);
                try
                {
                    for (int nodeIndex = 0; nodeIndex < prefabPatch.changedNodes.Count; nodeIndex++)
                    {
                        string nodeError;
                        if (!ValidateNode(contents.transform, prefabPatch.changedNodes[nodeIndex], out nodeError)) messages.Add(nodeError);
                        else nodeCount++;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            messages.Insert(0, "Patch: " + patch.packageId + "\nTarget prefabs: " + patch.prefabPatches.Count + "\nApproved changed nodes: " + nodeCount);
            report = string.Join("\n", messages.ToArray());
            return messages.Count == 1;
        }

        private static bool ValidateNode(Transform root, MobNodeState state, out string error)
        {
            error = null;
            Transform target = FindNode(root, state.path);
            if (target == null)
            {
                error = "FAIL CLOSED: prefab hierarchy no longer has node " + state.path;
                return false;
            }
            if (state.path == "$" &&
                (target.localPosition != state.localPosition || Quaternion.Angle(target.localRotation, state.localRotation) > 0.001f || target.localScale != state.localScale))
            {
                error = "FAIL CLOSED: root transforms are never staging-editable.";
                return false;
            }
            if (!ValidateComponentCounts(target, state, out error)) return false;
            return true;
        }

        private static bool ValidateComponentCounts(Transform target, MobNodeState state, out string error)
        {
            error = null;
            if (target.GetComponents<SpriteRenderer>().Length != state.sprites.Count || target.GetComponents<Animator>().Length != state.animators.Count || target.GetComponents<Collider2D>().Length != state.colliders.Count)
            {
                error = "FAIL CLOSED: component layout differs at " + state.path;
                return false;
            }
            Collider2D[] colliders = target.GetComponents<Collider2D>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index].GetType().FullName != state.colliders[index].type)
                {
                    error = "FAIL CLOSED: collider type differs at " + state.path;
                    return false;
                }
            }
            if (state.rigidbody != null && state.rigidbody.exists && target.GetComponent<Rigidbody2D>() == null)
            {
                error = "FAIL CLOSED: Rigidbody2D missing at " + state.path;
                return false;
            }
            return true;
        }

        private static void ApplyNode(Transform root, MobNodeState state)
        {
            Transform target = FindNode(root, state.path);
            if (state.path != "$")
            {
                target.localPosition = state.localPosition;
                target.localRotation = state.localRotation;
                target.localScale = state.localScale;
            }
            ApplySprites(target, state.sprites);
            ApplyAnimators(target, state.animators);
            ApplyColliders(target, state.colliders);
            ApplyRigidbody(target, state.rigidbody);
        }

        private static void ApplySprites(Transform target, List<MobSpriteState> states)
        {
            SpriteRenderer[] renderers = target.GetComponents<SpriteRenderer>();
            for (int index = 0; index < states.Count; index++)
            {
                MobSpriteState state = states[index];
                SpriteRenderer renderer = renderers[state.componentIndex];
                renderer.color = state.color;
                renderer.flipX = state.flipX;
                renderer.flipY = state.flipY;
                renderer.sortingLayerName = state.sortingLayerName;
                renderer.sortingOrder = state.sortingOrder;
                // A changed asset reference is review-required and cannot reach this method.
            }
        }

        private static void ApplyAnimators(Transform target, List<MobAnimatorState> states)
        {
            Animator[] animators = target.GetComponents<Animator>();
            for (int index = 0; index < states.Count; index++)
            {
                MobAnimatorState state = states[index];
                Animator animator = animators[state.componentIndex];
                animator.speed = state.speed;
                animator.applyRootMotion = state.applyRootMotion;
                animator.updateMode = (AnimatorUpdateMode)state.updateMode;
                animator.cullingMode = (AnimatorCullingMode)state.cullingMode;
            }
        }

        private static void ApplyColliders(Transform target, List<MobColliderState> states)
        {
            Collider2D[] colliders = target.GetComponents<Collider2D>();
            for (int index = 0; index < states.Count; index++)
            {
                MobColliderState state = states[index];
                Collider2D collider = colliders[state.componentIndex];
                collider.enabled = state.enabled;
                collider.isTrigger = state.isTrigger;
                collider.offset = state.offset;
                BoxCollider2D box = collider as BoxCollider2D;
                if (box != null) { box.size = state.size; box.edgeRadius = state.edgeRadius; continue; }
                CircleCollider2D circle = collider as CircleCollider2D;
                if (circle != null) { circle.radius = state.radius; continue; }
                CapsuleCollider2D capsule = collider as CapsuleCollider2D;
                if (capsule != null) { capsule.size = state.size; capsule.direction = (CapsuleDirection2D)state.direction; continue; }
                PolygonCollider2D polygon = collider as PolygonCollider2D;
                if (polygon != null) polygon.points = state.points.ToArray();
            }
        }

        private static void ApplyRigidbody(Transform target, MobRigidbodyState state)
        {
            if (state == null || !state.exists) return;
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            body.simulated = state.simulated;
            body.bodyType = (RigidbodyType2D)state.bodyType;
            body.mass = state.mass;
            body.drag = state.drag;
            body.angularDrag = state.angularDrag;
            body.gravityScale = state.gravityScale;
            body.collisionDetectionMode = (CollisionDetectionMode2D)state.collisionDetectionMode;
            body.interpolation = (RigidbodyInterpolation2D)state.interpolation;
            body.constraints = (RigidbodyConstraints2D)state.constraints;
        }

        internal static Transform FindNode(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path) || path == "$") return root;
            Transform current = root;
            string[] parts = path.Substring(2).Split('/');
            for (int index = 0; index < parts.Length; index++)
            {
                int open = parts[index].LastIndexOf('[');
                int close = parts[index].LastIndexOf(']');
                int sibling;
                if (open < 0 || close <= open || !int.TryParse(parts[index].Substring(open + 1, close - open - 1), out sibling) || sibling >= current.childCount) return null;
                Transform child = current.GetChild(sibling);
                string expectedName = parts[index].Substring(0, open);
                if (child.name != expectedName) return null;
                current = child;
            }
            return current;
        }
    }
}
#endif
