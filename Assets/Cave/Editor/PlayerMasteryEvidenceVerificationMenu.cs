using Cave.Axioms.Mastery;
using UnityEditor;
using UnityEngine;
namespace Cave.EditorTools
{
    public static class PlayerMasteryEvidenceVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Player Mastery Verification")]
        private static void RunFromMenu()
        { string failure; if (PlayerMasteryEvidenceVerification.TryRunAll(out failure)) { Debug.Log("[Cave] Player Mastery verification passed."); return; } Debug.LogError("[Cave] Player Mastery verification failed: " + failure); }
    }
}
