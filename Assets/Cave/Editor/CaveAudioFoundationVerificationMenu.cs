using Cave.Audio;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class CaveAudioFoundationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Audio Foundation Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (CaveAudioFoundationVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Audio foundation deterministic verification passed. Play Mode remains required for pooled AudioSource lifecycle validation.");
                return;
            }

            Debug.LogError("[Cave] Audio foundation verification failed: " + failure);
        }
    }
}
