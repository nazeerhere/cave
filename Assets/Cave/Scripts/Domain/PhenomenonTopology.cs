using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Stable pure identity supplied by an upstream world-to-Domain bridge.</summary>
    public struct PhenomenonCarrierId : IEquatable<PhenomenonCarrierId>, IComparable<PhenomenonCarrierId>
    {
        private readonly string value;

        public PhenomenonCarrierId(string value)
        {
            this.value = value;
        }

        public string Value => value;
        public bool IsValid => !string.IsNullOrWhiteSpace(value);

        public bool Equals(PhenomenonCarrierId other)
        {
            return string.Equals(value, other.value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PhenomenonCarrierId && Equals((PhenomenonCarrierId)obj);
        }

        public override int GetHashCode()
        {
            return value != null ? StringComparer.Ordinal.GetHashCode(value) : 0;
        }

        public int CompareTo(PhenomenonCarrierId other)
        {
            return StringComparer.Ordinal.Compare(value, other.value);
        }

        public override string ToString()
        {
            return value ?? string.Empty;
        }
    }

    /// <summary>Immutable transaction-local phenomenon carrier without scene references.</summary>
    public sealed class PhenomenonCarrierSnapshot
    {
        public PhenomenonCarrierSnapshot(
            PhenomenonCarrierId carrierId,
            LawPhenomenon phenomenon,
            PhenomenonSemanticSnapshot semanticState)
        {
            CarrierId = carrierId;
            Phenomenon = phenomenon;
            SemanticState = semanticState;
        }

        public PhenomenonCarrierId CarrierId { get; }
        public LawPhenomenon Phenomenon { get; }
        public PhenomenonSemanticSnapshot SemanticState { get; }
    }

    /// <summary>Explicit source plus ordered recipients supplied by upstream participant discovery.</summary>
    public sealed class PhenomenonCarrierTopology
    {
        private readonly IReadOnlyList<PhenomenonCarrierSnapshot> orderedRecipients;

        public PhenomenonCarrierTopology(
            PhenomenonCarrierSnapshot source,
            IEnumerable<PhenomenonCarrierSnapshot> recipients)
        {
            Source = source;
            List<PhenomenonCarrierSnapshot> copied = recipients != null
                ? new List<PhenomenonCarrierSnapshot>(recipients)
                : new List<PhenomenonCarrierSnapshot>();
            orderedRecipients = copied.AsReadOnly();
        }

        public PhenomenonCarrierSnapshot Source { get; }
        public IReadOnlyList<PhenomenonCarrierSnapshot> OrderedRecipients => orderedRecipients;
    }

    /// <summary>Whole-transaction outcomes for bounded pure Propagation.</summary>
    public enum PropagationEvaluationRejectionReason
    {
        None = 0,
        InvalidLaw = 1,
        ExpressionUnavailable = 2,
        InvalidExpressionContext = 3,
        ExpressionMismatch = 4,
        FrenzyInactive = 5,
        PhenomenonMismatch = 6,
        TerritoryBehaviorNotImplemented = 7,
        InvalidBaseRequest = 8,
        NegativeRecipientLimit = 9,
        SourceCarrierUnavailable = 10,
        SourcePhenomenonMismatch = 11,
        InvalidSourceSemanticState = 12,
        OperationNotSupportedByTerritory = 13
    }

    /// <summary>Inspectable outcome for one supplied downstream carrier.</summary>
    public enum PropagationRecipientRejectionReason
    {
        None = 0,
        DuplicateRecipient = 1,
        SourceReintroduced = 2,
        RecipientLimitReached = 3,
        InvalidCarrierIdentity = 4,
        PhenomenonMismatch = 5,
        SemanticStateUnavailable = 6,
        InvalidSemanticState = 7,
        BaseResolutionRejected = 8
    }

    /// <summary>Immutable Add/Remove propagation request for one candidate Law and supplied topology.</summary>
    public sealed class PropagationEvaluationRequest
    {
        public PropagationEvaluationRequest(
            DomainLaw candidateLaw,
            PhenomenonOperationRequest originalRequest,
            LawExpressionContext expressionContext,
            PhenomenonCarrierTopology topology,
            int maximumRecipients)
        {
            CandidateLaw = candidateLaw;
            OriginalRequest = originalRequest;
            ExpressionContext = expressionContext;
            Topology = topology;
            MaximumRecipients = maximumRecipients;
        }

        public DomainLaw CandidateLaw { get; }
        public PhenomenonOperationRequest OriginalRequest { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonCarrierTopology Topology { get; }
        public int MaximumRecipients { get; }
    }

    /// <summary>Immutable Transfer request path so unsupported Propagation is explicit.</summary>
    public sealed class PropagationTransferEvaluationRequest
    {
        public PropagationTransferEvaluationRequest(
            DomainLaw candidateLaw,
            PhenomenonTransferRequest originalRequest,
            LawExpressionContext expressionContext,
            PhenomenonCarrierTopology topology,
            int maximumRecipients)
        {
            CandidateLaw = candidateLaw;
            OriginalRequest = originalRequest;
            ExpressionContext = expressionContext;
            Topology = topology;
            MaximumRecipients = maximumRecipients;
        }

        public DomainLaw CandidateLaw { get; }
        public PhenomenonTransferRequest OriginalRequest { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonCarrierTopology Topology { get; }
        public int MaximumRecipients { get; }
    }

    /// <summary>Immutable proposed result for one recipient; it never updates the carrier snapshot.</summary>
    public sealed class PropagationRecipientResult
    {
        internal PropagationRecipientResult(
            PhenomenonCarrierSnapshot carrier,
            PropagationRecipientRejectionReason rejectionReason,
            PhenomenonResolutionResult baseResult)
        {
            Carrier = carrier;
            RejectionReason = rejectionReason;
            BaseResult = baseResult;
        }

        public PhenomenonCarrierSnapshot Carrier { get; }
        public PhenomenonCarrierId CarrierId => Carrier != null ? Carrier.CarrierId : default(PhenomenonCarrierId);
        public bool Succeeded => RejectionReason == PropagationRecipientRejectionReason.None
            && BaseResult != null
            && BaseResult.Succeeded;
        public PropagationRecipientRejectionReason RejectionReason { get; }
        public PhenomenonSemanticSnapshot Before => Carrier != null ? Carrier.SemanticState : null;
        public PhenomenonResolutionResult BaseResult { get; }
        public PhenomenonSemanticSnapshot After => BaseResult != null ? BaseResult.TargetAfter : null;
        public bool RegionChanged => Before != null && After != null && Before.Region != After.Region;
    }

    /// <summary>Immutable future-trace-friendly result for one bounded Propagation transaction.</summary>
    public sealed class PropagationEvaluationResult
    {
        internal PropagationEvaluationResult(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonOperationKind originalOperation,
            LawPhenomenon phenomenon,
            float magnitude,
            PhenomenonCarrierTopology topology,
            int maximumRecipients,
            bool eligible,
            bool succeeded,
            PropagationEvaluationRejectionReason rejectionReason,
            IReadOnlyList<PropagationRecipientResult> recipientResults)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            OriginalOperation = originalOperation;
            Phenomenon = phenomenon;
            Magnitude = magnitude;
            Topology = topology;
            MaximumRecipients = maximumRecipients;
            Eligible = eligible;
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            RecipientResults = recipientResults ?? new List<PropagationRecipientResult>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonOperationKind OriginalOperation { get; }
        public LawPhenomenon Phenomenon { get; }
        public float Magnitude { get; }
        public PhenomenonCarrierTopology Topology { get; }
        public PhenomenonCarrierId SourceCarrierId => Topology != null && Topology.Source != null
            ? Topology.Source.CarrierId
            : default(PhenomenonCarrierId);
        public int RequestedRecipientCount => Topology != null ? Topology.OrderedRecipients.Count : 0;
        public int MaximumRecipients { get; }
        public bool Eligible { get; }
        public bool Succeeded { get; }
        public PropagationEvaluationRejectionReason RejectionReason { get; }
        public IReadOnlyList<PropagationRecipientResult> RecipientResults { get; }
        public int ResolvedRecipientCount
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
        public int SkippedRecipientCount => RecipientResults.Count - ResolvedRecipientCount;
    }

    /// <summary>
    /// Pure bounded same-phenomenon Propagation. It repeats the original Add or
    /// Remove independently for supplied recipients and never recurses.
    /// </summary>
    public static class PropagationEvaluator
    {
        public static PropagationEvaluationResult Evaluate(PropagationEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonOperationRequest original = request != null ? request.OriginalRequest : null;
            PhenomenonCarrierTopology topology = request != null ? request.Topology : null;
            int maximumRecipients = request != null ? request.MaximumRecipients : 0;
            PhenomenonOperationKind operation = original != null ? original.Operation : default(PhenomenonOperationKind);
            LawPhenomenon phenomenon = original != null ? original.Phenomenon : default(LawPhenomenon);
            float magnitude = original != null ? original.Magnitude : 0f;

            PropagationEvaluationRejectionReason rejection;
            if (!TryValidateLawAndExpression(law, context, out rejection))
            {
                return Rejected(law, context, operation, phenomenon, magnitude, topology, maximumRecipients, false, rejection);
            }

            if (original == null || !PhenomenonOperationResolver.Resolve(original).Succeeded)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, topology, maximumRecipients, false,
                    PropagationEvaluationRejectionReason.InvalidBaseRequest);
            }

            if (law.Phenomenon != phenomenon)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, topology, maximumRecipients, false,
                    PropagationEvaluationRejectionReason.PhenomenonMismatch);
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Propagation)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, topology, maximumRecipients, true,
                    PropagationEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            }

            if (operation != PhenomenonOperationKind.Add && operation != PhenomenonOperationKind.Remove)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, topology, maximumRecipients, true,
                    PropagationEvaluationRejectionReason.OperationNotSupportedByTerritory);
            }

            if (maximumRecipients < 0)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, topology, maximumRecipients, true,
                    PropagationEvaluationRejectionReason.NegativeRecipientLimit);
            }

            if (!TryValidateSource(topology, phenomenon, out rejection))
            {
                return Rejected(law, context, operation, phenomenon, magnitude, topology, maximumRecipients, true, rejection);
            }

            List<PropagationRecipientResult> results = new List<PropagationRecipientResult>();
            HashSet<PhenomenonCarrierId> encountered = new HashSet<PhenomenonCarrierId>();
            PhenomenonCarrierId sourceId = topology.Source.CarrierId;
            int resolved = 0;
            for (int index = 0; index < topology.OrderedRecipients.Count; index++)
            {
                PhenomenonCarrierSnapshot recipient = topology.OrderedRecipients[index];
                PropagationRecipientRejectionReason recipientRejection;
                if (!TryValidateRecipientIdentity(recipient, sourceId, encountered, out recipientRejection))
                {
                    results.Add(new PropagationRecipientResult(recipient, recipientRejection, null));
                    continue;
                }

                if (resolved >= maximumRecipients)
                {
                    results.Add(new PropagationRecipientResult(recipient,
                        PropagationRecipientRejectionReason.RecipientLimitReached, null));
                    continue;
                }

                if (recipient.Phenomenon != phenomenon
                    || recipient.SemanticState == null
                    || recipient.SemanticState.Phenomenon != phenomenon)
                {
                    results.Add(new PropagationRecipientResult(recipient,
                        recipient.SemanticState == null
                            ? PropagationRecipientRejectionReason.SemanticStateUnavailable
                            : PropagationRecipientRejectionReason.PhenomenonMismatch,
                        null));
                    continue;
                }

                PhenomenonResolutionResult baseResult = PhenomenonOperationResolver.Resolve(
                    new PhenomenonOperationRequest(operation, phenomenon, magnitude, recipient.SemanticState));
                if (!baseResult.Succeeded)
                {
                    results.Add(new PropagationRecipientResult(recipient,
                        PropagationRecipientRejectionReason.BaseResolutionRejected, baseResult));
                    continue;
                }

                results.Add(new PropagationRecipientResult(recipient,
                    PropagationRecipientRejectionReason.None, baseResult));
                resolved++;
            }

            return new PropagationEvaluationResult(
                law, context, operation, phenomenon, magnitude, topology, maximumRecipients,
                true, true, PropagationEvaluationRejectionReason.None, results.AsReadOnly());
        }

        public static PropagationEvaluationResult EvaluateTransfer(PropagationTransferEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonTransferRequest original = request != null ? request.OriginalRequest : null;
            PhenomenonCarrierTopology topology = request != null ? request.Topology : null;
            int maximumRecipients = request != null ? request.MaximumRecipients : 0;
            LawPhenomenon phenomenon = original != null ? original.Phenomenon : default(LawPhenomenon);
            float magnitude = original != null ? original.Magnitude : 0f;
            PropagationEvaluationRejectionReason rejection;
            if (!TryValidateLawAndExpression(law, context, out rejection))
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, topology, maximumRecipients,
                    false, rejection);
            }

            if (original == null || !PhenomenonOperationResolver.ResolveTransfer(original).Succeeded)
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, topology, maximumRecipients,
                    false, PropagationEvaluationRejectionReason.InvalidBaseRequest);
            }

            if (law.Phenomenon != phenomenon)
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, topology, maximumRecipients,
                    false, PropagationEvaluationRejectionReason.PhenomenonMismatch);
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Propagation)
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, topology, maximumRecipients,
                    true, PropagationEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            }

            return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, topology, maximumRecipients,
                true, PropagationEvaluationRejectionReason.OperationNotSupportedByTerritory);
        }

        private static bool TryValidateLawAndExpression(
            DomainLaw law,
            LawExpressionContext context,
            out PropagationEvaluationRejectionReason rejection)
        {
            LawEvaluationRejectionReason lawRejection;
            if (DomainLawEvaluator.TryEvaluateEligibility(law, context, out lawRejection))
            {
                rejection = PropagationEvaluationRejectionReason.None;
                return true;
            }

            rejection = lawRejection == LawEvaluationRejectionReason.InvalidLaw
                ? PropagationEvaluationRejectionReason.InvalidLaw
                : lawRejection == LawEvaluationRejectionReason.ExpressionUnavailable
                    ? PropagationEvaluationRejectionReason.ExpressionUnavailable
                    : lawRejection == LawEvaluationRejectionReason.InvalidExpressionContext
                        ? PropagationEvaluationRejectionReason.InvalidExpressionContext
                        : lawRejection == LawEvaluationRejectionReason.ExpressionMismatch
                            ? PropagationEvaluationRejectionReason.ExpressionMismatch
                            : lawRejection == LawEvaluationRejectionReason.FrenzyInactive
                                ? PropagationEvaluationRejectionReason.FrenzyInactive
                                : PropagationEvaluationRejectionReason.InvalidLaw;
            return false;
        }

        private static bool TryValidateSource(
            PhenomenonCarrierTopology topology,
            LawPhenomenon phenomenon,
            out PropagationEvaluationRejectionReason rejection)
        {
            if (topology == null || topology.Source == null || !topology.Source.CarrierId.IsValid)
            {
                rejection = PropagationEvaluationRejectionReason.SourceCarrierUnavailable;
                return false;
            }

            if (topology.Source.Phenomenon != phenomenon
                || topology.Source.SemanticState == null
                || topology.Source.SemanticState.Phenomenon != phenomenon)
            {
                rejection = PropagationEvaluationRejectionReason.SourcePhenomenonMismatch;
                return false;
            }

            PhenomenonSemanticRegion derived;
            PhenomenonSemanticResult semanticResult;
            if (!PhenomenonSemanticClassifier.TryClassify(
                phenomenon,
                topology.Source.SemanticState.SemanticValue,
                out derived,
                out semanticResult)
                || !semanticResult.IsValid
                || derived != topology.Source.SemanticState.Region)
            {
                rejection = PropagationEvaluationRejectionReason.InvalidSourceSemanticState;
                return false;
            }

            rejection = PropagationEvaluationRejectionReason.None;
            return true;
        }

        private static bool TryValidateRecipientIdentity(
            PhenomenonCarrierSnapshot recipient,
            PhenomenonCarrierId sourceId,
            HashSet<PhenomenonCarrierId> encountered,
            out PropagationRecipientRejectionReason rejection)
        {
            if (recipient == null || !recipient.CarrierId.IsValid)
            {
                rejection = PropagationRecipientRejectionReason.InvalidCarrierIdentity;
                return false;
            }

            if (recipient.CarrierId.Equals(sourceId))
            {
                rejection = PropagationRecipientRejectionReason.SourceReintroduced;
                return false;
            }

            if (!encountered.Add(recipient.CarrierId))
            {
                rejection = PropagationRecipientRejectionReason.DuplicateRecipient;
                return false;
            }

            rejection = PropagationRecipientRejectionReason.None;
            return true;
        }

        private static PropagationEvaluationResult Rejected(
            DomainLaw law,
            LawExpressionContext context,
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            PhenomenonCarrierTopology topology,
            int maximumRecipients,
            bool eligible,
            PropagationEvaluationRejectionReason rejection)
        {
            return new PropagationEvaluationResult(
                law, context, operation, phenomenon, magnitude, topology, maximumRecipients,
                eligible, false, rejection, new List<PropagationRecipientResult>().AsReadOnly());
        }
    }
}
