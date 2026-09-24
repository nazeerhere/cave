using Cave.Enemies;
using Cave.Player;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class KnowledgeArchitectureVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Knowledge Architecture / Sword Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (!KnowledgeArchitectureVerification.TryRunAll(out failure)
                || !CombatTacticalCoordinatorVerification.TryRunAll(out failure)
                || !PlayerSwordUpgradeVerification.TryRunAll(out failure))
            {
                Debug.LogError("[Cave] Knowledge Architecture / Sword verification failed: " + failure);
                return;
            }

            Debug.Log("[Cave] Knowledge Architecture / Sword verification passed.");
        }
    }
}
