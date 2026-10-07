using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class AxiomReactiveStatusPresentationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Axiom Reactive Status Presentation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AxiomReactiveStatusPresentationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] AXIOM REACTIVE STATUS PRESENTATION PASS");
                return;
            }

            Debug.LogError("[Cave] Axiom reactive status presentation verification failed: " + failure);
        }

        public static void RunBatch()
        {
            string failure;
            if (!AxiomReactiveStatusPresentationVerification.TryRunAll(out failure))
            {
                throw new System.InvalidOperationException(failure);
            }

            Debug.Log("[Cave] AXIOM REACTIVE STATUS PRESENTATION PASS");
        }
    }
}
