using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for the pure Domain Law schema verifier.</summary>
    public static class DomainLawVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Law Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainLawVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Domain Law verification passed.");
                return;
            }

            Debug.LogError("[Cave] Domain Law verification failed: " + failure);
        }
    }
}
