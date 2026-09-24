using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Whole-transaction outcomes for pure same-phenomenon Catalysis.</summary>
    public enum CatalysisEvaluationRejectionReason
    {
        None = 0,
        InvalidLaw = 1,
        ExpressionUnavailable = 2,
        InvalidExpressionContext = 3,
        ExpressionMismatch = 4,
        FrenzyInactive = 5,
        TerritoryBehaviorNotImplemented = 6,
        SourceCarrierUnavailable = 7,
        SourcePhenomenonMismatch = 8,
        SourceStateUnavailable = 9,
        InvalidSourceSemanticState = 10,
        SourceSnapshotMismatch = 11
    }

    /// <summary>Inspectable outcome for one explicitly supplied Catalysis recipient.</summary>
    public enum CatalysisRecipientRejectionReason
    {
        None = 0,
        DuplicateRecipient = 1,
        SourceReintroduced = 2,
        InvalidCarrierIdentity = 3,
        PhenomenonMismatch = 4,
        SemanticStateUnavailable = 5,
        InvalidSemanticState = 6,
        AlreadyLow = 7,
        AlreadyHigh = 8,
        NotRegular = 9,
        BaseResolutionRejected = 10
    }

    /// <summary>
    /// Immutable one-source Catalysis request. Topology.Source represents the
    /// source before-state; SourceAfter is the already-proposed source outcome
    /// whose region crossing may trigger this separate relational transaction.
    /// </summary>
    public sealed class CatalysisEvaluationRequest
    {
        public CatalysisEvaluationRequest(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonCarrierTopology topology,
            PhenomenonSemanticSnapshot sourceBefore,
            PhenomenonSemanticSnapshot sourceAfter)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            Topology = topology;
            SourceBefore = sourceBefore;
            SourceAfter = sourceAfter;
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonCarrierTopology Topology { get; }
        public PhenomenonSemanticSnapshot SourceBefore { get; }
        public PhenomenonSemanticSnapshot SourceAfter { get; }
    }

    /// <summary>Immutable proposed Catalysis outcome for one recipient.</summary>
    public sealed class CatalysisRecipientResult
    {
        internal CatalysisRecipientResult(
            PhenomenonCarrierSnapshot carrier,
            bool wasEligible,
            CatalysisRecipientRejectionReason rejectionReason,
            bool hasGeneratedOperation,
            PhenomenonOperationKind generatedOperation,
            float generatedMagnitude,
            PhenomenonResolutionResult baseResult)
        {
            Carrier = carrier;
            WasEligible = wasEligible;
            RejectionReason = rejectionReason;
            HasGeneratedOperation = hasGeneratedOperation;
            GeneratedOperation = generatedOperation;
            GeneratedMagnitude = generatedMagnitude;
            BaseResult = baseResult;
        }

        public PhenomenonCarrierSnapshot Carrier { get; }
        public PhenomenonCarrierId CarrierId => Carrier != null ? Carrier.CarrierId : default(PhenomenonCarrierId);
        public PhenomenonSemanticSnapshot Before => Carrier != null ? Carrier.SemanticState : null;
        public bool WasEligible { get; }
        public CatalysisRecipientRejectionReason RejectionReason { get; }
        public bool HasGeneratedOperation { get; }
        public PhenomenonOperationKind GeneratedOperation { get; }
        public float GeneratedMagnitude { get; }
        public PhenomenonResolutionResult BaseResult { get; }
        public PhenomenonSemanticSnapshot After => BaseResult != null ? BaseResult.TargetAfter : null;
        public PhenomenonSemanticRegion ResultingRegion => After != null
            ? After.Region
            : Before != null ? Before.Region : default(PhenomenonSemanticRegion);
        public bool Succeeded => WasEligible
            && RejectionReason == CatalysisRecipientRejectionReason.None
            && BaseResult != null
            && BaseResult.Succeeded;
    }

    /// <summary>Immutable trace of one non-recursive Catalysis evaluation.</summary>
    public sealed class CatalysisEvaluationResult
    {
        internal CatalysisEvaluationResult(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonCarrierTopology topology,
            PhenomenonSemanticSnapshot sourceBefore,
            PhenomenonSemanticSnapshot sourceAfter,
            bool eligible,
            bool succeeded,
            CatalysisEvaluationRejectionReason rejectionReason,
            bool triggerDetected,
            PhenomenonSemanticRegion triggerDestinationRegion,
            IReadOnlyList<CatalysisRecipientResult> recipientResults)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            Topology = topology;
            SourceBefore = sourceBefore;
            SourceAfter = sourceAfter;
            Eligible = eligible;
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            TriggerDetected = triggerDetected;
            TriggerDestinationRegion = triggerDestinationRegion;
            RecipientResults = recipientResults ?? new List<CatalysisRecipientResult>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonCarrierTopology Topology { get; }
        public PhenomenonCarrierId SourceCarrierId => Topology != null && Topology.Source != null
            ? Topology.Source.CarrierId
            : default(PhenomenonCarrierId);
        public PhenomenonSemanticSnapshot SourceBefore { get; }
        public PhenomenonSemanticSnapshot SourceAfter { get; }
        public bool Eligible { get; }
        public bool Succeeded { get; }
        public CatalysisEvaluationRejectionReason RejectionReason { get; }
        public bool TriggerDetected { get; }
        public PhenomenonSemanticRegion TriggerDestinationRegion { get; }
        public int RequestedRecipientCount => Topology != null ? Topology.OrderedRecipients.Count : 0;
        public IReadOnlyList<CatalysisRecipientResult> RecipientResults { get; }
        public int EligibleRecipientCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < RecipientResults.Count; index++)
                {
                    if (RecipientResults[index].WasEligible) count++;
                }
                return count;
            }
        }
        public int ModifiedRecipientCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < RecipientResults.Count; index++)
                {
                    if (RecipientResults[index].Succeeded) count++;
                }
                return count;
            }
        }
    }

    /// <summary>
    /// Pure, transaction-local Catalysis. A source only triggers when its
    /// semantic state crosses Regular to High or Low; recipient proposals never
    /// become new trigger evaluations.
    /// </summary>
    public static class CatalysisEvaluator
    {
        public static CatalysisEvaluationResult Evaluate(CatalysisEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonCarrierTopology topology = request != null ? request.Topology : null;
            PhenomenonSemanticSnapshot sourceBefore = request != null ? request.SourceBefore : null;
            PhenomenonSemanticSnapshot sourceAfter = request != null ? request.SourceAfter : null;

            CatalysisEvaluationRejectionReason rejection;
            if (!TryValidateLawAndExpression(law, context, out rejection))
            {
                return Rejected(law, context, topology, sourceBefore, sourceAfter, false, rejection);
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Catalysis)
            {
                return Rejected(law, context, topology, sourceBefore, sourceAfter, true,
                    CatalysisEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            }

            if (!TryValidateSource(topology, law.Phenomenon, sourceBefore, sourceAfter, out rejection))
            {
                return Rejected(law, context, topology, sourceBefore, sourceAfter, true, rejection);
            }

            PhenomenonSemanticRegion destination;
            if (!TryGetTriggerDestination(sourceBefore, sourceAfter, out destination))
            {
                return new CatalysisEvaluationResult(
                    law, context, topology, sourceBefore, sourceAfter, true, true,
                    CatalysisEvaluationRejectionReason.None, false,
                    default(PhenomenonSemanticRegion), new List<CatalysisRecipientResult>().AsReadOnly());
            }

            PhenomenonSemanticProfile profile;
            if (!PhenomenonSemanticClassifier.TryGetProfile(law.Phenomenon, out profile))
            {
                return Rejected(law, context, topology, sourceBefore, sourceAfter, true,
                    CatalysisEvaluationRejectionReason.InvalidSourceSemanticState);
            }

            List<CatalysisRecipientResult> results = new List<CatalysisRecipientResult>();
            HashSet<PhenomenonCarrierId> encountered = new HashSet<PhenomenonCarrierId>();
            PhenomenonCarrierId sourceId = topology.Source.CarrierId;
            for (int index = 0; index < topology.OrderedRecipients.Count; index++)
            {
                PhenomenonCarrierSnapshot recipient = topology.OrderedRecipients[index];
                CatalysisRecipientRejectionReason recipientRejection;
                if (!TryValidateRecipientIdentity(recipient, sourceId, encountered, out recipientRejection))
                {
                    results.Add(Skipped(recipient, recipientRejection));
                    continue;
                }

                if (recipient.Phenomenon != law.Phenomenon
                    || recipient.SemanticState == null
                    || recipient.SemanticState.Phenomenon != law.Phenomenon)
                {
                    results.Add(Skipped(recipient, recipient.SemanticState == null
                        ? CatalysisRecipientRejectionReason.SemanticStateUnavailable
                        : CatalysisRecipientRejectionReason.PhenomenonMismatch));
                    continue;
                }

                if (!IsValidSemanticState(recipient.SemanticState, law.Phenomenon))
                {
                    results.Add(Skipped(recipient, CatalysisRecipientRejectionReason.InvalidSemanticState));
                    continue;
                }

                if (recipient.SemanticState.Region == PhenomenonSemanticRegion.Low)
                {
                    results.Add(Skipped(recipient, CatalysisRecipientRejectionReason.AlreadyLow));
                    continue;
                }

                if (recipient.SemanticState.Region == PhenomenonSemanticRegion.High)
                {
                    results.Add(Skipped(recipient, CatalysisRecipientRejectionReason.AlreadyHigh));
                    continue;
                }

                if (recipient.SemanticState.Region != PhenomenonSemanticRegion.Regular)
                {
                    results.Add(Skipped(recipient, CatalysisRecipientRejectionReason.NotRegular));
                    continue;
                }

                PhenomenonOperationKind operation = destination == PhenomenonSemanticRegion.High
                    ? PhenomenonOperationKind.Add
                    : PhenomenonOperationKind.Remove;
                float boundary = destination == PhenomenonSemanticRegion.High
                    ? profile.HighMinimum
                    : profile.LowMaximum;
                float magnitude = destination == PhenomenonSemanticRegion.High
                    ? boundary - recipient.SemanticState.SemanticValue
                    : recipient.SemanticState.SemanticValue - boundary;
                PhenomenonResolutionResult baseResult = PhenomenonOperationResolver.Resolve(
                    new PhenomenonOperationRequest(operation, law.Phenomenon, magnitude, recipient.SemanticState));
                if (!baseResult.Succeeded || baseResult.TargetAfter.Region != destination)
                {
                    results.Add(new CatalysisRecipientResult(recipient, true,
                        CatalysisRecipientRejectionReason.BaseResolutionRejected,
                        true, operation, magnitude, baseResult));
                    continue;
                }

                results.Add(new CatalysisRecipientResult(recipient, true,
                    CatalysisRecipientRejectionReason.None, true, operation, magnitude, baseResult));
            }

            return new CatalysisEvaluationResult(
                law, context, topology, sourceBefore, sourceAfter, true, true,
                CatalysisEvaluationRejectionReason.None, true, destination, results.AsReadOnly());
        }

        private static bool TryValidateLawAndExpression(
            DomainLaw law,
            LawExpressionContext context,
            out CatalysisEvaluationRejectionReason rejection)
        {
            LawEvaluationRejectionReason lawRejection;
            if (DomainLawEvaluator.TryEvaluateEligibility(law, context, out lawRejection))
            {
                rejection = CatalysisEvaluationRejectionReason.None;
                return true;
            }

            rejection = lawRejection == LawEvaluationRejectionReason.InvalidLaw
                ? CatalysisEvaluationRejectionReason.InvalidLaw
                : lawRejection == LawEvaluationRejectionReason.ExpressionUnavailable
                    ? CatalysisEvaluationRejectionReason.ExpressionUnavailable
                    : lawRejection == LawEvaluationRejectionReason.InvalidExpressionContext
                        ? CatalysisEvaluationRejectionReason.InvalidExpressionContext
                        : lawRejection == LawEvaluationRejectionReason.ExpressionMismatch
                            ? CatalysisEvaluationRejectionReason.ExpressionMismatch
                            : lawRejection == LawEvaluationRejectionReason.FrenzyInactive
                                ? CatalysisEvaluationRejectionReason.FrenzyInactive
                                : CatalysisEvaluationRejectionReason.InvalidLaw;
            return false;
        }

        private static bool TryValidateSource(
            PhenomenonCarrierTopology topology,
            LawPhenomenon phenomenon,
            PhenomenonSemanticSnapshot sourceBefore,
            PhenomenonSemanticSnapshot sourceAfter,
            out CatalysisEvaluationRejectionReason rejection)
        {
            if (topology == null || topology.Source == null || !topology.Source.CarrierId.IsValid)
            {
                rejection = CatalysisEvaluationRejectionReason.SourceCarrierUnavailable;
                return false;
            }

            if (topology.Source.Phenomenon != phenomenon)
            {
                rejection = CatalysisEvaluationRejectionReason.SourcePhenomenonMismatch;
                return false;
            }

            if (sourceBefore == null || sourceAfter == null || topology.Source.SemanticState == null)
            {
                rejection = CatalysisEvaluationRejectionReason.SourceStateUnavailable;
                return false;
            }

            if (sourceBefore.Phenomenon != phenomenon
                || sourceAfter.Phenomenon != phenomenon
                || topology.Source.SemanticState.Phenomenon != phenomenon)
            {
                rejection = CatalysisEvaluationRejectionReason.SourcePhenomenonMismatch;
                return false;
            }

            if (!IsValidSemanticState(sourceBefore, phenomenon)
                || !IsValidSemanticState(sourceAfter, phenomenon)
                || !IsValidSemanticState(topology.Source.SemanticState, phenomenon))
            {
                rejection = CatalysisEvaluationRejectionReason.InvalidSourceSemanticState;
                return false;
            }

            if (!SameSnapshot(topology.Source.SemanticState, sourceBefore))
            {
                rejection = CatalysisEvaluationRejectionReason.SourceSnapshotMismatch;
                return false;
            }

            rejection = CatalysisEvaluationRejectionReason.None;
            return true;
        }

        private static bool TryGetTriggerDestination(
            PhenomenonSemanticSnapshot sourceBefore,
            PhenomenonSemanticSnapshot sourceAfter,
            out PhenomenonSemanticRegion destination)
        {
            if (sourceBefore.Region == PhenomenonSemanticRegion.Regular
                && (sourceAfter.Region == PhenomenonSemanticRegion.High
                    || sourceAfter.Region == PhenomenonSemanticRegion.Low))
            {
                destination = sourceAfter.Region;
                return true;
            }

            destination = default(PhenomenonSemanticRegion);
            return false;
        }

        private static bool TryValidateRecipientIdentity(
            PhenomenonCarrierSnapshot recipient,
            PhenomenonCarrierId sourceId,
            HashSet<PhenomenonCarrierId> encountered,
            out CatalysisRecipientRejectionReason rejection)
        {
            if (recipient == null || !recipient.CarrierId.IsValid)
            {
                rejection = CatalysisRecipientRejectionReason.InvalidCarrierIdentity;
                return false;
            }

            if (recipient.CarrierId.Equals(sourceId))
            {
                rejection = CatalysisRecipientRejectionReason.SourceReintroduced;
                return false;
            }

            if (!encountered.Add(recipient.CarrierId))
            {
                rejection = CatalysisRecipientRejectionReason.DuplicateRecipient;
                return false;
            }

            rejection = CatalysisRecipientRejectionReason.None;
            return true;
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

        private static bool SameSnapshot(
            PhenomenonSemanticSnapshot left,
            PhenomenonSemanticSnapshot right)
        {
            return left != null
                && right != null
                && left.Phenomenon == right.Phenomenon
                && left.SemanticValue == right.SemanticValue
                && left.Region == right.Region;
        }

        private static CatalysisRecipientResult Skipped(
            PhenomenonCarrierSnapshot recipient,
            CatalysisRecipientRejectionReason rejection)
        {
            return new CatalysisRecipientResult(recipient, false, rejection, false,
                default(PhenomenonOperationKind), 0f, null);
        }

        private static CatalysisEvaluationResult Rejected(
            DomainLaw law,
            LawExpressionContext context,
            PhenomenonCarrierTopology topology,
            PhenomenonSemanticSnapshot sourceBefore,
            PhenomenonSemanticSnapshot sourceAfter,
            bool eligible,
            CatalysisEvaluationRejectionReason rejection)
        {
            return new CatalysisEvaluationResult(
                law, context, topology, sourceBefore, sourceAfter, eligible, false,
                rejection, false, default(PhenomenonSemanticRegion),
                new List<CatalysisRecipientResult>().AsReadOnly());
        }
    }
}
