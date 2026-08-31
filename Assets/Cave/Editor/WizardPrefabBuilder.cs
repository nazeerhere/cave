using Cave.Combat;
using Cave.Enemies;
using Cave.Progression;
using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class WizardPrefabBuilder
    {
        private const string WizardPrefabPath = "Assets/Cave/Prefabs/Wizard.prefab";
        private const string WizardSpritePath =
            "Assets/Brackeys/2D Mega Pack/Characters/Fantasy/DarkWizard.png";
        private const string FireballPrefabPath =
            "Assets/Cave/Prefabs/Projectiles/Fireball.prefab";
        private const string EyePrefabPath = "Assets/Cave/Prefabs/Eye.prefab";
        private const string StrategicSettingsPath =
            "Assets/Cave/Resources/StrategicCombatSettings.asset";
        private const string WizardVfxRoot = "Assets/Cave/Prefabs/Wizard/";

        [MenuItem("Tools/Cave/Build Missing Content/Wizard Prefab")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WizardPrefabPath) != null)
            {
                Debug.Log("Wizard prefab already exists; non-destructive builder left it unchanged.");
                return;
            }

            Sprite wizardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WizardSpritePath);
            GameObject fireballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FireballPrefabPath);
            FireballProjectile projectile = fireballPrefab != null
                ? fireballPrefab.GetComponent<FireballProjectile>()
                : null;
            GameObject eyePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EyePrefabPath);
            if (wizardSprite == null || projectile == null || eyePrefab == null)
            {
                Debug.LogError(
                    "Wizard prefab was not created because a required sprite, Fireball component, "
                    + "or Eye prefab reference is missing.");
                return;
            }

            GameObject wizard = new GameObject("Wizard");
            try
            {
                int damageableLayer = LayerMask.NameToLayer("Damageable");
                if (damageableLayer >= 0)
                {
                    wizard.layer = damageableLayer;
                }

                SpriteRenderer renderer = wizard.AddComponent<SpriteRenderer>();
                renderer.sprite = wizardSprite;
                renderer.sortingOrder = 3;
                if (renderer.sprite != null && renderer.sprite.bounds.size.y > 0.01f)
                {
                    float scale = 1.6f / renderer.sprite.bounds.size.y;
                    wizard.transform.localScale = Vector3.one * scale;
                }

                Rigidbody2D body = wizard.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;

                CapsuleCollider2D collider = wizard.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(0.8f, 1.4f);
                collider.direction = CapsuleDirection2D.Vertical;

                wizard.AddComponent<Damageable>();
                EnemyArchetypeProfile profile = wizard.AddComponent<EnemyArchetypeProfile>();
                SerializedObject serializedProfile = new SerializedObject(profile);
                serializedProfile.FindProperty("archetypes").intValue =
                    (int)(EnemyArchetype.Support | EnemyArchetype.Ranged);
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                wizard.AddComponent<EnemyDamageModifiers>();
                wizard.AddComponent<EnemyStatusEffects>();
                wizard.AddComponent<EnemyStagger>();
                wizard.AddComponent<EnemySkillEvolution>();
                wizard.AddComponent<WizardFlightMotor>();

                SwarmCaller caller = wizard.AddComponent<SwarmCaller>();
                WizardSupportAbilities support = wizard.AddComponent<WizardSupportAbilities>();
                SerializedObject serializedSupport = new SerializedObject(support);
                serializedSupport.FindProperty("eyePrefab").objectReferenceValue = eyePrefab;
                int groundLayer = LayerMask.NameToLayer("Ground");
                serializedSupport.FindProperty("teleportGroundLayers").intValue =
                    groundLayer >= 0 ? 1 << groundLayer : 0;
                AssignOptionalVfx(serializedSupport, "healCastVfx", "cast heal.prefab");
                AssignOptionalVfx(serializedSupport, "healTargetVfx", "heal target.prefab");
                AssignOptionalVfx(serializedSupport, "dispelVfx", "dispell.prefab");
                AssignOptionalVfx(
                    serializedSupport,
                    "fireBuffVfx",
                    "CFXR2 Firewall A Variant.prefab");
                AssignOptionalVfx(
                    serializedSupport,
                    "frostBuffVfx",
                    "CFXR4 Bubbles Breath Underwater Loop Variant.prefab");
                AssignOptionalVfx(serializedSupport, "teleportCastVfx", "cast teleport.prefab");
                AssignOptionalVfx(
                    serializedSupport,
                    "teleportArrivalVfx",
                    "teleport arrival.prefab");
                AssignOptionalVfx(
                    serializedSupport,
                    "warpMarkPlayerVfx",
                    "mark teleport.prefab");
                AssignOptionalVfx(
                    serializedSupport,
                    "corruptionStageVfx",
                    "Corruption.prefab");
                AssignOptionalVfx(serializedSupport, "eyeSummonVfx", "eye summon.prefab");
                serializedSupport.ApplyModifiedPropertiesWithoutUndo();

                Transform firePoint = new GameObject("Fire Point").transform;
                firePoint.SetParent(wizard.transform, false);
                firePoint.localPosition = new Vector3(-0.55f, 0.15f, 0f);
                EnemyShooter shooter = wizard.AddComponent<EnemyShooter>();
                shooter.ConfigureWizardFallback(
                    firePoint,
                    projectile,
                    1,
                    3.25f,
                    5f,
                    6f,
                    8f,
                    0.45f);

                wizard.AddComponent<WizardBrain>();
                wizard.AddComponent<EnemyRespawner>();
                wizard.AddComponent<EnemyDifficultyScaler>();

                StrategicCombatSettings settings =
                    AssetDatabase.LoadAssetAtPath<StrategicCombatSettings>(StrategicSettingsPath);
                support.ConfigureRuntime(settings, null);
                caller.SetBrainControlled(true);

                PrefabUtility.SaveAsPrefabAsset(wizard, WizardPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created non-destructive Wizard prefab at " + WizardPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(wizard);
            }
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void AssignOptionalVfx(
            SerializedObject serializedSupport,
            string propertyName,
            string assetName)
        {
            SerializedProperty property = serializedSupport.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                    WizardVfxRoot + assetName);
            }
        }
    }
}
