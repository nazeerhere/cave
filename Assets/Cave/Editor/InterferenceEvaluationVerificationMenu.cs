using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure constructive Interference verification.</summary>
    public static class InterferenceEvaluationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Interference Evaluation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (InterferenceEvaluationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Interference evaluation verification passed.");
                return;
            }

            Debug.LogError("[Cave] Interference evaluation verification failed: " + failure);
        }
    }
}
