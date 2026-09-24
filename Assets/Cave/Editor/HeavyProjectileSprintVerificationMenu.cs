using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class HeavyProjectileSprintVerificationMenu
    {
        // Deliberately mirrors the project's existing verification-menu pattern.
        [MenuItem("Tools/Cave/Verification/Run Heavy Projectile / Ice Tier 3 Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (HeavyProjectileSprintVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Heavy Projectile / Ice Tier 3 verification passed.");
                return;
            }

            Debug.LogError("[Cave] Heavy Projectile / Ice Tier 3 verification failed: " + failure);
        }
    }
}
