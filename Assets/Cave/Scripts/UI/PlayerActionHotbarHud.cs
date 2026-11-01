using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerActionHotbarHud : MonoBehaviour
    {
        private static readonly GameAction[] Actions =
        {
            GameAction.UseHealthPotion,
            GameAction.UseManaPotion,
            GameAction.ActivateEquipment
        };

        private static readonly PlayerConsumableType[] ConsumableTypes =
        {
            PlayerConsumableType.HealthPotion,
            PlayerConsumableType.ManaPotion
        };

        private Image[] slotBackgrounds;
        private Text[] keyLabels;
        private Text[] quantityLabels;
        private Text[] nameLabels;
        private Text[] symbolLabels;
        private PlayerLandmineInventory inventory;
        private PlayerEquipmentController equipment;
        private int selectedIndex = -1;

        public void Configure(
            Image[] backgrounds,
            Text[] bindings,
            Text[] quantities,
            Text[] names,
            Text[] symbols,
            PlayerLandmineInventory consumableInventory,
            PlayerEquipmentController equipmentController)
        {
            slotBackgrounds = backgrounds;
            keyLabels = bindings;
            quantityLabels = quantities;
            nameLabels = names;
            symbolLabels = symbols;
            BindInventory(consumableInventory);
            BindEquipment(equipmentController);
            RefreshBindings();
            RefreshQuantities();
            RefreshSlotVisuals();
        }

        private void Update()
        {
            int nextSelection = -1;
            if (GameInput.UseHealthPotionPressed)
            {
                nextSelection = 0;
            }
            else if (GameInput.UseManaPotionPressed)
            {
                nextSelection = 1;
            }
            else if (GameInput.ActivateEquipmentPressed)
            {
                nextSelection = 2;
            }

            if (nextSelection >= 0)
            {
                selectedIndex = nextSelection;
                RefreshSlotVisuals();
            }
        }

        private void BindEquipment(PlayerEquipmentController controller)
        {
            if (equipment != null)
            {
                equipment.SelectionChanged -= HandleEquipmentChanged;
                equipment.StateChanged -= HandleEquipmentChanged;
            }

            equipment = controller;
            if (equipment != null)
            {
                equipment.SelectionChanged += HandleEquipmentChanged;
                equipment.StateChanged += HandleEquipmentChanged;
            }
        }

        private void BindInventory(PlayerLandmineInventory consumableInventory)
        {
            if (inventory != null)
            {
                inventory.ConsumableQuantityChanged -= HandleQuantityChanged;
                inventory.DiskChargeStateChanged -= HandleDiskChargeStateChanged;
            }

            inventory = consumableInventory;
            if (inventory != null)
            {
                inventory.ConsumableQuantityChanged += HandleQuantityChanged;
                inventory.DiskChargeStateChanged += HandleDiskChargeStateChanged;
            }
        }

        private void RefreshBindings()
        {
            if (keyLabels == null)
            {
                return;
            }

            for (int index = 0; index < keyLabels.Length; index++)
            {
                if (keyLabels[index] == null)
                {
                    continue;
                }

                keyLabels[index].text = index < Actions.Length
                    ? FormatKey(GameInput.Bindings.GetBinding(Actions[index]).Primary)
                    : string.Empty;
            }
        }

        private void RefreshQuantities()
        {
            if (quantityLabels == null)
            {
                return;
            }

            for (int index = 0; index < quantityLabels.Length; index++)
            {
                if (quantityLabels[index] == null)
                {
                    continue;
                }

                quantityLabels[index].text = FormatQuantity(index);
            }

            if (equipment != null
                && nameLabels != null && nameLabels.Length > 2
                && symbolLabels != null && symbolLabels.Length > 2)
            {
                PlayerEquipmentPresentation presentation = equipment.GetPresentation(
                    equipment.SelectedEquipment);
                nameLabels[2].text = presentation.Name;
                symbolLabels[2].text = presentation.Symbol;
            }
        }

        private void RefreshSlotVisuals()
        {
            if (slotBackgrounds == null)
            {
                return;
            }

            for (int index = 0; index < slotBackgrounds.Length; index++)
            {
                Image background = slotBackgrounds[index];
                if (background == null)
                {
                    continue;
                }

                bool available = index < 2
                    ? GetQuantity(index) > 0
                    : equipment != null
                        && equipment.GetPresentation(equipment.SelectedEquipment).Available;
                if (!available)
                {
                    background.color = new Color(0.045f, 0.05f, 0.065f, 0.88f);
                }
                else
                {
                    background.color = index == selectedIndex
                        ? new Color(0.06f, 0.23f, 0.3f, 0.98f)
                        : CaveUiTheme.SurfaceInset;
                }
            }
        }

        private int GetQuantity(int index)
        {
            return inventory != null && index >= 0 && index < ConsumableTypes.Length
                ? inventory.GetOwnedCount(ConsumableTypes[index])
                : 0;
        }

        private string FormatQuantity(int index)
        {
            if (index == 0 || index == 1)
            {
                if (inventory == null) return "x0";
                if (GetQuantity(index) <= 0) return "x0";
                float potency = index == 0
                    ? inventory.NextHealthPotionRestorePercent
                    : inventory.NextManaPotionRestorePercent;
                return "x" + GetQuantity(index) + "\n" + Mathf.RoundToInt(potency * 100f) + "%";
            }

            if (equipment == null)
            {
                return "—";
            }
            return equipment.GetPresentation(equipment.SelectedEquipment).Status;
        }

        private void HandleQuantityChanged(PlayerConsumableType type, int owned)
        {
            RefreshQuantities();
            RefreshSlotVisuals();
        }

        private void HandleDiskChargeStateChanged()
        {
            RefreshQuantities();
            RefreshSlotVisuals();
        }

        private void HandleEquipmentChanged()
        {
            RefreshQuantities();
            RefreshSlotVisuals();
        }

        private static string FormatKey(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return "—";
            }

            string label = key.ToString();
            return label.StartsWith("Alpha") ? label.Substring(5) : label;
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.ConsumableQuantityChanged -= HandleQuantityChanged;
                inventory.DiskChargeStateChanged -= HandleDiskChargeStateChanged;
            }


            BindEquipment(null);
        }
    }
}
