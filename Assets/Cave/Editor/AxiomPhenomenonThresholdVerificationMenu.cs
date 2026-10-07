using Cave.Axioms.Thresholds;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class AxiomPhenomenonThresholdVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Phenomenon Threshold Foundation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AxiomPhenomenonThresholdVerification.TryRunAll(out failure)) { Debug.Log("[Cave] PHENOMENON THRESHOLD FOUNDATION PASS"); return; }
            Debug.LogError("[Cave] Phenomenon threshold foundation verification failed: " + failure);
        }
    }
}
