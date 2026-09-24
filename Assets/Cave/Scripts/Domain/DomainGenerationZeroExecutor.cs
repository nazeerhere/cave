using System.Collections.Generic;

namespace Cave.Domain
{
    public enum DomainGenerationZeroRejectionReason { None = 0, InvalidRequest = 1, BaseResolutionRejected = 2 }

    /// <summary>Immutable result for the sole Generation-0 unary primary resolution.</summary>
    public sealed class UnaryDomainGenerationZeroResult
    {
        internal UnaryDomainGenerationZeroResult(UnaryDomainOrchestrationRequest request, DomainResolutionPlan plan,
            EffectiveUnaryDomainIntent effectiveIntent, IReadOnlyList<DomainLawOutcomeTrace> outcomes,
            PhenomenonResolutionResult primaryResult, NormalizedSemanticTransition transition,
            DomainGenerationZeroRejectionReason rejection, DomainOrchestrationRequestValidation requestValidation)
        { Request = request; ResolutionPlan = plan; EffectiveIntent = effectiveIntent; TransformOutcomes = outcomes; PrimaryResult = primaryResult; PrimaryTransition = transition; RejectionReason = rejection; RequestValidation = requestValidation; }
        public UnaryDomainOrchestrationRequest Request { get; }
        public DomainResolutionPlan ResolutionPlan { get; }
        public EffectiveUnaryDomainIntent EffectiveIntent { get; }
        public IReadOnlyList<DomainLawOutcomeTrace> TransformOutcomes { get; }
        public PhenomenonResolutionResult PrimaryResult { get; }
        public NormalizedSemanticTransition PrimaryTransition { get; }
        public DomainLawOutcomeTrace ReversalOutcome => Find(LawTerritoryPrinciple.Reversal);
        public DomainLawOutcomeTrace InterferenceOutcome => Find(LawTerritoryPrinciple.Interference);
        public bool Succeeded => RejectionReason == DomainGenerationZeroRejectionReason.None && PrimaryResult != null && PrimaryResult.Succeeded;
        public DomainGenerationZeroRejectionReason RejectionReason { get; }
        public DomainOrchestrationRequestValidation RequestValidation { get; }
        public DomainOrchestrationGeneration MaximumGeneration => DomainOrchestrationGeneration.Generation0;
        private DomainLawOutcomeTrace Find(LawTerritoryPrinciple territory) { for (int i = 0; i < TransformOutcomes.Count; i++) if (TransformOutcomes[i].Law != null && TransformOutcomes[i].Law.TerritoryPrinciple == territory) return TransformOutcomes[i]; return null; }
    }

    /// <summary>Immutable result for the sole Generation-0 Transfer primary resolution.</summary>
    public sealed class TransferDomainGenerationZeroResult
    {
        internal TransferDomainGenerationZeroResult(TransferDomainOrchestrationRequest request, DomainResolutionPlan plan,
            IReadOnlyList<DomainLawOutcomeTrace> outcomes, PhenomenonResolutionResult primaryResult,
            NormalizedSemanticTransition sourceTransition, NormalizedSemanticTransition targetTransition,
            DomainGenerationZeroRejectionReason rejection, DomainOrchestrationRequestValidation requestValidation)
        { Request = request; ResolutionPlan = plan; TransformOutcomes = outcomes; PrimaryResult = primaryResult; SourceTransition = sourceTransition; TargetTransition = targetTransition; RejectionReason = rejection; RequestValidation = requestValidation; }
        public TransferDomainOrchestrationRequest Request { get; }
        public DomainResolutionPlan ResolutionPlan { get; }
        public IReadOnlyList<DomainLawOutcomeTrace> TransformOutcomes { get; }
        public PhenomenonResolutionResult PrimaryResult { get; }
        public NormalizedSemanticTransition SourceTransition { get; }
        public NormalizedSemanticTransition TargetTransition { get; }
        public bool Succeeded => RejectionReason == DomainGenerationZeroRejectionReason.None && PrimaryResult != null && PrimaryResult.Succeeded;
        public DomainGenerationZeroRejectionReason RejectionReason { get; }
        public DomainOrchestrationRequestValidation RequestValidation { get; }
        public DomainOrchestrationGeneration MaximumGeneration => DomainOrchestrationGeneration.Generation0;
    }

