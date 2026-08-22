using System.Text;
using Cave.Audio;
using Cave.InputSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private GameAction[] actions;
        private Button[] bindingButtons;
        private Text statusText;
        private Slider masterVolumeSlider;
        private Slider sfxVolumeSlider;
        private Text masterVolumeValue;
        private Text sfxVolumeValue;
        private Button cancelButton;
        private GameAction actionBeingRebound;
        private bool isWaitingForKey;
        private int consumedMenuInputFrame = -1;

        public bool IsWaitingForKey => isWaitingForKey;
        public bool ConsumedMenuInputThisFrame => consumedMenuInputFrame == Time.frameCount;

        public void Configure(
            GameAction[] configurableActions,
            Button[] configurableBindingButtons,
            Text settingsStatusText,
            Slider masterSlider,
            Text masterValue,
            Slider sfxSlider,
            Text sfxValue,
            Button restoreDefaultsButton,
            Button cancelRebindButton)
        {
            actions = configurableActions;
            bindingButtons = configurableBindingButtons;
            statusText = settingsStatusText;
            masterVolumeSlider = masterSlider;
            masterVolumeValue = masterValue;
            sfxVolumeSlider = sfxSlider;
            sfxVolumeValue = sfxValue;
            cancelButton = cancelRebindButton;

            for (int index = 0; index < actions.Length; index++)
            {
                GameAction action = actions[index];
                bindingButtons[index].onClick.AddListener(() => BeginRebind(action));
            }

            restoreDefaultsButton.onClick.AddListener(RestoreDefaults);
            cancelButton.onClick.AddListener(CancelRebind);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);

            masterVolumeSlider.SetValueWithoutNotify(CaveAudioSettings.MasterVolume);
            sfxVolumeSlider.SetValueWithoutNotify(CaveAudioSettings.SfxVolume);
            cancelButton.gameObject.SetActive(false);
            RefreshBindingLabels();
            RefreshVolumeLabels();
            SetStatus("Select a control to change its primary key.");
        }

        private void Update()
        {
            if (!isWaitingForKey || !GameInput.TryGetPressedKeyboardKey(out KeyCode pressedKey))
            {
                return;
            }

            consumedMenuInputFrame = Time.frameCount;

            if (pressedKey == KeyCode.Escape && actionBeingRebound != GameAction.Pause)
            {
                CancelRebind("Rebinding canceled.");
                return;
            }

            if (!GameInput.TryRebind(
                    actionBeingRebound,
                    BindingSlot.Primary,
                    pressedKey,
                    out GameAction conflictingAction))
            {
                SetStatus(
                    "Conflict: " + FormatKey(pressedKey) + " is already used by " +
                    FormatAction(conflictingAction) + ". Choose another key or Cancel.");
                return;
            }

            GameAction reboundAction = actionBeingRebound;
            FinishWaitingForKey();
            RefreshBindingLabels();
            SetStatus(FormatAction(reboundAction) + " is now " + FormatKey(pressedKey) + ".");
        }

        public void PrepareToShow()
        {
            CancelRebind("Select a control to change its primary key.");
            masterVolumeSlider.SetValueWithoutNotify(CaveAudioSettings.MasterVolume);
            sfxVolumeSlider.SetValueWithoutNotify(CaveAudioSettings.SfxVolume);
            RefreshBindingLabels();
            RefreshVolumeLabels();
        }

        public void CancelRebind()
        {
            CancelRebind("Rebinding canceled.");
        }

        private void BeginRebind(GameAction action)
        {
            actionBeingRebound = action;
            isWaitingForKey = true;
            cancelButton.gameObject.SetActive(true);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            string cancelHint = action == GameAction.Pause
                ? "Use Cancel to stop; Escape is a valid Pause binding."
                : "Press Escape or Cancel to stop.";
            SetStatus("Press a key for " + FormatAction(action) + ". " + cancelHint);
        }

        private void RestoreDefaults()
        {
            FinishWaitingForKey();
            GameInput.RestoreDefaults();
            RefreshBindingLabels();
            SetStatus("Default controls restored and saved.");
        }

        private void CancelRebind(string message)
        {
            FinishWaitingForKey();
            SetStatus(message);
        }

        private void FinishWaitingForKey()
        {
            isWaitingForKey = false;
            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(false);
            }
        }

        private void SetMasterVolume(float value)
        {
            CaveAudioSettings.SetMasterVolume(value);
            RefreshVolumeLabels();
        }

        private void SetSfxVolume(float value)
        {
            CaveAudioSettings.SetSfxVolume(value);
            RefreshVolumeLabels();
        }

        private void RefreshBindingLabels()
        {
            if (actions == null || bindingButtons == null)
            {
                return;
            }

            for (int index = 0; index < actions.Length; index++)
            {
                KeyBinding binding = GameInput.Bindings.GetBinding(actions[index]);
                Text label = bindingButtons[index].GetComponentInChildren<Text>();
                if (label == null)
                {
                    continue;
                }

                label.text = binding.Secondary == KeyCode.None
                    ? FormatKey(binding.Primary)
                    : FormatKey(binding.Primary) + " / " + FormatKey(binding.Secondary);
            }
        }

        private void RefreshVolumeLabels()
        {
            if (masterVolumeValue != null)
            {
                masterVolumeValue.text = Mathf.RoundToInt(CaveAudioSettings.MasterVolume * 100f) + "%";
            }

            if (sfxVolumeValue != null)
            {
                sfxVolumeValue.text = Mathf.RoundToInt(CaveAudioSettings.SfxVolume * 100f) + "%";
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public static string FormatAction(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveLeft:
                    return "Move Left";
                case GameAction.MoveRight:
                    return "Move Right";
                case GameAction.BasicAttack:
                    return "Basic Attack";
                case GameAction.ChargedAttack:
                    return "Charged Attack";
                case GameAction.FireProjectile:
                    return "Fire Projectile";
                default:
                    return InsertSpaces(action.ToString());
            }
        }

        private static string FormatKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None:
                    return "Unbound";
                case KeyCode.LeftArrow:
                    return "Left Arrow";
                case KeyCode.RightArrow:
                    return "Right Arrow";
                case KeyCode.UpArrow:
                    return "Up Arrow";
                case KeyCode.DownArrow:
                    return "Down Arrow";
                case KeyCode.Space:
                    return "Space";
                case KeyCode.Return:
                    return "Enter";
                default:
                    return InsertSpaces(key.ToString());
            }
        }

        private static string InsertSpaces(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 4);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
