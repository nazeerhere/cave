using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure base phenomenon-operation verification.</summary>
    public static class PhenomenonOperationsVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Phenomenon Operations Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (PhenomenonOperationsVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Phenomenon operations verification passed.");
                return;
            }

            Debug.LogError("[Cave] Phenomenon operations verification failed: " + failure);
        }
    }
}
