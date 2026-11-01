using System;
using Cave.Environmental;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.EditorTools
{
    /// <summary>
    /// Idempotent first Breach slice. It authors only a dedicated prototype root and
    /// deliberately never edits the suspension bridge, gate, terrain, or transition roots.
    /// </summary>
    public static class BreachEnvironmentalPrototypeInstaller
    {
        private const string ScenePath = "Assets/Cave/Scenes/01_UpperCave.unity";
        private const string AtlasPath = "Assets/Cave/Art/Rooms/Breach/BreachTileSheet_Source.png";
        private const string RootName = "Environmental Destructibles Prototype";

        [MenuItem("Tools/Cave/Environment/Install Breach Destructible Prototype")]
        public static void Install()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = FindSceneObject(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            EnsureProp(root.transform, "Breakable Camp Crate", "Breach_Crate", new Vector2(-1.7f, -1.25f),
                EnvironmentalMaterialFamily.Wood, 2f, false);
            EnsureProp(root.transform, "Breakable Camp Barrel", "Breach_Barrel", new Vector2(-3.0f, -1.25f),
                EnvironmentalMaterialFamily.Wood, 2f, false);
            EnsureScaffold(root.transform);
            EnsureCart(root.transform);
            EnsureProp(root.transform, "Cracked Mine Barrier", "Breach_RockPlatformSmallA", new Vector2(12.2f, -2.05f),
                EnvironmentalMaterialFamily.Stone, 4f, true);
            GameObject crystal = EnsureProp(root.transform, "Regenerating Blue Crystal", "Breach_BlueCrystalLarge",
                new Vector2(22.6f, -2.25f), EnvironmentalMaterialFamily.Crystal, 3f, true);
            if (crystal.GetComponent<CrystalPhenomenonStorage>() == null) crystal.AddComponent<CrystalPhenomenonStorage>();

            AssertProtected(scene, "Breach_Bridge");
            AssertProtected(scene, "Suspension Bridge");
            AssertProtected(scene, "Fortified City Gate");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Cave] Breach destructible prototype installed. Stalactite and flora presentation remain unplaced until approved Breach art exists.");
        }

        private static void EnsureScaffold(Transform root)
        {
            Transform existing = root.Find("Authored Mining Scaffold");
            GameObject scaffold = existing != null ? existing.gameObject : new GameObject("Authored Mining Scaffold");
            scaffold.transform.SetParent(root, false);
            scaffold.transform.position = new Vector2(20.7f, -4.15f);
            SpriteRenderer visual = EnsureRenderer(scaffold, "Breach_ScaffoldLower", 7);

            EnvironmentalDestructible left = EnsureSupport(scaffold.transform, "Left Support", new Vector2(-.65f, -.35f));
            EnvironmentalDestructible right = EnsureSupport(scaffold.transform, "Right Support", new Vector2(.65f, -.35f));
            StructuralSupportGroup group = scaffold.GetComponent<StructuralSupportGroup>();
            if (group == null) group = scaffold.AddComponent<StructuralSupportGroup>();
            SerializedObject serialized = new SerializedObject(group);
            SetArray(serialized.FindProperty("requiredSupports"), left, right);
            SetArray(serialized.FindProperty("intactObjects"), visual.gameObject);
            SetArray(serialized.FindProperty("collidersToDisableOnCollapse"), left.GetComponent<Collider2D>(), right.GetComponent<Collider2D>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnvironmentalDestructible EnsureSupport(Transform parent, string name, Vector2 localPosition)
        {
            Transform existing = parent.Find(name);
            GameObject support = existing != null ? existing.gameObject : new GameObject(name);
            support.transform.SetParent(parent, false);
            support.transform.localPosition = localPosition;
            BoxCollider2D collider = support.GetComponent<BoxCollider2D>();
            if (collider == null) collider = support.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.35f, 1.4f);
            EnvironmentalDestructible structural = support.GetComponent<EnvironmentalDestructible>();
            if (structural == null) structural = support.AddComponent<EnvironmentalDestructible>();
            ConfigureStructural(structural, EnvironmentalMaterialFamily.Wood, 2f, false, collider, null);
            return structural;
        }

        private static void EnsureCart(Transform root)
        {
            GameObject cart = EnsureProp(root, "Movable Mining Ore Cart", "Breach_OreCart", new Vector2(15.4f, -4.55f),
                EnvironmentalMaterialFamily.Metal, 6f, false);
            Rigidbody2D body = cart.GetComponent<Rigidbody2D>();
            if (body == null) body = cart.AddComponent<Rigidbody2D>();
            body.mass = 3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            if (cart.GetComponent<EnvironmentalImpactBody>() == null) cart.AddComponent<EnvironmentalImpactBody>();
        }

        private static GameObject EnsureProp(Transform root, string name, string spriteName, Vector2 position,
            EnvironmentalMaterialFamily material, float integrity, bool regenerates)
        {
            Transform existing = root.Find(name);
            GameObject prop = existing != null ? existing.gameObject : new GameObject(name);
            prop.transform.SetParent(root, false);
            prop.transform.position = position;
            SpriteRenderer renderer = EnsureRenderer(prop, spriteName, 8);
            BoxCollider2D collider = prop.GetComponent<BoxCollider2D>();
            if (collider == null) collider = prop.AddComponent<BoxCollider2D>();
            collider.size = renderer.sprite != null ? renderer.sprite.bounds.size * .72f : Vector2.one;
            EnvironmentalDestructible structural = prop.GetComponent<EnvironmentalDestructible>();
            if (structural == null) structural = prop.AddComponent<EnvironmentalDestructible>();
            ConfigureStructural(structural, material, integrity, regenerates, collider, renderer.gameObject);
            return prop;
        }

        private static SpriteRenderer EnsureRenderer(GameObject owner, string spriteName, int order)
        {
            SpriteRenderer renderer = owner.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = owner.AddComponent<SpriteRenderer>();
            renderer.sprite = FindSprite(spriteName);
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void ConfigureStructural(EnvironmentalDestructible structural, EnvironmentalMaterialFamily family,
            float integrity, bool regenerates, Collider2D collider, GameObject intactVisual)
        {
            SerializedObject serialized = new SerializedObject(structural);
            serialized.FindProperty("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnvironmentalMaterialProfile>(
                "Assets/Cave/Resources/Environment/Materials/" + family + ".asset");
            serialized.FindProperty("maximumIntegrity").floatValue = integrity;
            serialized.FindProperty("progressionCritical").boolValue = false;
            serialized.FindProperty("regenerates").boolValue = regenerates;
            serialized.FindProperty("regenerationDelay").floatValue = regenerates ? 10f : 0f;
            SetArray(serialized.FindProperty("structuralColliders"), collider);
            if (intactVisual != null) SetArray(serialized.FindProperty("intactVisuals"), intactVisual);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(structural);
        }

        private static Sprite FindSprite(string name)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AtlasPath);
            for (int i = 0; i < assets.Length; i++)
                if (assets[i] is Sprite sprite && sprite.name == name) return sprite;
            throw new InvalidOperationException("Missing approved Breach sprite: " + name);
        }

        private static void AssertProtected(Scene scene, string name)
        {
            GameObject candidate = FindSceneObject(scene, name);
            if (candidate != null && candidate.GetComponentInChildren<EnvironmentalDestructible>(true) != null)
                throw new InvalidOperationException("Progression-critical object acquired a destructible: " + name);
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++) if (transforms[i].name == name) return transforms[i].gameObject;
            }
            return null;
        }

        private static void SetArray(SerializedProperty property, params UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
