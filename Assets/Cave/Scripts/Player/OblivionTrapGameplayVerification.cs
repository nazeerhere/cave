using UnityEngine;

namespace Cave.Player
{
    /// <summary>Focused deterministic coverage for Oblivion Disk positional control.</summary>
    public static class OblivionTrapGameplayVerification
    {
        public static bool TryRunAll(out string failure)
        {
            bool between = OblivionTrapPullGeometry.IsWithinCorridor(
                Vector2.zero, new Vector2(2f, .2f), new Vector2(4f, 0f), .5f, 2.1f);
            bool behindPlayer = OblivionTrapPullGeometry.IsWithinCorridor(
                Vector2.zero, new Vector2(-1f, 0f), new Vector2(4f, 0f), .5f, 6f);
            bool beyondTrap = OblivionTrapPullGeometry.IsWithinCorridor(
                Vector2.zero, new Vector2(5f, 0f), new Vector2(4f, 0f), .5f, 6f);
            bool lateralMiss = OblivionTrapPullGeometry.IsWithinCorridor(
                Vector2.zero, new Vector2(2f, 1f), new Vector2(4f, 0f), .5f, 3f);
            bool rangeMiss = OblivionTrapPullGeometry.IsWithinCorridor(
                Vector2.zero, new Vector2(.1f, 0f), new Vector2(4f, 0f), .5f, 1f);
            bool valid = between && !behindPlayer && !beyondTrap && !lateralMiss && !rangeMiss;
            failure = valid ? null : "Oblivion Disk pull corridor geometry was incorrect.";
            return valid;
        }
    }
}
