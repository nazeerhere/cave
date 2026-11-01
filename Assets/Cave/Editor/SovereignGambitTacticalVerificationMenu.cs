#if UNITY_EDITOR
using Cave.Gambit;
using UnityEditor;

namespace Cave.Editor
{
    public static class SovereignGambitTacticalVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Sovereign Gambit Tactical Verification")]
        private static void Run(){SovereignGambitTacticalVerification.Run();}
    }
}
#endif
