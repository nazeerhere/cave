#if UNITY_EDITOR
using System;
using Cave.Gambit;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class SovereignGambitFoundationVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Sovereign Gambit Foundation Verification")]
        public static void Run()
        {
            string failure;
            if(!SovereignGambitFoundationVerification.TryRunAll(out failure))throw new InvalidOperationException("Sovereign Gambit foundation verification failed: "+failure);
            Debug.Log("[Cave] Sovereign Gambit foundation verification passed.");
        }
    }
}
#endif
