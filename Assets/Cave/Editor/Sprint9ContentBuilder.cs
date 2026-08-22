using System;
using Cave.Pickups;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class Sprint9ContentBuilder
    {
        private const string HealthPrefabPath = "Assets/Cave/Prefabs/HealthPickup.prefab";
        private const string StaminaPrefabPath = "Assets/Cave/Prefabs/StaminaPickup.prefab";
        private const string ManaPrefabPath = "Assets/Cave/Prefabs/ManaPickup.prefab";
        private const string CurrencyPrefabPath = "Assets/Cave/Prefabs/CurrencyPickup.prefab";
        private const string SettingsPath = "Assets/Cave/Resources/EnemyDropSettings.asset";

        [MenuItem("Tools/Cave/Apply Sprint 9.1 Resource Drop Correction")]
        public static void CreatePickupAssets()
        {
            RefuseToOverwriteNewPickupAssets();

            HealthPickup healthPickup = LoadRequiredPickup<HealthPickup>(HealthPrefabPath);
            StaminaPickup staminaPickup = LoadRequiredPickup<StaminaPickup>(StaminaPrefabPath);
            EnemyDropSettings settings = AssetDatabase.LoadAssetAtPath<EnemyDropSettings>(SettingsPath);
            if (settings == null)
            {
                throw new InvalidOperationException("Required enemy drop settings were not found: " + SettingsPath);
            }

            ValidateOriginalDropEntries(settings, healthPickup, staminaPickup);

            ManaPickup manaPickup = CreatePickupPrefab<ManaPickup>(
                "Mana Pickup", ManaPrefabPath, new Color(0.58f, 0.3f, 0.95f));
            CurrencyPickup currencyPickup = CreatePickupPrefab<CurrencyPickup>(
                "Currency Pickup", CurrencyPrefabPath, new Color(1f, 0.78f, 0.12f));

            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty entries = serializedSettings.FindProperty("entries");
            entries.arraySize = 3;
            SetEntry(entries.GetArrayElementAtIndex(0), healthPickup, 0.20f);
            SetEntry(entries.GetArrayElementAtIndex(1), manaPickup, 0.20f);
            SetEntry(entries.GetArrayElementAtIndex(2), currencyPickup, 0.35f);
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied Sprint 9.1: Health, Mana, and Currency drops configured; Stamina pickup preserved but unused.");
        }

        public static void BuildFromCommandLine()
        {
            CreatePickupAssets();
        }

        private static T CreatePickupPrefab<T>(string objectName, string assetPath, Color color)
            where T : PickupBase
        {
            GameObject pickupObject = new GameObject(objectName);
            SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.55f, 0.55f);
            renderer.color = color;
            renderer.sortingOrder = 4;

            CircleCollider2D pickupCollider = pickupObject.AddComponent<CircleCollider2D>();
            pickupCollider.isTrigger = true;
            pickupCollider.radius = 0.3f;
            pickupObject.AddComponent<T>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pickupObject, assetPath);
            UnityEngine.Object.DestroyImmediate(pickupObject);
            return prefab.GetComponent<T>();
        }

        private static void SetEntry(SerializedProperty entry, PickupBase pickupPrefab, float chance)
        {
            entry.FindPropertyRelative("pickupPrefab").objectReferenceValue = pickupPrefab;
            entry.FindPropertyRelative("chance").floatValue = chance;
        }

        private static T LoadRequiredPickup<T>(string path) where T : PickupBase
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            T pickup = prefab != null ? prefab.GetComponent<T>() : null;
            if (pickup == null)
            {
                throw new InvalidOperationException("Required pickup prefab was not found: " + path);
            }

            return pickup;
        }

        private static void ValidateOriginalDropEntries(
            EnemyDropSettings settings,
            HealthPickup healthPickup,
            StaminaPickup staminaPickup)
        {
            DropEntry[] entries = settings.Entries;
            if (entries == null
                || entries.Length != 2
                || entries[0].PickupPrefab != healthPickup
                || entries[1].PickupPrefab != staminaPickup)
            {
                throw new InvalidOperationException(
                    "EnemyDropSettings no longer matches the generated Sprint 9 table. "
                    + "The correction stopped to preserve possible manual Inspector changes.");
            }
        }

        private static void RefuseToOverwriteNewPickupAssets()
        {
            string[] paths = { ManaPrefabPath, CurrencyPrefabPath };
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    throw new InvalidOperationException("Sprint 9.1 builder refused to overwrite existing asset: " + path);
                }
            }
        }
    }
}
