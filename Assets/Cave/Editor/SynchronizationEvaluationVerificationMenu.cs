using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure persistent Synchronization verification.</summary>
    public static class SynchronizationEvaluationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Synchronization Evaluation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (SynchronizationEvaluationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Synchronization evaluation verification passed.");
                return;
            }

            Debug.LogError("[Cave] Synchronization evaluation verification failed: " + failure);
        }
    }
}
