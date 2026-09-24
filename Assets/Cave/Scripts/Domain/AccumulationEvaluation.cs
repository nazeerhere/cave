using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Pure transaction-local many-to-one carrier relationship supplied by upstream discovery.</summary>
    public sealed class PhenomenonConvergenceTopology
    {
        private readonly IReadOnlyList<PhenomenonCarrierSnapshot> orderedContributors;

        public PhenomenonConvergenceTopology(
            PhenomenonCarrierSnapshot focalRecipient,
            IEnumerable<PhenomenonCarrierSnapshot> contributors)
        {
            FocalRecipient = focalRecipient;
            List<PhenomenonCarrierSnapshot> copied = contributors != null
                ? new List<PhenomenonCarrierSnapshot>(contributors)
                : new List<PhenomenonCarrierSnapshot>();
            orderedContributors = copied.AsReadOnly();
        }

        public PhenomenonCarrierSnapshot FocalRecipient { get; }
        public IReadOnlyList<PhenomenonCarrierSnapshot> OrderedContributors => orderedContributors;
    }

    /// <summary>Whole-transaction outcomes for pure many-to-one Accumulation.</summary>
    public enum AccumulationEvaluationRejectionReason
    {
        None = 0,
        InvalidLaw = 1,
        ExpressionUnavailable = 2,
        InvalidExpressionContext = 3,
        ExpressionMismatch = 4,
        FrenzyInactive = 5,
        TerritoryBehaviorNotImplemented = 6,
        OperationNotSupportedByTerritory = 7,
        NegativeContributorLimit = 8,
        InvalidTransferMagnitude = 9,
        FocalRecipientUnavailable = 10,
        FocalRecipientPhenomenonMismatch = 11,
        InvalidFocalRecipientSemanticState = 12
    }

    /// <summary>Inspectable outcome for one explicitly supplied contributor.</summary>
    public enum AccumulationContributorRejectionReason
    {
        None = 0,
        DuplicateContributor = 1,
        FocalRecipientReintroduced = 2,
        ContributorLimitReached = 3,
        InvalidCarrierIdentity = 4,
        PhenomenonMismatch = 5,
        SemanticStateUnavailable = 6,
        InvalidSemanticState = 7,
        BaseResolutionRejected = 8
    }

    /// <summary>Immutable Accumulation request. Transfer is the only supported operation in this slice.</summary>
    public sealed class AccumulationEvaluationRequest
    {
        public AccumulationEvaluationRequest(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonConvergenceTopology topology,
            PhenomenonOperationKind operation,
            float transferMagnitude,
            int maximumContributors)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            Topology = topology;
            Operation = operation;
            TransferMagnitude = transferMagnitude;
            MaximumContributors = maximumContributors;
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonConvergenceTopology Topology { get; }
        public PhenomenonOperationKind Operation { get; }
        public float TransferMagnitude { get; }
        public int MaximumContributors { get; }
    }

    /// <summary>Immutable proposed outcome for one contributor-to-focal Transfer.</summary>
    public sealed class AccumulationContributorResult
    {
        internal AccumulationContributorResult(
            PhenomenonCarrierSnapshot contributor,
            AccumulationContributorRejectionReason rejectionReason,
            PhenomenonResolutionResult baseResult)
        {
            Contributor = contributor;
            RejectionReason = rejectionReason;
            BaseResult = baseResult;
        }

        public PhenomenonCarrierSnapshot Contributor { get; }
        public PhenomenonCarrierId ContributorId => Contributor != null
            ? Contributor.CarrierId
            : default(PhenomenonCarrierId);
        public PhenomenonSemanticSnapshot ContributorBefore => Contributor != null ? Contributor.SemanticState : null;
        public AccumulationContributorRejectionReason RejectionReason { get; }
        public PhenomenonResolutionResult BaseResult { get; }
        public bool Succeeded => RejectionReason == AccumulationContributorRejectionReason.None
            && BaseResult != null
            && BaseResult.Succeeded;
        public PhenomenonSemanticSnapshot ContributorAfter => BaseResult != null ? BaseResult.SourceAfter : null;
        public PhenomenonStateDelta SourceDelta => BaseResult != null ? BaseResult.SourceDelta : null;
        public PhenomenonSemanticSnapshot FocalBeforeThisContribution => BaseResult != null ? BaseResult.TargetBefore : null;
        public PhenomenonSemanticSnapshot FocalAfterThisContribution => BaseResult != null ? BaseResult.TargetAfter : null;
    }

    /// <summary>Immutable trace of one bounded, non-recursive Accumulation transaction.</summary>
    public sealed class AccumulationEvaluationResult
    {
        internal AccumulationEvaluationResult(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonConvergenceTopology topology,
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float transferMagnitude,
            int maximumContributors,
            bool eligible,
            bool succeeded,
            AccumulationEvaluationRejectionReason rejectionReason,
            PhenomenonSemanticSnapshot focalRecipientOriginal,
            PhenomenonSemanticSnapshot finalFocalRecipient,
            float totalSourceDelta,
            float totalFocalDelta,
            IReadOnlyList<AccumulationContributorResult> contributorResults)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            Topology = topology;
            Operation = operation;
            Phenomenon = phenomenon;
            TransferMagnitude = transferMagnitude;
            MaximumContributors = maximumContributors;
            Eligible = eligible;
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            FocalRecipientOriginal = focalRecipientOriginal;
            FinalFocalRecipient = finalFocalRecipient;
            TotalSourceDelta = totalSourceDelta;
            TotalFocalDelta = totalFocalDelta;
            ContributorResults = contributorResults ?? new List<AccumulationContributorResult>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonConvergenceTopology Topology { get; }
        public PhenomenonCarrierId FocalRecipientId => Topology != null && Topology.FocalRecipient != null
            ? Topology.FocalRecipient.CarrierId
            : default(PhenomenonCarrierId);
        public PhenomenonSemanticSnapshot FocalRecipientOriginal { get; }
        public PhenomenonSemanticSnapshot FinalFocalRecipient { get; }
        public PhenomenonOperationKind Operation { get; }
        public LawPhenomenon Phenomenon { get; }
        public float TransferMagnitude { get; }
        public int RequestedContributorCount => Topology != null ? Topology.OrderedContributors.Count : 0;
        public int MaximumContributors { get; }
        public bool Eligible { get; }
        public bool Succeeded { get; }
        public AccumulationEvaluationRejectionReason RejectionReason { get; }
        public IReadOnlyList<AccumulationContributorResult> ContributorResults { get; }
        public int AcceptedContributorCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < ContributorResults.Count; index++)
                {
                    if (ContributorResults[index].Succeeded) count++;
                }
                return count;
            }
        }
        public int SkippedContributorCount => ContributorResults.Count - AcceptedContributorCount;
        public float TotalSourceDelta { get; }
        public float TotalFocalDelta { get; }
        public bool IsConserved => Math.Abs(TotalSourceDelta + TotalFocalDelta) <= .0001f;
    }

    /// <summary>
    /// Pure bounded same-phenomenon Accumulation. Each accepted contributor
    /// transfers to the current proposed focal state; it never recurses.
    /// </summary>
    public static class AccumulationEvaluator
    {
        public static AccumulationEvaluationResult Evaluate(AccumulationEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonConvergenceTopology topology = request != null ? request.Topology : null;
            PhenomenonOperationKind operation = request != null ? request.Operation : default(PhenomenonOperationKind);
            float magnitude = request != null ? request.TransferMagnitude : 0f;
            int maximumContributors = request != null ? request.MaximumContributors : 0;
            LawPhenomenon phenomenon = law != null ? law.Phenomenon : default(LawPhenomenon);

            AccumulationEvaluationRejectionReason rejection;
            if (!TryValidateLawAndExpression(law, context, out rejection))
            {
                return Rejected(law, context, topology, operation, phenomenon, magnitude, maximumContributors, false, rejection);
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Accumulation)
            {
                return Rejected(law, context, topology, operation, phenomenon, magnitude, maximumContributors, true,
                    AccumulationEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            }

            if (operation != PhenomenonOperationKind.Transfer)
            {
                return Rejected(law, context, topology, operation, phenomenon, magnitude, maximumContributors, true,
                    AccumulationEvaluationRejectionReason.OperationNotSupportedByTerritory);
            }

            if (!IsValidMagnitude(magnitude))
            {
                return Rejected(law, context, topology, operation, phenomenon, magnitude, maximumContributors, true,
                    AccumulationEvaluationRejectionReason.InvalidTransferMagnitude);
            }

            if (maximumContributors < 0)
            {
                return Rejected(law, context, topology, operation, phenomenon, magnitude, maximumContributors, true,
                    AccumulationEvaluationRejectionReason.NegativeContributorLimit);
            }

            PhenomenonCarrierSnapshot focal;
            if (!TryValidateFocal(topology, phenomenon, out focal, out rejection))
            {
                return Rejected(law, context, topology, operation, phenomenon, magnitude, maximumContributors, true, rejection);
            }

            List<AccumulationContributorResult> results = new List<AccumulationContributorResult>();
            HashSet<PhenomenonCarrierId> encountered = new HashSet<PhenomenonCarrierId>();
            PhenomenonSemanticSnapshot originalFocal = focal.SemanticState;
            PhenomenonSemanticSnapshot currentFocal = originalFocal;
            float totalSourceDelta = 0f;
            int accepted = 0;
            for (int index = 0; index < topology.OrderedContributors.Count; index++)
            {
                PhenomenonCarrierSnapshot contributor = topology.OrderedContributors[index];
                AccumulationContributorRejectionReason contributorRejection;
                if (!TryValidateContributorIdentity(contributor, focal.CarrierId, encountered, out contributorRejection))
                {
                    results.Add(new AccumulationContributorResult(contributor, contributorRejection, null));
                    continue;
                }

                if (accepted >= maximumContributors)
                {
                    results.Add(new AccumulationContributorResult(contributor,
                        AccumulationContributorRejectionReason.ContributorLimitReached, null));
                    continue;
                }

                if (contributor.Phenomenon != phenomenon
                    || contributor.SemanticState == null
                    || contributor.SemanticState.Phenomenon != phenomenon)
                {
                    results.Add(new AccumulationContributorResult(contributor,
                        contributor.SemanticState == null
                            ? AccumulationContributorRejectionReason.SemanticStateUnavailable
                            : AccumulationContributorRejectionReason.PhenomenonMismatch,
                        null));
                    continue;
                }

                if (!IsValidSemanticState(contributor.SemanticState, phenomenon))
                {
                    results.Add(new AccumulationContributorResult(contributor,
                        AccumulationContributorRejectionReason.InvalidSemanticState, null));
                    continue;
                }

                PhenomenonResolutionResult baseResult = PhenomenonOperationResolver.ResolveTransfer(
                    new PhenomenonTransferRequest(phenomenon, magnitude, contributor.SemanticState, currentFocal));
                if (!baseResult.Succeeded)
                {
                    results.Add(new AccumulationContributorResult(contributor,
                        AccumulationContributorRejectionReason.BaseResolutionRejected, baseResult));
                    continue;
                }

                results.Add(new AccumulationContributorResult(contributor,
                    AccumulationContributorRejectionReason.None, baseResult));
                currentFocal = baseResult.TargetAfter;
                totalSourceDelta += baseResult.SourceDelta.Amount;
                accepted++;
            }

            float totalFocalDelta = currentFocal.SemanticValue - originalFocal.SemanticValue;
            return new AccumulationEvaluationResult(
                law, context, topology, operation, phenomenon, magnitude, maximumContributors,
                true, true, AccumulationEvaluationRejectionReason.None,
                originalFocal, currentFocal, totalSourceDelta, totalFocalDelta, results.AsReadOnly());
        }

        private static bool TryValidateLawAndExpression(
            DomainLaw law,
            LawExpressionContext context,
            out AccumulationEvaluationRejectionReason rejection)
        {
            LawEvaluationRejectionReason lawRejection;
            if (DomainLawEvaluator.TryEvaluateEligibility(law, context, out lawRejection))
            {
                rejection = AccumulationEvaluationRejectionReason.None;
                return true;
            }

            rejection = lawRejection == LawEvaluationRejectionReason.InvalidLaw
                ? AccumulationEvaluationRejectionReason.InvalidLaw
                : lawRejection == LawEvaluationRejectionReason.ExpressionUnavailable
                    ? AccumulationEvaluationRejectionReason.ExpressionUnavailable
                    : lawRejection == LawEvaluationRejectionReason.InvalidExpressionContext
                        ? AccumulationEvaluationRejectionReason.InvalidExpressionContext
                        : lawRejection == LawEvaluationRejectionReason.ExpressionMismatch
                            ? AccumulationEvaluationRejectionReason.ExpressionMismatch
                            : lawRejection == LawEvaluationRejectionReason.FrenzyInactive
                                ? AccumulationEvaluationRejectionReason.FrenzyInactive
                                : AccumulationEvaluationRejectionReason.InvalidLaw;
            return false;
        }

        private static bool TryValidateFocal(
            PhenomenonConvergenceTopology topology,
            LawPhenomenon phenomenon,
            out PhenomenonCarrierSnapshot focal,
            out AccumulationEvaluationRejectionReason rejection)
        {
            focal = topology != null ? topology.FocalRecipient : null;
            if (focal == null || !focal.CarrierId.IsValid)
            {
                rejection = AccumulationEvaluationRejectionReason.FocalRecipientUnavailable;
                return false;
            }

            if (focal.Phenomenon != phenomenon
                || focal.SemanticState == null
                || focal.SemanticState.Phenomenon != phenomenon)
            {
                rejection = AccumulationEvaluationRejectionReason.FocalRecipientPhenomenonMismatch;
                return false;
            }

            if (!IsValidSemanticState(focal.SemanticState, phenomenon))
            {
                rejection = AccumulationEvaluationRejectionReason.InvalidFocalRecipientSemanticState;
                return false;
            }

            rejection = AccumulationEvaluationRejectionReason.None;
            return true;
        }

        private static bool TryValidateContributorIdentity(
            PhenomenonCarrierSnapshot contributor,
            PhenomenonCarrierId focalId,
            HashSet<PhenomenonCarrierId> encountered,
            out AccumulationContributorRejectionReason rejection)
        {
            if (contributor == null || !contributor.CarrierId.IsValid)
            {
                rejection = AccumulationContributorRejectionReason.InvalidCarrierIdentity;
                return false;
            }

            if (contributor.CarrierId.Equals(focalId))
            {
                rejection = AccumulationContributorRejectionReason.FocalRecipientReintroduced;
                return false;
            }

            if (!encountered.Add(contributor.CarrierId))
            {
                rejection = AccumulationContributorRejectionReason.DuplicateContributor;
                return false;
            }

            rejection = AccumulationContributorRejectionReason.None;
            return true;
        }

        private static bool IsValidMagnitude(float magnitude)
        {
            return !float.IsNaN(magnitude) && !float.IsInfinity(magnitude) && magnitude >= 0f;
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

        private static AccumulationEvaluationResult Rejected(
            DomainLaw law,
            LawExpressionContext context,
            PhenomenonConvergenceTopology topology,
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            int maximumContributors,
            bool eligible,
            AccumulationEvaluationRejectionReason rejection)
        {
            PhenomenonSemanticSnapshot focal = topology != null && topology.FocalRecipient != null
                ? topology.FocalRecipient.SemanticState
                : null;
            return new AccumulationEvaluationResult(
                law, context, topology, operation, phenomenon, magnitude, maximumContributors,
                eligible, false, rejection, focal, focal, 0f, 0f,
                new List<AccumulationContributorResult>().AsReadOnly());
        }
    }
}
