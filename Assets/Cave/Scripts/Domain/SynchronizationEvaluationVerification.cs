using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for persistent Synchronization delta coupling.</summary>
    public static class SynchronizationEvaluationVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyBasicAddAndRemove(out failure)
                && VerifyNoEqualizationAndIndependentRegions(out failure)
                && VerifyMembershipOrderAndSnapshotAuthority(out failure)
                && VerifyMemberSkipsAndStructuralRejections(out failure)
                && VerifyLawEligibilityAndUnsupportedOperations(out failure)
                && VerifyPhenomenonSpecificBehavior(out failure)
                && VerifyImmutabilityDeterminismAndNoRecursion(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyBasicAddAndRemove(out string failure)
        {
            PhenomenonRelationshipGroup group = Group(LawPhenomenon.Heat, "basic", "A", "B", "C");
            PhenomenonCarrierSnapshot a = Carrier("A", LawPhenomenon.Heat, -1f);
            SynchronizationEvaluationResult add = Evaluate(
                Law(LawExpression.Projectile, LawPhenomenon.Heat), group, a,
                PhenomenonOperationKind.Add, 1f,
                Carrier("B", LawPhenomenon.Heat, 1f),
                Carrier("C", LawPhenomenon.Heat, 3f));
            SynchronizationEvaluationResult remove = Evaluate(
                Law(LawExpression.Projectile, LawPhenomenon.Heat), group, Carrier("A", LawPhenomenon.Heat, 0f),
                PhenomenonOperationKind.Remove, 2f,
                Carrier("B", LawPhenomenon.Heat, 3f),
                Carrier("C", LawPhenomenon.Heat, 0f));

            return Expect(add.Succeeded
                && add.SynchronizedRecipientCount == 2
                && add.MemberResults[0].MemberId.Value == "B"
                && add.MemberResults[1].MemberId.Value == "C"
                && add.MemberResults[0].MirroredOperation == PhenomenonOperationKind.Add
                && Approximately(add.MemberResults[0].MirroredMagnitude, 1f)
                && Approximately(add.MemberResults[0].After.SemanticValue, 2f)
                && Approximately(add.MemberResults[1].After.SemanticValue, 4f)
                && remove.Succeeded
                && Approximately(remove.MemberResults[0].After.SemanticValue, 1f)
                && Approximately(remove.MemberResults[1].After.SemanticValue, -2f),
                "Synchronization did not mirror the initiating Add/Remove exactly to other members.", out failure);
        }

        private static bool VerifyNoEqualizationAndIndependentRegions(out string failure)
        {
            PhenomenonRelationshipGroup group = Group(LawPhenomenon.Heat, "offsets", "A", "B", "C", "D");
            PhenomenonCarrierSnapshot a = Carrier("A", LawPhenomenon.Heat, -3f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 4f);
            PhenomenonCarrierSnapshot d = Carrier("D", LawPhenomenon.Heat, 1f);
            SynchronizationEvaluationResult result = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Heat), group, a,
                PhenomenonOperationKind.Add, 1f, b, c, d);

            return Expect(result.Succeeded
                && Approximately(a.SemanticState.SemanticValue, -3f)
                && Approximately(b.SemanticState.SemanticValue, 0f)
                && Approximately(c.SemanticState.SemanticValue, 4f)
                && Approximately(result.MemberResults[0].After.SemanticValue, 1f)
                && Approximately(result.MemberResults[1].After.SemanticValue, 5f)
                && Approximately(result.MemberResults[2].After.SemanticValue, 2f)
                && result.MemberResults[0].After.Region == PhenomenonSemanticRegion.Regular
                && result.MemberResults[1].After.Region == PhenomenonSemanticRegion.High
                && result.MemberResults[2].After.Region == PhenomenonSemanticRegion.High,
                "Synchronization equalized member state or imposed a shared region instead of mirroring delta.", out failure);
        }

        private static bool VerifyMembershipOrderAndSnapshotAuthority(out string failure)
        {
            PhenomenonRelationshipGroup group = Group(LawPhenomenon.Heat, "order", "C", "A", "D", "B");
            SynchronizationEvaluationResult ordered = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Heat), group,
                Carrier("A", LawPhenomenon.Heat, 0f), PhenomenonOperationKind.Add, 1f,
                Carrier("B", LawPhenomenon.Heat, 0f),
                Carrier("D", LawPhenomenon.Heat, 0f),
                Carrier("C", LawPhenomenon.Heat, 0f),
                Carrier("X", LawPhenomenon.Heat, 0f));

            return Expect(ordered.Succeeded
                && ordered.RequestedSnapshotCount == 4
                && ordered.MemberResults.Count == 3
                && ordered.MemberResults[0].MemberId.Value == "C"
                && ordered.MemberResults[1].MemberId.Value == "D"
                && ordered.MemberResults[2].MemberId.Value == "B"
                && ordered.SynchronizedRecipientCount == 3,
                "Synchronization did not use persistent membership order or synchronized a nonmember snapshot.", out failure);
        }

        private static bool VerifyMemberSkipsAndStructuralRejections(out string failure)
        {
            PhenomenonRelationshipGroup group = Group(LawPhenomenon.Heat, "skips", "A", "B", "C");
            SynchronizationEvaluationResult skips = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Heat), group,
                Carrier("A", LawPhenomenon.Heat, 0f), PhenomenonOperationKind.Add, 1f,
                Carrier("B", LawPhenomenon.Potential, 0f));
            SynchronizationEvaluationResult initiatorMissing = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Heat), group,
                Carrier("X", LawPhenomenon.Heat, 0f), PhenomenonOperationKind.Add, 1f,
                Carrier("B", LawPhenomenon.Heat, 0f), Carrier("C", LawPhenomenon.Heat, 0f));
            SynchronizationEvaluationResult lawMismatch = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Potential), group,
                Carrier("A", LawPhenomenon.Potential, 0f), PhenomenonOperationKind.Add, 1f,
                Carrier("B", LawPhenomenon.Potential, 0f), Carrier("C", LawPhenomenon.Potential, 0f));
            SynchronizationEvaluationResult dissolved = SynchronizationEvaluator.Evaluate(new SynchronizationEvaluationRequest(
                Law(LawExpression.Projectile, LawPhenomenon.Heat),
                new LawExpressionContext(true, LawExpression.Projectile, false), null, Id("A"),
                Snapshot(LawPhenomenon.Heat, 0f),
                new PhenomenonOperationRequest(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, Snapshot(LawPhenomenon.Heat, 0f)),
                new PhenomenonCarrierSnapshot[0]));

            return Expect(skips.Succeeded
                && skips.MemberResults[0].RejectionReason == SynchronizationMemberRejectionReason.PhenomenonMismatch
                && skips.MemberResults[1].RejectionReason == SynchronizationMemberRejectionReason.MissingSnapshot
                && skips.SynchronizedRecipientCount == 0
                && !initiatorMissing.Succeeded
                && initiatorMissing.RejectionReason == SynchronizationEvaluationRejectionReason.InitiatorNotMember
                && !lawMismatch.Succeeded
                && lawMismatch.RejectionReason == SynchronizationEvaluationRejectionReason.GroupLawPhenomenonMismatch
                && !dissolved.Succeeded
                && dissolved.RejectionReason == SynchronizationEvaluationRejectionReason.GroupUnavailable,
                "Synchronization did not distinguish member skips from structural group/request failures.", out failure);
        }

        private static bool VerifyLawEligibilityAndUnsupportedOperations(out string failure)
        {
            PhenomenonRelationshipGroup group = Group(LawPhenomenon.Heat, "law", "A", "B");
            PhenomenonCarrierSnapshot a = Carrier("A", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 0f);
            DomainLaw projectile = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            DomainLaw frenzy = Law(LawExpression.Frenzy, LawPhenomenon.Heat);
            SynchronizationEvaluationResult projectileEligible = Evaluate(projectile, group, a,
                PhenomenonOperationKind.Add, 1f, b);
            SynchronizationEvaluationResult mismatch = Evaluate(projectile, group, a,
                PhenomenonOperationKind.Add, 1f, new LawExpressionContext(true, LawExpression.Frenzy, true), b);
            SynchronizationEvaluationResult frenzyActive = Evaluate(frenzy, group, a,
                PhenomenonOperationKind.Add, 1f, new LawExpressionContext(true, LawExpression.Frenzy, true), b);
            SynchronizationEvaluationResult frenzyInactive = Evaluate(frenzy, group, a,
                PhenomenonOperationKind.Add, 1f, new LawExpressionContext(true, LawExpression.Frenzy, false), b);
            SynchronizationEvaluationResult transfer = Evaluate(projectile, group, a,
                PhenomenonOperationKind.Transfer, 1f, b);
            SynchronizationEvaluationResult zero = Evaluate(projectile, group, a,
                PhenomenonOperationKind.Add, 0f, b);
            SynchronizationEvaluationResult negative = Evaluate(projectile, group, a,
                PhenomenonOperationKind.Add, -1f, b);

            return Expect(projectileEligible.Succeeded && projectileEligible.Eligible
                && !mismatch.Succeeded
                && mismatch.RejectionReason == SynchronizationEvaluationRejectionReason.ExpressionMismatch
                && frenzyActive.Succeeded
                && !frenzyInactive.Succeeded
                && frenzyInactive.RejectionReason == SynchronizationEvaluationRejectionReason.FrenzyInactive
                && !transfer.Succeeded
                && transfer.RejectionReason == SynchronizationEvaluationRejectionReason.OperationNotSupportedByTerritory
                && zero.Succeeded
                && Approximately(zero.MemberResults[0].After.SemanticValue, 0f)
                && !negative.Succeeded
                && negative.RejectionReason == SynchronizationEvaluationRejectionReason.InvalidBaseRequest,
                "Synchronization did not preserve Law eligibility or base-operation support boundaries.", out failure);
        }

        private static bool VerifyPhenomenonSpecificBehavior(out string failure)
        {
            SynchronizationEvaluationResult resonance = Evaluate(
                Law(LawExpression.Projectile, LawPhenomenon.Resonance), Group(LawPhenomenon.Resonance, "res", "A", "B"),
                Carrier("A", LawPhenomenon.Resonance, 2f), PhenomenonOperationKind.Add, 1f,
                Carrier("B", LawPhenomenon.Resonance, 2f));
            SynchronizationEvaluationResult phase = Evaluate(
                Law(LawExpression.Projectile, LawPhenomenon.Phase), Group(LawPhenomenon.Phase, "phase", "A", "B"),
                Carrier("A", LawPhenomenon.Phase, 2f), PhenomenonOperationKind.Add, 1f,
                Carrier("B", LawPhenomenon.Phase, 2f));
            SynchronizationEvaluationResult mass = Evaluate(
                Law(LawExpression.Projectile, LawPhenomenon.Mass), Group(LawPhenomenon.Mass, "mass", "A", "B"),
                Carrier("A", LawPhenomenon.Mass, 1f), PhenomenonOperationKind.Add, .25f,
                Carrier("B", LawPhenomenon.Mass, 1f));

            return Expect(resonance.Succeeded
                && Approximately(resonance.MemberResults[0].After.SemanticValue, 3f)
                && phase.Succeeded
                && Approximately(phase.MemberResults[0].After.SemanticValue, 3f)
                && mass.Succeeded
                && Approximately(mass.MemberResults[0].After.SemanticValue, 1.25f),
                "Synchronization did not remain generic semantic Add/Remove for Resonance, Phase, and Mass.", out failure);
        }

        private static bool VerifyImmutabilityDeterminismAndNoRecursion(out string failure)
        {
            PhenomenonRelationshipGroup group = Group(LawPhenomenon.Heat, "immutable", "A", "B", "C");
            PhenomenonCarrierSnapshot a = Carrier("A", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 0f);
            SynchronizationEvaluationRequest request = Request(Law(LawExpression.Projectile, LawPhenomenon.Heat), group, a,
                PhenomenonOperationKind.Add, 1f, new LawExpressionContext(true, LawExpression.Projectile, false), b, c);
            SynchronizationEvaluationResult first = SynchronizationEvaluator.Evaluate(request);
            SynchronizationEvaluationResult second = SynchronizationEvaluator.Evaluate(request);

            return Expect(first.Succeeded
                && first.MemberResults.Count == 2
                && first.SynchronizedRecipientCount == 2
                && !first.CanTriggerAdditionalTerritoryEvaluation
                && first.MemberResults[0].IsDerivedSynchronizationProposal
                && !first.MemberResults[0].CanRebroadcastSynchronization
                && first.MemberResults[0].RegionChanged
                && Approximately(a.SemanticState.SemanticValue, 1f)
                && Approximately(b.SemanticState.SemanticValue, 1f)
                && Approximately(c.SemanticState.SemanticValue, 0f)
                && group.MemberCount == 3
                && first.MemberResults[0].MemberId.Equals(second.MemberResults[0].MemberId)
                && first.MemberResults[1].MemberId.Equals(second.MemberResults[1].MemberId)
                && Approximately(first.MemberResults[0].After.SemanticValue, second.MemberResults[0].After.SemanticValue)
                && first.MemberResults[0].BaseResult != second.MemberResults[0].BaseResult,
                "Synchronization mutated inputs, lacked deterministic order, rebroadcasted, or automatically chained Territory behavior.", out failure);
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
            string relationshipFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            bool operations = PhenomenonOperationsVerification.TryRunAll(out operationsFailure);
            bool lawEvaluation = DomainLawEvaluationVerification.TryRunAll(out lawEvaluationFailure);
            bool topology = PhenomenonTopologyVerification.TryRunAll(out topologyFailure);
            bool catalysis = CatalysisEvaluationVerification.TryRunAll(out catalysisFailure);
            bool accumulation = AccumulationEvaluationVerification.TryRunAll(out accumulationFailure);
            bool relationship = PhenomenonRelationshipGroupVerification.TryRunAll(out relationshipFailure);
            return Expect(laws && semantics && operations && lawEvaluation && topology && catalysis && accumulation && relationship,
                "Earlier Domain verification regressed: law=" + lawFailure
                + "; semantics=" + semanticsFailure
                + "; operations=" + operationsFailure
                + "; evaluation=" + lawEvaluationFailure
                + "; topology=" + topologyFailure
                + "; catalysis=" + catalysisFailure
                + "; accumulation=" + accumulationFailure
                + "; relationship=" + relationshipFailure,
                out failure);
        }

        private static SynchronizationEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonRelationshipGroup group,
            PhenomenonCarrierSnapshot initiator,
            PhenomenonOperationKind operation,
            float magnitude,
            params PhenomenonCarrierSnapshot[] members)
        {
            return Evaluate(law, group, initiator, operation, magnitude,
                new LawExpressionContext(true, law.Expression, law.Expression == LawExpression.Frenzy), members);
        }

        private static SynchronizationEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonRelationshipGroup group,
            PhenomenonCarrierSnapshot initiator,
            PhenomenonOperationKind operation,
            float magnitude,
            LawExpressionContext context,
            params PhenomenonCarrierSnapshot[] members)
        {
            return SynchronizationEvaluator.Evaluate(Request(law, group, initiator, operation, magnitude, context, members));
        }

        private static SynchronizationEvaluationRequest Request(
            DomainLaw law,
            PhenomenonRelationshipGroup group,
            PhenomenonCarrierSnapshot initiator,
            PhenomenonOperationKind operation,
            float magnitude,
            LawExpressionContext context,
            params PhenomenonCarrierSnapshot[] members)
        {
            return new SynchronizationEvaluationRequest(
                law, context, group, initiator.CarrierId, initiator.SemanticState,
                new PhenomenonOperationRequest(operation, law.Phenomenon, magnitude, initiator.SemanticState), members);
        }

        private static PhenomenonRelationshipGroup Group(
            LawPhenomenon phenomenon,
            string groupId,
            params string[] members)
        {
            DomainLaw law = Law(LawExpression.Projectile, phenomenon);
            PhenomenonCarrierId[] ids = new PhenomenonCarrierId[members.Length];
            for (int index = 0; index < members.Length; index++) ids[index] = Id(members[index]);
            PhenomenonRelationshipGroupResult result = SynchronizationRelationshipGroupEvaluator.Create(
                new SynchronizationRelationshipGroupCreationRequest(
                    law, new LawExpressionContext(true, LawExpression.Projectile, false),
                    new PhenomenonRelationshipGroupId(groupId), ids));
            if (!result.Succeeded) throw new InvalidOperationException("Verification could not create group: " + result.RejectionReason);
            return result.GroupAfter;
        }

        private static DomainLaw Law(LawExpression expression, LawPhenomenon phenomenon)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, LawTerritoryPrinciple.Synchronization, out law, out validation))
            {
                throw new InvalidOperationException("Verification could not create Law: " + validation.RejectionReason);
            }
            return law;
        }

        private static PhenomenonCarrierSnapshot Carrier(string id, LawPhenomenon phenomenon, float value)
        {
            return new PhenomenonCarrierSnapshot(Id(id), phenomenon, Snapshot(phenomenon, value));
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

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) <= Tolerance;
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }
    }
}
