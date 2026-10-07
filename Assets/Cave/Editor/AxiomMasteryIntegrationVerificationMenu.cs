using Cave.Axioms.Mastery;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class AxiomMasteryIntegrationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Axiom Mastery Integration Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AxiomMasteryIntegrationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] AXIOM MASTERY INTEGRATION PASS");
                return;
            }
            Debug.LogError("[Cave] Axiom mastery integration verification failed: " + failure);
        }
    }
}
