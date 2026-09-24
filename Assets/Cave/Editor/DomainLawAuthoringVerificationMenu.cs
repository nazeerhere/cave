using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class DomainLawAuthoringVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Domain Law Authoring Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (DomainLawAuthoringVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] DOMAIN LAW AUTHORING PASS");
                return;
            }

            Debug.LogError("[Cave] DOMAIN LAW AUTHORING FAIL: " + failure);
        }
    }
}
