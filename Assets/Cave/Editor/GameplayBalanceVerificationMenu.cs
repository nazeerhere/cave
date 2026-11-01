#if UNITY_EDITOR
using Cave.Enemies;
using Cave.Axioms.Mastery;
using Cave.Domain;
using Cave.Player;
using Cave.Projectiles;
using Cave.UI;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class GameplayBalanceVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Gameplay Balance Verification")]
        public static void RunFromMenu()
        {
            string failure;
            if (GameplayBalanceVerification.TryRunAll(out failure)
                && VerifySerializedValues(out failure)
                && HeavyProjectilePrefabVerification.TryVerify(out failure)
                && VerifyExistingRegressionSuites(out failure))
            {
                Debug.Log("[Cave] Gameplay Balance verification passed.");
                return;
            }

            Debug.LogError("[Cave] Gameplay Balance verification failed: " + failure);
        }

        public static void RunBatch()
        {
            RunFromMenu();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(EditorApplication.isCompiling ? 1 : 0);
            }
        }

        private static bool VerifySerializedValues(out string failure)
        {
            if (!VerifyFloat("Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab", "hookCooldown", 5.875f, out failure)
                || !VerifyFloat("Assets/Cave/Prefabs/Mobs/Melee/Corrupt Brute .prefab", "hookCooldown", 5.875f, out failure))
            {
                return false;
            }

            GameObject heavy = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Cave/Prefabs/Projectiles/HeavyPlayerProjectile.prefab");
            PlayerProjectile projectile = heavy != null ? heavy.GetComponent<PlayerProjectile>() : null;
            SerializedObject serialized = projectile != null ? new SerializedObject(projectile) : null;
            Object explosion = serialized != null
                ? serialized.FindProperty("heavyExplosionVisualPrefab")?.objectReferenceValue
                : null;
            SpriteRenderer renderer = heavy != null ? heavy.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (projectile == null || renderer == null || renderer.sprite == null || explosion == null)
            {
                failure = "Heavy prefab is missing its sprite renderer or presentation-only impact visual reference.";
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyExistingRegressionSuites(out string failure)
        {
            return FirePatternAndProgressionVerification.TryRunAll(out failure)
                && HeavyProjectileSprintVerification.TryRunAll(out failure)
                && OblivionTrapGameplayVerification.TryRunAll(out failure)
                && BraceResourceRecoveryVerification.TryRunAll(out failure)
                && DomainLawVerification.TryRunAll(out failure)
                && PhenomenonSemanticsVerification.TryRunAll(out failure)
                && PhenomenonOperationsVerification.TryRunAll(out failure)
                && DomainLawEvaluationVerification.TryRunAll(out failure)
                && PhenomenonTopologyVerification.TryRunAll(out failure)
                && CatalysisEvaluationVerification.TryRunAll(out failure)
                && AccumulationEvaluationVerification.TryRunAll(out failure)
                && PhenomenonRelationshipGroupVerification.TryRunAll(out failure)
                && SynchronizationEvaluationVerification.TryRunAll(out failure)
                && InterferenceEvaluationVerification.TryRunAll(out failure)
                && DomainCompositionVerification.TryRunAll(out failure)
                && DomainComplexityCapacityProgressionVerification.TryRunAll(out failure)
                && PlayerMasteryEvidenceVerification.TryRunAll(out failure)
                && AxiomMasteryIntegrationVerification.TryRunAll(out failure)
                && DomainAuthoringEligibilityVerification.TryRunAll(out failure)
                && DomainLawAuthoringVerification.TryRunAll(out failure)
                && DomainResolutionPlannerVerification.TryRunAll(out failure)
                && DomainContextualUiVerification.TryRunAll(out failure);
        }

        private static bool VerifyFloat(string assetPath, string propertyName, float expected, out string failure)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            BruteControlAbilities abilities = prefab != null ? prefab.GetComponent<BruteControlAbilities>() : null;
            SerializedProperty property = abilities != null
                ? new SerializedObject(abilities).FindProperty(propertyName)
                : null;
            if (property == null || !Mathf.Approximately(property.floatValue, expected))
            {
                failure = assetPath + " does not contain " + propertyName + " = " + expected + ".";
                return false;
            }

            failure = null;
            return true;
        }
    }
}
#endif
