using Cave.Missions;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class ContractRecoveryVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Contract Recovery Verification")]
        private static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Cave] Stop Play Mode before running Contract Recovery verification.");
                return;
            }

            if (ContractRecoveryVerification.TryRunAll(out string failure))
                Debug.Log("[Cave] CONTRACT RECOVERY VERIFICATION PASS");
            else
                Debug.LogError("[Cave] CONTRACT RECOVERY VERIFICATION FAILED: " + failure);
        }
    }
}
