using Cave.Domain;
using UnityEditor;
using UnityEngine;
namespace Cave.EditorTools
{
    public static class PhenomenonStateModelVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Phenomenon State Model Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (PhenomenonStateModelVerification.TryRunAll(out failure)) Debug.Log("[Cave] PHENOMENON STATE MODEL PASS");
            else Debug.LogError("[Cave] Phenomenon state-model verification failed: " + failure);
        }
    }
}
