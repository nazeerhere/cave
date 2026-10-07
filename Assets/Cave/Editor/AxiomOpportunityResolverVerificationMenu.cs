using Cave.Axioms.Control;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class AxiomOpportunityResolverVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Axiom Opportunity Resolver Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AxiomOpportunityResolverVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] AXIOM OPPORTUNITY RESOLVER PASS");
                return;
            }

            Debug.LogError("[Cave] Axiom opportunity resolver verification failed: " + failure);
        }
    }
}
