using System.Collections.Generic;

namespace Cave.Enemies
{
    /// <summary>Focused deterministic verification for the non-Unity CTC core.</summary>
    public static class CombatTacticalCoordinatorVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyDisabledFallback(out failure)
                || !VerifyFormationIsolation(out failure)
                || !VerifyMembershipLifecycle(out failure)
                || !VerifyIntentSafety(out failure)
                || !VerifyDeterminism(out failure)
                || !VerifyVerticalSliceRoles(out failure)
                || !VerifyBoundedEvaluation(out failure)
                || !VerifyKnowledgeSnapshotBoundary(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyDisabledFallback(out string failure)
        {
            CombatTacticalFormationState state = new CombatTacticalFormationState("disabled");
            state.Register("bandit", CombatTacticalRole.Skirmisher);
            bool evaluated = state.Evaluate();
            CombatTacticalIntent intent;
            bool hasIntent = state.TryGetIntent("bandit", out intent);
            failure = !evaluated && !hasIntent && intent == CombatTacticalIntent.None
                ? null
                : "Disabled CTC exposed an intent.";
            return failure == null;
        }

        private static bool VerifyFormationIsolation(out string failure)
        {
            CombatTacticalFormationState first = new CombatTacticalFormationState("first");
            CombatTacticalFormationState second = new CombatTacticalFormationState("second");
            first.Register("shared-member", CombatTacticalRole.Frontline);
            second.Register("shared-member", CombatTacticalRole.Support);
            first.SetEnabled(true);
            second.SetEnabled(true);
            first.Evaluate();
            second.Evaluate();
            CombatTacticalIntent firstIntent;
            CombatTacticalIntent secondIntent;
            bool isolated = first.TryGetIntent("shared-member", out firstIntent)
                && second.TryGetIntent("shared-member", out secondIntent)
                && firstIntent == CombatTacticalIntent.Pressure
                && secondIntent == CombatTacticalIntent.Support;
            failure = isolated ? null : "Formations shared tactical assignments.";
            return isolated;
        }

        private static bool VerifyMembershipLifecycle(out string failure)
        {
            CombatTacticalFormationState state = new CombatTacticalFormationState("lifecycle");
            bool joined = state.Register("brute", CombatTacticalRole.Frontline);
            bool updated = state.Register("brute", CombatTacticalRole.Frontline);
            state.SetEnabled(true);
            state.Evaluate();
            bool left = state.Unregister("brute");
            CombatTacticalIntent intent;
            bool stale = state.TryGetIntent("brute", out intent);
            bool valid = joined && updated && left && state.MemberCount == 0 && !stale;
            failure = valid ? null : "Membership lifecycle retained stale tactical state.";
            return valid;
        }

        private static bool VerifyIntentSafety(out string failure)
        {
            CombatTacticalFormationState state = new CombatTacticalFormationState("safety");
            bool rejected = !state.Register("unknown", CombatTacticalRole.None);
            state.SetEnabled(true);
            state.Evaluate();
            CombatTacticalIntent intent;
            bool noIntent = !state.TryGetIntent("unknown", out intent)
                && intent == CombatTacticalIntent.None;
            failure = rejected && noIntent ? null : "Unsupported intent participant was not fail-soft.";
            return failure == null;
        }

        private static bool VerifyDeterminism(out string failure)
        {
            List<CombatTacticalMemberDescriptor> first = new List<CombatTacticalMemberDescriptor>
            {
                new CombatTacticalMemberDescriptor("wizard", CombatTacticalRole.Support),
                new CombatTacticalMemberDescriptor("bandit", CombatTacticalRole.Skirmisher),
                new CombatTacticalMemberDescriptor("brute", CombatTacticalRole.Frontline)
            };
            List<CombatTacticalMemberDescriptor> second = new List<CombatTacticalMemberDescriptor>
            {
                new CombatTacticalMemberDescriptor("brute", CombatTacticalRole.Frontline),
                new CombatTacticalMemberDescriptor("wizard", CombatTacticalRole.Support),
                new CombatTacticalMemberDescriptor("bandit", CombatTacticalRole.Skirmisher)
            };
            IReadOnlyList<CombatTacticalAssignment> left = CombatTacticalCoordinator.Evaluate(first);
            IReadOnlyList<CombatTacticalAssignment> right = CombatTacticalCoordinator.Evaluate(second);
            bool equal = left.Count == right.Count;
            for (int index = 0; equal && index < left.Count; index++)
            {
                equal = left[index].MemberId == right[index].MemberId
                    && left[index].Intent == right[index].Intent;
            }

            failure = equal ? null : "Equivalent CTC input produced different assignments.";
            return equal;
        }

        private static bool VerifyVerticalSliceRoles(out string failure)
        {
            List<CombatTacticalMemberDescriptor> members = new List<CombatTacticalMemberDescriptor>
            {
                new CombatTacticalMemberDescriptor("bandit", CombatTacticalRole.Skirmisher),
                new CombatTacticalMemberDescriptor("brute", CombatTacticalRole.Frontline),
                new CombatTacticalMemberDescriptor("wizard", CombatTacticalRole.Support)
            };
            IReadOnlyList<CombatTacticalAssignment> assignments = CombatTacticalCoordinator.Evaluate(members);
            bool valid = assignments.Count == 3
                && assignments[0].MemberId == "bandit"
                && assignments[0].Intent == CombatTacticalIntent.Flank
                && assignments[1].MemberId == "brute"
                && assignments[1].Intent == CombatTacticalIntent.Pressure
                && assignments[2].MemberId == "wizard"
                && assignments[2].Intent == CombatTacticalIntent.Support;
            failure = valid ? null : "Bandit/Brute/Wizard tactical assignments were incorrect.";
            return valid;
        }

        private static bool VerifyBoundedEvaluation(out string failure)
        {
            CombatTacticalFormationState state = new CombatTacticalFormationState("bounded");
            state.Register("a", CombatTacticalRole.Frontline);
            state.Register("b", CombatTacticalRole.Skirmisher);
            state.SetEnabled(true);
            state.Evaluate();
            bool valid = state.EvaluationCount == 1 && state.MemberCount == 2;
            failure = valid ? null : "Formation evaluation did not remain formation-bounded.";
            return valid;
        }

        private static bool VerifyKnowledgeSnapshotBoundary(out string failure)
        {
            CombatTacticalFormationState state = new CombatTacticalFormationState("knowledge");
            state.Register("brute", CombatTacticalRole.Frontline);
            KnowledgeStore bruteKnowledge = new KnowledgeStore("brute");
            bruteKnowledge.Observe(
                KnowledgeFactType.PlayerRetreating,
                KnowledgeSubject.Player,
                default,
                0f);
            state.SetKnowledgeSnapshot("brute", bruteKnowledge.CreateSnapshot(0f));
            state.SetEnabled(true);
            state.Evaluate();
            CombatTacticalIntent intent;
            bool valid = state.LastKnowledgeFactCount == 1
                && state.TryGetIntent("brute", out intent)
                && intent == CombatTacticalIntent.Pressure;
            failure = valid ? null : "CTC did not consume the bounded member knowledge snapshot.";
            return valid;
        }
    }
}
