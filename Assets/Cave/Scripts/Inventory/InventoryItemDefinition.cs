using UnityEngine;

namespace Cave.Inventory
{
    public enum InventoryItemCategory
    {
        Materials,
        Consumables,
        Equipment
    }

    /// <summary>Authoring data only. Quantities belong to PlayerCollectedInventory.</summary>
    [CreateAssetMenu(menuName = "Cave/Inventory/Item Definition", fileName = "InventoryItem")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private InventoryItemCategory category = InventoryItemCategory.Materials;
        [SerializeField] private bool stackable = true;
        [Tooltip("Use for future Flora/material definitions that are lost on player death.")]
        [SerializeField] private bool lostOnDeath;
        [SerializeField] private Sprite icon;

        public string ItemId => itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public InventoryItemCategory Category => category;
        public bool Stackable => stackable;
        public bool LostOnDeath => lostOnDeath;
        public Sprite Icon => icon;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId)) itemId = name;
        }
    }
}
