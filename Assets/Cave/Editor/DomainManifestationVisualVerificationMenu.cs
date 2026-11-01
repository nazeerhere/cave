using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class DomainManifestationVisualVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Manifestation Visual Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainManifestationVisualVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] DOMAIN MANIFESTATION VISUAL PASS");
                return;
            }

            Debug.LogError("[Cave] Domain manifestation visual verification failed: " + failure);
        }
    }
}