    /// <summary>Pure Generation-0 executor. It consumes the planner and resolves exactly one final unary or Transfer base request.</summary>
    public static class DomainGenerationZeroExecutor
    {
        public static UnaryDomainGenerationZeroResult Execute(UnaryDomainOrchestrationRequest request)
        {
            DomainOrchestrationRequestValidation validation = DomainOrchestrationRequestValidator.Validate(request);
            if (!validation.IsValid) return new UnaryDomainGenerationZeroResult(request, null, null, EmptyOutcomes(), null, null, DomainGenerationZeroRejectionReason.InvalidRequest, validation);
            DomainResolutionPlan plan = DomainResolutionPlanner.Plan(new DomainResolutionIntent(request.Composition, request.ExpressionContext, request.Phenomenon, request.Operation, request.Magnitude));
            List<DomainLawOutcomeTrace> outcomes = new List<DomainLawOutcomeTrace>();
            PhenomenonOperationKind effectiveOperation = request.Operation;
            float effectiveMagnitude = request.Magnitude;
            bool reversalApplied = ApplyReversal(plan, ref effectiveOperation, outcomes);
            int acceptedContributors;
            bool interferenceApplied = ApplyInterference(plan, request, effectiveOperation, ref effectiveMagnitude, outcomes, out acceptedContributors);
            EffectiveUnaryDomainIntent effective = new EffectiveUnaryDomainIntent(request.Phenomenon, request.Operation, request.Magnitude, effectiveOperation, effectiveMagnitude, reversalApplied, interferenceApplied, acceptedContributors);
            PhenomenonResolutionResult primary = PhenomenonOperationResolver.Resolve(new PhenomenonOperationRequest(effectiveOperation, request.Phenomenon, effectiveMagnitude, request.FocalCarrier.SemanticState));
            if (!primary.Succeeded) return new UnaryDomainGenerationZeroResult(request, plan, effective, outcomes.AsReadOnly(), primary, null, DomainGenerationZeroRejectionReason.BaseResolutionRejected, validation);
            NormalizedSemanticTransition transition = new NormalizedSemanticTransition(new DomainTransitionSequenceId(0), DomainOrchestrationGeneration.Generation0, null,
                request.FocalCarrier.CarrierId, request.Phenomenon, primary.TargetBefore, primary.TargetAfter,
                primary.Operation, primary.Magnitude, DomainTransitionOrigin.Base, null, true);
            return new UnaryDomainGenerationZeroResult(request, plan, effective, outcomes.AsReadOnly(), primary, transition, DomainGenerationZeroRejectionReason.None, validation);
        }

        public static TransferDomainGenerationZeroResult Execute(TransferDomainOrchestrationRequest request)
        {
            DomainOrchestrationRequestValidation validation = DomainOrchestrationRequestValidator.Validate(request);
            if (!validation.IsValid) return new TransferDomainGenerationZeroResult(request, null, EmptyOutcomes(), null, null, null, DomainGenerationZeroRejectionReason.InvalidRequest, validation);
            DomainResolutionPlan plan = DomainResolutionPlanner.Plan(new DomainResolutionIntent(request.Composition, request.ExpressionContext, request.Phenomenon, PhenomenonOperationKind.Transfer, request.Magnitude));
            List<DomainLawOutcomeTrace> outcomes = TransferTransformOutcomes(plan);
            PhenomenonResolutionResult primary = PhenomenonOperationResolver.ResolveTransfer(new PhenomenonTransferRequest(request.Phenomenon, request.Magnitude, request.SourceCarrier.SemanticState, request.FocalTargetCarrier.SemanticState));
            if (!primary.Succeeded) return new TransferDomainGenerationZeroResult(request, plan, outcomes.AsReadOnly(), primary, null, null, DomainGenerationZeroRejectionReason.BaseResolutionRejected, validation);
            NormalizedSemanticTransition source = new NormalizedSemanticTransition(new DomainTransitionSequenceId(0), DomainOrchestrationGeneration.Generation0, null,
                request.SourceCarrier.CarrierId, request.Phenomenon, primary.SourceBefore, primary.SourceAfter,
                PhenomenonOperationKind.Transfer, primary.Magnitude, DomainTransitionOrigin.Base, null, true);
            NormalizedSemanticTransition target = new NormalizedSemanticTransition(new DomainTransitionSequenceId(1), DomainOrchestrationGeneration.Generation0, null,
                request.FocalTargetCarrier.CarrierId, request.Phenomenon, primary.TargetBefore, primary.TargetAfter,
                PhenomenonOperationKind.Transfer, primary.Magnitude, DomainTransitionOrigin.Base, null, true);
            return new TransferDomainGenerationZeroResult(request, plan, outcomes.AsReadOnly(), primary, source, target, DomainGenerationZeroRejectionReason.None, validation);
        }

