using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class DomainRuntimeWorldBridgeVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Runtime World Bridge Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainRuntimeWorldBridgeVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] DOMAIN RUNTIME WORLD BRIDGE PASS");
                return;
            }

            Debug.LogError("[Cave] Domain runtime world bridge verification failed: " + failure);
        }
    }
}
