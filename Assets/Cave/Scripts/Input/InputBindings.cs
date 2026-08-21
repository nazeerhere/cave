using System;
using UnityEngine;

namespace Cave.InputSystem
{
    [Serializable]
    public struct KeyBinding
    {
        [SerializeField] private KeyCode primary;
        [SerializeField] private KeyCode secondary;

        public KeyCode Primary => primary;
        public KeyCode Secondary => secondary;

        public KeyBinding(KeyCode primaryKey, KeyCode secondaryKey)
        {
            primary = primaryKey;
            secondary = secondaryKey;
        }

        public KeyBinding WithKey(BindingSlot slot, KeyCode key)
        {
            return slot == BindingSlot.Primary
                ? new KeyBinding(key, secondary)
                : new KeyBinding(primary, key);
        }

        public bool Contains(KeyCode key)
        {
            return key != KeyCode.None && (primary == key || secondary == key);
        }
    }

    [Serializable]
    public sealed class InputBindings
    {
        public const string PlayerPrefsPrefix = "Cave.Settings.Input.v1.";

        [SerializeField] private KeyBinding moveLeft;
        [SerializeField] private KeyBinding moveRight;
        [SerializeField] private KeyBinding jump;
        [SerializeField] private KeyBinding basicAttack;
        [SerializeField] private KeyBinding chargedAttack;
        [SerializeField] private KeyBinding parry;
        [SerializeField] private KeyBinding pause;

        public static InputBindings CreateDefault()
        {
            InputBindings bindings = new InputBindings();
            bindings.RestoreDefaults();
            return bindings;
        }

        public static InputBindings LoadSavedOrDefault()
        {
            InputBindings bindings = CreateDefault();
            bindings.LoadSavedBindings();
            return bindings;
        }

        public KeyBinding GetBinding(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveLeft:
                    return moveLeft;
                case GameAction.MoveRight:
                    return moveRight;
                case GameAction.Jump:
                    return jump;
                case GameAction.BasicAttack:
                    return basicAttack;
                case GameAction.ChargedAttack:
                    return chargedAttack;
                case GameAction.Parry:
                    return parry;
                case GameAction.Pause:
                    return pause;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public bool TryRebind(
            GameAction action,
            BindingSlot slot,
            KeyCode newKey,
            out GameAction conflictingAction)
        {
            if (newKey == KeyCode.None)
            {
                conflictingAction = action;
                return false;
            }

            if (TryFindConflict(action, newKey, out conflictingAction))
            {
                return false;
            }

            SetBinding(action, GetBinding(action).WithKey(slot, newKey));
            conflictingAction = action;
            return true;
        }

        public void RestoreDefaults()
        {
            moveLeft = new KeyBinding(KeyCode.A, KeyCode.LeftArrow);
            moveRight = new KeyBinding(KeyCode.D, KeyCode.RightArrow);
            jump = new KeyBinding(KeyCode.Space, KeyCode.UpArrow);
            basicAttack = new KeyBinding(KeyCode.X, KeyCode.W);
            chargedAttack = new KeyBinding(KeyCode.C, KeyCode.None);
            parry = new KeyBinding(KeyCode.V, KeyCode.None);
            pause = new KeyBinding(KeyCode.Escape, KeyCode.None);
        }

        public void LoadSavedBindings()
        {
            RestoreDefaults();

            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                LoadSavedKey(action, BindingSlot.Primary);
                LoadSavedKey(action, BindingSlot.Secondary);
            }
        }

        public void Save()
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                KeyBinding binding = GetBinding(action);
                PlayerPrefs.SetInt(GetPlayerPrefsKey(action, BindingSlot.Primary), (int)binding.Primary);
                PlayerPrefs.SetInt(GetPlayerPrefsKey(action, BindingSlot.Secondary), (int)binding.Secondary);
            }

            PlayerPrefs.Save();
        }

        private void LoadSavedKey(GameAction action, BindingSlot slot)
        {
            string playerPrefsKey = GetPlayerPrefsKey(action, slot);
            if (!PlayerPrefs.HasKey(playerPrefsKey))
            {
                return;
            }

            int savedValue = PlayerPrefs.GetInt(playerPrefsKey);
            if (!Enum.IsDefined(typeof(KeyCode), savedValue))
            {
                return;
            }

            KeyCode savedKey = (KeyCode)savedValue;
            if (savedKey == KeyCode.None && slot == BindingSlot.Secondary)
            {
                SetBinding(action, GetBinding(action).WithKey(slot, savedKey));
                return;
            }

            TryRebind(action, slot, savedKey, out _);
        }

        private static string GetPlayerPrefsKey(GameAction action, BindingSlot slot)
        {
            return PlayerPrefsPrefix + action + "." + slot;
        }

        private bool TryFindConflict(
            GameAction actionBeingChanged,
            KeyCode newKey,
            out GameAction conflictingAction)
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                if (action != actionBeingChanged && GetBinding(action).Contains(newKey))
                {
                    conflictingAction = action;
                    return true;
                }
            }

            conflictingAction = actionBeingChanged;
            return false;
        }

        private void SetBinding(GameAction action, KeyBinding binding)
        {
            switch (action)
            {
                case GameAction.MoveLeft:
                    moveLeft = binding;
                    break;
                case GameAction.MoveRight:
                    moveRight = binding;
                    break;
                case GameAction.Jump:
                    jump = binding;
                    break;
                case GameAction.BasicAttack:
                    basicAttack = binding;
                    break;
                case GameAction.ChargedAttack:
                    chargedAttack = binding;
                    break;
                case GameAction.Parry:
                    parry = binding;
                    break;
                case GameAction.Pause:
                    pause = binding;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }
    }
}
