using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for persistent pure relationship membership.</summary>
    public static class PhenomenonRelationshipGroupVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyCreateAndDuplicates(out failure)
                && VerifyCreateRejections(out failure)
                && VerifyJoin(out failure)
                && VerifyLeaveAndDissolution(out failure)
                && VerifyExplicitDissolution(out failure)
                && VerifyStateIndependenceImmutabilityAndIds(out failure)
                && VerifyLawEligibility(out failure)
                && VerifyDeterminism(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyCreateAndDuplicates(out string failure)
        {
            PhenomenonRelationshipGroupResult created = Create("heat-group", "A", "B", "C");
            PhenomenonRelationshipGroupResult duplicates = Create("duplicates", "A", "B", "A", "C");
            PhenomenonRelationshipGroupResult invalid = Create("invalid-member", "A", " ", "B");

            return Expect(created.Succeeded
                && created.GroupAfter.Phenomenon == LawPhenomenon.Heat
                && created.GroupAfter.MemberCount == 3
                && created.GroupAfter.OrderedMembers[0].Value == "A"
                && created.GroupAfter.OrderedMembers[1].Value == "B"
                && created.GroupAfter.OrderedMembers[2].Value == "C"
                && duplicates.Succeeded
                && duplicates.GroupAfter.MemberCount == 3
                && duplicates.InitialMemberResults[2].Reason == PhenomenonRelationshipGroupInitialMemberReason.DuplicateMember
                && invalid.Succeeded
                && invalid.GroupAfter.MemberCount == 2
                && invalid.InitialMemberResults[1].Reason == PhenomenonRelationshipGroupInitialMemberReason.InvalidCarrierId,
                "Relationship-group creation did not preserve first valid membership order or inspectable skipped inputs.", out failure);
        }

        private static bool VerifyCreateRejections(out string failure)
        {
            PhenomenonRelationshipGroupResult insufficient = Create("small", "A");
            PhenomenonRelationshipGroupResult invalidId = Create("", "A", "B");

            return Expect(!insufficient.Succeeded
                && insufficient.RejectionReason == PhenomenonRelationshipGroupRejectionReason.InsufficientMembers
                && !invalidId.Succeeded
                && invalidId.RejectionReason == PhenomenonRelationshipGroupRejectionReason.InvalidGroupId,
                "Relationship-group creation did not reject insufficient membership or an invalid ID deterministically.", out failure);
        }

        private static bool VerifyJoin(out string failure)
        {
            PhenomenonRelationshipGroup original = Create("join", "A", "B").GroupAfter;
            PhenomenonRelationshipGroupResult joined = SynchronizationRelationshipGroupEvaluator.Join(
                original, Id("C"), LawPhenomenon.Heat);
            PhenomenonRelationshipGroupResult duplicate = SynchronizationRelationshipGroupEvaluator.Join(
                original, Id("B"), LawPhenomenon.Heat);
            PhenomenonRelationshipGroupResult mismatch = SynchronizationRelationshipGroupEvaluator.Join(
                original, Id("P"), LawPhenomenon.Potential);
            PhenomenonRelationshipGroupResult invalidPhenomenon = SynchronizationRelationshipGroupEvaluator.Join(
                original, Id("X"), (LawPhenomenon)999);

            return Expect(joined.Succeeded
                && joined.MembershipChanged
                && joined.GroupAfter.MemberCount == 3
                && joined.GroupAfter.OrderedMembers[2].Value == "C"
                && original.MemberCount == 2
                && duplicate.Succeeded
                && !duplicate.MembershipChanged
                && duplicate.RejectionReason == PhenomenonRelationshipGroupRejectionReason.AlreadyMember
                && !mismatch.Succeeded
                && mismatch.RejectionReason == PhenomenonRelationshipGroupRejectionReason.PhenomenonMismatch
                && !invalidPhenomenon.Succeeded
                && invalidPhenomenon.RejectionReason == PhenomenonRelationshipGroupRejectionReason.InvalidPhenomenon,
                "Relationship-group Join did not preserve immutability or validate duplicate/phenomenon membership.", out failure);
        }

        private static bool VerifyLeaveAndDissolution(out string failure)
        {
            PhenomenonRelationshipGroup group = Create("leave", "A", "B", "C").GroupAfter;
            PhenomenonRelationshipGroupResult leave = SynchronizationRelationshipGroupEvaluator.Leave(group, Id("B"));
            PhenomenonRelationshipGroup twoMembers = Create("dissolve-on-leave", "A", "B").GroupAfter;
            PhenomenonRelationshipGroupResult dissolved = SynchronizationRelationshipGroupEvaluator.Leave(twoMembers, Id("B"));
            PhenomenonRelationshipGroupResult missing = SynchronizationRelationshipGroupEvaluator.Leave(group, Id("X"));

            return Expect(leave.Succeeded
                && leave.MembershipChanged
                && !leave.Dissolved
                && leave.GroupAfter.MemberCount == 2
                && leave.GroupAfter.OrderedMembers[0].Value == "A"
                && leave.GroupAfter.OrderedMembers[1].Value == "C"
                && group.MemberCount == 3
                && dissolved.Succeeded
                && dissolved.Dissolved
                && dissolved.GroupAfter == null
                && dissolved.PreviousMemberCount == 2
                && !missing.Succeeded
                && missing.RejectionReason == PhenomenonRelationshipGroupRejectionReason.MemberNotFound,
                "Relationship-group Leave did not preserve order or expose dissolution/missing-member outcomes.", out failure);
        }

        private static bool VerifyExplicitDissolution(out string failure)
        {
            PhenomenonRelationshipGroup group = Create("dissolve", "A", "B", "C").GroupAfter;
            PhenomenonRelationshipGroupResult result = SynchronizationRelationshipGroupEvaluator.Dissolve(group);
            PhenomenonRelationshipGroupResult already = SynchronizationRelationshipGroupEvaluator.Dissolve(null);

            return Expect(result.Succeeded
                && result.Dissolved
                && result.GroupBefore == group
                && result.GroupAfter == null
                && result.PreviousMemberCount == 3
                && !already.Succeeded
                && already.RejectionReason == PhenomenonRelationshipGroupRejectionReason.GroupAlreadyDissolved,
                "Explicit dissolution did not preserve previous group inspection or report already-dissolved state.", out failure);
        }

        private static bool VerifyStateIndependenceImmutabilityAndIds(out string failure)
        {
            PhenomenonRelationshipGroup group = Create("state-free", "A", "B", "C").GroupAfter;
            PhenomenonSemanticSnapshot heatLow = Snapshot(LawPhenomenon.Heat, -2f);
            PhenomenonSemanticSnapshot heatHigh = Snapshot(LawPhenomenon.Heat, 4f);
            PhenomenonResolutionResult operation = PhenomenonOperationResolver.Resolve(new PhenomenonOperationRequest(
                PhenomenonOperationKind.Add, LawPhenomenon.Heat, 2f, heatLow));
            PhenomenonRelationshipGroupResult join = SynchronizationRelationshipGroupEvaluator.Join(
                group, Id("D"), LawPhenomenon.Heat);
            PhenomenonRelationshipGroupId first = new PhenomenonRelationshipGroupId("same");
            PhenomenonRelationshipGroupId second = new PhenomenonRelationshipGroupId("same");
            PhenomenonRelationshipGroupId third = new PhenomenonRelationshipGroupId("other");

            return Expect(operation.Succeeded
                && heatLow.Region == PhenomenonSemanticRegion.Low
                && heatHigh.Region == PhenomenonSemanticRegion.High
                && group.MemberCount == 3
                && group.Contains(Id("A"))
                && group.Contains(Id("B"))
                && group.Contains(Id("C"))
                && !group.Contains(Id("D"))
                && join.GroupAfter.MemberCount == 4
                && first.Equals(second)
                && first.GetHashCode() == second.GetHashCode()
                && first.CompareTo(third) != 0
                && new PhenomenonRelationshipGroupId(" ").IsValid == false,
                "Relationship groups stored state, equalized members, mutated prior instances, or had unstable IDs.", out failure);
        }

        private static bool VerifyLawEligibility(out string failure)
        {
            DomainLaw projectile = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Synchronization);
            DomainLaw frenzy = Law(LawExpression.Frenzy, LawPhenomenon.Heat, LawTerritoryPrinciple.Synchronization);
            PhenomenonRelationshipGroupResult projectileCreated = Create(
                projectile, new LawExpressionContext(true, LawExpression.Projectile, false), "projectile", "A", "B");
            PhenomenonRelationshipGroupResult mismatch = Create(
                projectile, new LawExpressionContext(true, LawExpression.Frenzy, true), "mismatch", "A", "B");
            PhenomenonRelationshipGroupResult frenzyActive = Create(
                frenzy, new LawExpressionContext(true, LawExpression.Frenzy, true), "frenzy-active", "A", "B");
            PhenomenonRelationshipGroupResult frenzyInactive = Create(
                frenzy, new LawExpressionContext(true, LawExpression.Frenzy, false), "frenzy-inactive", "A", "B");

            return Expect(projectileCreated.Succeeded
                && !mismatch.Succeeded
                && mismatch.RejectionReason == PhenomenonRelationshipGroupRejectionReason.ExpressionMismatch
                && frenzyActive.Succeeded
                && !frenzyInactive.Succeeded
                && frenzyInactive.RejectionReason == PhenomenonRelationshipGroupRejectionReason.FrenzyInactive,
                "Synchronization group creation did not reuse pure Law eligibility.", out failure);
        }

        private static bool VerifyDeterminism(out string failure)
        {
            PhenomenonRelationshipGroupResult first = Create("deterministic", "A", "B", "A", "C");
            PhenomenonRelationshipGroupResult second = Create("deterministic", "A", "B", "A", "C");

            return Expect(first.Succeeded
                && second.Succeeded
                && first.GroupAfter.MemberCount == second.GroupAfter.MemberCount
                && first.GroupAfter.OrderedMembers[0].Equals(second.GroupAfter.OrderedMembers[0])
                && first.GroupAfter.OrderedMembers[1].Equals(second.GroupAfter.OrderedMembers[1])
                && first.GroupAfter.OrderedMembers[2].Equals(second.GroupAfter.OrderedMembers[2])
                && first.InitialMemberResults[2].Reason == second.InitialMemberResults[2].Reason,
                "Relationship group creation did not preserve deterministic first-occurrence order.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string lawFailure;
            string semanticsFailure;
            string operationsFailure;
            string lawEvaluationFailure;
            string topologyFailure;
            string catalysisFailure;
            string accumulationFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            bool operations = PhenomenonOperationsVerification.TryRunAll(out operationsFailure);
            bool lawEvaluation = DomainLawEvaluationVerification.TryRunAll(out lawEvaluationFailure);
            bool topology = PhenomenonTopologyVerification.TryRunAll(out topologyFailure);
            bool catalysis = CatalysisEvaluationVerification.TryRunAll(out catalysisFailure);
            bool accumulation = AccumulationEvaluationVerification.TryRunAll(out accumulationFailure);
            return Expect(laws && semantics && operations && lawEvaluation && topology && catalysis && accumulation,
                "Earlier Domain verification regressed: law=" + lawFailure
                + "; semantics=" + semanticsFailure
                + "; operations=" + operationsFailure
                + "; evaluation=" + lawEvaluationFailure
                + "; topology=" + topologyFailure
                + "; catalysis=" + catalysisFailure
                + "; accumulation=" + accumulationFailure,
                out failure);
        }

        private static PhenomenonRelationshipGroupResult Create(string groupId, params string[] members)
        {
            return Create(
                Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Synchronization),
                new LawExpressionContext(true, LawExpression.Projectile, false), groupId, members);
        }

        private static PhenomenonRelationshipGroupResult Create(
            DomainLaw law,
            LawExpressionContext context,
            string groupId,
            params string[] members)
        {
            PhenomenonCarrierId[] ids = new PhenomenonCarrierId[members.Length];
            for (int index = 0; index < members.Length; index++) ids[index] = Id(members[index]);
            return SynchronizationRelationshipGroupEvaluator.Create(
                new SynchronizationRelationshipGroupCreationRequest(
                    law, context, new PhenomenonRelationshipGroupId(groupId), ids));
        }

        private static DomainLaw Law(
            LawExpression expression,
            LawPhenomenon phenomenon,
            LawTerritoryPrinciple principle)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, principle, out law, out validation))
            {
                throw new InvalidOperationException("Verification could not create Law: " + validation.RejectionReason);
            }
            return law;
        }

        private static PhenomenonCarrierId Id(string value)
        {
            return new PhenomenonCarrierId(value);
        }

        private static PhenomenonSemanticSnapshot Snapshot(LawPhenomenon phenomenon, float value)
        {
            PhenomenonSemanticSnapshot snapshot;
            PhenomenonSemanticResult result;
            if (!PhenomenonSemanticClassifier.TryCreateSnapshot(phenomenon, value, out snapshot, out result))
            {
                throw new InvalidOperationException("Verification could not create semantic state: " + result.RejectionReason);
            }
            return snapshot;
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }
    }
}
