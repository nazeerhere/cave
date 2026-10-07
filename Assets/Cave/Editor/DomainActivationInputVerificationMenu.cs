#if UNITY_EDITOR
using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class DomainActivationInputVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Activation Input Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainActivationInputVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Domain activation input verification passed.");
                return;
            }

            Debug.LogError("[Cave] Domain activation input verification failed: " + failure);
        }
    }
}
#endif
