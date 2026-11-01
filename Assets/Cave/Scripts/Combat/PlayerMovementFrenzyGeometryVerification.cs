using System.Collections.Generic;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Combat
{
    public static class PlayerMovementFrenzyGeometryVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return TryRunPure(out failure)
                && VerifyCollisionRestoration(out failure);
        }

        public static bool TryRunPure(out string failure)
        {
            return VerifyGuardDashTimingAndCost(out failure)
                && VerifyInputPriorities(out failure)
                && VerifyPhaseDashContactRules(out failure)
                && VerifyFrenzyMilestones(out failure)
                && VerifyStructuralResistance(out failure)
                && VerifyRedirectionControl(out failure);
        }

        private static bool VerifyGuardDashTimingAndCost(out string failure)
        {
            bool pass = Mathf.Approximately(
                    MovementFrenzyGeometryRules.ResolveGuardDashDuration(0.01f), 0.12f)
                && Mathf.Approximately(
                    MovementFrenzyGeometryRules.ResolveGuardDashDuration(2f), 0.5f)
                && Mathf.Approximately(
                    MovementFrenzyGeometryRules.ResolveGuardDashStamina(0.12f), 8f)
                && Mathf.Approximately(
                    MovementFrenzyGeometryRules.ResolveGuardDashStamina(0.5f), 30.8f);
            failure = pass ? null : "Guard Dash duration or frame-independent Stamina policy changed.";
            return pass;
        }

        private static bool VerifyInputPriorities(out string failure)
        {
            bool pass = MovementFrenzyGeometryRules.ResolveDashInput(true, true, true, true)
                    == DashInputResolution.GuardDash
                && MovementFrenzyGeometryRules.ResolveDashInput(false, true, true, true)
                    == DashInputResolution.DefensiveSlip
                && MovementFrenzyGeometryRules.ResolveDashInput(false, true, true, false)
                    == DashInputResolution.CrossStep
                && MovementFrenzyGeometryRules.ResolveDashInput(false, false, true, false)
                    == DashInputResolution.PhaseDash
                && MovementFrenzyGeometryRules.ResolveDashInput(false, false, false, true)
                    == DashInputResolution.DefensiveSlip;
            failure = pass ? null : "Guard/Cross-Step/Heavy/Dash arbitration order failed.";
            return pass;
        }

        private static bool VerifyPhaseDashContactRules(out string failure)
        {
            HashSet<int> contacts = new HashSet<int>();
            bool first = contacts.Add(101);
            bool duplicate = contacts.Add(101);
            bool second = contacts.Add(202);
            bool pass = first && !duplicate && second && contacts.Count == 2
                && MovementFrenzyGeometryRules.ShouldStopPhaseDash(true)
                && !MovementFrenzyGeometryRules.ShouldStopPhaseDash(false);
            failure = pass ? null : "Phase Dash exactly-once contact or block continuation policy failed.";
            return pass;
        }

        private static bool VerifyFrenzyMilestones(out string failure)
        {
            int remaining;
            GeometricStrikeResult inactive = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                false, 7, true, true, 2f, 0f, 1, out remaining);
            GeometricStrikeResult levelThree = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 3, true, true, 2f, 0f, 1, out remaining);
            GeometricStrikeResult levelFive = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 5, false, true, 2f, 0f, 1, out remaining);
            GeometricStrikeResult levelSeven = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 7, true, true, 1f, 0.5f, 1, out remaining);
            bool pass = inactive == GeometricStrikeResult.Rejected
                && levelThree == GeometricStrikeResult.Rejected
                && levelFive == GeometricStrikeResult.Interrupted
                && levelSeven == GeometricStrikeResult.Redirected;
            failure = pass ? null : "Frenzy levels 0/3/5/7 did not preserve reveal/disrupt/redirect gates.";
            return pass;
        }

        private static bool VerifyStructuralResistance(out string failure)
        {
            int remaining = 3;
            GeometricStrikeResult first = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 5, false, false, 0f, 1f, remaining, out remaining);
            GeometricStrikeResult second = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 5, false, false, 0f, 1f, remaining, out remaining);
            GeometricStrikeResult third = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 5, false, false, 0f, 1f, remaining, out remaining);
            bool pass = first == GeometricStrikeResult.Damaged
                && second == GeometricStrikeResult.Damaged
                && third == GeometricStrikeResult.Interrupted
                && remaining == 0;
            failure = pass ? null : "Configurable multi-hit structural resistance failed.";
            return pass;
        }

        private static bool VerifyRedirectionControl(out string failure)
        {
            int remaining;
            GeometricStrikeResult resisted = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 7, true, true, 0.75f, 1f, 2, out remaining);
            GeometricStrikeResult controlled = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 7, true, true, 1.5f, 1f, 2, out remaining);
            GeometricStrikeResult inaccurate = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                true, 7, false, true, 1.5f, 1f, 2, out remaining);
            bool pass = resisted == GeometricStrikeResult.Damaged
                && controlled == GeometricStrikeResult.Redirected
                && inaccurate == GeometricStrikeResult.Damaged;
            failure = pass ? null : "Accurate redirection did not respect structural Control/Resistance.";
            return pass;
        }

        private static bool VerifyCollisionRestoration(out string failure)
        {
            GameObject owner = new GameObject("Phase Owner");
            GameObject target = new GameObject("Phase Target");
            try
            {
                Rigidbody2D ownerBody = owner.AddComponent<Rigidbody2D>();
                ownerBody.bodyType = RigidbodyType2D.Kinematic;
                BoxCollider2D ownerCollider = owner.AddComponent<BoxCollider2D>();
                target.AddComponent<EnemyArchetypeProfile>();
                BoxCollider2D targetCollider = target.AddComponent<BoxCollider2D>();
                CommittedAttackCollisionPhasing phasing =
                    owner.AddComponent<CommittedAttackCollisionPhasing>();
                phasing.BeginTargetOnly(ownerCollider, target.transform, Vector2.right);
                bool ignored = Physics2D.GetIgnoreCollision(ownerCollider, targetCollider);
                phasing.End();
                bool restored = !Physics2D.GetIgnoreCollision(ownerCollider, targetCollider);
                bool pass = ignored && restored && !phasing.IsPhasingCharacterBodies;
                failure = pass ? null : "Committed character-body collision was not restored.";
                return pass;
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(target);
            }
        }
    }
}
