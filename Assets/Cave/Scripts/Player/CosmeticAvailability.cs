using Cave.Diagnostics;

namespace Cave.Player
{
    /// <summary>Shared cosmetic availability policy. Progression ownership remains with each cosmetic owner.</summary>
    public static class CosmeticAvailability
    {
        public static bool IsAvailable(bool actuallyUnlocked)
        {
            return IsAvailable(actuallyUnlocked, DeveloperDiagnosticsSettings.UnlockAllCosmetics);
        }

        public static bool IsAvailable(bool actuallyUnlocked, bool developerUnlockAllCosmetics)
        {
            return actuallyUnlocked || developerUnlockAllCosmetics;
        }
    }
}
