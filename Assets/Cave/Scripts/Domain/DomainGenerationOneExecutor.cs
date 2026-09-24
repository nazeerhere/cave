using System.Collections.Generic;

namespace Cave.Domain
{
    public enum DomainGenerationOneRejectionReason { None = 0, GenerationZeroRequired = 1 }
    public enum OriginalTransferSourceSkipReason { OriginalSourceReintroduced = 1 }
    public sealed class OriginalTransferSourceSkip
    {
        public OriginalTransferSourceSkip(PhenomenonCarrierId carrierId) { CarrierId = carrierId; Reason = OriginalTransferSourceSkipReason.OriginalSourceReintroduced; }
        public PhenomenonCarrierId CarrierId { get; }
        public OriginalTransferSourceSkipReason Reason { get; }
    }

    public sealed class UnaryDomainGenerationOneResult
    {
        internal UnaryDomainGenerationOneResult(UnaryDomainGenerationZeroResult zero, IReadOnlyList<DomainLawOutcomeTrace> outcomes,
            IReadOnlyList<PropagationEvaluationResult> propagation, IReadOnlyList<SynchronizationEvaluationResult> synchronization,
            IReadOnlyList<NormalizedSemanticTransition> transitions, IReadOnlyList<ProposalCollision> collisions, DomainGenerationOneRejectionReason rejection)
        { GenerationZero = zero; ResolutionPlan = zero != null ? zero.ResolutionPlan : null; RelationalOutcomes = outcomes; PropagationResults = propagation; SynchronizationResults = synchronization; Transitions = transitions; ProposalCollisions = collisions; RejectionReason = rejection; }
        public UnaryDomainGenerationZeroResult GenerationZero { get; }
        public DomainResolutionPlan ResolutionPlan { get; }
        public IReadOnlyList<DomainLawOutcomeTrace> RelationalOutcomes { get; }
        public IReadOnlyList<PropagationEvaluationResult> PropagationResults { get; }
        public IReadOnlyList<SynchronizationEvaluationResult> SynchronizationResults { get; }
        public IReadOnlyList<NormalizedSemanticTransition> Transitions { get; }
        public IReadOnlyList<ProposalCollision> ProposalCollisions { get; }
        public bool HasUnresolvedCollisions => ProposalCollisions.Count > 0;
        public bool Generation2Executed => false;
        public DomainGenerationOneRejectionReason RejectionReason { get; }
        public bool Succeeded => RejectionReason == DomainGenerationOneRejectionReason.None;
        public DomainOrchestrationGeneration MaximumGeneration => Transitions.Count > 0 ? DomainOrchestrationGeneration.Generation1 : DomainOrchestrationGeneration.Generation0;
    }

    public sealed class TransferDomainGenerationOneResult
    {
        internal TransferDomainGenerationOneResult(TransferDomainGenerationZeroResult zero, IReadOnlyList<DomainLawOutcomeTrace> outcomes,
            IReadOnlyList<AccumulationEvaluationResult> accumulation, IReadOnlyList<OriginalTransferSourceSkip> sourceSkips,
            IReadOnlyList<NormalizedSemanticTransition> transitions, IReadOnlyList<ProposalCollision> collisions, DomainGenerationOneRejectionReason rejection)
        { GenerationZero = zero; ResolutionPlan = zero != null ? zero.ResolutionPlan : null; RelationalOutcomes = outcomes; AccumulationResults = accumulation; OriginalTransferSourceSkips = sourceSkips; Transitions = transitions; ProposalCollisions = collisions; RejectionReason = rejection; }
        public TransferDomainGenerationZeroResult GenerationZero { get; }
        public DomainResolutionPlan ResolutionPlan { get; }
        public IReadOnlyList<DomainLawOutcomeTrace> RelationalOutcomes { get; }
        public IReadOnlyList<AccumulationEvaluationResult> AccumulationResults { get; }
        public IReadOnlyList<OriginalTransferSourceSkip> OriginalTransferSourceSkips { get; }
        public IReadOnlyList<NormalizedSemanticTransition> Transitions { get; }
        public IReadOnlyList<ProposalCollision> ProposalCollisions { get; }
        public bool HasUnresolvedCollisions => ProposalCollisions.Count > 0;
        public bool Generation2Executed => false;
        public DomainGenerationOneRejectionReason RejectionReason { get; }
        public bool Succeeded => RejectionReason == DomainGenerationOneRejectionReason.None;
        public DomainOrchestrationGeneration MaximumGeneration => Transitions.Count > 0 ? DomainOrchestrationGeneration.Generation1 : DomainOrchestrationGeneration.Generation0;
    }