        private static bool ApplyReversal(DomainResolutionPlan plan, ref PhenomenonOperationKind operation, List<DomainLawOutcomeTrace> outcomes)
        {
            bool applied = false;
            for (int index = 0; index < plan.Laws.Count; index++)
            {
                PlannedDomainLaw planned = plan.Laws[index];
                if (planned.Phase != DomainResolutionCausalPhase.IntentTransformation || planned.CandidateLaw.TerritoryPrinciple != LawTerritoryPrinciple.Reversal) continue;
                if (!planned.SharedEligible) { outcomes.Add(Skipped(planned, DomainLawOutcomeReason.EvaluatorRejected)); continue; }
                if (planned.OperationCompatibility != DomainResolutionOperationCompatibility.Supported) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, false, false, DomainLawOutcomeReason.NoDefinedInverse)); continue; }
                PhenomenonOperationKind reversed;
                if (!DomainLawEvaluator.TryGetReversedOperation(operation, out reversed)) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, true, false, DomainLawOutcomeReason.NoDefinedInverse)); continue; }
                operation = reversed; applied = true;
                outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, true, true, DomainLawOutcomeReason.None));
            }
            return applied;
        }

        private static bool ApplyInterference(DomainResolutionPlan plan, UnaryDomainOrchestrationRequest request, PhenomenonOperationKind operation, ref float magnitude, List<DomainLawOutcomeTrace> outcomes, out int acceptedContributors)
        {
            acceptedContributors = 0;
            bool applied = false;
            for (int index = 0; index < plan.Laws.Count; index++)
            {
                PlannedDomainLaw planned = plan.Laws[index];
                if (planned.Phase != DomainResolutionCausalPhase.IntentTransformation || planned.CandidateLaw.TerritoryPrinciple != LawTerritoryPrinciple.Interference) continue;
                if (!planned.SharedEligible) { outcomes.Add(Skipped(planned, DomainLawOutcomeReason.EvaluatorRejected)); continue; }
                if (planned.OperationCompatibility != DomainResolutionOperationCompatibility.Supported) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, false, false, DomainLawOutcomeReason.OperationUnsupported)); continue; }
                InterferenceOrchestrationContext context;
                if (request.Contexts == null || !request.Contexts.TryGetInterference(planned.CandidateLaw, out context)) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, false, false, DomainLawOutcomeReason.ContextUnavailable)); continue; }
                if (context.FocalRecipient == null || !context.FocalRecipient.CarrierId.Equals(request.FocalCarrier.CarrierId)) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, false, false, DomainLawOutcomeReason.ContextMismatch)); continue; }
                InterferenceTransformResult transform = InterferenceEvaluator.TryGetTransform(new InterferenceEvaluationRequest(planned.CandidateLaw, request.ExpressionContext,
                    context.ReferenceCarrier, context.OrderedAdditionalContributors, context.FocalRecipient,
                    new PhenomenonOperationRequest(operation, request.Phenomenon, magnitude, request.FocalCarrier.SemanticState), context.MaximumContributors));
                if (!transform.Succeeded) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, true, false, DomainLawOutcomeReason.EvaluatorRejected, null, null, transform.RejectionReason)); continue; }
                magnitude = transform.EffectiveMagnitude; acceptedContributors = transform.AcceptedContributorCount; applied = true;
                outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, true, true, DomainLawOutcomeReason.None, acceptedContributors));
            }
            return applied;
        }

        private static List<DomainLawOutcomeTrace> TransferTransformOutcomes(DomainResolutionPlan plan)
        {
            List<DomainLawOutcomeTrace> outcomes = new List<DomainLawOutcomeTrace>();
            for (int index = 0; index < plan.Laws.Count; index++)
            {
                PlannedDomainLaw planned = plan.Laws[index];
                if (planned.Phase != DomainResolutionCausalPhase.IntentTransformation) continue;
                if (!planned.SharedEligible) { outcomes.Add(Skipped(planned, DomainLawOutcomeReason.EvaluatorRejected)); continue; }
                DomainLawOutcomeReason reason = planned.CandidateLaw.TerritoryPrinciple == LawTerritoryPrinciple.Reversal ? DomainLawOutcomeReason.NoDefinedInverse : DomainLawOutcomeReason.OperationUnsupported;
                outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, false, false, reason));
            }
            return outcomes;
        }

        private static DomainLawOutcomeTrace Skipped(PlannedDomainLaw planned, DomainLawOutcomeReason reason)
        { return new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation0, false, false, reason, null, planned.EligibilityReason); }
        private static IReadOnlyList<DomainLawOutcomeTrace> EmptyOutcomes() { return new List<DomainLawOutcomeTrace>().AsReadOnly(); }
    }
}
