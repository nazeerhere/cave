using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class CombatTacticalCoordinatorVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Combat Tactical Coordinator Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (CombatTacticalCoordinatorVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Combat Tactical Coordinator verification passed.");
                return;
            }

            Debug.LogError("[Cave] Combat Tactical Coordinator verification failed: " + failure);
        }
    }
}
