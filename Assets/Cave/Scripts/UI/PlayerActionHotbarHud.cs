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
            GameAction.PlaceLandmine
        };

        private static readonly PlayerConsumableType[] ConsumableTypes =
        {
            PlayerConsumableType.HealthPotion,
            PlayerConsumableType.ManaPotion,
            PlayerConsumableType.Landmine
        };

        private Image[] slotBackgrounds;
        private Text[] keyLabels;
        private Text[] quantityLabels;
        private PlayerLandmineInventory inventory;
        private int selectedIndex = -1;

        public void Configure(
            Image[] backgrounds,
            Text[] bindings,
            Text[] quantities,
            PlayerLandmineInventory consumableInventory)
        {
            slotBackgrounds = backgrounds;
            keyLabels = bindings;
            quantityLabels = quantities;
            BindInventory(consumableInventory);
            RefreshBindings();
            RefreshQuantities();
            RefreshSlotVisuals();
        }

        private void Update()
        {
            RefreshBindings();

            int nextSelection = -1;
            if (GameInput.UseHealthPotionPressed)
            {
                nextSelection = 0;
            }
            else if (GameInput.UseManaPotionPressed)
            {
                nextSelection = 1;
            }
            else if (GameInput.PlaceLandminePressed)
            {
                nextSelection = 2;
            }

            if (nextSelection >= 0)
            {
                selectedIndex = nextSelection;
                RefreshSlotVisuals();
            }
        }

        private void BindInventory(PlayerLandmineInventory consumableInventory)
        {
            if (inventory != null)
            {
                inventory.ConsumableQuantityChanged -= HandleQuantityChanged;
            }

            inventory = consumableInventory;
            if (inventory != null)
            {
                inventory.ConsumableQuantityChanged += HandleQuantityChanged;
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

                quantityLabels[index].text = index < ConsumableTypes.Length
                    ? "x" + GetQuantity(index)
                    : string.Empty;
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

                if (index >= ConsumableTypes.Length)
                {
                    background.color = new Color(
                        CaveUiTheme.SurfaceInset.r,
                        CaveUiTheme.SurfaceInset.g,
                        CaveUiTheme.SurfaceInset.b,
                        0.45f);
                }
                else if (GetQuantity(index) <= 0)
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

        private void HandleQuantityChanged(PlayerConsumableType type, int owned)
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
            }
        }
    }
}
