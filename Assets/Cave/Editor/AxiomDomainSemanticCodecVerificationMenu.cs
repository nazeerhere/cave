using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class AxiomDomainSemanticCodecVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Semantic Codec Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (AxiomDomainSemanticCodecVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] DOMAIN SEMANTIC CODEC PASS");
                return;
            }

            Debug.LogError("[Cave] Domain semantic codec verification failed: " + failure);
        }
    }
}
