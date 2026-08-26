using Cave.Combat;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class DetectivePrefabBuilder
    {
        private const string PrefabPath = "Assets/Cave/Prefabs/Detective.prefab";
        private const string SpriteSheetPath =
            "Assets/Brackeys/2D Mega Pack/Characters/Detective.png";
        private const string AnimatorPath =
            "Assets/Brackeys/2D Mega Pack/Characters/Animation/Detective/Detective.controller";

        [InitializeOnLoadMethod]
        private static void ScheduleBuildIfMissing()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("Tools/Cave/Build Detective Prototype Prefab")]
        public static void BuildFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log($"Detective prefab already exists at {PrefabPath}; no changes made.");
                return;
            }

            BuildPrefab();
        }

        private static void BuildIfMissing()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            BuildPrefab();
        }

        private static void BuildPrefab()
        {
            Sprite detectiveSprite = FindDetectiveSprite();
            RuntimeAnimatorController animatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);

            GameObject root = new GameObject("Detective");
            try
            {
                root.layer = LayerMask.NameToLayer("Damageable");

                SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = detectiveSprite;

                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;

                Rigidbody2D body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;

                BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.65f, 1.25f);
                collider.offset = new Vector2(0f, 0.625f);

                Damageable damageable = root.AddComponent<Damageable>();
                SetInteger(damageable, "maxHealth", 2);

                EnemyController controller = root.AddComponent<EnemyController>();
                SetFloat(controller, "moveSpeed", 2.6f);
                SetFloat(controller, "patrolDistance", 3f);

                EnemyStagger stagger = root.AddComponent<EnemyStagger>();
                SetInteger(stagger, "eligibilityOverride", (int)StaggerEligibilityOverride.Immune);
                root.AddComponent<EnemyStaggerVisuals>();
                root.AddComponent<EnemyPoisonShooter>();
                root.AddComponent<EnemyKeepDistance>();
                root.AddComponent<DetectiveStunGrenadeAbility>();
                root.AddComponent<DetectiveSuppressionZoneAbility>();
                DetectiveProjectileRedirector redirector =
                    root.AddComponent<DetectiveProjectileRedirector>();
                SetInteger(
                    redirector,
                    "projectileLayers",
                    1 << LayerMask.NameToLayer("Projectile"));
                root.AddComponent<DetectiveIdentity>();
                root.AddComponent<DetectiveCloneAbility>();
                root.AddComponent<DetectiveSacrificialAttack>();
                root.AddComponent<EnemyAllyCollisionPhasing>();
                root.AddComponent<EnemySkillEvolution>();
                root.AddComponent<EnemyDifficultyScaler>();
                DetectiveBrain brain = root.AddComponent<DetectiveBrain>();
                SetInteger(
                    brain,
                    "lineOfSightBlockingLayers",
                    1 << LayerMask.NameToLayer("Ground"));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created Detective prototype prefab at {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Sprite FindDetectiveSprite()
        {
            Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteSheetPath);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == "Detective_0")
                {
                    return sprite;
                }
            }

            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite && !sprite.name.Contains("Skeleton"))
                {
                    return sprite;
                }
            }

            Debug.LogWarning(
                $"Detective sprite was not found at {SpriteSheetPath}; prefab will retain an empty presentation slot.");
            return null;
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
