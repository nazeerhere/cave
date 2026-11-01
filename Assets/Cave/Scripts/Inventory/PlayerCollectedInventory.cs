using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Inventory
{
    /// <summary>
    /// Player-owned collected items only. It deliberately excludes potions and
    /// Oblivion Disks, which remain owned by PlayerLandmineInventory.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCollectedInventory : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
            [SerializeField] private InventoryItemDefinition definition;
            [SerializeField, Min(1)] private int quantity;
            public Entry(InventoryItemDefinition item, int count) { definition = item; quantity = Mathf.Max(1, count); }
            public InventoryItemDefinition Definition => definition;
            public int Quantity => quantity;
            public Entry WithQuantity(int value) { return new Entry(definition, value); }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        public event Action Changed;

        public int GetQuantity(InventoryItemDefinition definition)
        {
            if (definition == null) return 0;
            int total = 0;
            for (int index = 0; index < entries.Count; index++)
                if (entries[index].Definition == definition) total += entries[index].Quantity;
            return total;
        }

        public bool HasItem(InventoryItemDefinition definition, int quantity = 1)
        {
            return definition != null && quantity > 0 && GetQuantity(definition) >= quantity;
        }

        public bool Add(InventoryItemDefinition definition, int quantity = 1)
        {
            if (definition == null || quantity <= 0) return false;
            if (definition.Stackable)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    if (entries[index].Definition != definition) continue;
                    entries[index] = entries[index].WithQuantity(entries[index].Quantity + quantity);
                    Changed?.Invoke();
                    return true;
                }
                entries.Add(new Entry(definition, quantity));
            }
            else
            {
                for (int index = 0; index < quantity; index++) entries.Add(new Entry(definition, 1));
            }
            Changed?.Invoke();
            return true;
        }

        public bool Remove(InventoryItemDefinition definition, int quantity = 1)
        {
            if (!HasItem(definition, quantity)) return false;
            for (int index = entries.Count - 1; index >= 0 && quantity > 0; index--)
            {
                if (entries[index].Definition != definition) continue;
                int remove = Mathf.Min(entries[index].Quantity, quantity);
                int remaining = entries[index].Quantity - remove;
                quantity -= remove;
                if (remaining == 0) entries.RemoveAt(index); else entries[index] = entries[index].WithQuantity(remaining);
            }
            Changed?.Invoke();
            return true;
        }

        public void CopyEntries(List<InventoryEntrySnapshot> destination, InventoryItemCategory category)
        {
            if (destination == null) return;
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry.Definition != null && entry.Definition.Category == category)
                    destination.Add(new InventoryEntrySnapshot(entry.Definition.DisplayName, entry.Definition.Description, entry.Quantity, entry.Definition.Icon));
            }
        }

        /// <summary>Only definitions explicitly marked for death loss are removed.</summary>
        public void RemoveDeathLossItems()
        {
            bool changed = false;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index].Definition != null && entries[index].Definition.LostOnDeath)
                {
                    entries.RemoveAt(index);
                    changed = true;
                }
            }
            if (changed) Changed?.Invoke();
        }
    }

    public struct InventoryEntrySnapshot
    {
        public readonly string Name;
        public readonly string Description;
        public readonly int Quantity;
        public readonly Sprite Icon;
        public InventoryEntrySnapshot(string name, string description, int quantity, Sprite icon)
        { Name = name; Description = description; Quantity = quantity; Icon = icon; }
    }

    /// <summary>Optional bridge implemented by the real Carapace authority once installed.</summary>
    public interface IResonantCarapaceInventorySource
    {
        bool IsOwned { get; }
        bool IsEquipped { get; }
        string ConditionText { get; }
        bool TryGrantAndEquipForDevelopment();
    }
}
