using System;
using UnityEngine;

namespace Cave.InputSystem
{
    public static class GameInput
    {
        private static readonly InputBindings CurrentBindings = InputBindings.LoadSavedOrDefault();
        private static readonly KeyCode[] KeyboardKeys = CreateKeyboardKeyList();

        private static bool gameplayInputRequested = true;
        private static bool suppressGameplayUntilRelease;
        private static int consumedInputFrame = -1;

        public static InputBindings Bindings => CurrentBindings;
        public static bool GameplayInputEnabled
        {
            get
            {
                RefreshGameplayReleaseGate();
                return gameplayInputRequested && !suppressGameplayUntilRelease;
            }
        }

        public static float Horizontal
        {
            get
            {
                if (!GameplayInputEnabled)
                {
                    return 0f;
                }

                float left = IsHeld(GameAction.MoveLeft) ? -1f : 0f;
                float right = IsHeld(GameAction.MoveRight) ? 1f : 0f;
                return left + right;
            }
        }

        public static float AimVertical
        {
            get
            {
                if (!GameplayInputEnabled)
                {
                    return 0f;
                }

                float down = IsKeyHeld(KeyCode.DownArrow) ? -1f : 0f;
                float up = IsKeyHeld(KeyCode.UpArrow) ? 1f : 0f;
                return down + up;
            }
        }

        public static bool JumpPressed => GameplayInputEnabled && WasPressed(GameAction.Jump);
        public static bool JumpHeld => GameplayInputEnabled && IsHeld(GameAction.Jump);
        public static bool BasicAttackPressed => GameplayInputEnabled && WasPressed(GameAction.BasicAttack);
        public static bool BasicAttackHeld => GameplayInputEnabled && IsHeld(GameAction.BasicAttack);
        public static bool ChargePressed => GameplayInputEnabled && WasPressed(GameAction.ChargedAttack);
        public static bool ChargeReleased => GameplayInputEnabled && WasReleased(GameAction.ChargedAttack);
        public static bool ChargeHeld => GameplayInputEnabled && IsHeld(GameAction.ChargedAttack);
        public static bool ParryPressed => GameplayInputEnabled && WasPressed(GameAction.Parry);
        public static bool ParryReleased => GameplayInputEnabled && WasReleased(GameAction.Parry);
        public static bool ParryHeld => GameplayInputEnabled && IsHeld(GameAction.Parry);
        public static bool GuardBreakPressed => GameplayInputEnabled
            && WasPressed(GameAction.GuardBreak);
        public static bool FireProjectilePressed => GameplayInputEnabled && WasPressed(GameAction.FireProjectile);
        public static bool FireProjectileHeld => GameplayInputEnabled && IsHeld(GameAction.FireProjectile);
        public static bool UseHealthPotionPressed => GameplayInputEnabled
            && WasPressed(GameAction.UseHealthPotion);
        public static bool UseManaPotionPressed => GameplayInputEnabled
            && WasPressed(GameAction.UseManaPotion);
        public static bool PlaceLandminePressed => GameplayInputEnabled && WasPressed(GameAction.PlaceLandmine);
        public static bool UseDistractionPressed => GameplayInputEnabled
            && WasPressed(GameAction.UseDistraction);
        public static bool InteractPressed => GameplayInputEnabled && WasPressed(GameAction.Interact);
        public static bool InteractHeld => GameplayInputEnabled && IsHeld(GameAction.Interact);
        public static bool InteractReleased => GameplayInputEnabled && WasReleased(GameAction.Interact);
        public static bool SummonCurseAltarPressed => GameplayInputEnabled
            && WasPressed(GameAction.SummonCurseAltar);
        public static bool DashPressed => GameplayInputEnabled && WasPressed(GameAction.Dash);
        public static bool PausePressed => !WasInputConsumedThisFrame && WasPressed(GameAction.Pause);
        public static bool MenuCancelPressed => !WasInputConsumedThisFrame && WasKeyPressed(KeyCode.Escape);

