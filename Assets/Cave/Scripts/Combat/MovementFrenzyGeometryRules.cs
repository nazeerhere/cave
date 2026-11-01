using UnityEngine;

namespace Cave.Combat
{
    public enum DashInputResolution
    {
        None,
        GuardDash,
        CrossStep,
        PhaseDash,
        DefensiveSlip,
        NormalDash
    }

    /// <summary>Pure policy shared by focused verification and runtime presentation.</summary>
    public static class MovementFrenzyGeometryRules
    {
        public static float ResolveGuardDashDuration(float heldDuration)
        {
            return Mathf.Clamp(heldDuration, 0.12f, 0.5f);
        }

        public static float ResolveGuardDashStamina(float heldDuration)
        {
            float duration = ResolveGuardDashDuration(heldDuration);
            return 8f + Mathf.Max(0f, duration - 0.12f) * 60f;
        }

        public static DashInputResolution ResolveDashInput(
            bool guarding,
            bool crossStepAvailable,
            bool heavyHeld,
            bool defensiveSlipAvailable)
        {
            if (guarding) return DashInputResolution.GuardDash;
            if (defensiveSlipAvailable) return DashInputResolution.DefensiveSlip;
            if (crossStepAvailable) return DashInputResolution.CrossStep;
            if (heavyHeld) return DashInputResolution.PhaseDash;
            return DashInputResolution.NormalDash;
        }

        public static bool ShouldStopPhaseDash(bool defenseBlocked)
        {
            return defenseBlocked;
        }

        public static GeometricStrikeResult ResolveGeometricStrike(
            bool frenzyActive,
            int masteryLevel,
            bool accurateStrike,
            bool redirectable,
            float control,
            float resistance,
            int remainingResistance,
            out int resolvedRemainingResistance)
        {
            resolvedRemainingResistance = Mathf.Max(0, remainingResistance);
            if (!frenzyActive || masteryLevel < 5 || resolvedRemainingResistance <= 0)
            {
                return GeometricStrikeResult.Rejected;
            }

            if (masteryLevel >= 7
                && accurateStrike
                && redirectable
                && control + 0.0001f >= Mathf.Max(0f, resistance))
            {
                return GeometricStrikeResult.Redirected;
            }

            resolvedRemainingResistance--;
            return resolvedRemainingResistance == 0
                ? GeometricStrikeResult.Interrupted
                : GeometricStrikeResult.Damaged;
        }
    }
}
