using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class AxiomStatusPresentationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Axiom Status Presentation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AxiomStatusPresentationVerification.TryRunAll(out failure)
                && AxiomStatusPresentationVerification.TryVerifyUnityCellSeam(out failure))
            {
                Debug.Log("[Cave] AXIOM STATUS PRESENTATION PASS");
                return;
            }

            Debug.LogError("[Cave] Axiom status presentation verification failed: " + failure);
        }
    }
}