        private static bool WasInputConsumedThisFrame => consumedInputFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            CurrentBindings.LoadSavedBindings();
            gameplayInputRequested = true;
            suppressGameplayUntilRelease = false;
            consumedInputFrame = -1;
        }

        public static void SetGameplayInputEnabled(bool enabled)
        {
            gameplayInputRequested = enabled;
            if (!enabled)
            {
                suppressGameplayUntilRelease = false;
            }
        }

        public static void EnableGameplayAfterInputRelease()
        {
            gameplayInputRequested = true;
            suppressGameplayUntilRelease = AnyGameplayKeyHeld();
        }

        public static void ConsumeMenuInputForCurrentFrame()
        {
            consumedInputFrame = Time.frameCount;
        }

        public static bool TryRebind(
            GameAction action,
            BindingSlot slot,
            KeyCode newKey,
            out GameAction conflictingAction)
        {
            if (!CurrentBindings.TryRebind(action, slot, newKey, out conflictingAction))
            {
                return false;
            }

            CurrentBindings.Save();
            return true;
        }

        public static void RestoreDefaults()
        {
            CurrentBindings.RestoreDefaults();
            CurrentBindings.Save();
        }

        public static bool TryGetPressedKeyboardKey(out KeyCode pressedKey)
        {
            pressedKey = KeyCode.None;
            if (!UnityEngine.Input.anyKeyDown)
            {
                return false;
            }

            foreach (KeyCode key in KeyboardKeys)
            {
                if (UnityEngine.Input.GetKeyDown(key))
                {
                    pressedKey = key;
                    consumedInputFrame = Time.frameCount;
                    return true;
                }
            }

            return false;
        }

        private static bool IsHeld(GameAction action)
        {
            KeyBinding binding = CurrentBindings.GetBinding(action);
            return IsKeyHeld(binding.Primary) || IsKeyHeld(binding.Secondary);
        }

        private static bool WasPressed(GameAction action)
        {
            KeyBinding binding = CurrentBindings.GetBinding(action);
            return WasKeyPressed(binding.Primary) || WasKeyPressed(binding.Secondary);
        }

        private static bool WasReleased(GameAction action)
        {
            KeyBinding binding = CurrentBindings.GetBinding(action);
            return WasKeyReleased(binding.Primary) || WasKeyReleased(binding.Secondary);
        }

        private static bool IsKeyHeld(KeyCode key)
        {
            return key != KeyCode.None && UnityEngine.Input.GetKey(key);
        }

        private static bool WasKeyPressed(KeyCode key)
        {
            return key != KeyCode.None && UnityEngine.Input.GetKeyDown(key);
        }

        private static bool WasKeyReleased(KeyCode key)
        {
            return key != KeyCode.None && UnityEngine.Input.GetKeyUp(key);
        }

        private static void RefreshGameplayReleaseGate()
        {
            if (suppressGameplayUntilRelease && !AnyGameplayKeyHeld())
            {
                suppressGameplayUntilRelease = false;
            }
        }

        private static bool AnyGameplayKeyHeld()
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                if (action == GameAction.Pause)
                {
                    continue;
                }

                KeyBinding binding = CurrentBindings.GetBinding(action);
                if (IsKeyHeld(binding.Primary) || IsKeyHeld(binding.Secondary))
                {
                    return true;
                }
            }

            return false;
        }

        private static KeyCode[] CreateKeyboardKeyList()
        {
            Array values = Enum.GetValues(typeof(KeyCode));
            KeyCode[] keys = new KeyCode[values.Length];
            int keyCount = 0;

            foreach (KeyCode key in values)
            {
                int keyValue = (int)key;
                if (key == KeyCode.None || keyValue >= (int)KeyCode.Mouse0)
                {
                    continue;
                }

                keys[keyCount] = key;
                keyCount++;
            }

            Array.Resize(ref keys, keyCount);
            return keys;
        }
    }
}