    /// <summary>Pure bounded Generation-1 relational dispatch. It reuses Generation-0's stored plan and never invokes Catalysis.</summary>
    public static class DomainGenerationOneExecutor
    {
        public static UnaryDomainGenerationOneResult Execute(UnaryDomainGenerationZeroResult zero)
        {
            if (zero == null || !zero.Succeeded || zero.ResolutionPlan == null || zero.PrimaryTransition == null)
                return new UnaryDomainGenerationOneResult(zero, Empty<DomainLawOutcomeTrace>(), Empty<PropagationEvaluationResult>(), Empty<SynchronizationEvaluationResult>(), Empty<NormalizedSemanticTransition>(), Empty<ProposalCollision>(), DomainGenerationOneRejectionReason.GenerationZeroRequired);
            List<DomainLawOutcomeTrace> outcomes = new List<DomainLawOutcomeTrace>();
            List<PropagationEvaluationResult> propagationResults = new List<PropagationEvaluationResult>();
            List<SynchronizationEvaluationResult> synchronizationResults = new List<SynchronizationEvaluationResult>();
            List<NormalizedSemanticTransition> transitions = new List<NormalizedSemanticTransition>();
            int nextId = zero.PrimaryTransition.SequenceId.Value + 1;
            for (int index = 0; index < zero.ResolutionPlan.Laws.Count; index++)
            {
                PlannedDomainLaw planned = zero.ResolutionPlan.Laws[index];
                if (planned.Phase != DomainResolutionCausalPhase.RelationalResolution) continue;
                if (!planned.SharedEligible) { outcomes.Add(Skipped(planned)); continue; }
                if (planned.CandidateLaw.TerritoryPrinciple == LawTerritoryPrinciple.Propagation)
                    RunPropagation(zero, planned, propagationResults, outcomes, transitions, ref nextId);
                else if (planned.CandidateLaw.TerritoryPrinciple == LawTerritoryPrinciple.Synchronization)
                    RunSynchronization(zero, planned, synchronizationResults, outcomes, transitions, ref nextId);
                else outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, false, false, DomainLawOutcomeReason.OperationUnsupported));
            }
            List<ProposalCollision> collisions;
            IReadOnlyList<NormalizedSemanticTransition> finalTransitions = DetectAndFlagCollisions(transitions, out collisions);
            return new UnaryDomainGenerationOneResult(zero, outcomes.AsReadOnly(), propagationResults.AsReadOnly(), synchronizationResults.AsReadOnly(), finalTransitions, collisions.AsReadOnly(), DomainGenerationOneRejectionReason.None);
        }

        public static TransferDomainGenerationOneResult Execute(TransferDomainGenerationZeroResult zero)
        {
            if (zero == null || !zero.Succeeded || zero.ResolutionPlan == null || zero.TargetTransition == null)
                return new TransferDomainGenerationOneResult(zero, Empty<DomainLawOutcomeTrace>(), Empty<AccumulationEvaluationResult>(), Empty<OriginalTransferSourceSkip>(), Empty<NormalizedSemanticTransition>(), Empty<ProposalCollision>(), DomainGenerationOneRejectionReason.GenerationZeroRequired);
            List<DomainLawOutcomeTrace> outcomes = new List<DomainLawOutcomeTrace>();
            List<AccumulationEvaluationResult> accumulationResults = new List<AccumulationEvaluationResult>();
            List<OriginalTransferSourceSkip> sourceSkips = new List<OriginalTransferSourceSkip>();
            List<NormalizedSemanticTransition> transitions = new List<NormalizedSemanticTransition>();
            int nextId = zero.TargetTransition.SequenceId.Value + 1;
            for (int index = 0; index < zero.ResolutionPlan.Laws.Count; index++)
            {
                PlannedDomainLaw planned = zero.ResolutionPlan.Laws[index];
                if (planned.Phase != DomainResolutionCausalPhase.RelationalResolution) continue;
                if (!planned.SharedEligible) { outcomes.Add(Skipped(planned)); continue; }
                if (planned.CandidateLaw.TerritoryPrinciple == LawTerritoryPrinciple.Accumulation)
                    RunAccumulation(zero, planned, accumulationResults, sourceSkips, outcomes, transitions, ref nextId);
                else outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, false, false, DomainLawOutcomeReason.OperationUnsupported));
            }
            List<ProposalCollision> collisions;
            IReadOnlyList<NormalizedSemanticTransition> finalTransitions = DetectAndFlagCollisions(transitions, out collisions);
            return new TransferDomainGenerationOneResult(zero, outcomes.AsReadOnly(), accumulationResults.AsReadOnly(), sourceSkips.AsReadOnly(), finalTransitions, collisions.AsReadOnly(), DomainGenerationOneRejectionReason.None);
        }

        private static void RunPropagation(UnaryDomainGenerationZeroResult zero, PlannedDomainLaw planned, List<PropagationEvaluationResult> results, List<DomainLawOutcomeTrace> outcomes, List<NormalizedSemanticTransition> transitions, ref int nextId)
        {
            PropagationOrchestrationContext context;
            if (zero.Request.Contexts == null || !zero.Request.Contexts.TryGetPropagation(planned.CandidateLaw, out context)) { outcomes.Add(ContextUnavailable(planned)); return; }
            if (context.Topology == null || context.Topology.Source == null || !context.Topology.Source.CarrierId.Equals(zero.Request.FocalCarrier.CarrierId)) { outcomes.Add(ContextMismatch(planned)); return; }
            PhenomenonCarrierSnapshot source = new PhenomenonCarrierSnapshot(zero.Request.FocalCarrier.CarrierId, zero.Request.Phenomenon, zero.PrimaryResult.TargetAfter);
            PhenomenonCarrierTopology topology = new PhenomenonCarrierTopology(source, context.Topology.OrderedRecipients);
            PropagationEvaluationResult result = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(planned.CandidateLaw,
                new PhenomenonOperationRequest(zero.EffectiveIntent.EffectiveOperation, zero.Request.Phenomenon, zero.EffectiveIntent.EffectiveMagnitude, source.SemanticState), zero.Request.ExpressionContext, topology, context.MaximumRecipients));
            results.Add(result);
            if (!result.Succeeded) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, true, false, DomainLawOutcomeReason.EvaluatorRejected)); return; }
            int start = transitions.Count;
            for (int index = 0; index < result.RecipientResults.Count; index++)
            {
                PropagationRecipientResult recipient = result.RecipientResults[index];
                if (!recipient.Succeeded) continue;
                transitions.Add(new NormalizedSemanticTransition(new DomainTransitionSequenceId(nextId++), DomainOrchestrationGeneration.Generation1, zero.PrimaryTransition.SequenceId,
                    recipient.CarrierId, zero.Request.Phenomenon, recipient.Before, recipient.After, result.OriginalOperation, result.Magnitude, DomainTransitionOrigin.Propagation, planned.CandidateLaw, true));
            }
            outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, true, true, DomainLawOutcomeReason.None, null, null, null, transitions.Count - start));
        }

        private static void RunSynchronization(UnaryDomainGenerationZeroResult zero, PlannedDomainLaw planned, List<SynchronizationEvaluationResult> results, List<DomainLawOutcomeTrace> outcomes, List<NormalizedSemanticTransition> transitions, ref int nextId)
        {
            SynchronizationOrchestrationContext context;
            if (zero.Request.Contexts == null || !zero.Request.Contexts.TryGetSynchronization(planned.CandidateLaw, out context)) { outcomes.Add(ContextUnavailable(planned)); return; }
            if (context.ActiveGroup == null || !context.ActiveGroup.Contains(zero.Request.FocalCarrier.CarrierId) || context.ActiveGroup.Phenomenon != zero.Request.Phenomenon) { outcomes.Add(ContextMismatch(planned)); return; }
            PhenomenonSemanticSnapshot initiatorAfter = zero.PrimaryResult.TargetAfter;
            SynchronizationEvaluationResult result = SynchronizationEvaluator.Evaluate(new SynchronizationEvaluationRequest(planned.CandidateLaw,
                zero.Request.ExpressionContext, context.ActiveGroup, zero.Request.FocalCarrier.CarrierId, initiatorAfter,
                new PhenomenonOperationRequest(zero.EffectiveIntent.EffectiveOperation, zero.Request.Phenomenon, zero.EffectiveIntent.EffectiveMagnitude, initiatorAfter), context.CurrentMemberSnapshots));
            results.Add(result);
            if (!result.Succeeded) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, true, false, DomainLawOutcomeReason.EvaluatorRejected)); return; }
            int start = transitions.Count;
            for (int index = 0; index < result.MemberResults.Count; index++)
            {
                SynchronizationMemberResult member = result.MemberResults[index];
                if (!member.Succeeded) continue;
                transitions.Add(new NormalizedSemanticTransition(new DomainTransitionSequenceId(nextId++), DomainOrchestrationGeneration.Generation1, zero.PrimaryTransition.SequenceId,
                    member.MemberId, zero.Request.Phenomenon, member.Before, member.After, member.MirroredOperation, member.MirroredMagnitude, DomainTransitionOrigin.Synchronization, planned.CandidateLaw, true));
            }
            outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, true, true, DomainLawOutcomeReason.None, null, null, null, transitions.Count - start));
        }

        private static void RunAccumulation(TransferDomainGenerationZeroResult zero, PlannedDomainLaw planned, List<AccumulationEvaluationResult> results, List<OriginalTransferSourceSkip> sourceSkips, List<DomainLawOutcomeTrace> outcomes, List<NormalizedSemanticTransition> transitions, ref int nextId)
        {
            AccumulationOrchestrationContext context;
            if (zero.Request.Contexts == null || !zero.Request.Contexts.TryGetAccumulation(planned.CandidateLaw, out context)) { outcomes.Add(ContextUnavailable(planned)); return; }
            if (context.Topology == null || context.Topology.FocalRecipient == null || !context.Topology.FocalRecipient.CarrierId.Equals(zero.Request.FocalTargetCarrier.CarrierId) || context.Topology.FocalRecipient.Phenomenon != zero.Request.Phenomenon) { outcomes.Add(ContextMismatch(planned)); return; }
            List<PhenomenonCarrierSnapshot> contributors = new List<PhenomenonCarrierSnapshot>();
            for (int index = 0; index < context.Topology.OrderedContributors.Count; index++)
            {
                PhenomenonCarrierSnapshot contributor = context.Topology.OrderedContributors[index];
                if (contributor != null && contributor.CarrierId.Equals(zero.Request.SourceCarrier.CarrierId)) { sourceSkips.Add(new OriginalTransferSourceSkip(contributor.CarrierId)); continue; }
                contributors.Add(contributor);
            }
            PhenomenonCarrierSnapshot focal = new PhenomenonCarrierSnapshot(zero.Request.FocalTargetCarrier.CarrierId, zero.Request.Phenomenon, zero.PrimaryResult.TargetAfter);
            AccumulationEvaluationResult result = AccumulationEvaluator.Evaluate(new AccumulationEvaluationRequest(planned.CandidateLaw, zero.Request.ExpressionContext,
                new PhenomenonConvergenceTopology(focal, contributors), PhenomenonOperationKind.Transfer, zero.Request.Magnitude, context.MaximumContributors));
            results.Add(result);
            if (!result.Succeeded) { outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, true, false, DomainLawOutcomeReason.EvaluatorRejected)); return; }
            int start = transitions.Count;
            DomainTransitionSequenceId parent = zero.TargetTransition.SequenceId;
            for (int index = 0; index < result.ContributorResults.Count; index++)
            {
                AccumulationContributorResult contributor = result.ContributorResults[index];
                if (!contributor.Succeeded) continue;
                transitions.Add(new NormalizedSemanticTransition(new DomainTransitionSequenceId(nextId++), DomainOrchestrationGeneration.Generation1, parent,
                    contributor.ContributorId, zero.Request.Phenomenon, contributor.ContributorBefore, contributor.ContributorAfter, PhenomenonOperationKind.Transfer, result.TransferMagnitude, DomainTransitionOrigin.Accumulation, planned.CandidateLaw, true));
                NormalizedSemanticTransition focalTransition = new NormalizedSemanticTransition(new DomainTransitionSequenceId(nextId++), DomainOrchestrationGeneration.Generation1, parent,
                    zero.Request.FocalTargetCarrier.CarrierId, zero.Request.Phenomenon, contributor.FocalBeforeThisContribution, contributor.FocalAfterThisContribution, PhenomenonOperationKind.Transfer, result.TransferMagnitude, DomainTransitionOrigin.Accumulation, planned.CandidateLaw, true);
                transitions.Add(focalTransition); parent = focalTransition.SequenceId;
            }
            outcomes.Add(new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, true, true, sourceSkips.Count > 0 ? DomainLawOutcomeReason.OriginalSourceReintroduced : DomainLawOutcomeReason.None, null, null, null, transitions.Count - start));
        }

        private static IReadOnlyList<NormalizedSemanticTransition> DetectAndFlagCollisions(IReadOnlyList<NormalizedSemanticTransition> source, out List<ProposalCollision> collisions)
        {
            collisions = new List<ProposalCollision>();
            bool[] collided = new bool[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index].Generation != DomainOrchestrationGeneration.Generation1) continue;
                List<int> matching = new List<int>(); List<DomainLaw> laws = new List<DomainLaw>();
                for (int candidateIndex = 0; candidateIndex < source.Count; candidateIndex++)
                {
                    NormalizedSemanticTransition candidate = source[candidateIndex];
                    if (candidate.Generation != DomainOrchestrationGeneration.Generation1 || !candidate.CarrierId.Equals(source[index].CarrierId) || candidate.Phenomenon != source[index].Phenomenon) continue;
                    matching.Add(candidateIndex);
                    if (candidate.OriginatingLaw != null && !laws.Contains(candidate.OriginatingLaw)) laws.Add(candidate.OriginatingLaw);
                }
                if (laws.Count < 2) continue;
                bool alreadyRecorded = false; for (int c = 0; c < collisions.Count; c++) if (collisions[c].CarrierId.Equals(source[index].CarrierId) && collisions[c].Phenomenon == source[index].Phenomenon) alreadyRecorded = true;
                if (alreadyRecorded) continue;
                List<DomainTransitionSequenceId> ids = new List<DomainTransitionSequenceId>();
                for (int m = 0; m < matching.Count; m++) { collided[matching[m]] = true; ids.Add(source[matching[m]].SequenceId); }
                collisions.Add(new ProposalCollision(source[index].CarrierId, source[index].Phenomenon, DomainOrchestrationGeneration.Generation1, ids, laws));
            }
            List<NormalizedSemanticTransition> final = new List<NormalizedSemanticTransition>();
            for (int index = 0; index < source.Count; index++) final.Add(collided[index] ? source[index].WithUnambiguous(false) : source[index]);
            return final.AsReadOnly();
        }

        private static DomainLawOutcomeTrace Skipped(PlannedDomainLaw planned) { return new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, false, false, DomainLawOutcomeReason.EvaluatorRejected, null, planned.EligibilityReason); }
        private static DomainLawOutcomeTrace ContextUnavailable(PlannedDomainLaw planned) { return new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, false, false, DomainLawOutcomeReason.ContextUnavailable); }
        private static DomainLawOutcomeTrace ContextMismatch(PlannedDomainLaw planned) { return new DomainLawOutcomeTrace(planned.CandidateLaw, planned.Phase, DomainOrchestrationGeneration.Generation1, false, false, DomainLawOutcomeReason.ContextMismatch); }
        private static IReadOnlyList<T> Empty<T>() { return new List<T>().AsReadOnly(); }
    }
}
