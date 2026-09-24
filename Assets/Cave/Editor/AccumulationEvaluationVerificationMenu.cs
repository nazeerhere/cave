using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure Accumulation verification.</summary>
    public static class AccumulationEvaluationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Accumulation Evaluation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AccumulationEvaluationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Accumulation evaluation verification passed.");
                return;
            }

            Debug.LogError("[Cave] Accumulation evaluation verification failed: " + failure);
        }
    }
}
