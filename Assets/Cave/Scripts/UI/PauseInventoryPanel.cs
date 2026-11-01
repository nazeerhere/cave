using System.Collections.Generic;
using Cave.Inventory;
using Cave.Player;
using Cave.World;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Paused, read-only inventory presentation. It observes existing owners.</summary>
    public sealed class PauseInventoryPanel : MonoBehaviour
    {
        private Text heading;
        private Text body;
        private Image icon;
        private InventoryItemCategory category;
        private PlayerCollectedInventory collected;
        private PlayerLandmineInventory consumables;
        private IResonantCarapaceInventorySource carapace;
        private float nextRefresh;

        public void Configure(Text configuredHeading, Text configuredBody, Image configuredIcon)
        {
            heading = configuredHeading;
            body = configuredBody;
            icon = configuredIcon;
            ResolveOwners();
            Refresh();
        }

        public void ShowCategory(InventoryItemCategory value)
        {
            category = value;
            Refresh();
        }

        private void OnEnable() { ResolveOwners(); Refresh(); }
        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .2f;
                Refresh();
            }
        }

        private void ResolveOwners()
        {
            PlayerHealth health = PlayerRunPersistence.CurrentPlayerHealth ?? FindObjectOfType<PlayerHealth>();
            if (health == null) return;
            collected = health.GetComponent<PlayerCollectedInventory>();
            consumables = health.GetComponent<PlayerLandmineInventory>();
            carapace = null;
            MonoBehaviour[] candidates = health.GetComponents<MonoBehaviour>();
            for (int index = 0; index < candidates.Length; index++)
            {
                carapace = candidates[index] as IResonantCarapaceInventorySource;
                if (carapace != null) break;
            }
        }

        private void Refresh()
        {
            if (heading != null) heading.text = "INVENTORY — " + category.ToString().ToUpperInvariant();
            if (body == null) return;
            List<InventoryEntrySnapshot> entries = new List<InventoryEntrySnapshot>();
            collected?.CopyEntries(entries, category);
            if (category == InventoryItemCategory.Consumables && consumables != null)
            {
                entries.Add(new InventoryEntrySnapshot("HEALTH POTION", "Restores health. Quantity is owned by the existing consumable authority.", consumables.OwnedHealthPotions, null));
                entries.Add(new InventoryEntrySnapshot("MANA POTION", "Restores mana. Quantity is owned by the existing consumable authority.", consumables.OwnedManaPotions, null));
                entries.Add(new InventoryEntrySnapshot("OBLIVION DISK", "Available placed-disk charges. Capacity: " + consumables.DiskCapacity + ".", consumables.StoredDiskCharges, null));
            }
            if (category == InventoryItemCategory.Equipment)
            {
                if (carapace != null)
                {
                    entries.Add(new InventoryEntrySnapshot("RESONANT CARAPACE", carapace.ConditionText, carapace.IsOwned ? 1 : 0, null));
                }
                else
                {
                    entries.Add(new InventoryEntrySnapshot("RESONANT CARAPACE", "Carapace authority is not installed in this build.", 0, null));
                }
            }

            if (entries.Count == 0)
            {
                body.text = category == InventoryItemCategory.Materials
                    ? "No materials collected. Flora collection will populate this category when its drop authority is implemented."
                    : "No items in this category.";
                if (icon != null) { icon.sprite = null; icon.enabled = false; }
                return;
            }

            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int index = 0; index < entries.Count; index++)
            {
                InventoryEntrySnapshot entry = entries[index];
                text.Append("◆ ").Append(entry.Name).Append("  x").Append(entry.Quantity).Append("\n");
                text.Append("  ").Append(entry.Description).Append("\n\n");
            }
            body.text = text.ToString().TrimEnd();
            if (icon != null)
            {
                icon.sprite = entries[0].Icon;
                icon.enabled = entries[0].Icon != null;
            }
        }
    }
}
