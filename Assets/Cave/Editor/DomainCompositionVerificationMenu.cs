using Cave.Domain;
using UnityEditor;
using UnityEngine;
namespace Cave.EditorTools
{
    public static class DomainCompositionVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Composition Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainCompositionVerification.TryRunAll(out failure)) { Debug.Log("[Cave] Domain composition verification passed."); return; }
            Debug.LogError("[Cave] Domain composition verification failed: " + failure);
        }
    }
}
