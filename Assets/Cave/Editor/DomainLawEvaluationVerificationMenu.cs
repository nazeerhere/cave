using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure single-Law evaluation verification.</summary>
    public static class DomainLawEvaluationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Law Evaluation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainLawEvaluationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Domain Law evaluation verification passed.");
                return;
            }

            Debug.LogError("[Cave] Domain Law evaluation verification failed: " + failure);
        }
    }
}
