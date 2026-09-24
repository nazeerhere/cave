using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Whole-request outcomes for pure constructive Interference.</summary>
    public enum InterferenceEvaluationRejectionReason
    {
        None = 0,
        InvalidLaw = 1,
        ExpressionUnavailable = 2,
        InvalidExpressionContext = 3,
        ExpressionMismatch = 4,
        FrenzyInactive = 5,
        TerritoryBehaviorNotImplemented = 6,
        InvalidBaseRequest = 7,
        OperationNotSupportedByTerritory = 8,
        InvalidContributorLimit = 9,
        ReferenceCarrierUnavailable = 10,
        ReferencePhenomenonMismatch = 11,
        InvalidReferenceSemanticState = 12,
        FocalRecipientUnavailable = 13,
        FocalRecipientPhenomenonMismatch = 14,
        InvalidFocalRecipientSemanticState = 15,
        FocalSnapshotMismatch = 16,
        InvalidEffectiveMagnitude = 17,
        BaseResolutionRejected = 18
    }

    /// <summary>Inspectable contribution outcome in one constructive Interference transaction.</summary>
    public enum InterferenceContributorRejectionReason
    {
        None = 0,
        ReferenceContribution = 1,
        InvalidCarrierIdentity = 2,
        ReferenceReintroduced = 3,
        FocalRecipientReintroduced = 4,
        DuplicateContributor = 5,
        ContributorLimitReached = 6,
        PhenomenonMismatch = 7,
        SemanticStateUnavailable = 8,
        InvalidSemanticState = 9,
        RegionMismatch = 10
    }

    /// <summary>Immutable request for one transaction-local constructive Interference resolution.</summary>
    public sealed class InterferenceEvaluationRequest
    {
        public InterferenceEvaluationRequest(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonCarrierSnapshot referenceCarrier,
            IEnumerable<PhenomenonCarrierSnapshot> orderedAdditionalContributors,
            PhenomenonCarrierSnapshot focalRecipient,
            PhenomenonOperationRequest originalOperation,
            int maximumContributors)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            ReferenceCarrier = referenceCarrier;
            OrderedAdditionalContributors = orderedAdditionalContributors != null
                ? new List<PhenomenonCarrierSnapshot>(orderedAdditionalContributors).AsReadOnly()
                : new List<PhenomenonCarrierSnapshot>().AsReadOnly();
            FocalRecipient = focalRecipient;
            OriginalOperation = originalOperation;
            MaximumContributors = maximumContributors;
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonCarrierSnapshot ReferenceCarrier { get; }
        public IReadOnlyList<PhenomenonCarrierSnapshot> OrderedAdditionalContributors { get; }
        public PhenomenonCarrierSnapshot FocalRecipient { get; }
        public PhenomenonOperationRequest OriginalOperation { get; }
        public int MaximumContributors { get; }
    }

    /// <summary>Internal transform-only result shared by isolated evaluation and Generation-0 orchestration.</summary>
    internal sealed class InterferenceTransformResult
    {
        internal InterferenceTransformResult(bool succeeded, InterferenceEvaluationRejectionReason rejectionReason,
            float effectiveMagnitude, IReadOnlyList<InterferenceContributorResult> contributorResults)
        { Succeeded = succeeded; RejectionReason = rejectionReason; EffectiveMagnitude = effectiveMagnitude; ContributorResults = contributorResults ?? new List<InterferenceContributorResult>().AsReadOnly(); }
        public bool Succeeded { get; }
        public InterferenceEvaluationRejectionReason RejectionReason { get; }
        public float EffectiveMagnitude { get; }
        public IReadOnlyList<InterferenceContributorResult> ContributorResults { get; }
        public int AcceptedContributorCount { get { int count = 0; for (int index = 0; index < ContributorResults.Count; index++) if (ContributorResults[index].Accepted) count++; return count; } }
    }

    /// <summary>Immutable accepted or skipped contribution record. Contributors are never mutated.</summary>
    public sealed class InterferenceContributorResult
    {
        internal InterferenceContributorResult(
            PhenomenonCarrierSnapshot carrier,
            bool isReference,
            bool accepted,
            InterferenceContributorRejectionReason rejectionReason)
        {
            Carrier = carrier;
            IsReference = isReference;
            Accepted = accepted;
            RejectionReason = rejectionReason;
        }

        public PhenomenonCarrierSnapshot Carrier { get; }
        public PhenomenonCarrierId CarrierId => Carrier != null ? Carrier.CarrierId : default(PhenomenonCarrierId);
        public PhenomenonSemanticSnapshot SemanticState => Carrier != null ? Carrier.SemanticState : null;
        public PhenomenonSemanticRegion Region => SemanticState != null ? SemanticState.Region : default(PhenomenonSemanticRegion);
        public bool IsReference { get; }
        public bool Accepted { get; }
        public InterferenceContributorRejectionReason RejectionReason { get; }
    }

    /// <summary>Immutable trace of one non-recursive constructive Interference resolution.</summary>
    public sealed class InterferenceEvaluationResult
    {
        internal InterferenceEvaluationResult(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonCarrierSnapshot referenceCarrier,
            PhenomenonCarrierSnapshot focalRecipient,
            PhenomenonOperationKind originalOperation,
            LawPhenomenon phenomenon,
            float baseMagnitude,
            int requestedContributorCount,
            int maximumContributors,
            bool eligible,
            bool succeeded,
            InterferenceEvaluationRejectionReason rejectionReason,
            float effectiveMagnitude,
            PhenomenonResolutionResult baseResult,
            IReadOnlyList<InterferenceContributorResult> contributorResults)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            ReferenceCarrier = referenceCarrier;
            FocalRecipient = focalRecipient;
            OriginalOperation = originalOperation;
            Phenomenon = phenomenon;
            BaseMagnitude = baseMagnitude;
            RequestedContributorCount = requestedContributorCount;
            MaximumContributors = maximumContributors;
            Eligible = eligible;
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            EffectiveMagnitude = effectiveMagnitude;
            BaseResult = baseResult;
            ContributorResults = contributorResults ?? new List<InterferenceContributorResult>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonCarrierSnapshot ReferenceCarrier { get; }
        public PhenomenonCarrierId ReferenceCarrierId => ReferenceCarrier != null
            ? ReferenceCarrier.CarrierId
            : default(PhenomenonCarrierId);
        public PhenomenonSemanticRegion ReferenceRegion => ReferenceCarrier != null && ReferenceCarrier.SemanticState != null
            ? ReferenceCarrier.SemanticState.Region
            : default(PhenomenonSemanticRegion);
        public PhenomenonCarrierSnapshot FocalRecipient { get; }
        public PhenomenonCarrierId FocalRecipientId => FocalRecipient != null
            ? FocalRecipient.CarrierId
            : default(PhenomenonCarrierId);
        public PhenomenonSemanticSnapshot FocalBefore => FocalRecipient != null ? FocalRecipient.SemanticState : null;
        public PhenomenonOperationKind OriginalOperation { get; }
        public LawPhenomenon Phenomenon { get; }
        public float BaseMagnitude { get; }
        public int RequestedContributorCount { get; }
        public int MaximumContributors { get; }
        public bool Eligible { get; }
        public bool Succeeded { get; }
        public InterferenceEvaluationRejectionReason RejectionReason { get; }
        public IReadOnlyList<InterferenceContributorResult> ContributorResults { get; }
        public int AcceptedContributorCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < ContributorResults.Count; index++)
                {
                    if (ContributorResults[index].Accepted) count++;
                }
                return count;
            }
        }
        public int SkippedContributorCount => ContributorResults.Count - AcceptedContributorCount;
        public float EffectiveMagnitude { get; }
        public PhenomenonResolutionResult BaseResult { get; }
        public PhenomenonSemanticSnapshot FocalAfter => BaseResult != null ? BaseResult.TargetAfter : null;
        public bool AmplificationOccurred => AcceptedContributorCount > 1;
        /// <summary>Interference consumes no contributor state and makes no conservation claim.</summary>
        public bool HasConservationRequirement => false;
        /// <summary>This one-hop result never invokes another Territory evaluator.</summary>
        public bool CanTriggerAdditionalTerritoryEvaluation => false;
    }

    /// <summary>
    /// Pure transaction-local constructive Interference. Coherent participants
    /// linearly amplify one Add/Remove proposal resolved once against the focal
    /// target; contributor snapshots remain unchanged.
    /// </summary>
    public static class InterferenceEvaluator
    {
        public static InterferenceEvaluationResult Evaluate(InterferenceEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonCarrierSnapshot reference = request != null ? request.ReferenceCarrier : null;
            PhenomenonCarrierSnapshot focal = request != null ? request.FocalRecipient : null;
            PhenomenonOperationRequest original = request != null ? request.OriginalOperation : null;
            IReadOnlyList<PhenomenonCarrierSnapshot> contributors = request != null
                ? request.OrderedAdditionalContributors
                : null;
            int maximumContributors = request != null ? request.MaximumContributors : 0;
            PhenomenonOperationKind operation = original != null ? original.Operation : default(PhenomenonOperationKind);
            LawPhenomenon phenomenon = original != null ? original.Phenomenon : law != null
                ? law.Phenomenon : default(LawPhenomenon);
            float baseMagnitude = original != null ? original.Magnitude : 0f;
            int requestedContributorCount = 1 + (contributors != null ? contributors.Count : 0);
            InterferenceTransformResult transform = TryGetTransform(request);
            if (!transform.Succeeded)
                return Rejected(law, context, reference, focal, operation, phenomenon, baseMagnitude,
                    requestedContributorCount, maximumContributors, transform.RejectionReason != InterferenceEvaluationRejectionReason.InvalidLaw
                    && transform.RejectionReason != InterferenceEvaluationRejectionReason.ExpressionUnavailable
                    && transform.RejectionReason != InterferenceEvaluationRejectionReason.InvalidExpressionContext
                    && transform.RejectionReason != InterferenceEvaluationRejectionReason.ExpressionMismatch
                    && transform.RejectionReason != InterferenceEvaluationRejectionReason.FrenzyInactive, transform.RejectionReason);

            PhenomenonResolutionResult baseResult = PhenomenonOperationResolver.Resolve(
                new PhenomenonOperationRequest(operation, law.Phenomenon, transform.EffectiveMagnitude, focal.SemanticState));
            if (!baseResult.Succeeded)
            {
                return new InterferenceEvaluationResult(
                    law, context, reference, focal, operation, law.Phenomenon, baseMagnitude,
                    requestedContributorCount, maximumContributors, true, false,
                    InterferenceEvaluationRejectionReason.BaseResolutionRejected, transform.EffectiveMagnitude, baseResult, transform.ContributorResults);
            }

            return new InterferenceEvaluationResult(
                law, context, reference, focal, operation, law.Phenomenon, baseMagnitude,
                requestedContributorCount, maximumContributors, true, true,
                InterferenceEvaluationRejectionReason.None, transform.EffectiveMagnitude, baseResult, transform.ContributorResults);
        }

        /// <summary>Shared transform seam. It validates participation and derives magnitude without resolving a semantic after-state.</summary>
        internal static InterferenceTransformResult TryGetTransform(InterferenceEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonCarrierSnapshot reference = request != null ? request.ReferenceCarrier : null;
            PhenomenonCarrierSnapshot focal = request != null ? request.FocalRecipient : null;
            PhenomenonOperationRequest original = request != null ? request.OriginalOperation : null;
            IReadOnlyList<PhenomenonCarrierSnapshot> contributors = request != null ? request.OrderedAdditionalContributors : null;
            int maximumContributors = request != null ? request.MaximumContributors : 0;
            InterferenceEvaluationRejectionReason rejection;
            if (!TryValidateLawAndExpression(law, context, out rejection)) return TransformRejected(rejection);
            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Interference) return TransformRejected(InterferenceEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            if (original == null || (original.Operation != PhenomenonOperationKind.Add && original.Operation != PhenomenonOperationKind.Remove))
                return TransformRejected(original != null && original.Operation == PhenomenonOperationKind.Transfer ? InterferenceEvaluationRejectionReason.OperationNotSupportedByTerritory : InterferenceEvaluationRejectionReason.InvalidBaseRequest);
            if (maximumContributors <= 0) return TransformRejected(InterferenceEvaluationRejectionReason.InvalidContributorLimit);
            if (!TryValidateReference(reference, law.Phenomenon, out rejection)) return TransformRejected(rejection);
            if (!TryValidateFocal(focal, law.Phenomenon, original.Target, out rejection)) return TransformRejected(rejection);
            if (original.Phenomenon != law.Phenomenon || float.IsNaN(original.Magnitude) || float.IsInfinity(original.Magnitude) || original.Magnitude < 0f)
                return TransformRejected(InterferenceEvaluationRejectionReason.InvalidBaseRequest);

            List<InterferenceContributorResult> results = new List<InterferenceContributorResult>();
            results.Add(new InterferenceContributorResult(reference, true, true, InterferenceContributorRejectionReason.ReferenceContribution));
            HashSet<PhenomenonCarrierId> encountered = new HashSet<PhenomenonCarrierId>();
            encountered.Add(reference.CarrierId);
            int accepted = 1;
            if (contributors != null)
                for (int index = 0; index < contributors.Count; index++)
                {
                    PhenomenonCarrierSnapshot contributor = contributors[index];
                    InterferenceContributorRejectionReason contributorRejection;
                    if (!TryValidateContributorIdentity(contributor, reference.CarrierId, focal.CarrierId, encountered, out contributorRejection)) { results.Add(new InterferenceContributorResult(contributor, false, false, contributorRejection)); continue; }
                    if (accepted >= maximumContributors) { results.Add(new InterferenceContributorResult(contributor, false, false, InterferenceContributorRejectionReason.ContributorLimitReached)); continue; }
                    if (contributor.Phenomenon != law.Phenomenon || contributor.SemanticState == null || contributor.SemanticState.Phenomenon != law.Phenomenon) { results.Add(new InterferenceContributorResult(contributor, false, false, contributor.SemanticState == null ? InterferenceContributorRejectionReason.SemanticStateUnavailable : InterferenceContributorRejectionReason.PhenomenonMismatch)); continue; }
                    if (!IsValidSemanticState(contributor.SemanticState, law.Phenomenon)) { results.Add(new InterferenceContributorResult(contributor, false, false, InterferenceContributorRejectionReason.InvalidSemanticState)); continue; }
                    if (contributor.SemanticState.Region != reference.SemanticState.Region) { results.Add(new InterferenceContributorResult(contributor, false, false, InterferenceContributorRejectionReason.RegionMismatch)); continue; }
                    results.Add(new InterferenceContributorResult(contributor, false, true, InterferenceContributorRejectionReason.None)); accepted++;
                }
            float effectiveMagnitude = original.Magnitude * accepted;
            return float.IsNaN(effectiveMagnitude) || float.IsInfinity(effectiveMagnitude)
                ? TransformRejected(InterferenceEvaluationRejectionReason.InvalidEffectiveMagnitude)
                : new InterferenceTransformResult(true, InterferenceEvaluationRejectionReason.None, effectiveMagnitude, results.AsReadOnly());
        }

        private static InterferenceTransformResult TransformRejected(InterferenceEvaluationRejectionReason rejection)
        { return new InterferenceTransformResult(false, rejection, 0f, new List<InterferenceContributorResult>().AsReadOnly()); }

        private static bool TryValidateLawAndExpression(
            DomainLaw law,
            LawExpressionContext context,
            out InterferenceEvaluationRejectionReason rejection)
        {
            LawEvaluationRejectionReason lawRejection;
            if (DomainLawEvaluator.TryEvaluateEligibility(law, context, out lawRejection))
            {
                rejection = InterferenceEvaluationRejectionReason.None;
                return true;
            }

            rejection = lawRejection == LawEvaluationRejectionReason.InvalidLaw
                ? InterferenceEvaluationRejectionReason.InvalidLaw
                : lawRejection == LawEvaluationRejectionReason.ExpressionUnavailable
                    ? InterferenceEvaluationRejectionReason.ExpressionUnavailable
                    : lawRejection == LawEvaluationRejectionReason.InvalidExpressionContext
                        ? InterferenceEvaluationRejectionReason.InvalidExpressionContext
                        : lawRejection == LawEvaluationRejectionReason.ExpressionMismatch
                            ? InterferenceEvaluationRejectionReason.ExpressionMismatch
                            : lawRejection == LawEvaluationRejectionReason.FrenzyInactive
                                ? InterferenceEvaluationRejectionReason.FrenzyInactive
                                : InterferenceEvaluationRejectionReason.InvalidLaw;
            return false;
        }

        private static bool TryValidateReference(
            PhenomenonCarrierSnapshot reference,
            LawPhenomenon phenomenon,
            out InterferenceEvaluationRejectionReason rejection)
        {
            if (reference == null || !reference.CarrierId.IsValid)
            {
                rejection = InterferenceEvaluationRejectionReason.ReferenceCarrierUnavailable;
                return false;
            }

            if (reference.Phenomenon != phenomenon
                || reference.SemanticState == null
                || reference.SemanticState.Phenomenon != phenomenon)
            {
                rejection = InterferenceEvaluationRejectionReason.ReferencePhenomenonMismatch;
                return false;
            }

            if (!IsValidSemanticState(reference.SemanticState, phenomenon))
            {
                rejection = InterferenceEvaluationRejectionReason.InvalidReferenceSemanticState;
                return false;
            }

            rejection = InterferenceEvaluationRejectionReason.None;
            return true;
        }

        private static bool TryValidateFocal(
            PhenomenonCarrierSnapshot focal,
            LawPhenomenon phenomenon,
            PhenomenonSemanticSnapshot originalTarget,
            out InterferenceEvaluationRejectionReason rejection)
        {
            if (focal == null || !focal.CarrierId.IsValid)
            {
                rejection = InterferenceEvaluationRejectionReason.FocalRecipientUnavailable;
                return false;
            }

            if (focal.Phenomenon != phenomenon
                || focal.SemanticState == null
                || focal.SemanticState.Phenomenon != phenomenon)
            {
                rejection = InterferenceEvaluationRejectionReason.FocalRecipientPhenomenonMismatch;
                return false;
            }

            if (!IsValidSemanticState(focal.SemanticState, phenomenon))
            {
                rejection = InterferenceEvaluationRejectionReason.InvalidFocalRecipientSemanticState;
                return false;
            }

            if (!SameSnapshot(focal.SemanticState, originalTarget))
            {
                rejection = InterferenceEvaluationRejectionReason.FocalSnapshotMismatch;
                return false;
            }

            rejection = InterferenceEvaluationRejectionReason.None;
            return true;
        }

        private static bool TryValidateContributorIdentity(
            PhenomenonCarrierSnapshot contributor,
            PhenomenonCarrierId referenceId,
            PhenomenonCarrierId focalId,
            HashSet<PhenomenonCarrierId> encountered,
            out InterferenceContributorRejectionReason rejection)
        {
            if (contributor == null || !contributor.CarrierId.IsValid)
            {
                rejection = InterferenceContributorRejectionReason.InvalidCarrierIdentity;
                return false;
            }

            if (contributor.CarrierId.Equals(referenceId))
            {
                rejection = InterferenceContributorRejectionReason.ReferenceReintroduced;
                return false;
            }

            if (contributor.CarrierId.Equals(focalId))
            {
                rejection = InterferenceContributorRejectionReason.FocalRecipientReintroduced;
                return false;
            }

            if (!encountered.Add(contributor.CarrierId))
            {
                rejection = InterferenceContributorRejectionReason.DuplicateContributor;
                return false;
            }

            rejection = InterferenceContributorRejectionReason.None;
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

        private static bool SameSnapshot(PhenomenonSemanticSnapshot left, PhenomenonSemanticSnapshot right)
        {
            return left != null
                && right != null
                && left.Phenomenon == right.Phenomenon
                && left.SemanticValue == right.SemanticValue
                && left.Region == right.Region;
        }

        private static InterferenceEvaluationResult Rejected(
            DomainLaw law,
            LawExpressionContext context,
            PhenomenonCarrierSnapshot reference,
            PhenomenonCarrierSnapshot focal,
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float baseMagnitude,
            int requestedContributorCount,
            int maximumContributors,
            bool eligible,
            InterferenceEvaluationRejectionReason rejection)
        {
            return new InterferenceEvaluationResult(
                law, context, reference, focal, operation, phenomenon, baseMagnitude,
                requestedContributorCount, maximumContributors, eligible, false, rejection,
                0f, null, new List<InterferenceContributorResult>().AsReadOnly());
        }
    }
}
