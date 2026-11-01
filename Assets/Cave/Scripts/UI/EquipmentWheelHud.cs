using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class EquipmentWheelHud : MonoBehaviour
    {
        private GameObject wheelRoot;
        private Image[] itemBackgrounds;
        private Text[] itemNames;
        private Text[] itemSymbols;
        private Text[] itemStatuses;
        private Text centerName;
        private Text centerDescription;
        private Text inputHint;
        private PlayerEquipmentController controller;

        public void Configure(
            GameObject root,
            Image[] backgrounds,
            Text[] names,
            Text[] symbols,
            Text[] statuses,
            Text selectedName,
            Text selectedDescription,
            Text hint,
            PlayerEquipmentController equipmentController)
        {
            wheelRoot = root;
            itemBackgrounds = backgrounds;
            itemNames = names;
            itemSymbols = symbols;
            itemStatuses = statuses;
            centerName = selectedName;
            centerDescription = selectedDescription;
            inputHint = hint;
            Bind(equipmentController);
            Refresh();
            wheelRoot.SetActive(false);
        }

        private void Bind(PlayerEquipmentController next)
        {
            if (controller != null)
            {
                controller.WheelVisibilityChanged -= HandleVisibilityChanged;
                controller.HighlightChanged -= Refresh;
                controller.SelectionChanged -= Refresh;
                controller.StateChanged -= Refresh;
            }

            controller = next;
            if (controller != null)
            {
                controller.WheelVisibilityChanged += HandleVisibilityChanged;
                controller.HighlightChanged += Refresh;
                controller.SelectionChanged += Refresh;
                controller.StateChanged += Refresh;
            }
        }

        private void HandleVisibilityChanged(bool visible)
        {
            if (wheelRoot == null)
            {
                return;
            }

            wheelRoot.SetActive(visible);
            if (visible)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (controller == null)
            {
                return;
            }

            for (int index = 0; index < itemBackgrounds.Length; index++)
            {
                PlayerEquipmentPresentation presentation = controller.GetPresentationAt(index);
                bool visible = presentation.Id != PlayerEquipmentId.None;
                itemBackgrounds[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }
                bool highlighted = presentation.Id == controller.HighlightedEquipment;
                bool selected = presentation.Id == controller.SelectedEquipment;
                itemBackgrounds[index].color = !presentation.Available
                    ? new Color(0.035f, 0.04f, 0.052f, 0.94f)
                    : highlighted
                        ? new Color(0.055f, 0.27f, 0.38f, 0.98f)
                        : selected
                            ? new Color(0.11f, 0.12f, 0.14f, 0.98f)
                            : CaveUiTheme.SurfaceInset;
                itemNames[index].text = presentation.Name;
                itemNames[index].color = highlighted
                    ? CaveUiTheme.BorderBright
                    : presentation.Available
                        ? CaveUiTheme.PrimaryText
                        : CaveUiTheme.SecondaryText;
                itemSymbols[index].text = presentation.Symbol;
                itemSymbols[index].color = highlighted
                    ? CaveUiTheme.BorderBright
                    : presentation.Available ? CaveUiTheme.Gold : CaveUiTheme.IronLight;
                itemStatuses[index].text = presentation.Status;
                itemStatuses[index].color = presentation.Available
                    ? CaveUiTheme.SecondaryText
                    : CaveUiTheme.IronLight;
            }

            PlayerEquipmentId displayId = controller.HighlightedEquipment != PlayerEquipmentId.None
                ? controller.HighlightedEquipment
                : controller.SelectedEquipment;
            PlayerEquipmentPresentation display = controller.GetPresentation(displayId);
            centerName.text = display.Name;
            centerDescription.text = display.Description;
            inputHint.text = "RELEASE "
                + FormatBinding(GameAction.SelectEquipment)
                + " TO EQUIP   •   ESC CANCEL   •   "
                + FormatBinding(GameAction.ActivateEquipment)
                + " USE";
        }

        private static string FormatBinding(GameAction action)
        {
            KeyCode key = GameInput.Bindings.GetBinding(action).Primary;
            string value = key == KeyCode.None ? "—" : key.ToString();
            return value.StartsWith("Alpha") ? value.Substring(5) : value.ToUpperInvariant();
        }

        private void OnDestroy()
        {
            Bind(null);
        }
    }
}
