using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class FirePatternAndProgressionVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Fire Pattern + Progression Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (FirePatternAndProgressionVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Fire Pattern + Progression verification passed.");
                return;
            }

            Debug.LogError("[Cave] Fire Pattern + Progression verification failed: " + failure);
        }
    }
}
