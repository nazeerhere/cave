using Cave.Axioms.Control;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class AxiomDynamicsControlUnificationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Axiom Dynamics Control Unification Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AxiomDynamicsControlUnificationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] AXIOM DYNAMICS CONTROL UNIFICATION PASS");
                return;
            }

            Debug.LogError("[Cave] Axiom dynamics/control unification verification failed: " + failure);
        }
    }
}
