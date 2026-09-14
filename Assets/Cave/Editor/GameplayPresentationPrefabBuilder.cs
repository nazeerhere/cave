#if UNITY_EDITOR
using System.IO;
using Cave.Presentation;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Creates editable, appearance-only prefab assets. It never touches scene
    /// instances, combat prefabs, colliders, or gameplay scripts.
    /// </summary>
    public static class GameplayPresentationPrefabBuilder
    {
        private const string SwordRoot = "Assets/Cave/Prefabs/Visuals/Swords";
        private const string FieldRoot = "Assets/Cave/Prefabs/FieldControl";
        private const string VfxRoot = "Assets/Cave/Prefabs/Visuals/VFX";
        private const string ProjectileRoot = "Assets/Cave/Prefabs/Visuals/Projectiles";
        private const string WeaponRoot = "Assets/Cave/Prefabs/Visuals/EnemyWeapons";

        [MenuItem("Tools/Cave/Presentation/Build Editable Gameplay Visual Prefabs")]
        public static void Build()
        {
            CreateSpritePrefab(SwordRoot + "/Sword01Visual.prefab", "Sword01 Visual", LoadSprite("Assets/Cave/Resources/Cosmetics/Swords/Sword01.png"), 12, 1f);
            CreateSpritePrefab(SwordRoot + "/Sword02Visual.prefab", "Sword02 Visual", LoadSprite("Assets/Cave/Resources/Cosmetics/Swords/Sword02.png"), 12, 1f);
            CreateSpritePrefab(SwordRoot + "/Sword03Visual.prefab", "Sword03 Visual", LoadSprite("Assets/Cave/Resources/Cosmetics/Swords/Sword03.png"), 12, 1f);
            CreateSpritePrefab(WeaponRoot + "/TrollClubVisual.prefab", "Troll Club Visual", null, 6, 1f);
            CreateSpritePrefab(WeaponRoot + "/CorruptTrollClubVisual.prefab", "Corrupt Troll Club Visual", null, 6, 1f);
            CreateSpritePrefab(WeaponRoot + "/BrutePickaxeVisual.prefab", "Brute Pickaxe Visual", null, 6, 1f);
            CreateSpritePrefab(WeaponRoot + "/CorruptBrutePickaxeVisual.prefab", "Corrupt Brute Pickaxe Visual", null, 6, 1f);

            CreateSpritePrefab(FieldRoot + "/OblivionDiskVisual.prefab", "Oblivion Disk Visual", null, 9, 1f);
            CreateLinePrefab(FieldRoot + "/OblivionDiskFieldLinkVisual.prefab", "Oblivion Disk Field Link Visual", new Color(.2f, .85f, 1f, .8f), 7, true);
            CreateSpritePrefab(VfxRoot + "/OblivionDiskLocalPulseVfx.prefab", "Oblivion Disk Local Pulse Visual", null, 8, 1f);
            CreateSpritePrefab(VfxRoot + "/OblivionDiskOverloadVfx.prefab", "Oblivion Disk Overload Visual", null, 9, 1f);
            CreateSpritePrefab(VfxRoot + "/AntiPyreRepulseVfx.prefab", "Anti-Pyre Repulse Visual", null, 8, 1f);
            CreateSpritePrefab(VfxRoot + "/AntiPyrePartitionAnchorVisual.prefab", "Anti-Pyre Partition Anchor Visual", null, 8, 1f);
            CreateLinePrefab(VfxRoot + "/AntiPyrePartitionLinkVisual.prefab", "Anti-Pyre Partition Link Visual", new Color(.75f, .25f, 1f, .8f), 7);
            CreateSpritePrefab(VfxRoot + "/OblivionDiskExplosionVfx.prefab", "Oblivion Disk Explosion Visual", null, 9, 1f);
            CreateSpritePrefab(VfxRoot + "/FireballImpactVfx.prefab", "Fireball Impact Visual", null, 9, 1f);

            CreateSpritePrefab(ProjectileRoot + "/FireballVisual.prefab", "Fireball Visual", null, 6, 1f);
            CreateSpritePrefab(ProjectileRoot + "/MeteorFireballVisual.prefab", "Meteor Fireball Visual", null, 6, 1f);
            CreateSpritePrefab(ProjectileRoot + "/AntiPyreAntimatterVisual.prefab", "Anti-Pyre Antimatter Visual", null, 6, 1f);
            AssignRuntimeReferences();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Cave] Built editable gameplay presentation prefabs. Assign them through the future visual catalog without adding gameplay components.");
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateSpritePrefab(string path, string objectName, Sprite sprite, int sortingOrder, float scale)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = new GameObject(objectName);
            root.AddComponent<PresentationVisualMarker>();
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            root.transform.localScale = Vector3.one * Mathf.Max(.01f, scale);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateLinePrefab(string path, string objectName, Color color, int sortingOrder, bool useOblivionDiskFrames = false)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
            GameObject root = created ? null : PrefabUtility.LoadPrefabContents(path);
            if (created)
            {
                root = new GameObject(objectName);
                root.AddComponent<PresentationVisualMarker>();
            }
            LineRenderer line = root.GetComponent<LineRenderer>();
            if (line == null) line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = .08f;
            line.endWidth = .08f;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            if (line.sharedMaterial == null) line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            if (useOblivionDiskFrames && root.GetComponent<Cave.FieldControl.OblivionDiskFieldLinkVisual>() == null)
                root.AddComponent<Cave.FieldControl.OblivionDiskFieldLinkVisual>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            if (created) Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
        }

        private static void AssignRuntimeReferences()
        {
            AssignPlayer("Assets/Cave/Prefabs/Player.prefab");
            AssignPlayer("Assets/Cave/Prefabs/Player_Sprint2.prefab");
            AssignGoth("Assets/Cave/Prefabs/Mobs/Range/Goth .prefab");
            AssignGoth("Assets/Cave/Prefabs/Mobs/Range/Corrupt Goth .prefab");
        }

        private static void AssignPlayer(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;
            Cave.Player.PlayerSwordCosmetics swords = root.GetComponent<Cave.Player.PlayerSwordCosmetics>();
            if (swords != null)
            {
                SerializedObject serialized = new SerializedObject(swords);
                SetObject(serialized, "sword1VisualPrefab", SwordRoot + "/Sword01Visual.prefab");
                SetObject(serialized, "sword2VisualPrefab", SwordRoot + "/Sword02Visual.prefab");
                SetObject(serialized, "sword3VisualPrefab", SwordRoot + "/Sword03Visual.prefab");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Cave.Player.PlayerLandmineInventory mines = root.GetComponent<Cave.Player.PlayerLandmineInventory>();
            if (mines != null)
            {
                SerializedObject serialized = new SerializedObject(mines);
                SetObject(serialized, "diskVisualPrefab", FieldRoot + "/OblivionDiskVisual.prefab");
                SetObject(serialized, "localPulseVisualPrefab", VfxRoot + "/OblivionDiskLocalPulseVfx.prefab");
                SetObject(serialized, "fieldLinkVisualPrefab", FieldRoot + "/OblivionDiskFieldLinkVisual.prefab");
                SetObject(serialized, "overloadVisualPrefab", VfxRoot + "/OblivionDiskOverloadVfx.prefab");
                SetObject(serialized, "sacrificeExplosionVisualPrefab", VfxRoot + "/OblivionDiskExplosionVfx.prefab");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void AssignGoth(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;
            Cave.Enemies.GothArtilleryAbilities abilities = root.GetComponent<Cave.Enemies.GothArtilleryAbilities>();
            if (abilities != null)
            {
                SerializedObject serialized = new SerializedObject(abilities);
                SetObject(serialized, "repulseVisualPrefab", VfxRoot + "/AntiPyreRepulseVfx.prefab");
                SetObject(serialized, "partitionAnchorVisualPrefab", VfxRoot + "/AntiPyrePartitionAnchorVisual.prefab");
                SetObject(serialized, "partitionLinkVisualPrefab", VfxRoot + "/AntiPyrePartitionLinkVisual.prefab");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetObject(SerializedObject serialized, string property, string path)
        {
            SerializedProperty target = serialized.FindProperty(property);
            if (target != null) target.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string name = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
