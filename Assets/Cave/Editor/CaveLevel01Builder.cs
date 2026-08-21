using System.Collections.Generic;
using Cave.CameraSystem;
using Cave.Enemies;
using Cave.Hazards;
using Cave.Projectiles;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class CaveLevel01Builder
    {
        private const string SourceScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";
        private const string LevelScenePath = "Assets/Cave/Scenes/Cave_Level01.unity";
        private const string FireballPrefabPath = "Assets/Cave/Prefabs/Fireball.prefab";
        private const string GroundSpritePath = "Assets/Brackeys/2D Mega Pack/Environment/Tiles/GroundTile.png";
        private const string PlatformSpritePath = "Assets/Brackeys/2D Mega Pack/Platforms/Platform_Wood_Wide.png";
        private const string BackgroundSpritePath = "Assets/Brackeys/2D Mega Pack/Backgrounds/DarkBackground.png";
        private const string ExitSpritePath = "Assets/Brackeys/2D Mega Pack/Environment/Gothic/Door.png";

        private const int GroundLayer = 8;

        [MenuItem("Tools/Cave/Create Level 01 (New Scene Only)")]
        public static void CreateLevel01()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelScenePath) != null)
            {
                throw new System.InvalidOperationException(
                    "Cave_Level01 already exists. This additive builder refuses to overwrite it.");
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            FireballProjectile fireballPrefab =
                AssetDatabase.LoadAssetAtPath<FireballProjectile>(FireballPrefabPath);
            if (fireballPrefab == null)
            {
                throw new System.InvalidOperationException(
                    "The existing Fireball prefab could not be loaded. No level was created.");
            }

            Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            GameObject sourcePlayer = FindSceneObject(sourceScene, "Player");
            GameObject sourceCamera = FindSceneObject(sourceScene, "Main Camera");
            GameObject sourceLight = FindSceneObject(sourceScene, "Directional Light");
            GameObject sourceEnemyOne = FindSceneObject(sourceScene, "Enemy 01");
            GameObject sourceEnemyTwo = FindSceneObject(sourceScene, "Enemy 02");
            GameObject sourceBombSpawner = FindSceneObject(sourceScene, "Bomb Spawner");

            RequireSourceObject(sourcePlayer, "Player");
            RequireSourceObject(sourceCamera, "Main Camera");
            RequireSourceObject(sourceEnemyOne, "Enemy 01");
            RequireSourceObject(sourceEnemyTwo, "Enemy 02");
            RequireSourceObject(sourceBombSpawner, "Bomb Spawner");

            Scene levelScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(levelScene);

            GameObject environment = CreateRoot("Environment", levelScene);
            Transform ground = CreateContainer("Ground", environment.transform);
            Transform platforms = CreateContainer("Platforms", environment.transform);
            Transform walls = CreateContainer("Walls", environment.transform);
            Transform decoration = CreateContainer("Decoration", environment.transform);

            Sprite groundSprite = LoadSpriteOrPlaceholder(GroundSpritePath);
            Sprite platformSprite = LoadSpriteOrPlaceholder(PlatformSpritePath);
            CreateBackground(decoration, LoadSpriteOrPlaceholder(BackgroundSpritePath));
            BuildGround(ground, groundSprite);
            BuildPlatforms(platforms, platformSprite);
            CreateSolid("Left Wall", walls, new Vector2(-3f, 4f), new Vector2(2f, 12f), groundSprite);
            CreateSolid("Right Wall", walls, new Vector2(64f, 4f), new Vector2(2f, 12f), groundSprite);

            GameObject player = CloneToScene(sourcePlayer, levelScene);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1.5f, 0f);
            player.SetActive(true);

            GameObject cameraObject = CloneToScene(sourceCamera, levelScene);
            cameraObject.name = "Main Camera";
            CameraFollow cameraFollow = cameraObject.GetComponent<CameraFollow>();
            if (cameraFollow == null)
            {
                throw new System.InvalidOperationException("The configured camera is missing CameraFollow.");
            }

            SerializedObject serializedCamera = new SerializedObject(cameraFollow);
            serializedCamera.FindProperty("target").objectReferenceValue = player.transform;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();

            if (sourceLight != null)
            {
                GameObject lightObject = CloneToScene(sourceLight, levelScene);
                lightObject.name = "Directional Light";
            }

            GameObject enemies = CreateRoot("Enemies", levelScene);
            Transform encounterOne = CreateContainer("Encounter 01 - First Patrol", enemies.transform);
            Transform encounterTwo = CreateContainer("Encounter 02 - Platform Pressure", enemies.transform);
            Transform encounterThree = CreateContainer("Encounter 03 - Fireball Crossing", enemies.transform);
            Transform encounterFour = CreateContainer("Encounter 04 - Mixed Finale", enemies.transform);

            CreateEnemy(sourceEnemyOne, levelScene, encounterOne, "Patrol Enemy", new Vector2(9f, 2f), false, player, fireballPrefab, 1.5f);
            CreateEnemy(sourceEnemyTwo, levelScene, encounterTwo, "Lower Patrol Enemy", new Vector2(19.5f, 2f), false, player, fireballPrefab, 1.25f);
            CreateEnemy(sourceEnemyOne, levelScene, encounterTwo, "Upper Patrol Enemy", new Vector2(23f, 4.2f), false, player, fireballPrefab, 0.8f);
            CreateEnemy(sourceEnemyTwo, levelScene, encounterThree, "Ranged Enemy", new Vector2(30f, 4.2f), true, player, fireballPrefab, 0.8f);
            CreateEnemy(sourceEnemyOne, levelScene, encounterFour, "Final Patrol Enemy", new Vector2(48.5f, 2f), false, player, fireballPrefab, 1.2f);
            CreateEnemy(sourceEnemyTwo, levelScene, encounterFour, "Final Ranged Enemy", new Vector2(55f, 4.2f), true, player, fireballPrefab, 0.8f);

            GameObject hazards = CreateRoot("Hazards", levelScene);
            Transform bombPressure = CreateContainer("Bomb Pressure Section", hazards.transform);
            ConfigureBombSpawner(sourceBombSpawner, levelScene, bombPressure, hazards.transform, player.transform);

            GameObject checkpoints = CreateRoot("Checkpoints", levelScene);
            CreateCheckpoint("Checkpoint 01", checkpoints.transform, new Vector2(35f, 0f));
            CreateCheckpoint("Checkpoint 02", checkpoints.transform, new Vector2(51f, 0f));

            GameObject boundaries = CreateRoot("Boundaries", levelScene);
            CreateDeathBoundary("Fall Boundary", boundaries.transform, new Vector2(30f, -8f), new Vector2(72f, 2f));
            CreateDeathBoundary("Left Boundary", boundaries.transform, new Vector2(-5f, 3f), new Vector2(2f, 20f));
            CreateDeathBoundary("Right Boundary", boundaries.transform, new Vector2(66f, 3f), new Vector2(2f, 20f));

            CreateLevelExit(levelScene, LoadSpriteOrPlaceholder(ExitSpritePath));

            Physics2D.SyncTransforms();
            ValidateFirePoint("Ranged Enemy", levelScene, fireballPrefab);
            ValidateFirePoint("Final Ranged Enemy", levelScene, fireballPrefab);
            ValidateNoSourceSceneReferences(levelScene, sourceScene);

            EditorSceneManager.SaveScene(levelScene, LevelScenePath);
            EditorSceneManager.CloseScene(sourceScene, true);
            SceneManager.SetActiveScene(levelScene);
            AddLevelToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!Application.isBatchMode)
            {
                Selection.activeGameObject = player;
                EditorGUIUtility.PingObject(player);
            }

            Debug.Log("Cave Level 01 created non-destructively at " + LevelScenePath);
        }

        public static void CreateFromCommandLine()
        {
            CreateLevel01();
        }

        private static void BuildGround(Transform parent, Sprite sprite)
        {
            CreateSolid("Start Ground", parent, new Vector2(4f, -1f), new Vector2(12f, 2f), sprite);
            CreateSolid("Encounter 01 Ground", parent, new Vector2(14f, -1f), new Vector2(6f, 2f), sprite);
            CreateSolid("Traversal Ground", parent, new Vector2(21f, -1f), new Vector2(6f, 2f), sprite);
            CreateSolid("Ranged Ground", parent, new Vector2(29f, -1f), new Vector2(8f, 2f), sprite);
            CreateSolid("Checkpoint 01 Ground", parent, new Vector2(35f, -1f), new Vector2(4f, 2f), sprite);
            CreateSolid("Hazard Ground", parent, new Vector2(43.5f, -1f), new Vector2(13f, 2f), sprite);
            CreateSolid("Final Ground", parent, new Vector2(56.5f, -1f), new Vector2(13f, 2f), sprite);
        }

        private static void BuildPlatforms(Transform parent, Sprite sprite)
        {
            CreateSolid("Traversal Step 01", parent, new Vector2(19.5f, 1.5f), new Vector2(3.5f, 0.5f), sprite);
            CreateSolid("Traversal Step 02", parent, new Vector2(23f, 2.8f), new Vector2(4f, 0.5f), sprite);
            CreateSolid("Ranged Perch", parent, new Vector2(30f, 2.8f), new Vector2(5f, 0.5f), sprite);
            CreateSolid("Hazard Shelter 01", parent, new Vector2(39.5f, 2f), new Vector2(3.5f, 0.5f), sprite);
            CreateSolid("Hazard Shelter 02", parent, new Vector2(45.5f, 2.8f), new Vector2(4f, 0.5f), sprite);
            CreateSolid("Final Ranged Perch", parent, new Vector2(55f, 2.8f), new Vector2(5f, 0.5f), sprite);
        }

        private static GameObject CreateEnemy(
            GameObject source,
            Scene scene,
            Transform parent,
            string name,
            Vector2 position,
            bool ranged,
            GameObject player,
            FireballProjectile fireballPrefab,
            float patrolDistance)
        {
            GameObject enemy = CloneToScene(source, scene);
            enemy.name = name;
            enemy.transform.SetParent(parent, true);
            enemy.transform.position = position;
            enemy.SetActive(true);

            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null)
            {
                SerializedObject serializedController = new SerializedObject(controller);
                serializedController.FindProperty("patrolDistance").floatValue = patrolDistance;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
            }

            EnemyShooter shooter = enemy.GetComponent<EnemyShooter>();
            if (shooter == null)
            {
                throw new System.InvalidOperationException(name + " is missing the existing EnemyShooter component.");
            }

            shooter.enabled = ranged;
            SerializedObject serializedShooter = new SerializedObject(shooter);
            SerializedProperty firePointProperty = serializedShooter.FindProperty("firePoint");
            Transform firePoint = firePointProperty.objectReferenceValue as Transform;
            if (firePoint == null)
            {
                firePoint = FindChild(enemy.transform, "Fire Point");
            }

            if (firePoint == null)
            {
                throw new System.InvalidOperationException(name + " is missing its configured Fire Point.");
            }

            if (ranged)
            {
                firePoint.localPosition = new Vector3(-1.4f, 0.6f, 0f);
            }

            serializedShooter.FindProperty("target").objectReferenceValue = player.transform;
            firePointProperty.objectReferenceValue = firePoint;
            serializedShooter.FindProperty("projectilePrefab").objectReferenceValue = fireballPrefab;
            serializedShooter.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        private static void ConfigureBombSpawner(
            GameObject sourceSpawner,
            Scene scene,
            Transform parent,
            Transform hazardContainer,
            Transform player)
        {
            GameObject spawnerObject = CloneToScene(sourceSpawner, scene);
            spawnerObject.name = "Level 01 Bomb Spawner";
            spawnerObject.transform.SetParent(parent, false);
            spawnerObject.SetActive(true);

            BombSpawner spawner = spawnerObject.GetComponent<BombSpawner>();
            if (spawner == null)
            {
                throw new System.InvalidOperationException("The configured Bomb Spawner component is missing.");
            }

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("hazardContainer").objectReferenceValue = hazardContainer;
            serializedSpawner.FindProperty("player").objectReferenceValue = player;
            serializedSpawner.FindProperty("environmentLayers").intValue = 1 << GroundLayer;
            serializedSpawner.FindProperty("minimumSpawnInterval").floatValue = 3.5f;
            serializedSpawner.FindProperty("maximumSpawnInterval").floatValue = 5f;
            serializedSpawner.FindProperty("initialDelay").floatValue = 2f;
            serializedSpawner.FindProperty("maximumActiveBombs").intValue = 2;
            serializedSpawner.FindProperty("minimumX").floatValue = 38f;
            serializedSpawner.FindProperty("maximumX").floatValue = 47.5f;
            serializedSpawner.FindProperty("spawnHeight").floatValue = 8f;
            serializedSpawner.FindProperty("fallbackGroundY").floatValue = 0.05f;
            serializedSpawner.FindProperty("playerBiasChance").floatValue = 0.35f;
            serializedSpawner.FindProperty("playerOffsetRange").floatValue = 2.5f;
            serializedSpawner.FindProperty("minimumSpawnSeparation").floatValue = 2f;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCheckpoint(string name, Transform parent, Vector2 position)
        {
            GameObject checkpointObject = new GameObject(name);
            checkpointObject.transform.SetParent(parent, false);
            checkpointObject.transform.position = position;

            BoxCollider2D trigger = checkpointObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.offset = new Vector2(0f, 1.5f);
            trigger.size = new Vector2(1.4f, 3f);

            GameObject markerObject = new GameObject("Checkpoint Marker");
            markerObject.transform.SetParent(checkpointObject.transform, false);
            markerObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            markerObject.transform.localScale = new Vector3(0.25f, 2.5f, 1f);
            SpriteRenderer marker = markerObject.AddComponent<SpriteRenderer>();
            marker.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            marker.sortingOrder = 2;

            GameObject spawnPointObject = new GameObject("Safe Spawn Point");
            spawnPointObject.transform.SetParent(checkpointObject.transform, false);
            spawnPointObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            Checkpoint checkpoint = checkpointObject.AddComponent<Checkpoint>();
            SerializedObject serializedCheckpoint = new SerializedObject(checkpoint);
            serializedCheckpoint.FindProperty("spawnPoint").objectReferenceValue = spawnPointObject.transform;
            serializedCheckpoint.FindProperty("indicatorRenderer").objectReferenceValue = marker;
            serializedCheckpoint.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateLevelExit(Scene scene, Sprite sprite)
        {
            GameObject exitObject = new GameObject("Level Exit");
            SceneManager.MoveGameObjectToScene(exitObject, scene);
            exitObject.transform.position = new Vector3(61f, 0f, 0f);

            BoxCollider2D trigger = exitObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.offset = new Vector2(0f, 1.75f);
            trigger.size = new Vector2(2f, 3.5f);
            exitObject.AddComponent<LevelExit>();

            GameObject visual = new GameObject("Exit Door Visual");
            visual.transform.SetParent(exitObject.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 1;
            ScaleSpriteToSize(visual.transform, sprite, new Vector2(2f, 3f));
        }

        private static void CreateDeathBoundary(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject boundary = new GameObject(name);
            boundary.transform.SetParent(parent, false);
            boundary.transform.position = position;
            BoxCollider2D collider = boundary.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;
            boundary.AddComponent<DeathBoundary>();
        }

        private static GameObject CreateSolid(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Sprite sprite)
        {
            GameObject solid = new GameObject(name);
            solid.layer = GroundLayer;
            solid.transform.SetParent(parent, false);
            solid.transform.position = position;

            SpriteRenderer renderer = solid.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.42f, 0.4f, 0.46f, 1f);
            renderer.sortingOrder = 0;
            ScaleSpriteToSize(solid.transform, sprite, size);

            BoxCollider2D collider = solid.AddComponent<BoxCollider2D>();
            collider.size = sprite.bounds.size;
            collider.offset = sprite.bounds.center;
            return solid;
        }

        private static void CreateBackground(Transform parent, Sprite sprite)
        {
            GameObject background = new GameObject("Cave Background");
            background.transform.SetParent(parent, false);
            background.transform.position = new Vector3(30f, 4f, 5f);
            SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.3f, 0.3f, 0.36f, 1f);
            renderer.sortingOrder = -10;
            ScaleSpriteToSize(background.transform, sprite, new Vector2(70f, 20f));
        }

        private static void ScaleSpriteToSize(Transform target, Sprite sprite, Vector2 size)
        {
            Vector2 spriteSize = sprite.bounds.size;
            float width = Mathf.Max(0.01f, spriteSize.x);
            float height = Mathf.Max(0.01f, spriteSize.y);
            target.localScale = new Vector3(size.x / width, size.y / height, 1f);
        }

        private static void ValidateFirePoint(string enemyName, Scene scene, FireballProjectile prefab)
        {
            GameObject enemy = FindSceneObject(scene, enemyName);
            EnemyShooter shooter = enemy != null ? enemy.GetComponent<EnemyShooter>() : null;
            if (shooter == null)
            {
                throw new System.InvalidOperationException(enemyName + " is missing EnemyShooter.");
            }

            SerializedObject serializedShooter = new SerializedObject(shooter);
            Transform firePoint = serializedShooter.FindProperty("firePoint").objectReferenceValue as Transform;
            Collider2D prefabCollider = prefab.GetComponent<Collider2D>();
            Vector2 checkSize = prefabCollider != null
                ? Vector2.Max(prefabCollider.bounds.size, new Vector2(0.35f, 0.35f))
                : new Vector2(0.35f, 0.35f);

            Collider2D[] overlaps = Physics2D.OverlapBoxAll(firePoint.position, checkSize, 0f);
            if (overlaps.Length > 0)
            {
                throw new System.InvalidOperationException(
                    enemyName + " Fire Point overlaps collider '" + overlaps[0].name + "'.");
            }

            if (serializedShooter.FindProperty("projectilePrefab").objectReferenceValue != prefab)
            {
                throw new System.InvalidOperationException(enemyName + " is not wired to the Fireball prefab asset.");
            }
        }

        private static void ValidateNoSourceSceneReferences(Scene levelScene, Scene sourceScene)
        {
            foreach (GameObject root in levelScene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        continue;
                    }

                    SerializedObject serializedObject = new SerializedObject(behaviour);
                    SerializedProperty property = serializedObject.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference)
                        {
                            continue;
                        }

                        UnityEngine.Object reference = property.objectReferenceValue;
                        GameObject referencedObject = reference as GameObject;
                        if (referencedObject == null && reference is Component component)
                        {
                            referencedObject = component.gameObject;
                        }

                        if (referencedObject != null && referencedObject.scene == sourceScene)
                        {
                            throw new System.InvalidOperationException(
                                behaviour.name + "." + property.propertyPath +
                                " still references the Sprint 5 source scene.");
                        }
                    }
                }
            }
        }

        private static GameObject CloneToScene(GameObject source, Scene scene)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source);
            clone.name = source.name;
            if (clone.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(clone, scene);
            }

            return clone;
        }

        private static GameObject CreateRoot(string name, Scene scene)
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static Transform CreateContainer(string name, Transform parent)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            return container.transform;
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform;
                }
            }

            return null;
        }

        private static void RequireSourceObject(GameObject gameObject, string name)
        {
            if (gameObject == null)
            {
                throw new System.InvalidOperationException(
                    "Sprint5_Hazards does not contain the configured " + name + ".");
            }
        }

        private static Sprite LoadSpriteOrPlaceholder(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null
                ? sprite
                : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void AddLevelToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == LevelScenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(LevelScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
