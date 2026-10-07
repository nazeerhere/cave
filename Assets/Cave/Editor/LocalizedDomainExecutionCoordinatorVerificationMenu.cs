#if UNITY_EDITOR
using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class LocalizedDomainExecutionCoordinatorVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Localized Domain Execution Coordinator Verification")]
        private static void Run()
        {
            string failure;
            if (LocalizedDomainExecutionCoordinatorVerification.TryRunAll(out failure)) Debug.Log("[Cave] LOCALIZED DOMAIN EXECUTION COORDINATOR PASS");
            else Debug.LogError("[Cave] Localized Domain execution coordinator verification failed: " + failure);
        }
    }
}
#endif
