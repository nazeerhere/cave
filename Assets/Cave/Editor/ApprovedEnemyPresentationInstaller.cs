#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Binds approved sprite frames to the existing enemy prefabs. This tool
    /// only changes visual renderers and adds the presentation component; it
    /// never modifies colliders, rigidbodies, layers, AI, or combat settings.
    /// </summary>
    public static class ApprovedEnemyPresentationInstaller
    {
        private const string MaterialPath = "Assets/Cave/Materials/EnemySpriteBlackKey.mat";
        private const string VisualName = "Approved Enemy Sheet Visual";
        private const string GothVisualName = "Approved Goth Visual";
        private const string GothPrefabPath = "Assets/Cave/Prefabs/Mobs/Range/Goth .prefab";
        private const string CorruptGothPrefabPath = "Assets/Cave/Prefabs/Mobs/Range/Corrupt Goth .prefab";

        [MenuItem("Tools/Cave/Enemies/Rebuild Approved Pyre + Brute Visuals", priority = 210)]
        public static void RebuildApprovedPyreAndBruteVisuals()
        {
            GothAssetProcessor.Build();
            ApprovedBruteAssetProcessor.Build();
            BuildGothVisuals();
            Install();
            Debug.Log("[Cave] Rebuilt and bound the corrected Pyre and approved Brute presentation sets.");
        }

        [MenuItem("Tools/Cave/Enemies/Install Approved Enemy Presentation")]
        public static void Install()
        {
            Sprite[] bruteIdle = FramesInFolder(ApprovedBruteAssetProcessor.RegularRuntimeRoot, "Brute_Regular_Idle_");
            Sprite[] corruptIdle = FramesInFolder(ApprovedBruteAssetProcessor.CorruptRuntimeRoot, "Brute_Corrupt_Idle_");
            if (bruteIdle.Length == 0 || corruptIdle.Length == 0)
            {
                Debug.LogError("[Cave] Enemy presentation install stopped: import the approved enemy sheets first.");
                return;
            }

            InstallBrute("Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab", false, null);
            InstallBrute("Assets/Cave/Prefabs/Mobs/Melee/Corrupt Brute .prefab", true, null);
            if (FramesInFolder(ApprovedTrollAssetProcessor.RegularRuntimeRoot, "Troll_Regular_Idle_").Length > 0
                && FramesInFolder(ApprovedTrollAssetProcessor.CorruptRuntimeRoot, "Troll_Corrupt_Idle_").Length > 0)
            {
                InstallTroll("Assets/Cave/Prefabs/Mobs/Tank/Troll.prefab", false);
                InstallTroll("Assets/Cave/Prefabs/Mobs/Tank/Corrupt Troll.prefab", true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Installed the approved Brute and Corrupt Brute visual presentation. Detective uses Tools/Cave/Enemies/Build Approved Detective Visuals exclusively.");
        }

        [MenuItem("Tools/Cave/Enemies/Rebuild + Install Approved Troll Visuals", priority = 215)]
        public static void RebuildAndInstallTrollVisuals()
        {
            ApprovedTrollAssetProcessor.Build();
            InstallTroll("Assets/Cave/Prefabs/Mobs/Tank/Troll.prefab", false);
            InstallTroll("Assets/Cave/Prefabs/Mobs/Tank/Corrupt Troll.prefab", true);
            BindApprovedWeapon("Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab", "Brute_Regular_Pickaxe", "pickaxe");
            BindApprovedWeapon("Assets/Cave/Prefabs/Mobs/Melee/Corrupt Brute .prefab", "Brute_Corrupt_Pickaxe", "pickaxe");
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Installed approved Troll bodies and approved Brute/Troll weapon presentation.");
        }

        [MenuItem("Tools/Cave/Enemies/Bind Approved Brute Hook Presentation", priority = 216)]
        public static void BindApprovedBruteHookPresentation()
        {
            ApprovedBruteAssetProcessor.ConfigureApprovedHookImports();
            BindBruteHook("Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab", ApprovedBruteAssetProcessor.RegularHookPath);
            BindBruteHook("Assets/Cave/Prefabs/Mobs/Melee/Corrupt Brute .prefab", ApprovedBruteAssetProcessor.CorruptHookPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Bound the approved Regular and Corrupt Brute hook presentation sprites.");
        }

        [MenuItem("Tools/Cave/Enemies/Build Goth Visuals")]
        public static void BuildGothVisuals()
        {
            // Runtime crops are generated separately. This command deliberately only
            // binds the already-approved Anti-Pyre sprites to the prefabs.
            if (!HasCanonicalAntiPyreSprites())
            {
                Debug.LogError("[Cave] Goth visual bind stopped: expected generated Anti-Pyre runtime sprites are missing. Run the asset generation step first, then bind visuals.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(GothPrefabPath) == null)
            {
                Debug.LogError("[Cave] Goth build stopped: normal Goth prefab is missing.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(CorruptGothPrefabPath) == null)
            {
                GameObject source = PrefabUtility.LoadPrefabContents(GothPrefabPath);
                try { PrefabUtility.SaveAsPrefabAsset(source, CorruptGothPrefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(source); }
            }

            InstallGoth(GothPrefabPath, false);
            InstallGoth(CorruptGothPrefabPath, true);
            BindGothCorruption();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built approved normal and corrupted Goth visuals and artillery bindings.");
        }

        private static void InstallGoth(string prefabPath, bool corrupt)
        {
            string form = corrupt ? "Corrupt" : "Regular";
            string folder = GothAssetProcessor.RuntimeRootFor(form);
            Sprite[] idle = FramesInFolder(folder, "AntiPyre_" + form + "_Idle_");
            Sprite[] move = FramesInFolder(folder, "AntiPyre_" + form + "_" + (corrupt ? "Move_" : "Walk_"));
            Sprite[] fireball = FramesInFolder(folder, "AntiPyre_" + form + "_" + (corrupt ? "FireballCastVolley_" : "FireballCast_"));
            Sprite[] charge = corrupt ? Empty() : FramesInFolder(folder, "AntiPyre_Regular_LaserCharge_");
            Sprite[] release = corrupt ? Empty() : FramesInFolder(folder, "AntiPyre_Regular_LaserRelease_");
            Sprite[] hurt = corrupt ? Empty() : FramesInFolder(folder, "AntiPyre_Regular_Hurt_");
            Sprite[] death = FramesInFolder(folder, "AntiPyre_" + form + "_Death_");
            Sprite[] burst = corrupt ? FramesInFolder(folder, "AntiPyre_Corrupt_AntimatterBurst_") : Empty();
            Sprite[] focus = corrupt ? FramesInFolder(folder, "AntiPyre_Corrupt_FocusOrbCast_") : Empty();
            Sprite[] beam = corrupt ? FramesInFolder(folder, "AntiPyre_Corrupt_AntimatterBeam_") : Empty();
            Sprite[] ritual = corrupt ? FramesInFolder(folder, "AntiPyre_Corrupt_MeteorRitual_") : Empty();
            if (idle.Length == 0 || move.Length == 0 || fireball.Length == 0 || death.Length == 0 ||
                (!corrupt && (charge.Length == 0 || release.Length == 0 || hurt.Length == 0)) ||
                (corrupt && (burst.Length == 0 || focus.Length == 0 || beam.Length == 0 || ritual.Length == 0)))
            {
                Debug.LogError("[Cave] Goth build stopped: runtime crops are missing. Re-run Build Goth Visuals after Assets Refresh.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer legacy = root.GetComponent<SpriteRenderer>();
                if (legacy == null) { Debug.LogError("[Cave] Goth prefab has no root SpriteRenderer: " + prefabPath); return; }
                Transform visualTransform = root.transform.Find(GothVisualName);
                if (visualTransform == null)
                {
                    GameObject visual = new GameObject(GothVisualName);
                    visualTransform = visual.transform; visualTransform.SetParent(root.transform, false);
                }
                visualTransform.localPosition = Vector3.zero; visualTransform.localRotation = Quaternion.identity; visualTransform.localScale = Vector3.one;
                SpriteRenderer renderer = visualTransform.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = legacy.sortingLayerID; renderer.sortingOrder = legacy.sortingOrder; renderer.sprite = idle[0]; renderer.sharedMaterial = null; renderer.enabled = true;
                legacy.sprite = null;
                legacy.enabled = false;

                GothVfxPlayback vfx = root.GetComponent<GothVfxPlayback>();
                if (vfx == null) vfx = root.AddComponent<GothVfxPlayback>();
                GothArtilleryAbilities abilities = root.GetComponent<GothArtilleryAbilities>();
                if (abilities == null) abilities = root.AddComponent<GothArtilleryAbilities>();
                GothArtilleryBrain brain = root.GetComponent<GothArtilleryBrain>();
                if (brain == null) brain = root.AddComponent<GothArtilleryBrain>();
                if (root.GetComponent<EnemySkillEvolution>() == null) root.AddComponent<EnemySkillEvolution>();
                if (root.GetComponent<EnemyCorruptionLifecycle>() == null) root.AddComponent<EnemyCorruptionLifecycle>();

                GothVisualAnimator animator = root.GetComponent<GothVisualAnimator>();
                if (animator == null) animator = root.AddComponent<GothVisualAnimator>();
                animator.Configure(renderer, legacy, corrupt, idle, move, fireball, charge, release, hurt, death, burst, focus, beam, ritual);
                GameObject bombObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cave/Prefabs/Projectiles/Bomb.prefab");
                GameObject warningObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cave/Prefabs/BombWarning.prefab");
                abilities.Configure(root.GetComponent<EnemyShooter>(), vfx, root.transform,
                    bombObject != null ? bombObject.GetComponent<Cave.Hazards.BombProjectile>() : null,
                    warningObject != null ? warningObject.GetComponent<Cave.Hazards.HazardWarning>() : null,
                    root.transform,
                    GothVfxFrames("Fireball"),
                    GothVfxFrames("FireballSequence"),
                    GothVfxFrames("MeteorFireball"),
                    Take(GothVfxFrames("AntimatterBurst"), 3),
                    GothVfxFrames("AntimatterBurst"),
                    Take(GothVfxFrames("AntimatterBeam"), 4),
                    Range(GothVfxFrames("AntimatterBeam"), 4, 4),
                    Range(GothVfxFrames("AntimatterBeam"), 8, 4),
                    GothVfxFrames("FocusOrbTravel"),
                    GothVfxFrames("FocusOrbCharge"),
                    GothVfxFrames("MeteorStormCircle"));
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static bool HasCanonicalAntiPyreSprites()
        {
            return HasFrames("Regular", "Idle_") &&
                   HasFrames("Regular", "Walk_") &&
                   HasFrames("Regular", "FireballCast_") &&
                   HasFrames("Regular", "LaserCharge_") &&
                   HasFrames("Regular", "LaserRelease_") &&
                   HasFrames("Regular", "Hurt_") &&
                   HasFrames("Regular", "Death_") &&
                   HasFrames("Corrupt", "Idle_") &&
                   HasFrames("Corrupt", "Move_") &&
                   HasFrames("Corrupt", "FireballCastVolley_") &&
                   HasFrames("Corrupt", "AntimatterBurst_") &&
                   HasFrames("Corrupt", "FocusOrbCast_") &&
                   HasFrames("Corrupt", "AntimatterBeam_") &&
                   HasFrames("Corrupt", "MeteorRitual_") &&
                   HasFrames("Corrupt", "Death_");
        }

        private static bool HasFrames(string form, string action)
        {
            string prefix = "AntiPyre_" + form + "_" + action;
            return FramesInFolder(GothAssetProcessor.RuntimeRootFor(form), prefix).Length > 0;
        }

        private static void BindGothCorruption()
        {
            GameObject replacement = AssetDatabase.LoadAssetAtPath<GameObject>(CorruptGothPrefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(GothPrefabPath);
            try
            {
                AuthoredCorruptedPrefabBinding binding = root.GetComponent<AuthoredCorruptedPrefabBinding>();
                if (binding == null) binding = root.AddComponent<AuthoredCorruptedPrefabBinding>();
                SerializedObject serialized = new SerializedObject(binding);
                serialized.FindProperty("authoredCorruptedPrefab").objectReferenceValue = replacement;
                serialized.FindProperty("isBossMapping").boolValue = false;
                serialized.FindProperty("grantGeneralShardOnFinalDeath").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GothPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Sprite[] FramesInFolder(string folder, string prefix)
        {
            string[] ids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var frames = new List<Sprite>();
            for (int index = 0; index < ids.Length; index++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(ids[index]));
                if (sprite != null && sprite.name.StartsWith(prefix, StringComparison.Ordinal)) frames.Add(sprite);
            }
            frames.Sort((left, right) => string.CompareOrdinal(left.name, right.name)); return frames.ToArray();
        }

        private static Sprite[] GothVfxFrames(string action)
        {
            return FramesInFolder(GothAssetProcessor.VfxRuntimeRoot + "/" + action, "AntiPyre_Vfx_" + action + "_");
        }

        private static Sprite[] Take(Sprite[] source, int count) => Range(source, 0, count);
        private static Sprite[] Range(Sprite[] source, int start, int count)
        {
            if (source == null || start >= source.Length || count <= 0) return Empty();
            int length = Mathf.Min(count, source.Length - start); Sprite[] result = new Sprite[length];
            Array.Copy(source, start, result, 0, length); return result;
        }

        private static void InstallBrute(string prefabPath, bool corrupt, Material material)
        {
            string form = corrupt ? "Corrupt" : "Regular";
            string prefix = "Brute_" + form + "_";
            string folder = ApprovedBruteAssetProcessor.RuntimeRootFor(corrupt);
            ConfigurePrefab(prefabPath,
                corrupt ? ApprovedEnemySheetAnimator.VisualRole.CorruptBrute : ApprovedEnemySheetAnimator.VisualRole.Brute,
                material,
                FramesInFolder(folder, prefix + "Idle_"),
                FramesInFolder(folder, prefix + "Walk_"),
                FramesInFolder(folder, prefix + "Walk_"),
                FramesInFolder(folder, prefix + "Light_"),
                FramesInFolder(folder, prefix + "Heavy_"),
                FramesInFolder(folder, prefix + "Grab_"),
                Empty(),
                FramesInFolder(folder, prefix + "Block_"),
                FramesInFolder(folder, prefix + "Hurt_"),
                FramesInFolder(folder, prefix + "Death_"));
            BindApprovedWeapon(prefabPath, corrupt ? "Brute_Corrupt_Pickaxe" : "Brute_Regular_Pickaxe", "pickaxe");
            BindBruteHook(prefabPath, corrupt ? ApprovedBruteAssetProcessor.CorruptHookPath : ApprovedBruteAssetProcessor.RegularHookPath);
        }

        private static void BindBruteHook(string prefabPath, string hookPath)
        {
            Sprite hook = AssetDatabase.LoadAssetAtPath<Sprite>(hookPath);
            if (hook == null)
            {
                Debug.LogError("[Cave] Brute hook bind stopped; approved hook sprite is missing or not yet imported: " + hookPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                BruteControlAbilities abilities = root.GetComponent<BruteControlAbilities>();
                if (abilities == null)
                {
                    Debug.LogError("[Cave] Brute hook bind stopped; BruteControlAbilities is missing: " + prefabPath);
                    return;
                }

                SerializedObject serialized = new SerializedObject(abilities);
                serialized.FindProperty("chainPickaxeSprite").objectReferenceValue = hook;
                serialized.FindProperty("chainHookSpriteAngleOffset").floatValue = -45f;
                serialized.FindProperty("chainHookSpriteScale").floatValue = 0.55f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallTroll(string prefabPath, bool corrupt)
        {
            string form = corrupt ? "Corrupt" : "Regular";
            string prefix = "Troll_" + form + "_";
            string folder = ApprovedTrollAssetProcessor.RuntimeRootFor(corrupt);
            Sprite[] idle = FramesInFolder(folder, prefix + "Idle_");
            if (idle.Length == 0)
            {
                Debug.LogError("[Cave] Troll install stopped; runtime Idle frames are missing: " + folder);
                return;
            }
            ConfigurePrefab(prefabPath,
                corrupt ? ApprovedEnemySheetAnimator.VisualRole.CorruptTroll : ApprovedEnemySheetAnimator.VisualRole.Troll,
                null,
                idle,
                FramesInFolder(folder, prefix + "Walk_"),
                FramesInFolder(folder, prefix + "Run_"),
                FramesInFolder(folder, prefix + "Light_"),
                FramesInFolder(folder, prefix + "Heavy_"),
                Empty(), Empty(),
                FramesInFolder(folder, prefix + "Block_"),
                FramesInFolder(folder, prefix + "Hurt_"),
                FramesInFolder(folder, prefix + "Death_"));
            BindApprovedWeapon(prefabPath, corrupt ? "Troll_Corrupt_Club" : "Troll_Regular_Club", corrupt ? "axe" : "club");
        }

        private static void BindApprovedWeapon(string prefabPath, string spriteName, string nameToken)
        {
            Sprite weapon = AssetDatabase.LoadAssetAtPath<Sprite>(
                ApprovedTrollAssetProcessor.WeaponRuntimeRoot + "/" + spriteName + ".png");
            if (weapon == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index].name.IndexOf(nameToken, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    renderers[index].sprite = weapon;
                    renderers[index].enabled = true;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    return;
                }
                Debug.LogWarning("[Cave] Approved weapon built, but no " + nameToken + " renderer exists in " + prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigurePrefab(
            string prefabPath,
            ApprovedEnemySheetAnimator.VisualRole role,
            Material material,
            Sprite[] idle,
            Sprite[] walk,
            Sprite[] run,
            Sprite[] light,
            Sprite[] heavy,
            Sprite[] grab,
            Sprite[] study,
            Sprite[] utility,
            Sprite[] hurt,
            Sprite[] death)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning("[Cave] Enemy prefab was not found: " + prefabPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer legacyBody = root.GetComponent<SpriteRenderer>();
                if (legacyBody == null)
                {
                    Debug.LogWarning("[Cave] Enemy prefab has no root body renderer: " + prefabPath);
                    return;
                }

                Transform visualTransform = root.transform.Find(VisualName);
                if (visualTransform == null)
                {
                    GameObject visual = new GameObject(VisualName);
                    visualTransform = visual.transform;
                    visualTransform.SetParent(root.transform, false);
                }

                visualTransform.localPosition = Vector3.zero;
                visualTransform.localRotation = Quaternion.identity;
                visualTransform.localScale = Vector3.one;
                SpriteRenderer approvedRenderer = visualTransform.GetComponent<SpriteRenderer>();
                if (approvedRenderer == null)
                {
                    approvedRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
                }

                approvedRenderer.sortingLayerID = legacyBody.sortingLayerID;
                approvedRenderer.sortingOrder = legacyBody.sortingOrder;
                approvedRenderer.sharedMaterial = material;
                approvedRenderer.sprite = idle[0];

                ApprovedEnemySheetAnimator animator = root.GetComponent<ApprovedEnemySheetAnimator>();
                if (animator == null)
                {
                    animator = root.AddComponent<ApprovedEnemySheetAnimator>();
                }

                SpriteRenderer[] legacyVisuals = FindLegacyVisuals(root.transform, visualTransform);
                animator.Configure(role, approvedRenderer, legacyBody, legacyVisuals,
                    idle, walk, run, light, heavy, grab, study, utility, hurt, death, true);
                legacyBody.enabled = false;
                SetEnabled(legacyVisuals, false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SpriteRenderer[] FindLegacyVisuals(Transform root, Transform approvedVisual)
        {
            SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
            var legacy = new List<SpriteRenderer>(all.Length);
            for (int index = 0; index < all.Length; index++)
            {
                SpriteRenderer candidate = all[index];
                if (candidate == null || candidate.transform == root || candidate.transform.IsChildOf(approvedVisual))
                {
                    continue;
                }

                string lowerName = candidate.name.ToLowerInvariant();
                if (lowerName.Contains("pickaxe") || lowerName.Contains("weapon") || lowerName.Contains("chain")
                    || lowerName.Contains("club") || lowerName.Contains("axe"))
                {
                    continue;
                }

                legacy.Add(candidate);
            }

            return legacy.ToArray();
        }

        private static void SetEnabled(SpriteRenderer[] renderers, bool enabled)
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = enabled;
                }
            }
        }

        private static Sprite[] Frames(string path, string prefix)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var frames = new List<Sprite>();
            for (int index = 0; index < assets.Length; index++)
            {
                Sprite sprite = assets[index] as Sprite;
                if (sprite != null && sprite.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    frames.Add(sprite);
                }
            }

            frames.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return frames.ToArray();
        }

        private static Sprite[] Empty()
        {
            return new Sprite[0];
        }

        private static Material GetOrCreateBlackKeyMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Cave/Enemy Sprite Black Key");
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader)
            {
                name = "EnemySpriteBlackKey"
            };
            if (!AssetDatabase.IsValidFolder("Assets/Cave/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Cave", "Materials");
            }
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
#endif
