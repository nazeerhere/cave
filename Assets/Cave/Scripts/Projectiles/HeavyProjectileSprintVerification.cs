using UnityEngine;
using Cave.Combat;

namespace Cave.Projectiles
{
    /// <summary>Pure deterministic coverage for Sprint 2 charge and Frost merge rules.</summary>
    public static class HeavyProjectileSprintVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyChargeThresholds(out failure)
                || !VerifyHeavyNeverPierces(out failure)
                || !VerifyMergeGeometryAndCap(out failure)
                || !VerifyHeavyFollowUpEntries(out failure)
                || !VerifyProjectilePresentationRouting(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyChargeThresholds(out string failure)
        {
            bool valid = Mathf.Approximately(ChargedProjectilePolicy.NormalizedCharge(-1f, 1.2f), 0f)
                && Mathf.Approximately(ChargedProjectilePolicy.NormalizedCharge(0.6f, 1.2f), 0.5f)
                && Mathf.Approximately(ChargedProjectilePolicy.NormalizedCharge(3f, 1.2f), 1f)
                && !ChargedProjectilePolicy.IsReady(0.34f, 0.35f)
                && ChargedProjectilePolicy.IsReady(0.35f, 0.35f);
            failure = valid ? null : "Heavy charge threshold or normalized progress was incorrect.";
            return valid;
        }

        private static bool VerifyHeavyNeverPierces(out string failure)
        {
            bool valid = ChargedProjectilePolicy.HeavyMaximumEnemyHits == 1;
            failure = valid ? null : "Heavy projectile could inherit rapid-fire pierce.";
            return valid;
        }

        private static bool VerifyMergeGeometryAndCap(out string failure)
        {
            bool overlaps = SlowFieldMergePolicy.Overlaps(Vector2.zero, 2f, new Vector2(3.9f, 0f), 2f);
            bool separate = !SlowFieldMergePolicy.Overlaps(Vector2.zero, 2f, new Vector2(4.1f, 0f), 2f);
            float grown = SlowFieldMergePolicy.ResolveMergedRadius(2.5f, 0.4f, 4.5f);
            float capped = SlowFieldMergePolicy.ResolveMergedRadius(4.4f, 0.4f, 4.5f);
            bool valid = overlaps && separate
                && Mathf.Approximately(grown, 2.9f)
                && Mathf.Approximately(capped, 4.5f);
            failure = valid ? null : "Frost field merge geometry or growth cap was incorrect.";
            return valid;
        }

        private static bool VerifyHeavyFollowUpEntries(out string failure)
        {
            bool valid = HeavyFollowUpPolicy.StartingTier(FormalFollowUpSource.NormalParry) == 2
                && HeavyFollowUpPolicy.StartingTier(FormalFollowUpSource.PerfectParry) == 3
                && HeavyFollowUpPolicy.StartingTier(FormalFollowUpSource.GuardBreak) == 2
                && ChargedAttack.StartingDurationForTier(2, .25f, .67f, 1.5f) == .67f
                && ChargedAttack.StartingDurationForTier(3, .25f, .67f, 1.5f) == 1.5f
                && ChargedAttack.StartingDurationForTier(0, .25f, .67f, 1.5f) == 0f;
            failure = valid ? null : "Heavy follow-up entry no longer starts at the normal T2/T3 chain state.";
            return valid;
        }

        private static bool VerifyProjectilePresentationRouting(out string failure)
        {
            bool valid = PlayerProjectilePresentation.ResourcePathFor(SpecialMode.BurnShot, 1, false)
                    == "Projectiles/Player/Fire/fire_stage_1"
                && PlayerProjectilePresentation.ResourcePathFor(SpecialMode.SlowShot, 2, false)
                    == "Projectiles/Player/Ice/ice_stage_2"
                && PlayerProjectilePresentation.ResourcePathFor(SpecialMode.Flight, 3, false)
                    == "Projectiles/Player/Wind/wind_stage_3"
                && PlayerProjectilePresentation.ResourcePathFor(SpecialMode.DamageBoost, 3, false)
                    == "Projectiles/Player/Earth/earth_stage_3"
                && PlayerProjectilePresentation.ResourcePathFor(SpecialMode.BurnShot, 1, true)
                    == "Projectiles/Player/Imaginary_Axiom/imaginary_axiom_stage_1"
                && PlayerProjectilePresentation.ResourcePathFor(SpecialMode.SlowShot, 2, true)
                    == "Projectiles/Player/Imaginary_Axiom/imaginary_axiom_stage_2"
                && PlayerProjectilePresentation.ResourcePathFor(SpecialMode.DamageBoost, 3, true)
                    == "Projectiles/Player/Imaginary_Axiom/imaginary_axiom_stage_3";
            failure = valid ? null : "Projectile stage or Imaginary presentation routing was incorrect.";
            return valid;
        }
    }
}
