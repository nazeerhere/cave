using Cave.Player;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class ResonantCarapaceVerificationMenu
    {
        [MenuItem("Cave/Verification/Resonant Carapace")]
        private static void Verify()
        {
            if (!ResonantCarapaceVerification.TryVerify(out string failure))
            {
                Debug.LogError("RESONANT CARAPACE VERIFICATION FAILED: " + failure);
                return;
            }
            Debug.Log("RESONANT CARAPACE VERIFICATION PASS");
        }
    }
}
