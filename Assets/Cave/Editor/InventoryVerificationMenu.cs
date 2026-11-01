using Cave.Inventory;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class InventoryVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Inventory")]
        private static void Verify()
        {
            string failure;
            if (!InventoryVerification.VerifyAddRemoveHasItem(out failure))
            {
                Debug.LogError("[Cave] INVENTORY VERIFICATION FAILED: " + failure);
                return;
            }
            Debug.Log("[Cave] INVENTORY VERIFICATION PASS");
        }
    }
}
