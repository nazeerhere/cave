using UnityEngine;

namespace Cave.Player
{
    /// <summary>Focused deterministic checks for the policy portion of the Carapace system.</summary>
    public static class ResonantCarapaceVerification
    {
        public static bool TryVerify(out string failure)
        {
            if (ResonantCarapacePolicy.ResolveNormalAbsorption(40, 100, .5f) != 20)
            {
                failure = "40 damage did not split into 20 Carapace durability.";
                return false;
            }
            if (ResonantCarapacePolicy.ResolveNormalAbsorption(20, 6, .5f) != 6)
            {
                failure = "Low durability did not cap Carapace absorption.";
                return false;
            }
            if (ResonantCarapacePolicy.ResolveNormalAbsorption(20, 0, .5f) != 0)
            {
                failure = "Depleted Carapace still absorbed damage.";
                return false;
            }
            if (ResonantCarapacePolicy.ResolveNormalAbsorption(7, 100, .5f) != 3)
            {
                failure = "Odd damage did not use the deterministic player-favouring split.";
                return false;
            }
            if (!Mathf.Approximately(ResonantCarapacePolicy.ResolveDeepBraceDiversion(10f), 5f)
                || !Mathf.Approximately(ResonantCarapacePolicy.ResolveDeepBraceDiversion(-1f), 0f))
            {
                failure = "Deep Brace diversion was not a bounded 50% of existing recovery.";
                return false;
            }
            if (!Mathf.Approximately(ResonantCarapacePolicy.ResolvePhysicalControlMultiplier(.4f), .6f)
                || !Mathf.Approximately(ResonantCarapacePolicy.ResolvePhysicalControlMultiplier(2f), 0f))
            {
                failure = "Physical-control resistance did not clamp to a non-negative multiplier.";
                return false;
            }
            if (ResonantCarapacePolicy.ResolveIntegrityStage(100, 100, .75f, .5f, .25f)
                    != ResonantCarapaceIntegrityStage.Sealed
                || ResonantCarapacePolicy.ResolveIntegrityStage(74, 100, .75f, .5f, .25f)
                    != ResonantCarapaceIntegrityStage.Cracked
                || ResonantCarapacePolicy.ResolveIntegrityStage(49, 100, .75f, .5f, .25f)
                    != ResonantCarapaceIntegrityStage.Compromised
                || ResonantCarapacePolicy.ResolveIntegrityStage(24, 100, .75f, .5f, .25f)
                    != ResonantCarapaceIntegrityStage.Critical
                || ResonantCarapacePolicy.ResolveIntegrityStage(0, 100, .75f, .5f, .25f)
                    != ResonantCarapaceIntegrityStage.Depleted)
            {
                failure = "Progressive integrity thresholds did not select the approved stages.";
                return false;
            }
            if (ResonantCarapacePolicy.ResolveNormalAbsorption(100, 100, .5f) != 50
                || ResonantCarapacePolicy.ResolveNormalAbsorption(100, 100, .4f) != 40
                || ResonantCarapacePolicy.ResolveNormalAbsorption(100, 100, .3f) != 30
                || ResonantCarapacePolicy.ResolveNormalAbsorption(100, 100, .15f) != 15)
            {
                failure = "Progressive damage shares did not retain deterministic split rounding.";
                return false;
            }
            if (!Mathf.Approximately(ResonantCarapacePolicy.ResolveStagedPhysicalResistance(.4f, .75f), .3f)
                || !Mathf.Approximately(ResonantCarapacePolicy.ResolveStagedPhysicalResistance(.4f, .25f), .1f))
            {
                failure = "Integrity effectiveness did not scale existing Anchored Lattice resistance.";
                return false;
            }
            if (!Mathf.Approximately(ResonantCarapacePolicy.ResolveRestorationManaCost(130f, .5f), 65f)
                || ResonantCarapacePolicy.ResolveRestorationAmount(500, .25f) != 125)
            {
                failure = "Restoration Burst did not use maximum-resource percentage costs and repair.";
                return false;
            }
            if (!ResonantCarapacePolicy.CanRestoreWithBurst(true, false, false, 0, 100, 50f, 50f)
                || ResonantCarapacePolicy.CanRestoreWithBurst(true, false, false, 100, 100, 100f, 50f)
                || ResonantCarapacePolicy.CanRestoreWithBurst(true, true, false, 0, 100, 100f, 50f)
                || ResonantCarapacePolicy.CanRestoreWithBurst(true, false, true, 50, 100, 100f, 50f)
                || ResonantCarapacePolicy.CanRestoreWithBurst(true, false, false, 50, 100, 49f, 50f))
            {
                failure = "Restoration Burst eligibility did not fail closed for full, terminal, suppressed, or underfunded states.";
                return false;
            }
            failure = null;
            return true;
        }
    }
}
