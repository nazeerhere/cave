#if UNITY_EDITOR
using Cave.Gambit;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class SovereignGambitStandardMatchVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Sovereign Gambit Standard Match Verification")]
        private static void Run()
        {
            if (!SovereignGambitStandardMatchVerification.TryRunAll(out string failure))
                throw new System.InvalidOperationException("Sovereign Gambit Phase 3 verification failed: " + failure);
            Debug.Log("[Cave][Verification] Sovereign Gambit Phase 3 standard match verification passed.");
        }
    }
}
#endif
