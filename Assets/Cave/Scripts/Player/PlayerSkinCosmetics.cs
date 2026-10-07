using System;
using Cave.Diagnostics;
using UnityEngine;

namespace Cave.Player
{
    public enum PlayerSkinSelection
    {
        DefaultMiner = 0,
        IvorySovereign = 1,
        FrostboundWanderer = 2,
        CrimsonExile = 3,
        AshenMonarch = 4,
        FrostboundThornQueen = 5,
        CyberpunkStarlet = 6,
        OrnateRoseWitch = 7
    }

    /// <summary>
    /// Permanent, presentation-only skin ownership and selection.  This class deliberately
    /// does not own player level, Gambit state, body sprites, or combat presentation.
    /// Those systems report an approved unlock through the narrow public seams below.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSkinCosmetics : MonoBehaviour
    {
        public const int IvorySovereignRequiredPlayerLevel = 15;

        private const string SelectionKey = "Cave.CosmeticSkin.Selected";

        [SerializeField] private PlayerSkinSelection selectedAppearance;
        public event Action<PlayerSkinSelection> SelectionChanged;
        public event Action<PlayerSkinSelection> UnlockChanged;

        public PlayerSkinSelection SelectedAppearance => selectedAppearance;
        public bool IvorySovereignUnlocked => IsUnlocked(PlayerSkinSelection.IvorySovereign);
        public bool CrimsonExileUnlocked => IsUnlocked(PlayerSkinSelection.CrimsonExile);

        public static PlayerSkinCosmetics EnsureInstalled(GameObject player)
        {
            return player == null ? null
                : player.GetComponent<PlayerSkinCosmetics>() ?? player.AddComponent<PlayerSkinCosmetics>();
        }

        private void Awake()
        {
            PlayerSkinSelection saved = (PlayerSkinSelection)PlayerPrefs.GetInt(SelectionKey, (int)selectedAppearance);
            selectedAppearance = PlayerSkinLibrary.IsRegistered(saved) ? saved : PlayerSkinSelection.DefaultMiner;

            // An old save can never leave the player equipped with an inaccessible skin.
            if (!IsAvailable(selectedAppearance))
            {
                selectedAppearance = PlayerSkinSelection.DefaultMiner;
                SaveSelection();
            }
        }

        public bool IsUnlocked(PlayerSkinSelection selection)
        {
            if (!PlayerSkinLibrary.IsRegistered(selection)) return false;
            PlayerSkinLibrary.Definition definition = PlayerSkinLibrary.GetDefinition(selection);
            return definition.Policy == PlayerSkinLibrary.UnlockPolicy.AlwaysAvailable
                || !string.IsNullOrEmpty(definition.UnlockKey)
                    && PlayerPrefs.GetInt(definition.UnlockKey, 0) != 0;
        }

        public bool IsAvailable(PlayerSkinSelection selection)
        {
            return PlayerSkinLibrary.IsRegistered(selection)
                && CosmeticAvailability.IsAvailable(IsUnlocked(selection));
        }

        public bool Select(PlayerSkinSelection selection)
        {
            if (!PlayerSkinLibrary.IsRegistered(selection)
                || !IsAvailable(selection))
            {
                return false;
            }

            if (selectedAppearance == selection)
            {
                return true;
            }

            selectedAppearance = selection;
            SaveSelection();
            SelectionChanged?.Invoke(selectedAppearance);
            return true;
        }

        private void OnEnable()
        {
            DeveloperDiagnosticsSettings.Changed -= HandleDeveloperSettingsChanged;
            DeveloperDiagnosticsSettings.Changed += HandleDeveloperSettingsChanged;
            RevertUnavailableSelectionIfNeeded();
        }

        private void OnDisable()
        {
            DeveloperDiagnosticsSettings.Changed -= HandleDeveloperSettingsChanged;
        }

        /// <summary>Called by the authoritative player-level owner once it exists.</summary>
        public void NotifyPlayerLevelChanged(int playerLevel)
        {
            if (playerLevel >= IvorySovereignRequiredPlayerLevel)
            {
                Unlock(PlayerSkinSelection.IvorySovereign);
            }
        }

        /// <summary>Called only by the Gambit match host after an authoritative Victory.</summary>
        public void NotifySovereignGambitCompleted()
        {
            Unlock(PlayerSkinSelection.CrimsonExile);
        }

        private void Unlock(PlayerSkinSelection selection)
        {
            PlayerSkinLibrary.Definition definition = PlayerSkinLibrary.GetDefinition(selection);
            string key = definition.UnlockKey;
            if (string.IsNullOrEmpty(key)) return;
            if (PlayerPrefs.GetInt(key, 0) != 0)
            {
                return;
            }

            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            UnlockChanged?.Invoke(selection);
        }

        private void SaveSelection()
        {
            PlayerPrefs.SetInt(SelectionKey, (int)selectedAppearance);
            PlayerPrefs.Save();
        }

        private void HandleDeveloperSettingsChanged()
        {
            RevertUnavailableSelectionIfNeeded();
        }

        private void RevertUnavailableSelectionIfNeeded()
        {
            if (IsAvailable(selectedAppearance)) return;
            selectedAppearance = PlayerSkinSelection.DefaultMiner;
            SaveSelection();
            SelectionChanged?.Invoke(selectedAppearance);
        }
    }
}
