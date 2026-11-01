#if UNITY_EDITOR
using Cave.Combat;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerMovementFrenzyGeometryVerificationMenu
    {
        private const string SessionKey = "Cave.Batch1MovementFrenzyGeometryVerification.v1";

        static PlayerMovementFrenzyGeometryVerificationMenu()
        {
            EditorApplication.delayCall += RunOnceAfterCompile;
        }

        private static void RunOnceAfterCompile()
        {
            if (SessionState.GetBool(SessionKey, false)
                || EditorApplication.isCompiling
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            RunFromMenu();
        }

        [MenuItem("Tools/Cave/Verification/Run Player Movement and Frenzy Geometry Verification")]
        private static void RunFromMenu()
        {
            if (!PlayerMovementFrenzyGeometryVerification.TryRunAll(out string failure))
            {
                throw new System.InvalidOperationException(
                    "Player movement/Frenzy geometry verification failed: " + failure);
            }

            Debug.Log("[Cave][Verification] Player movement and Frenzy geometry verification passed.");
        }
    }
}
#endif
