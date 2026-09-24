using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Structural outcomes only; this layer never evaluates a Territory or produces an after-state.</summary>
    public enum DomainOrchestrationRequestRejectionReason
    {
        None = 0,
        CompositionUnavailable = 1,
        InvalidComposition = 2,
        InvalidExpressionContext = 3,
        InvalidOperation = 4,
        InvalidPhenomenon = 5,
        InvalidMagnitude = 6,
        FocalCarrierUnavailable = 7,
        SourceCarrierUnavailable = 8,
        InvalidCarrierId = 9,
        CarrierPhenomenonMismatch = 10,
        SemanticSnapshotUnavailable = 11,
        SemanticSnapshotMismatch = 12,
        InvalidSemanticSnapshot = 13
    }

    public struct DomainOrchestrationRequestValidation
    {
        private readonly DomainOrchestrationRequestRejectionReason rejectionReason;

        private DomainOrchestrationRequestValidation(DomainOrchestrationRequestRejectionReason reason)
        {
            rejectionReason = reason;
        }

        public bool IsValid => rejectionReason == DomainOrchestrationRequestRejectionReason.None;
        public DomainOrchestrationRequestRejectionReason RejectionReason => rejectionReason;
        public static DomainOrchestrationRequestValidation Valid() { return new DomainOrchestrationRequestValidation(DomainOrchestrationRequestRejectionReason.None); }
        public static DomainOrchestrationRequestValidation Rejected(DomainOrchestrationRequestRejectionReason reason) { return new DomainOrchestrationRequestValidation(reason); }
    }

    /// <summary>Immutable external Add/Remove intent. Transfer is deliberately represented by its own request type.</summary>
    public sealed class UnaryDomainOrchestrationRequest
    {
        public UnaryDomainOrchestrationRequest(DomainComposition composition, LawExpressionContext expressionContext,
            LawPhenomenon phenomenon, PhenomenonOperationKind operation, float magnitude,
            PhenomenonCarrierSnapshot focalCarrier, DomainOrchestrationContextSet contexts)
        {
            Composition = composition;
            ExpressionContext = expressionContext;
            Phenomenon = phenomenon;
            Operation = operation;
            Magnitude = magnitude;
            FocalCarrier = focalCarrier;
            Contexts = contexts;
        }

        public DomainComposition Composition { get; }
        public LawExpressionContext ExpressionContext { get; }
        public LawPhenomenon Phenomenon { get; }
        public PhenomenonOperationKind Operation { get; }
        public float Magnitude { get; }
        public PhenomenonCarrierSnapshot FocalCarrier { get; }
        public DomainOrchestrationContextSet Contexts { get; }
    }

    /// <summary>Immutable external Transfer intent with explicit source and focal target.</summary>
    public sealed class TransferDomainOrchestrationRequest
    {
        public TransferDomainOrchestrationRequest(DomainComposition composition, LawExpressionContext expressionContext,
            LawPhenomenon phenomenon, float magnitude, PhenomenonCarrierSnapshot sourceCarrier,
            PhenomenonCarrierSnapshot focalTargetCarrier, DomainOrchestrationContextSet contexts)
        {
            Composition = composition;
            ExpressionContext = expressionContext;
            Phenomenon = phenomenon;
            Magnitude = magnitude;
            SourceCarrier = sourceCarrier;
            FocalTargetCarrier = focalTargetCarrier;
            Contexts = contexts;
        }

        public DomainComposition Composition { get; }
        public LawExpressionContext ExpressionContext { get; }
        public LawPhenomenon Phenomenon { get; }
        public PhenomenonOperationKind Operation => PhenomenonOperationKind.Transfer;
        public float Magnitude { get; }
        public PhenomenonCarrierSnapshot SourceCarrier { get; }
        public PhenomenonCarrierSnapshot FocalTargetCarrier { get; }
        public DomainOrchestrationContextSet Contexts { get; }
    }

    /// <summary>Pure structural validation; it does not invoke the planner, an evaluator, or base arithmetic.</summary>
    public static class DomainOrchestrationRequestValidator
    {
        public static DomainOrchestrationRequestValidation Validate(UnaryDomainOrchestrationRequest request)
        {
            if (request == null) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.CompositionUnavailable);
            DomainOrchestrationRequestValidation common = ValidateCommon(request.Composition, request.ExpressionContext, request.Phenomenon, request.Magnitude);
            if (!common.IsValid) return common;
            if (request.Operation != PhenomenonOperationKind.Add && request.Operation != PhenomenonOperationKind.Remove)
                return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.InvalidOperation);
            return ValidateCarrier(request.FocalCarrier, request.Phenomenon, DomainOrchestrationRequestRejectionReason.FocalCarrierUnavailable);
        }

        public static DomainOrchestrationRequestValidation Validate(TransferDomainOrchestrationRequest request)
        {
            if (request == null) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.CompositionUnavailable);
            DomainOrchestrationRequestValidation common = ValidateCommon(request.Composition, request.ExpressionContext, request.Phenomenon, request.Magnitude);
            if (!common.IsValid) return common;
            DomainOrchestrationRequestValidation source = ValidateCarrier(request.SourceCarrier, request.Phenomenon, DomainOrchestrationRequestRejectionReason.SourceCarrierUnavailable);
            return source.IsValid ? ValidateCarrier(request.FocalTargetCarrier, request.Phenomenon, DomainOrchestrationRequestRejectionReason.FocalCarrierUnavailable) : source;
        }

        private static DomainOrchestrationRequestValidation ValidateCommon(DomainComposition composition, LawExpressionContext expressionContext, LawPhenomenon phenomenon, float magnitude)
        {
            if (composition == null) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.CompositionUnavailable);
            for (int index = 0; index < composition.OrderedLaws.Count; index++)
            {
                DomainLaw law = composition.OrderedLaws[index];
                if (law == null || !DomainLaw.Validate(law.Expression, law.Phenomenon, law.TerritoryPrinciple).IsValid)
                    return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.InvalidComposition);
            }
            if (expressionContext == null || !expressionContext.HasExpression || !IsDefinedExpression(expressionContext.Expression))
                return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.InvalidExpressionContext);
            PhenomenonSemanticProfile ignored;
            if (!PhenomenonSemanticClassifier.TryGetProfile(phenomenon, out ignored))
                return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.InvalidPhenomenon);
            if (!IsFinite(magnitude) || magnitude < 0f) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.InvalidMagnitude);
            return DomainOrchestrationRequestValidation.Valid();
        }

        private static DomainOrchestrationRequestValidation ValidateCarrier(PhenomenonCarrierSnapshot carrier, LawPhenomenon phenomenon, DomainOrchestrationRequestRejectionReason unavailable)
        {
            if (carrier == null) return DomainOrchestrationRequestValidation.Rejected(unavailable);
            if (!carrier.CarrierId.IsValid) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.InvalidCarrierId);
            if (carrier.Phenomenon != phenomenon) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.CarrierPhenomenonMismatch);
            if (carrier.SemanticState == null) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.SemanticSnapshotUnavailable);
            if (carrier.SemanticState.Phenomenon != phenomenon) return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.SemanticSnapshotMismatch);
            PhenomenonSemanticRegion region;
            PhenomenonSemanticResult result;
            if (!PhenomenonSemanticClassifier.TryClassify(phenomenon, carrier.SemanticState.SemanticValue, out region, out result)
                || region != carrier.SemanticState.Region)
                return DomainOrchestrationRequestValidation.Rejected(DomainOrchestrationRequestRejectionReason.InvalidSemanticSnapshot);
            return DomainOrchestrationRequestValidation.Valid();
        }

        private static bool IsDefinedExpression(LawExpression expression) { return expression == LawExpression.Projectile || expression == LawExpression.Frenzy; }
        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public sealed class PropagationOrchestrationContext
    {
        public PropagationOrchestrationContext(PhenomenonCarrierTopology topology, int maximumRecipients) { Topology = topology; MaximumRecipients = maximumRecipients; }
        public PhenomenonCarrierTopology Topology { get; }
        public int MaximumRecipients { get; }
    }

    public sealed class AccumulationOrchestrationContext
    {
        public AccumulationOrchestrationContext(PhenomenonConvergenceTopology topology, int maximumContributors) { Topology = topology; MaximumContributors = maximumContributors; }
        public PhenomenonConvergenceTopology Topology { get; }
        public int MaximumContributors { get; }
    }

    public sealed class SynchronizationOrchestrationContext
    {
        private readonly IReadOnlyList<PhenomenonCarrierSnapshot> currentMemberSnapshots;
        public SynchronizationOrchestrationContext(PhenomenonRelationshipGroup activeGroup, IEnumerable<PhenomenonCarrierSnapshot> snapshots)
        {
            ActiveGroup = activeGroup;
            currentMemberSnapshots = snapshots != null ? new List<PhenomenonCarrierSnapshot>(snapshots).AsReadOnly() : new List<PhenomenonCarrierSnapshot>().AsReadOnly();
        }
        public PhenomenonRelationshipGroup ActiveGroup { get; }
        public IReadOnlyList<PhenomenonCarrierSnapshot> CurrentMemberSnapshots => currentMemberSnapshots;
    }

    public sealed class InterferenceOrchestrationContext
    {
        private readonly IReadOnlyList<PhenomenonCarrierSnapshot> orderedAdditionalContributors;
        public InterferenceOrchestrationContext(PhenomenonCarrierSnapshot referenceCarrier, IEnumerable<PhenomenonCarrierSnapshot> contributors, PhenomenonCarrierSnapshot focalRecipient, int maximumContributors)
        {
            ReferenceCarrier = referenceCarrier;
            orderedAdditionalContributors = contributors != null ? new List<PhenomenonCarrierSnapshot>(contributors).AsReadOnly() : new List<PhenomenonCarrierSnapshot>().AsReadOnly();
            FocalRecipient = focalRecipient;
            MaximumContributors = maximumContributors;
        }
        public PhenomenonCarrierSnapshot ReferenceCarrier { get; }
        public IReadOnlyList<PhenomenonCarrierSnapshot> OrderedAdditionalContributors => orderedAdditionalContributors;
        public PhenomenonCarrierSnapshot FocalRecipient { get; }
        public int MaximumContributors { get; }
    }

    public sealed class CatalysisOrchestrationContext
    {
        public CatalysisOrchestrationContext(PhenomenonCarrierTopology topology) { Topology = topology; }
        public PhenomenonCarrierTopology Topology { get; }
    }

    public sealed class DomainLawContextBinding<TContext> where TContext : class
    {
        public DomainLawContextBinding(DomainLaw law, TContext context) { Law = law; Context = context; }
        public DomainLaw Law { get; }
        public TContext Context { get; }
    }

    public sealed class CatalysisContextBinding
    {
        public CatalysisContextBinding(DomainLaw law, PhenomenonCarrierId sourceCarrierId, CatalysisOrchestrationContext context)
        { Law = law; SourceCarrierId = sourceCarrierId; Context = context; }
        public DomainLaw Law { get; }
        public PhenomenonCarrierId SourceCarrierId { get; }
        public CatalysisOrchestrationContext Context { get; }
    }

    /// <summary>Typed, Law-keyed evaluator context lookup. Duplicate keys intentionally return no context rather than selecting arbitrarily.</summary>
    public sealed class DomainOrchestrationContextSet
    {
        private readonly IReadOnlyList<DomainLawContextBinding<PropagationOrchestrationContext>> propagation;
        private readonly IReadOnlyList<DomainLawContextBinding<AccumulationOrchestrationContext>> accumulation;
        private readonly IReadOnlyList<DomainLawContextBinding<SynchronizationOrchestrationContext>> synchronization;
        private readonly IReadOnlyList<DomainLawContextBinding<InterferenceOrchestrationContext>> interference;
        private readonly IReadOnlyList<CatalysisContextBinding> catalysis;

        public DomainOrchestrationContextSet(
            IEnumerable<DomainLawContextBinding<PropagationOrchestrationContext>> propagation = null,
            IEnumerable<DomainLawContextBinding<AccumulationOrchestrationContext>> accumulation = null,
            IEnumerable<DomainLawContextBinding<SynchronizationOrchestrationContext>> synchronization = null,
            IEnumerable<DomainLawContextBinding<InterferenceOrchestrationContext>> interference = null,
            IEnumerable<CatalysisContextBinding> catalysis = null)
        {
            this.propagation = Copy(propagation);
            this.accumulation = Copy(accumulation);
            this.synchronization = Copy(synchronization);
            this.interference = Copy(interference);
            this.catalysis = Copy(catalysis);
        }

        public static DomainOrchestrationContextSet Empty { get; } = new DomainOrchestrationContextSet();
        public bool TryGetPropagation(DomainLaw law, out PropagationOrchestrationContext context) { return TryGet(propagation, law, out context); }
        public bool TryGetAccumulation(DomainLaw law, out AccumulationOrchestrationContext context) { return TryGet(accumulation, law, out context); }
        public bool TryGetSynchronization(DomainLaw law, out SynchronizationOrchestrationContext context) { return TryGet(synchronization, law, out context); }
        public bool TryGetInterference(DomainLaw law, out InterferenceOrchestrationContext context) { return TryGet(interference, law, out context); }
        public bool TryGetCatalysisForSource(DomainLaw law, PhenomenonCarrierId sourceCarrierId, out CatalysisOrchestrationContext context)
        {
            context = null;
            int matches = 0;
            for (int index = 0; index < catalysis.Count; index++)
            {
                CatalysisContextBinding candidate = catalysis[index];
                if (candidate != null && Equals(candidate.Law, law) && candidate.SourceCarrierId.Equals(sourceCarrierId) && candidate.Context != null)
                { context = candidate.Context; matches++; }
            }
            if (matches == 1) return true;
            context = null;
            return false;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) { return values != null ? new List<T>(values).AsReadOnly() : new List<T>().AsReadOnly(); }
        private static bool TryGet<T>(IReadOnlyList<DomainLawContextBinding<T>> bindings, DomainLaw law, out T context) where T : class
        {
            context = null;
            int matches = 0;
            for (int index = 0; index < bindings.Count; index++)
            {
                DomainLawContextBinding<T> candidate = bindings[index];
                if (candidate != null && Equals(candidate.Law, law) && candidate.Context != null) { context = candidate.Context; matches++; }
            }
            if (matches == 1) return true;
            context = null;
            return false;
        }
    }

    /// <summary>Future Generation-0 unary intent after Reversal/Interference transforms, without a resolved state.</summary>
    public sealed class EffectiveUnaryDomainIntent
    {
        public EffectiveUnaryDomainIntent(LawPhenomenon phenomenon, PhenomenonOperationKind originalOperation, float originalMagnitude,
            PhenomenonOperationKind effectiveOperation, float effectiveMagnitude, bool reversalApplied, bool interferenceApplied, int interferenceContributionCount)
        {
            Phenomenon = phenomenon; OriginalOperation = originalOperation; OriginalMagnitude = originalMagnitude;
            EffectiveOperation = effectiveOperation; EffectiveMagnitude = effectiveMagnitude;
            ReversalApplied = reversalApplied; InterferenceApplied = interferenceApplied; InterferenceContributionCount = interferenceContributionCount;
        }
        public LawPhenomenon Phenomenon { get; }
        public PhenomenonOperationKind OriginalOperation { get; }
        public float OriginalMagnitude { get; }
        public PhenomenonOperationKind EffectiveOperation { get; }
        public float EffectiveMagnitude { get; }
        public bool ReversalApplied { get; }
        public bool InterferenceApplied { get; }
        public int InterferenceContributionCount { get; }
        public bool IsStructurallyValid => IsUnary(OriginalOperation) && IsUnary(EffectiveOperation) && IsFinite(OriginalMagnitude) && IsFinite(EffectiveMagnitude) && OriginalMagnitude >= 0f && EffectiveMagnitude >= 0f && InterferenceContributionCount >= 0;
        private static bool IsUnary(PhenomenonOperationKind operation) { return operation == PhenomenonOperationKind.Add || operation == PhenomenonOperationKind.Remove; }
        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    /// <summary>Bounded causal stages: Generation2 is terminal; no arbitrary recursion depth exists.</summary>
    public enum DomainOrchestrationGeneration { Generation0 = 0, Generation1 = 1, Generation2 = 2 }
    public enum DomainTransitionOrigin { Base = 0, Propagation = 1, Accumulation = 2, Synchronization = 3, Catalysis = 4 }

    /// <summary>Deterministic transaction-local sequence identity allocated by a future executor.</summary>
    public struct DomainTransitionSequenceId : IEquatable<DomainTransitionSequenceId>, IComparable<DomainTransitionSequenceId>
    {
        public DomainTransitionSequenceId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => Value >= 0;
        public bool Equals(DomainTransitionSequenceId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is DomainTransitionSequenceId && Equals((DomainTransitionSequenceId)obj); }
        public override int GetHashCode() { return Value; }
        public int CompareTo(DomainTransitionSequenceId other) { return Value.CompareTo(other.Value); }
    }

    /// <summary>Immutable semantic proposal for future resolver trace; it does not mutate either snapshot.</summary>
    public sealed class NormalizedSemanticTransition
    {
        public NormalizedSemanticTransition(DomainTransitionSequenceId sequenceId, DomainOrchestrationGeneration generation,
            DomainTransitionSequenceId? parentTransitionId, PhenomenonCarrierId carrierId, LawPhenomenon phenomenon,
            PhenomenonSemanticSnapshot before, PhenomenonSemanticSnapshot after, PhenomenonOperationKind operation,
            float magnitude, DomainTransitionOrigin origin, DomainLaw originatingLaw, bool isUnambiguous)
        {
            SequenceId = sequenceId; Generation = generation; ParentTransitionId = parentTransitionId; CarrierId = carrierId;
            Phenomenon = phenomenon; Before = before; After = after; Operation = operation; Magnitude = magnitude;
            Origin = origin; OriginatingLaw = originatingLaw; IsUnambiguous = isUnambiguous;
        }
        public DomainTransitionSequenceId SequenceId { get; }
        public DomainOrchestrationGeneration Generation { get; }
        public DomainTransitionSequenceId? ParentTransitionId { get; }
        public PhenomenonCarrierId CarrierId { get; }
        public LawPhenomenon Phenomenon { get; }
        public PhenomenonSemanticSnapshot Before { get; }
        public PhenomenonSemanticSnapshot After { get; }
        public PhenomenonOperationKind Operation { get; }
        public float Magnitude { get; }
        public DomainTransitionOrigin Origin { get; }
        public DomainLaw OriginatingLaw { get; }
        public bool IsUnambiguous { get; }
        public NormalizedSemanticTransition WithUnambiguous(bool isUnambiguous)
        {
            return new NormalizedSemanticTransition(SequenceId, Generation, ParentTransitionId, CarrierId, Phenomenon,
                Before, After, Operation, Magnitude, Origin, OriginatingLaw, isUnambiguous);
        }
    }

    /// <summary>Unresolved same-generation proposal collision, distinct from the locked cross-Domain authority pipeline.</summary>
    public sealed class ProposalCollision
    {
        private readonly IReadOnlyList<DomainTransitionSequenceId> involvedTransitionIds;
        private readonly IReadOnlyList<DomainLaw> originatingLaws;
        public ProposalCollision(PhenomenonCarrierId carrierId, LawPhenomenon phenomenon, DomainOrchestrationGeneration generation,
            IEnumerable<DomainTransitionSequenceId> transitionIds, IEnumerable<DomainLaw> laws)
        {
            CarrierId = carrierId; Phenomenon = phenomenon; Generation = generation;
            involvedTransitionIds = transitionIds != null ? new List<DomainTransitionSequenceId>(transitionIds).AsReadOnly() : new List<DomainTransitionSequenceId>().AsReadOnly();
            originatingLaws = laws != null ? new List<DomainLaw>(laws).AsReadOnly() : new List<DomainLaw>().AsReadOnly();
        }
        public PhenomenonCarrierId CarrierId { get; }
        public LawPhenomenon Phenomenon { get; }
        public DomainOrchestrationGeneration Generation { get; }
        public IReadOnlyList<DomainTransitionSequenceId> InvolvedTransitionIds => involvedTransitionIds;
        public IReadOnlyList<DomainLaw> OriginatingLaws => originatingLaws;
        public bool IsUnresolved => true;
    }

    public enum DomainLawOutcomeReason { None = 0, ContextUnavailable = 1, ContextMismatch = 2, OperationUnsupported = 3, NoDefinedInverse = 4, EvaluatorRejected = 5, ProposalCollisionBlocked = 6, OriginalSourceReintroduced = 7 }
    /// <summary>Small immutable trace vocabulary for future Law-local participation; no evaluator result is duplicated here.</summary>
    public sealed class DomainLawOutcomeTrace
    {
        public DomainLawOutcomeTrace(DomainLaw law, DomainResolutionCausalPhase phase, DomainOrchestrationGeneration generation, bool attempted, bool applied, DomainLawOutcomeReason reason,
            int? acceptedContributorCount = null, LawEvaluationRejectionReason? lawEligibilityRejection = null,
            InterferenceEvaluationRejectionReason? interferenceRejection = null, int? resultingTransitionCount = null)
        { Law = law; Phase = phase; Generation = generation; Attempted = attempted; Applied = applied; Reason = reason; AcceptedContributorCount = acceptedContributorCount; LawEligibilityRejection = lawEligibilityRejection; InterferenceRejection = interferenceRejection; ResultingTransitionCount = resultingTransitionCount; }
        public DomainLaw Law { get; }
        public DomainResolutionCausalPhase Phase { get; }
        public DomainOrchestrationGeneration Generation { get; }
        public bool Attempted { get; }
        public bool Applied { get; }
        public bool Skipped => !Applied;
        public DomainLawOutcomeReason Reason { get; }
        public int? AcceptedContributorCount { get; }
        public LawEvaluationRejectionReason? LawEligibilityRejection { get; }
        public InterferenceEvaluationRejectionReason? InterferenceRejection { get; }
        public int? ResultingTransitionCount { get; }
    }
}
