#if UNITY_EDITOR
using Cave.Axioms.Elemental;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class ThreeStateAxiomDynamicsVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Axiom Three-State Control Verification")]
        public static void Run()
        {
            string failure;
            if (!ThreeStateAxiomDynamicsVerification.TryRunAll(out failure)
                || !AuthoritativeThreeStateControlVerification.TryRunAll(out failure))
            {
                throw new System.InvalidOperationException("Axiom Three-State Control verification failed: " + failure);
            }

            Debug.Log("[Cave] AXIOM THREE-STATE CONTROL PASS");
        }
    }
}
#endif
