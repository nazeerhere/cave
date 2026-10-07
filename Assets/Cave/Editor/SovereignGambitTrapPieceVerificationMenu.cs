#if UNITY_EDITOR
using System;
using Cave.Gambit;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class SovereignGambitTrapPieceVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Sovereign Gambit Trap Piece Verification")]
        public static void Run()
        {
            string failure;
            if(!SovereignGambitTrapPieceVerification.TryRunAll(out failure))throw new InvalidOperationException(failure);
            Debug.Log("[Cave] SOVEREIGN GAMBIT TRAP PIECE PASS");
        }
    }
}
#endif
