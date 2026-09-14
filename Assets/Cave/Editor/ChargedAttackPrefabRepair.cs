using Cave.Combat;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Repairs the Player prefab's explicitly authored Charged Attack Hitbox
    /// references. Kept Editor-only so no scene or runtime object is searched.
    /// </summary>
    internal static class ChargedAttackPrefabRepair
    {
        private const string PlayerPrefabPath = "Assets/Cave/Prefabs/Player.prefab";
        private const string HitboxName = "Charged Attack Hitbox";
        private const string ChargeIndicatorName = "Charge Indicator";
        private const string DamageableLayerName = "Damageable";

        [MenuItem("Tools/Cave/Combat/Repair Player Charged Attack References")]
        private static void RepairPlayerPrefab()
        {
            if (!TryInspect(out GameObject prefabRoot, out ChargedAttack attack, out GameObject hitboxObject,
                    out Collider2D hitboxCollider, out GameObject chargeIndicatorObject, out string problem))
            {
                Debug.LogError("[Cave] Charged Attack prefab repair aborted: " + problem);
                return;
            }

            try
            {
                SerializedObject attackObject = new SerializedObject(attack);
                attackObject.FindProperty("damageableLayers").intValue = 1 << LayerMask.NameToLayer(DamageableLayerName);
                attackObject.FindProperty("chargeIndicator").objectReferenceValue = chargeIndicatorObject;
                attackObject.FindProperty("attackVisual").objectReferenceValue = hitboxObject;
                attackObject.FindProperty("attackCollider").objectReferenceValue = hitboxCollider;
                attackObject.ApplyModifiedPropertiesWithoutUndo();

                ChargedAttackHitbox hitbox = hitboxObject.GetComponent<ChargedAttackHitbox>();
                SerializedObject hitboxObjectSerialized = new SerializedObject(hitbox);
                hitboxObjectSerialized.FindProperty("attack").objectReferenceValue = attack;
                hitboxObjectSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Cave] Repaired Player Charged Attack references: Damageable mask, indicator, visual, collider, and hitbox owner.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [MenuItem("Tools/Cave/Combat/Validate Player Charged Attack References")]
        private static void ValidatePlayerPrefab()
        {
            if (!TryInspect(out GameObject prefabRoot, out ChargedAttack attack, out GameObject hitboxObject,
                    out Collider2D hitboxCollider, out GameObject chargeIndicatorObject, out string problem))
            {
                Debug.LogError("[Cave] Charged Attack prefab validation failed: " + problem);
                return;
            }

            try
            {
                SerializedObject attackObject = new SerializedObject(attack);
                int expectedMask = 1 << LayerMask.NameToLayer(DamageableLayerName);
                ChargedAttackHitbox hitbox = hitboxObject.GetComponent<ChargedAttackHitbox>();
                bool valid = attackObject.FindProperty("damageableLayers").intValue == expectedMask
                    && attackObject.FindProperty("chargeIndicator").objectReferenceValue == chargeIndicatorObject
                    && attackObject.FindProperty("attackVisual").objectReferenceValue == hitboxObject
                    && attackObject.FindProperty("attackCollider").objectReferenceValue == hitboxCollider
                    && new SerializedObject(hitbox).FindProperty("attack").objectReferenceValue == attack;
                Debug.Log(valid
                    ? "[Cave] Player Charged Attack prefab validation passed."
                    : "[Cave] Player Charged Attack prefab needs repair. Use Tools/Cave/Combat/Repair Player Charged Attack References.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool TryInspect(
            out GameObject prefabRoot,
            out ChargedAttack attack,
            out GameObject hitboxObject,
            out Collider2D hitboxCollider,
            out GameObject chargeIndicatorObject,
            out string problem)
        {
            prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            attack = null;
            hitboxObject = null;
            hitboxCollider = null;
            chargeIndicatorObject = null;
            problem = null;

            if (prefabRoot == null)
            {
                problem = "Player prefab was not found at " + PlayerPrefabPath + ".";
                return false;
            }

            prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            attack = prefabRoot.GetComponent<ChargedAttack>();
            Transform hitboxTransform = prefabRoot.transform.Find(HitboxName);
            Transform indicatorTransform = prefabRoot.transform.Find(ChargeIndicatorName);
            int damageableLayer = LayerMask.NameToLayer(DamageableLayerName);
            hitboxObject = hitboxTransform != null ? hitboxTransform.gameObject : null;
            hitboxCollider = hitboxObject != null ? hitboxObject.GetComponent<Collider2D>() : null;
            chargeIndicatorObject = indicatorTransform != null ? indicatorTransform.gameObject : null;

            if (attack == null || hitboxObject == null || hitboxCollider == null || chargeIndicatorObject == null
                || hitboxObject.GetComponent<SpriteRenderer>() == null
                || chargeIndicatorObject.GetComponent<SpriteRenderer>() == null
                || hitboxObject.GetComponent<ChargedAttackHitbox>() == null
                || damageableLayer < 0)
            {
                problem = "Expected Charged Attack, its direct '" + HitboxName
                    + "' child, its '" + ChargeIndicatorName + "' child, Collider2D, SpriteRenderers, ChargedAttackHitbox, and Damageable layer.";
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                prefabRoot = null;
                return false;
            }

            return true;
        }
    }
}
