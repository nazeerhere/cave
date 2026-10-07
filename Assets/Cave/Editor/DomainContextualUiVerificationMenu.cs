using Cave.UI;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class DomainContextualUiVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Contextual UI Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainContextualUiVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] DOMAIN CONTEXTUAL UI PASS");
                return;
            }
            Debug.LogError("[Cave] Domain contextual UI verification failed: " + failure);
        }
    }
}
