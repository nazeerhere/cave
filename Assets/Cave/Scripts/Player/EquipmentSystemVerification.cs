using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    public static class EquipmentSystemVerification
    {
        public static bool Run()
        {
            InputBindings defaults = InputBindings.CreateDefault();
            bool appendedEnums = (int)GameAction.RecallManifestation == 17
                && (int)GameAction.SelectEquipment == 18
                && (int)GameAction.ActivateEquipment == 19;
            bool defaultsCorrect = defaults.GetBinding(GameAction.SelectEquipment).Primary == KeyCode.R
                && defaults.GetBinding(GameAction.ActivateEquipment).Primary == KeyCode.Q
                && defaults.GetBinding(GameAction.PlaceLandmine).Primary == KeyCode.None
                && defaults.GetBinding(GameAction.UseDistraction).Primary == KeyCode.None
                && defaults.GetBinding(GameAction.UseHealthPotion).Primary == KeyCode.Alpha1
                && defaults.GetBinding(GameAction.UseManaPotion).Primary == KeyCode.Alpha2;

            bool diskHoldExactlyOnce = EquipmentInputPolicy.Resolve(
                    true, true, false, false) == EquipmentActivationDecision.Secondary
                && EquipmentInputPolicy.Resolve(
                    true, true, true, true) == EquipmentActivationDecision.None;
            bool diskTap = EquipmentInputPolicy.Resolve(
                true, false, true, false) == EquipmentActivationDecision.Primary;
            bool primaryOnlyHold = EquipmentInputPolicy.Resolve(
                    false, true, false, false) == EquipmentActivationDecision.None
                && EquipmentInputPolicy.Resolve(
                    false, true, true, false) == EquipmentActivationDecision.Primary;
            bool selectionDoesNotActivate = EquipmentInputPolicy.ResolveSelection(
                    PlayerEquipmentId.OblivionDisk,
                    PlayerEquipmentId.Distraction,
                    true,
                    false) == PlayerEquipmentId.Distraction
                && EquipmentInputPolicy.ResolveSelection(
                    PlayerEquipmentId.OblivionDisk,
                    PlayerEquipmentId.Distraction,
                    true,
                    true) == PlayerEquipmentId.OblivionDisk
                && EquipmentInputPolicy.ResolveSelection(
                    PlayerEquipmentId.OblivionDisk,
                    PlayerEquipmentId.Distraction,
                    false,
                    false) == PlayerEquipmentId.OblivionDisk;

            bool passed = appendedEnums
                && defaultsCorrect
                && diskHoldExactlyOnce
                && diskTap
                && primaryOnlyHold
                && selectionDoesNotActivate;
            Debug.Log(passed
                ? "Equipment System Verification: PASS"
                : "Equipment System Verification: FAIL");
            return passed;
        }
    }
}
