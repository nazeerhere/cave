using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Whole-request outcomes for persistent Synchronization delta coupling.</summary>
    public enum SynchronizationEvaluationRejectionReason
    {
        None = 0,
        InvalidLaw = 1,
        ExpressionUnavailable = 2,
        InvalidExpressionContext = 3,
        ExpressionMismatch = 4,
        FrenzyInactive = 5,
        TerritoryBehaviorNotImplemented = 6,
        GroupUnavailable = 7,
        GroupLawPhenomenonMismatch = 8,
        InitiatorNotMember = 9,
        InitiatorPhenomenonMismatch = 10,
        InitiatingSnapshotUnavailable = 11,
        InvalidInitiatingSemanticState = 12,
        InitiatingSnapshotMismatch = 13,
        InvalidBaseRequest = 14,
        OperationNotSupportedByTerritory = 15
    }

    /// <summary>Inspectable outcome for one persistent group member other than the initiator.</summary>
    public enum SynchronizationMemberRejectionReason
    {
        None = 0,
        MissingSnapshot = 1,
        PhenomenonMismatch = 2,
        InvalidSemanticState = 3,
        InitiatorReintroduced = 4,
        BaseResolutionRejected = 5
    }

    /// <summary>
    /// Immutable request for one external Add/Remove operation received by a
    /// persistent Synchronization group member. Member snapshots remain outside
    /// the persistent group because they are current transaction input.
    /// </summary>
    public sealed class SynchronizationEvaluationRequest
    {
        public SynchronizationEvaluationRequest(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonRelationshipGroup activeGroup,
            PhenomenonCarrierId initiatingCarrierId,
            PhenomenonSemanticSnapshot initiatingCarrierSnapshot,
            PhenomenonOperationRequest originalOperation,
            IEnumerable<PhenomenonCarrierSnapshot> currentMemberSnapshots)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            ActiveGroup = activeGroup;
            InitiatingCarrierId = initiatingCarrierId;
            InitiatingCarrierSnapshot = initiatingCarrierSnapshot;
            OriginalOperation = originalOperation;
            CurrentMemberSnapshots = currentMemberSnapshots != null
                ? new List<PhenomenonCarrierSnapshot>(currentMemberSnapshots).AsReadOnly()
                : new List<PhenomenonCarrierSnapshot>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonRelationshipGroup ActiveGroup { get; }
        public PhenomenonCarrierId InitiatingCarrierId { get; }
        public PhenomenonSemanticSnapshot InitiatingCarrierSnapshot { get; }
        public PhenomenonOperationRequest OriginalOperation { get; }
        public IReadOnlyList<PhenomenonCarrierSnapshot> CurrentMemberSnapshots { get; }
    }

    /// <summary>Immutable derived operation proposal for one synchronized member.</summary>
    public sealed class SynchronizationMemberResult
    {
        internal SynchronizationMemberResult(
            PhenomenonCarrierId memberId,
            PhenomenonCarrierSnapshot suppliedSnapshot,
            SynchronizationMemberRejectionReason rejectionReason,
            PhenomenonOperationKind mirroredOperation,
            float mirroredMagnitude,
            PhenomenonResolutionResult baseResult)
        {
            MemberId = memberId;
            SuppliedSnapshot = suppliedSnapshot;
            RejectionReason = rejectionReason;
            MirroredOperation = mirroredOperation;
            MirroredMagnitude = mirroredMagnitude;
            BaseResult = baseResult;
        }

        public PhenomenonCarrierId MemberId { get; }
        public PhenomenonCarrierSnapshot SuppliedSnapshot { get; }
        public PhenomenonSemanticSnapshot Before => SuppliedSnapshot != null ? SuppliedSnapshot.SemanticState : null;
        public SynchronizationMemberRejectionReason RejectionReason { get; }
        public PhenomenonOperationKind MirroredOperation { get; }
        public float MirroredMagnitude { get; }
        public PhenomenonResolutionResult BaseResult { get; }
        public PhenomenonSemanticSnapshot After => BaseResult != null ? BaseResult.TargetAfter : null;
        public bool RegionChanged => Before != null && After != null && Before.Region != After.Region;
        public bool Succeeded => RejectionReason == SynchronizationMemberRejectionReason.None
            && BaseResult != null
            && BaseResult.Succeeded;
        /// <summary>Future orchestration must not treat this derived proposal as a new broadcast source.</summary>
        public bool IsDerivedSynchronizationProposal => true;
        public bool CanRebroadcastSynchronization => false;
    }

    /// <summary>Immutable trace of one bounded, non-recursive Synchronization evaluation.</summary>
    public sealed class SynchronizationEvaluationResult
    {
        internal SynchronizationEvaluationResult(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonRelationshipGroup activeGroup,
            PhenomenonCarrierId initiatingCarrierId,
            PhenomenonSemanticSnapshot initiatingCarrierSnapshot,
            PhenomenonOperationKind originalOperation,
            LawPhenomenon phenomenon,
            float magnitude,
            int requestedSnapshotCount,
            bool eligible,
            bool succeeded,
            SynchronizationEvaluationRejectionReason rejectionReason,
            IReadOnlyList<SynchronizationMemberResult> memberResults)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            ActiveGroup = activeGroup;
            InitiatingCarrierId = initiatingCarrierId;
            InitiatingCarrierSnapshot = initiatingCarrierSnapshot;
            OriginalOperation = originalOperation;
            Phenomenon = phenomenon;
            Magnitude = magnitude;
            RequestedSnapshotCount = requestedSnapshotCount;
            Eligible = eligible;
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            MemberResults = memberResults ?? new List<SynchronizationMemberResult>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonRelationshipGroup ActiveGroup { get; }
        public PhenomenonRelationshipGroupId GroupId => ActiveGroup != null
            ? ActiveGroup.GroupId
            : default(PhenomenonRelationshipGroupId);
        public PhenomenonCarrierId InitiatingCarrierId { get; }
        public PhenomenonSemanticSnapshot InitiatingCarrierSnapshot { get; }
        public PhenomenonOperationKind OriginalOperation { get; }
        public LawPhenomenon Phenomenon { get; }
        public float Magnitude { get; }
        public int GroupMemberCount => ActiveGroup != null ? ActiveGroup.MemberCount : 0;
        public int RequestedSnapshotCount { get; }
        public bool Eligible { get; }
        public bool Succeeded { get; }
        public SynchronizationEvaluationRejectionReason RejectionReason { get; }
        public IReadOnlyList<SynchronizationMemberResult> MemberResults { get; }
        public int SynchronizedRecipientCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < MemberResults.Count; index++)
                {
                    if (MemberResults[index].Succeeded) count++;
                }
                return count;
            }
        }
        public int SkippedRecipientCount => MemberResults.Count - SynchronizedRecipientCount;
        /// <summary>The evaluator is a one-hop boundary and invokes no further Territory behavior.</summary>
        public bool CanTriggerAdditionalTerritoryEvaluation => false;
    }

    /// <summary>
    /// Pure persistent-membership delta coupling. It mirrors one external
    /// Add/Remove operation to current snapshots of other group members in
    /// group order, and never rebroadcasts derived proposals.
    /// </summary>
    public static class SynchronizationEvaluator
    {
        public static SynchronizationEvaluationResult Evaluate(SynchronizationEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonRelationshipGroup group = request != null ? request.ActiveGroup : null;
            PhenomenonCarrierId initiatorId = request != null
                ? request.InitiatingCarrierId
                : default(PhenomenonCarrierId);
            PhenomenonSemanticSnapshot initiatorSnapshot = request != null ? request.InitiatingCarrierSnapshot : null;
            PhenomenonOperationRequest original = request != null ? request.OriginalOperation : null;
            IReadOnlyList<PhenomenonCarrierSnapshot> suppliedSnapshots = request != null
                ? request.CurrentMemberSnapshots
                : null;
            PhenomenonOperationKind operation = original != null ? original.Operation : default(PhenomenonOperationKind);
            LawPhenomenon phenomenon = original != null ? original.Phenomenon : law != null
                ? law.Phenomenon : default(LawPhenomenon);
            float magnitude = original != null ? original.Magnitude : 0f;
            int requestedSnapshotCount = suppliedSnapshots != null ? suppliedSnapshots.Count : 0;

            SynchronizationEvaluationRejectionReason rejection;
            if (!TryValidateLawAndExpression(law, context, out rejection))
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, false, rejection);
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Synchronization)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            }

            if (group == null)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.GroupUnavailable);
            }

            if (group.Phenomenon != law.Phenomenon)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.GroupLawPhenomenonMismatch);
            }

            if (!initiatorId.IsValid || !group.Contains(initiatorId))
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.InitiatorNotMember);
            }

            if (initiatorSnapshot == null)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.InitiatingSnapshotUnavailable);
            }

            if (initiatorSnapshot.Phenomenon != law.Phenomenon)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.InitiatorPhenomenonMismatch);
            }

            if (!IsValidSemanticState(initiatorSnapshot, law.Phenomenon))
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.InvalidInitiatingSemanticState);
            }

            if (original == null)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.InvalidBaseRequest);
            }

            if (original.Phenomenon != law.Phenomenon)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.InitiatorPhenomenonMismatch);
            }

            if (operation == PhenomenonOperationKind.Transfer)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.OperationNotSupportedByTerritory);
            }

            if (operation != PhenomenonOperationKind.Add && operation != PhenomenonOperationKind.Remove
                || !SameSnapshot(original.Target, initiatorSnapshot)
                || !PhenomenonOperationResolver.Resolve(original).Succeeded)
            {
                return Rejected(law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                    requestedSnapshotCount, true, SynchronizationEvaluationRejectionReason.InvalidBaseRequest);
            }

            Dictionary<PhenomenonCarrierId, PhenomenonCarrierSnapshot> snapshots = IndexMemberSnapshots(suppliedSnapshots);
            List<SynchronizationMemberResult> results = new List<SynchronizationMemberResult>();
            for (int index = 0; index < group.OrderedMembers.Count; index++)
            {
                PhenomenonCarrierId memberId = group.OrderedMembers[index];
                if (memberId.Equals(initiatorId))
                {
                    continue;
                }

                PhenomenonCarrierSnapshot snapshot;
                if (!snapshots.TryGetValue(memberId, out snapshot))
                {
                    results.Add(new SynchronizationMemberResult(memberId, null,
                        SynchronizationMemberRejectionReason.MissingSnapshot, operation, magnitude, null));
                    continue;
                }

                if (snapshot.Phenomenon != law.Phenomenon
                    || snapshot.SemanticState == null
                    || snapshot.SemanticState.Phenomenon != law.Phenomenon)
                {
                    results.Add(new SynchronizationMemberResult(memberId, snapshot,
                        SynchronizationMemberRejectionReason.PhenomenonMismatch, operation, magnitude, null));
                    continue;
                }

                if (!IsValidSemanticState(snapshot.SemanticState, law.Phenomenon))
                {
                    results.Add(new SynchronizationMemberResult(memberId, snapshot,
                        SynchronizationMemberRejectionReason.InvalidSemanticState, operation, magnitude, null));
                    continue;
                }

                PhenomenonResolutionResult baseResult = PhenomenonOperationResolver.Resolve(
                    new PhenomenonOperationRequest(operation, law.Phenomenon, magnitude, snapshot.SemanticState));
                if (!baseResult.Succeeded)
                {
                    results.Add(new SynchronizationMemberResult(memberId, snapshot,
                        SynchronizationMemberRejectionReason.BaseResolutionRejected, operation, magnitude, baseResult));
                    continue;
                }

                results.Add(new SynchronizationMemberResult(memberId, snapshot,
                    SynchronizationMemberRejectionReason.None, operation, magnitude, baseResult));
            }

            return new SynchronizationEvaluationResult(
                law, context, group, initiatorId, initiatorSnapshot, operation, law.Phenomenon, magnitude,
                requestedSnapshotCount, true, true, SynchronizationEvaluationRejectionReason.None, results.AsReadOnly());
        }

        private static bool TryValidateLawAndExpression(
            DomainLaw law,
            LawExpressionContext context,
            out SynchronizationEvaluationRejectionReason rejection)
        {
            LawEvaluationRejectionReason lawRejection;
            if (DomainLawEvaluator.TryEvaluateEligibility(law, context, out lawRejection))
            {
                rejection = SynchronizationEvaluationRejectionReason.None;
                return true;
            }

            rejection = lawRejection == LawEvaluationRejectionReason.InvalidLaw
                ? SynchronizationEvaluationRejectionReason.InvalidLaw
                : lawRejection == LawEvaluationRejectionReason.ExpressionUnavailable
                    ? SynchronizationEvaluationRejectionReason.ExpressionUnavailable
                    : lawRejection == LawEvaluationRejectionReason.InvalidExpressionContext
                        ? SynchronizationEvaluationRejectionReason.InvalidExpressionContext
                        : lawRejection == LawEvaluationRejectionReason.ExpressionMismatch
                            ? SynchronizationEvaluationRejectionReason.ExpressionMismatch
                            : lawRejection == LawEvaluationRejectionReason.FrenzyInactive
                                ? SynchronizationEvaluationRejectionReason.FrenzyInactive
                                : SynchronizationEvaluationRejectionReason.InvalidLaw;
            return false;
        }

        private static Dictionary<PhenomenonCarrierId, PhenomenonCarrierSnapshot> IndexMemberSnapshots(
            IReadOnlyList<PhenomenonCarrierSnapshot> suppliedSnapshots)
        {
            Dictionary<PhenomenonCarrierId, PhenomenonCarrierSnapshot> indexed =
                new Dictionary<PhenomenonCarrierId, PhenomenonCarrierSnapshot>();
            if (suppliedSnapshots == null) return indexed;

            for (int index = 0; index < suppliedSnapshots.Count; index++)
            {
                PhenomenonCarrierSnapshot snapshot = suppliedSnapshots[index];
                if (snapshot != null && snapshot.CarrierId.IsValid && !indexed.ContainsKey(snapshot.CarrierId))
                {
                    indexed.Add(snapshot.CarrierId, snapshot);
                }
            }
            return indexed;
        }

        private static bool IsValidSemanticState(PhenomenonSemanticSnapshot snapshot, LawPhenomenon phenomenon)
        {
            if (snapshot == null || snapshot.Phenomenon != phenomenon) return false;

            PhenomenonSemanticRegion derived;
            PhenomenonSemanticResult result;
            return PhenomenonSemanticClassifier.TryClassify(
                phenomenon, snapshot.SemanticValue, out derived, out result)
                && result.IsValid
                && derived == snapshot.Region;
        }

        private static bool SameSnapshot(PhenomenonSemanticSnapshot left, PhenomenonSemanticSnapshot right)
        {
            return left != null
                && right != null
                && left.Phenomenon == right.Phenomenon
                && left.SemanticValue == right.SemanticValue
                && left.Region == right.Region;
        }

        private static SynchronizationEvaluationResult Rejected(
            DomainLaw law,
            LawExpressionContext context,
            PhenomenonRelationshipGroup group,
            PhenomenonCarrierId initiatorId,
            PhenomenonSemanticSnapshot initiatorSnapshot,
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            int requestedSnapshotCount,
            bool eligible,
            SynchronizationEvaluationRejectionReason rejection)
        {
            return new SynchronizationEvaluationResult(
                law, context, group, initiatorId, initiatorSnapshot, operation, phenomenon, magnitude,
                requestedSnapshotCount, eligible, false, rejection,
                new List<SynchronizationMemberResult>().AsReadOnly());
        }
    }
}
