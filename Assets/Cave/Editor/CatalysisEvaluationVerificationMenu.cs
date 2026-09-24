using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure Catalysis verification.</summary>
    public static class CatalysisEvaluationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Catalysis Evaluation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (CatalysisEvaluationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Catalysis evaluation verification passed.");
                return;
            }

            Debug.LogError("[Cave] Catalysis evaluation verification failed: " + failure);
        }
    }
}
