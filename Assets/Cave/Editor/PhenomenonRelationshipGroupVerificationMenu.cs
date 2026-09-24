using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure persistent relationship-group verification.</summary>
    public static class PhenomenonRelationshipGroupVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Phenomenon Relationship Group Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (PhenomenonRelationshipGroupVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Phenomenon relationship group verification passed.");
                return;
            }

            Debug.LogError("[Cave] Phenomenon relationship group verification failed: " + failure);
        }
    }
}
