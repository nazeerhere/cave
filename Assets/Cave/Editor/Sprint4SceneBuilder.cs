using Cave.Combat;
using Cave.Enemies;
using Cave.Projectiles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class Sprint4SceneBuilder
    {
        private const string Sprint1ScenePath = "Assets/Cave/Scenes/Sprint1_Movement.unity";
        private const string Sprint2ScenePath = "Assets/Cave/Scenes/Sprint2_Combat.unity";
        private const string Sprint3ScenePath = "Assets/Cave/Scenes/Sprint3_Enemies.unity";
        private const string Sprint4ScenePath = "Assets/Cave/Scenes/Sprint4_RangedCombat.unity";
        private const string FireballPrefabPath = "Assets/Cave/Prefabs/Fireball.prefab";
        private const int DamageableLayer = 9;
        private const int ProjectileLayer = 10;

        [MenuItem("Tools/Cave/Destructive Rebuild/Sprint 4 Ranged Combat Scene")]
        public static void BuildSprint4TestScene()
        {
            ConfigureLayer(ProjectileLayer, "Projectile");
            FireballProjectile fireballPrefab = CreateFireballPrefab();

            Scene scene = EditorSceneManager.OpenScene(Sprint3ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Sprint4ScenePath);

            GameObject player = GameObject.Find("Player");
            GameObject enemies = GameObject.Find("Enemies");
            if (player == null || enemies == null)
            {
                throw new System.InvalidOperationException("Sprint 3 scene must contain Player and Enemies objects.");
            }

            CreateChargedAttackSetup(player);
            CreateParrySetup(player);
            ConfigureRangedEnemies(enemies.transform, player.transform, fireballPrefab);
            new GameObject("Projectile Spawn Test Area");

            EditorSceneManager.SaveScene(scene);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);
            Debug.Log("Cave Sprint 4 scene built successfully at " + Sprint4ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildSprint4TestScene();
        }

        private static void CreateChargedAttackSetup(GameObject player)
        {
            ChargedAttack chargedAttack = player.AddComponent<ChargedAttack>();

            GameObject chargeIndicator = CreateVisualObject(
                "Charge Indicator",
                player.transform,
                Vector2.zero,
                new Vector2(1.4f, 1.4f),
                new Color(0.25f, 0.65f, 1f, 0.35f),
                0);

            GameObject attackObject = CreateVisualObject(
                "Charged Attack Hitbox",
                player.transform,
                Vector2.zero,
                new Vector2(3f, 1.8f),
                new Color(0.35f, 0.75f, 1f, 0.45f),
                2);

            BoxCollider2D collider = attackObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3f, 1.8f);
            collider.isTrigger = true;
            collider.enabled = false;

            ChargedAttackHitbox hitbox = attackObject.AddComponent<ChargedAttackHitbox>();
            SerializedObject serializedHitbox = new SerializedObject(hitbox);
            serializedHitbox.FindProperty("attack").objectReferenceValue = chargedAttack;
            serializedHitbox.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedAttack = new SerializedObject(chargedAttack);
            serializedAttack.FindProperty("damageableLayers").intValue = 1 << DamageableLayer;
            serializedAttack.FindProperty("chargeIndicator").objectReferenceValue = chargeIndicator;
            serializedAttack.FindProperty("attackVisual").objectReferenceValue = attackObject;
            serializedAttack.FindProperty("attackCollider").objectReferenceValue = collider;
            serializedAttack.ApplyModifiedPropertiesWithoutUndo();

            chargeIndicator.SetActive(false);
            attackObject.SetActive(false);
        }

        private static void CreateParrySetup(GameObject player)
        {
            SidewaysParryAttack parryAttack = player.AddComponent<SidewaysParryAttack>();

            GameObject parryObject = CreateVisualObject(
                "Sideways Parry Hitbox",
                player.transform,
                new Vector2(1.1f, 0f),
                new Vector2(1.6f, 0.3f),
                new Color(0.3f, 1f, 0.95f, 0.8f),
                3);

            BoxCollider2D collider = parryObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.6f, 1.2f);
            collider.isTrigger = true;
            collider.enabled = false;

            ParryHitbox hitbox = parryObject.AddComponent<ParryHitbox>();
            SerializedObject serializedHitbox = new SerializedObject(hitbox);
            serializedHitbox.FindProperty("attack").objectReferenceValue = parryAttack;
            serializedHitbox.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedAttack = new SerializedObject(parryAttack);
            serializedAttack.FindProperty("parryTransform").objectReferenceValue = parryObject.transform;
            serializedAttack.FindProperty("parryVisual").objectReferenceValue = parryObject;
            serializedAttack.FindProperty("parryCollider").objectReferenceValue = collider;
            serializedAttack.ApplyModifiedPropertiesWithoutUndo();

            parryObject.SetActive(false);
        }

        private static void ConfigureRangedEnemies(
            Transform enemies,
            Transform player,
            FireballProjectile fireballPrefab)
        {
            foreach (Transform enemy in enemies)
            {
                enemy.gameObject.AddComponent<KnockbackReceiver>();

                GameObject firePoint = new GameObject("Fire Point");
                firePoint.transform.SetParent(enemy, false);
                firePoint.transform.localPosition = new Vector3(0.7f, 0.15f, 0f);

                EnemyShooter shooter = enemy.gameObject.AddComponent<EnemyShooter>();
                SerializedObject serializedShooter = new SerializedObject(shooter);
                serializedShooter.FindProperty("target").objectReferenceValue = player;
                serializedShooter.FindProperty("firePoint").objectReferenceValue = firePoint.transform;
                serializedShooter.FindProperty("projectilePrefab").objectReferenceValue = fireballPrefab;
                serializedShooter.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static FireballProjectile CreateFireballPrefab()
        {
            GameObject fireball = new GameObject("Fireball");
            fireball.layer = ProjectileLayer;

            SpriteRenderer renderer = fireball.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.35f, 0.35f);
            renderer.color = new Color(1f, 0.35f, 0.1f);
            renderer.sortingOrder = 4;

            CircleCollider2D collider = fireball.AddComponent<CircleCollider2D>();
            collider.radius = 0.18f;
            collider.isTrigger = true;

            Rigidbody2D body = fireball.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            fireball.AddComponent<FireballProjectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(fireball, FireballPrefabPath);
            Object.DestroyImmediate(fireball);
            return prefab.GetComponent<FireballProjectile>();
        }

        private static GameObject CreateVisualObject(
            string name,
            Transform parent,
            Vector2 localPosition,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            GameObject visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return visual;
        }

        private static Sprite GetPlaceholderSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void ConfigureLayer(int layerIndex, string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(layerIndex);

            if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == layerName)
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            throw new System.InvalidOperationException(
                "Layer " + layerIndex + " is already named '" + layer.stringValue
                + "'. Assign an unused layer to " + layerName + " before building.");
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Sprint1ScenePath, true),
                new EditorBuildSettingsScene(Sprint2ScenePath, true),
                new EditorBuildSettingsScene(Sprint3ScenePath, true),
                new EditorBuildSettingsScene(Sprint4ScenePath, true)
            };
        }
    }
}
