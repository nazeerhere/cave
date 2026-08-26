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
        [SerializeField] private KeyBinding guardBreak;
        [SerializeField] private KeyBinding fireProjectile;
        [SerializeField] private KeyBinding useHealthPotion;
        [SerializeField] private KeyBinding useManaPotion;
        [SerializeField] private KeyBinding placeLandmine;
        [SerializeField] private KeyBinding useDistraction;
        [SerializeField] private KeyBinding interact;
        [SerializeField] private KeyBinding summonCurseAltar;
        [SerializeField] private KeyBinding dash;
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
                case GameAction.GuardBreak:
                    return guardBreak;
                case GameAction.FireProjectile:
                    return fireProjectile;
                case GameAction.UseHealthPotion:
                    return useHealthPotion;
                case GameAction.UseManaPotion:
                    return useManaPotion;
                case GameAction.PlaceLandmine:
                    return placeLandmine;
                case GameAction.UseDistraction:
                    return useDistraction;
                case GameAction.Interact:
                    return interact;
                case GameAction.SummonCurseAltar:
                    return summonCurseAltar;
                case GameAction.Dash:
                    return dash;
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
            guardBreak = new KeyBinding(KeyCode.E, KeyCode.None);
            fireProjectile = new KeyBinding(KeyCode.B, KeyCode.None);
            useHealthPotion = new KeyBinding(KeyCode.Alpha1, KeyCode.None);
            useManaPotion = new KeyBinding(KeyCode.Alpha2, KeyCode.None);
            placeLandmine = new KeyBinding(KeyCode.Q, KeyCode.None);
            useDistraction = new KeyBinding(KeyCode.R, KeyCode.None);
            interact = new KeyBinding(KeyCode.F, KeyCode.None);
            summonCurseAltar = new KeyBinding(KeyCode.None, KeyCode.None);
            dash = new KeyBinding(KeyCode.LeftShift, KeyCode.None);
            pause = new KeyBinding(KeyCode.Escape, KeyCode.None);
        }

        public void LoadSavedBindings()
        {
            RestoreDefaults();
            bool hasSavedDashPrimary = PlayerPrefs.HasKey(
                GetPlayerPrefsKey(GameAction.Dash, BindingSlot.Primary));
            bool hasSavedDashSecondary = PlayerPrefs.HasKey(
                GetPlayerPrefsKey(GameAction.Dash, BindingSlot.Secondary));
            dash = new KeyBinding(KeyCode.None, KeyCode.None);

            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                if (action == GameAction.Dash)
                {
                    continue;
                }

                LoadSavedKey(action, BindingSlot.Primary);
                LoadSavedKey(action, BindingSlot.Secondary);
            }

            if (hasSavedDashPrimary)
            {
                LoadSavedKey(GameAction.Dash, BindingSlot.Primary);
            }
            else if (!TryFindConflict(GameAction.Dash, KeyCode.LeftShift, out _))
            {
                dash = dash.WithKey(BindingSlot.Primary, KeyCode.LeftShift);
            }

            if (hasSavedDashSecondary)
            {
                LoadSavedKey(GameAction.Dash, BindingSlot.Secondary);
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
                case GameAction.GuardBreak:
                    guardBreak = binding;
                    break;
                case GameAction.FireProjectile:
                    fireProjectile = binding;
                    break;
                case GameAction.UseHealthPotion:
                    useHealthPotion = binding;
                    break;
                case GameAction.UseManaPotion:
                    useManaPotion = binding;
                    break;
                case GameAction.PlaceLandmine:
                    placeLandmine = binding;
                    break;
                case GameAction.UseDistraction:
                    useDistraction = binding;
                    break;
                case GameAction.Interact:
                    interact = binding;
                    break;
                case GameAction.SummonCurseAltar:
                    summonCurseAltar = binding;
                    break;
                case GameAction.Dash:
                    dash = binding;
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
