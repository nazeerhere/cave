using UnityEngine;
using Cave.FieldControl;

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
            bool authoredBalance = Mathf.Approximately(PlayerLandmineInventory.BalancedMineLifetime, 216f)
                && Mathf.Approximately(PlayerLandmineInventory.BalancedTriggerRadius, 1.95f)
                && Mathf.Approximately(PlayerLandmineInventory.BalancedLocalPulseRadius, 3.6f)
                && Mathf.Approximately(PlayerLandmineInventory.BalancedBlastRadius, 6.6f)
                && Mathf.Approximately(PlayerLandmineInventory.BalancedMaximumLinkDistance, 18f)
                && PlayerLandmine.OverlapQueryCapacity >= 64;
            bool topology = FieldNetwork.IsWithinLinkDistance(Vector2.zero, new Vector2(18f, 0f), 18f)
                && !FieldNetwork.IsWithinLinkDistance(Vector2.zero, new Vector2(18.01f, 0f), 18f);
            float oldTwoNodeDuration = ResolveOperationalDuration(72f, 8f, .16f, 1);
            float newTwoNodeDuration = ResolveOperationalDuration(
                PlayerLandmineInventory.BalancedMineLifetime,
                PlayerLandmineInventory.BalancedMineEnergy,
                .16f,
                1);
            float oldTriangleDuration = ResolveOperationalDuration(72f, 8f, .16f, 3);
            float newTriangleDuration = ResolveOperationalDuration(
                PlayerLandmineInventory.BalancedMineLifetime,
                PlayerLandmineInventory.BalancedMineEnergy,
                .16f,
                3);
            bool energyBudget = Mathf.Approximately(newTwoNodeDuration, oldTwoNodeDuration * 3f)
                && Mathf.Approximately(newTriangleDuration, oldTriangleDuration * 3f)
                && Mathf.Approximately(FieldNetwork.ResolveEnergyDrain(.16f, .25f, 3), .12f);
            bool valid = between && !behindPlayer && !beyondTrap && !lateralMiss && !rangeMiss
                && authoredBalance && topology && energyBudget;
            failure = valid ? null : "Oblivion Disk balance, topology, energy, or pull geometry was incorrect.";
            return valid;
        }

        private static float ResolveOperationalDuration(
            float lifetime,
            float energy,
            float drainPerLinkPerSecond,
            int linkCount)
        {
            float drain = FieldNetwork.ResolveEnergyDrain(drainPerLinkPerSecond, 1f, linkCount);
            return drain <= 0f
                ? Mathf.Max(0f, lifetime)
                : Mathf.Min(Mathf.Max(0f, lifetime), Mathf.Max(0f, energy) / drain);
        }
    }
}
