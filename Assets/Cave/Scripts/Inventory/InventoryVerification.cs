using UnityEngine;

namespace Cave.Inventory
{
    /// <summary>Focused deterministic checks for the collected-item authority.</summary>
    public static class InventoryVerification
    {
        public static bool VerifyAddRemoveHasItem(out string failure)
        {
            GameObject owner = new GameObject("Inventory Verification");
            InventoryItemDefinition material = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            PlayerCollectedInventory inventory = owner.AddComponent<PlayerCollectedInventory>();
            bool changed = false;
            inventory.Changed += () => changed = true;
            bool valid = inventory.Add(material, 3)
                && inventory.HasItem(material, 3)
                && inventory.GetQuantity(material) == 3
                && inventory.Remove(material, 2)
                && inventory.GetQuantity(material) == 1
                && !inventory.Remove(material, 2)
                && changed;
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(owner);
            failure = valid ? null : "Collected inventory Add/Remove/HasItem contract failed.";
            return valid;
        }
    }
}
