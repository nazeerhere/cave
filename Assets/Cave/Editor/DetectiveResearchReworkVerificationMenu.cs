#if UNITY_EDITOR
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class DetectiveResearchReworkVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Detective Research Rework Verification")]
        private static void RunFromMenu()
        {
            if (!DetectiveResearchReworkVerification.TryRunAll(out string failure))
            {
                throw new System.InvalidOperationException(
                    "Detective research rework verification failed: " + failure);
            }

            Debug.Log("[Cave][Verification] Detective research rework verification passed.");
        }
    }
}
#endif
