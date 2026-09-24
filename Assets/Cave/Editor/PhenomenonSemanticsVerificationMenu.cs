using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure Domain semantic-region verification.</summary>
    public static class PhenomenonSemanticsVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Phenomenon Semantics Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (PhenomenonSemanticsVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Phenomenon semantics verification passed.");
                return;
            }

            Debug.LogError("[Cave] Phenomenon semantics verification failed: " + failure);
        }
    }
}
