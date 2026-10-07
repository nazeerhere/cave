using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    public enum LocalizedDomainExecutionRejectionReason
    {
        None = 0,
        CarrierInactive = 1,
        AlreadyExecuted = 2,
        TargetResolutionFailed = 3,
        TerritoryContextUnavailable = 4,
        OrchestrationRejected = 5,
        CommitPlanRejected = 6,
        RuntimeCommitRejected = 7
        , FocalTargetUnavailable = 8, OperationUnsupported = 9
    }

    /// <summary>
    /// One bounded activation transaction for a localized carrier. It resolves
    /// only explicitly registered live targets and routes every write through
    /// the existing bounded orchestrator and runtime committer.
    /// </summary>
    public sealed class LocalizedDomainRuntimeExecutor
    {
        private readonly HashSet<string> executedGenerations = new HashSet<string>();

        /// <summary>
        /// Produces bounded, fully planned participants but never commits them.
        /// The shared coordinator is the only caller that batches these with
        /// sibling requests before the existing arbitrator/committer path.
        /// </summary>
        internal LocalizedDomainPreparationResult Prepare(LocalizedDomainCarrier carrier, float timestamp, string generationId)
        {
            if (carrier == null || !carrier.IsActive(timestamp) || !carrier.TryBindExecutionGeneration(generationId))
                return LocalizedDomainPreparationResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.CarrierInactive);
            if (carrier.BoundLaw == null || carrier.BoundLaw.Law == null)
                return LocalizedDomainPreparationResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.TerritoryContextUnavailable);
            LawTerritoryPrinciple territory = carrier.BoundLaw.Law.TerritoryPrinciple;
            LocalizedDomainTargetResolution resolution = LocalizedDomainTargetResolver.Resolve(carrier, timestamp);
            if (!resolution.Succeeded)
                return LocalizedDomainPreparationResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.TargetResolutionFailed);
            if (territory == LawTerritoryPrinciple.Accumulation)
                return PrepareAccumulation(carrier, resolution.Targets);

            List<LocalizedDomainPreparedProposal> proposals = new List<LocalizedDomainPreparedProposal>();
            for (int index = 0; index < resolution.Targets.Count; index++)
            {
                LocalizedDomainRuntimeTarget target = resolution.Targets[index];
                // Interference is rooted in the ordinary pulse's authentic
                // focal carrier. Other localized live carriers are semantic
                // contributors only; neither Trap proposals nor FieldNodes
                // can supply a contributor identity.
                if (territory == LawTerritoryPrinciple.Interference
                    && (carrier.ActivationContext == null || !carrier.ActivationContext.HasFocalCarrier
                        || !target.CarrierId.Equals(carrier.ActivationContext.FocalCarrierId))) continue;
                DomainOrchestrationContextSet contexts;
                if (!TryBuildContexts(carrier, target, resolution.Targets, timestamp, out contexts))
                    continue; // typed context absence remains fail-closed per target.
                UnaryDomainOrchestrationRequest request = new UnaryDomainOrchestrationRequest(carrier.Composition,
                    new LawExpressionContext(true, LawExpression.Trap, false), carrier.BoundLaw.Law.Phenomenon,
                    PhenomenonOperationKind.Add, 1f,
                    new PhenomenonCarrierSnapshot(target.CarrierId, carrier.BoundLaw.Law.Phenomenon, target.Snapshot), contexts);
                DomainBoundedOrchestrationResult bounded = DomainBoundedOrchestrator.Execute(request);
                DomainCommitPlan plan = bounded.Succeeded ? DomainCommitPlanBuilder.Build(bounded) : null;
                if (!bounded.Succeeded || plan == null || !plan.IsCommittable) continue;
                PhenomenonOperationKind operation = bounded.UnaryGenerationZero != null && bounded.UnaryGenerationZero.EffectiveIntent != null
                    ? bounded.UnaryGenerationZero.EffectiveIntent.EffectiveOperation : PhenomenonOperationKind.Add;
                DomainConflictParticipant participant = new DomainConflictParticipant(carrier.Id + ":" + target.CarrierId,
                    plan, carrier.Composition, null, DomainConflictEvidenceSet.Empty, carrier.Id, carrier.CreateProvenance(operation));
                proposals.Add(new LocalizedDomainPreparedProposal(carrier, target.CarrierId, bounded, plan, participant));
            }
            return new LocalizedDomainPreparationResult(carrier, proposals.AsReadOnly(), LocalizedDomainExecutionRejectionReason.None);
        }

        private static LocalizedDomainPreparationResult PrepareAccumulation(LocalizedDomainCarrier carrier,
            IReadOnlyList<LocalizedDomainRuntimeTarget> targets)
        {
            if (!PhenomenonSemanticClassifier.SupportsOperation(carrier.BoundLaw.Law.Phenomenon, PhenomenonOperationKind.Transfer))
                return LocalizedDomainPreparationResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.OperationUnsupported);
            if (carrier.ActivationContext == null || !carrier.ActivationContext.HasFocalCarrier)
                return LocalizedDomainPreparationResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.FocalTargetUnavailable);
            LocalizedDomainRuntimeTarget focal = null; List<LocalizedDomainRuntimeTarget> contributors = new List<LocalizedDomainRuntimeTarget>();
            for (int index = 0; index < targets.Count; index++) { if (targets[index].CarrierId.Equals(carrier.ActivationContext.FocalCarrierId)) focal = targets[index]; else contributors.Add(targets[index]); }
            if (focal == null || contributors.Count == 0) return LocalizedDomainPreparationResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.FocalTargetUnavailable);
            List<PhenomenonCarrierSnapshot> snapshots = new List<PhenomenonCarrierSnapshot>();
            for (int index = 0; index < contributors.Count; index++) snapshots.Add(new PhenomenonCarrierSnapshot(contributors[index].CarrierId, carrier.BoundLaw.Law.Phenomenon, contributors[index].Snapshot));
            PhenomenonCarrierSnapshot focalSnapshot = new PhenomenonCarrierSnapshot(focal.CarrierId, carrier.BoundLaw.Law.Phenomenon, focal.Snapshot);
            DomainOrchestrationContextSet contexts = new DomainOrchestrationContextSet(accumulation: new[] {
                new DomainLawContextBinding<AccumulationOrchestrationContext>(carrier.BoundLaw.Law,
                    new AccumulationOrchestrationContext(new PhenomenonConvergenceTopology(focalSnapshot, snapshots), snapshots.Count)) });
            DomainBoundedOrchestrationResult bounded = DomainBoundedOrchestrator.Execute(new TransferDomainOrchestrationRequest(
                carrier.Composition, new LawExpressionContext(true, LawExpression.Trap, false), carrier.BoundLaw.Law.Phenomenon, 1f,
                snapshots[0], focalSnapshot, contexts));
            DomainCommitPlan plan = bounded.Succeeded ? DomainCommitPlanBuilder.Build(bounded) : null;
            if (!bounded.Succeeded || plan == null || !plan.IsCommittable) return LocalizedDomainPreparationResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.OrchestrationRejected);
            DomainConflictParticipant participant = new DomainConflictParticipant(carrier.Id + ":" + focal.CarrierId, plan,
                carrier.Composition, null, DomainConflictEvidenceSet.Empty, carrier.Id, carrier.CreateProvenance(PhenomenonOperationKind.Transfer));
            return new LocalizedDomainPreparationResult(carrier, new[] { new LocalizedDomainPreparedProposal(carrier, focal.CarrierId, bounded, plan, participant) }, LocalizedDomainExecutionRejectionReason.None);
        }

        private static bool TryBuildContexts(LocalizedDomainCarrier carrier, LocalizedDomainRuntimeTarget target,
            IReadOnlyList<LocalizedDomainRuntimeTarget> allTargets, float timestamp, out DomainOrchestrationContextSet contexts)
        {
            contexts = DomainOrchestrationContextSet.Empty;
            LawTerritoryPrinciple territory = carrier.BoundLaw.Law.TerritoryPrinciple;
            if (territory == LawTerritoryPrinciple.Catalysis) { contexts = BuildUnaryContexts(carrier, target, allTargets); return true; }
            if (territory == LawTerritoryPrinciple.Synchronization)
            {
                PhenomenonRelationshipGroup group; IReadOnlyList<PhenomenonCarrierSnapshot> members;
                if (!SynchronizationRuntimeRegistry.TryGetLiveMembers(target.CarrierId, carrier.BoundLaw.Law.Phenomenon, timestamp, out group, out members)) return false;
                contexts = new DomainOrchestrationContextSet(synchronization: new[] {
                    new DomainLawContextBinding<SynchronizationOrchestrationContext>(carrier.BoundLaw.Law, new SynchronizationOrchestrationContext(group, members)) });
                return true;
            }
            if (territory == LawTerritoryPrinciple.Propagation)
            {
                PropagationOrchestrationContext propagation;
                if (!FieldNetworkDomainCarrierBinding.TryBuildPropagationContext(target.CarrierId, carrier.BoundLaw.Law.Phenomenon, timestamp, out propagation)) return false;
                contexts = new DomainOrchestrationContextSet(propagation: new[] { new DomainLawContextBinding<PropagationOrchestrationContext>(carrier.BoundLaw.Law, propagation) });
                return true;
            }
            if (territory == LawTerritoryPrinciple.Interference)
            {
                List<PhenomenonCarrierSnapshot> contributors = new List<PhenomenonCarrierSnapshot>();
                for (int index = 0; index < allTargets.Count; index++)
                {
                    LocalizedDomainRuntimeTarget candidate = allTargets[index];
                    if (candidate == null || candidate.CarrierId.Equals(target.CarrierId)) continue;
                    contributors.Add(new PhenomenonCarrierSnapshot(candidate.CarrierId, carrier.BoundLaw.Law.Phenomenon, candidate.Snapshot));
                }
                contributors.Sort((left, right) => left.CarrierId.CompareTo(right.CarrierId));
                if (contributors.Count == 0) return false;
                PhenomenonCarrierSnapshot focal = new PhenomenonCarrierSnapshot(target.CarrierId,
                    carrier.BoundLaw.Law.Phenomenon, target.Snapshot);
                PhenomenonCarrierSnapshot reference = contributors[0];
                List<PhenomenonCarrierSnapshot> additional = new List<PhenomenonCarrierSnapshot>();
                for (int index = 1; index < contributors.Count; index++) additional.Add(contributors[index]);
                contexts = new DomainOrchestrationContextSet(interference: new[] {
                    new DomainLawContextBinding<InterferenceOrchestrationContext>(carrier.BoundLaw.Law,
                        new InterferenceOrchestrationContext(reference, additional, focal, contributors.Count)) });
                return true;
            }
            return territory == LawTerritoryPrinciple.Reversal;
        }

        public LocalizedDomainExecutionResult Execute(LocalizedDomainCarrier carrier, float timestamp)
        {
            if (carrier == null || !carrier.IsActive(timestamp))
                return LocalizedDomainExecutionResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.CarrierInactive);
            if (!executedGenerations.Add(carrier.ExecutionGenerationId))
                return LocalizedDomainExecutionResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.AlreadyExecuted);
            // A standalone localized pulse can supply only contexts whose
            // authority exists in this transaction. Other Territories stay
            // fail-closed until their owner-specific adapter is available.
            if (carrier.BoundLaw == null || carrier.BoundLaw.Law == null
                || (carrier.BoundLaw.Law.TerritoryPrinciple != LawTerritoryPrinciple.Reversal
                    && carrier.BoundLaw.Law.TerritoryPrinciple != LawTerritoryPrinciple.Catalysis
                    && carrier.BoundLaw.Law.TerritoryPrinciple != LawTerritoryPrinciple.Accumulation))
                return LocalizedDomainExecutionResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.TerritoryContextUnavailable);

            LocalizedDomainTargetResolution resolution = LocalizedDomainTargetResolver.Resolve(carrier, timestamp);
            if (!resolution.Succeeded)
                return LocalizedDomainExecutionResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.TargetResolutionFailed);

            if (carrier.BoundLaw.Law.TerritoryPrinciple == LawTerritoryPrinciple.Accumulation)
                return ExecuteAccumulation(carrier, resolution.Targets, timestamp);

            List<LocalizedDomainTargetExecution> results = new List<LocalizedDomainTargetExecution>();
            for (int index = 0; index < resolution.Targets.Count; index++)
            {
                LocalizedDomainRuntimeTarget target = resolution.Targets[index];
                UnaryDomainOrchestrationRequest request = new UnaryDomainOrchestrationRequest(
                    carrier.Composition,
                    new LawExpressionContext(true, LawExpression.Trap, false),
                    carrier.BoundLaw.Law.Phenomenon,
                    PhenomenonOperationKind.Add,
                    1f,
                    new PhenomenonCarrierSnapshot(target.CarrierId, carrier.BoundLaw.Law.Phenomenon, target.Snapshot),
                    BuildUnaryContexts(carrier, target, resolution.Targets));
                DomainBoundedOrchestrationResult bounded = DomainBoundedOrchestrator.Execute(request);
                if (!bounded.Succeeded)
                {
                    results.Add(new LocalizedDomainTargetExecution(target.CarrierId, bounded, null, null,
                        LocalizedDomainExecutionRejectionReason.OrchestrationRejected));
                    continue;
                }

                DomainCommitPlan plan = DomainCommitPlanBuilder.Build(bounded);
                if (!plan.IsCommittable)
                {
                    results.Add(new LocalizedDomainTargetExecution(target.CarrierId, bounded, plan, null,
                        LocalizedDomainExecutionRejectionReason.CommitPlanRejected));
                    continue;
                }

                PhenomenonOperationKind effectiveOperation = bounded.UnaryGenerationZero != null
                    && bounded.UnaryGenerationZero.EffectiveIntent != null
                    ? bounded.UnaryGenerationZero.EffectiveIntent.EffectiveOperation
                    : PhenomenonOperationKind.Add;
                DomainConflictParticipant participant = new DomainConflictParticipant(
                    carrier.Id + ":" + target.CarrierId,
                    plan,
                    carrier.Composition,
                    null,
                    DomainConflictEvidenceSet.Empty,
                    carrier.Id,
                    carrier.CreateProvenance(effectiveOperation));
                CrossDomainArbitrationResult arbitration = DomainConflictArbitrator.Arbitrate(
                    new[] { participant });
                ArbitratedDomainCommitPlan arbitrated = ArbitratedDomainCommitPlanBuilder.Build(arbitration);
                ArbitratedDomainRuntimeCommitResult arbitratedCommit = new ArbitratedDomainRuntimeCommitter().Commit(
                    arbitrated, DomainRuntimeCarrierRegistry.CreateAccessor(timestamp));
                DomainCommitResult commit = arbitratedCommit.RuntimeResult;
                results.Add(new LocalizedDomainTargetExecution(target.CarrierId, bounded, plan, commit,
                    arbitratedCommit.Succeeded ? LocalizedDomainExecutionRejectionReason.None
                        : LocalizedDomainExecutionRejectionReason.RuntimeCommitRejected));
            }
            return new LocalizedDomainExecutionResult(carrier, results.AsReadOnly(), LocalizedDomainExecutionRejectionReason.None);
        }

        private static LocalizedDomainExecutionResult ExecuteAccumulation(LocalizedDomainCarrier carrier,
            IReadOnlyList<LocalizedDomainRuntimeTarget> targets, float timestamp)
        {
            if (!PhenomenonSemanticClassifier.SupportsOperation(carrier.BoundLaw.Law.Phenomenon, PhenomenonOperationKind.Transfer))
                return LocalizedDomainExecutionResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.OperationUnsupported);
            if (carrier.ActivationContext == null || !carrier.ActivationContext.HasFocalCarrier)
                return LocalizedDomainExecutionResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.FocalTargetUnavailable);

            LocalizedDomainRuntimeTarget focal = null;
            List<LocalizedDomainRuntimeTarget> contributors = new List<LocalizedDomainRuntimeTarget>();
            for (int index = 0; index < targets.Count; index++)
            {
                LocalizedDomainRuntimeTarget target = targets[index];
                if (target.CarrierId.Equals(carrier.ActivationContext.FocalCarrierId)) focal = target;
                else contributors.Add(target);
            }
            if (focal == null || contributors.Count == 0)
                return LocalizedDomainExecutionResult.Rejected(carrier, LocalizedDomainExecutionRejectionReason.FocalTargetUnavailable);

            List<PhenomenonCarrierSnapshot> contributorSnapshots = new List<PhenomenonCarrierSnapshot>(contributors.Count);
            for (int index = 0; index < contributors.Count; index++) contributorSnapshots.Add(
                new PhenomenonCarrierSnapshot(contributors[index].CarrierId, carrier.BoundLaw.Law.Phenomenon, contributors[index].Snapshot));
            PhenomenonCarrierSnapshot focalSnapshot = new PhenomenonCarrierSnapshot(focal.CarrierId,
                carrier.BoundLaw.Law.Phenomenon, focal.Snapshot);
            DomainOrchestrationContextSet contexts = new DomainOrchestrationContextSet(accumulation: new[]
            {
                new DomainLawContextBinding<AccumulationOrchestrationContext>(carrier.BoundLaw.Law,
                    new AccumulationOrchestrationContext(new PhenomenonConvergenceTopology(focalSnapshot, contributorSnapshots), contributorSnapshots.Count))
            });
            // The first contributor is selected only by carrier-ID ordering
            // already imposed by the live registry; it is a real participant,
            // not a spatial or health heuristic.
            PhenomenonCarrierSnapshot source = contributorSnapshots[0];
            TransferDomainOrchestrationRequest request = new TransferDomainOrchestrationRequest(carrier.Composition,
                new LawExpressionContext(true, LawExpression.Trap, false), carrier.BoundLaw.Law.Phenomenon, 1f,
                source, focalSnapshot, contexts);
            DomainBoundedOrchestrationResult bounded = DomainBoundedOrchestrator.Execute(request);
            if (!bounded.Succeeded)
                return new LocalizedDomainExecutionResult(carrier, new List<LocalizedDomainTargetExecution>
                {
                    new LocalizedDomainTargetExecution(focal.CarrierId, bounded, null, null, LocalizedDomainExecutionRejectionReason.OrchestrationRejected)
                }.AsReadOnly(), LocalizedDomainExecutionRejectionReason.None);
            DomainCommitPlan plan = DomainCommitPlanBuilder.Build(bounded);
            if (!plan.IsCommittable)
                return new LocalizedDomainExecutionResult(carrier, new List<LocalizedDomainTargetExecution>
                {
                    new LocalizedDomainTargetExecution(focal.CarrierId, bounded, plan, null, LocalizedDomainExecutionRejectionReason.CommitPlanRejected)
                }.AsReadOnly(), LocalizedDomainExecutionRejectionReason.None);
            DomainConflictParticipant participant = new DomainConflictParticipant(carrier.Id + ":" + focal.CarrierId,
                plan, carrier.Composition, null, DomainConflictEvidenceSet.Empty, carrier.Id,
                carrier.CreateProvenance(PhenomenonOperationKind.Transfer));
            ArbitratedDomainRuntimeCommitResult commit = new ArbitratedDomainRuntimeCommitter().Commit(
                ArbitratedDomainCommitPlanBuilder.Build(DomainConflictArbitrator.Arbitrate(new[] { participant })),
                DomainRuntimeCarrierRegistry.CreateAccessor(timestamp));
            return new LocalizedDomainExecutionResult(carrier, new List<LocalizedDomainTargetExecution>
            {
                new LocalizedDomainTargetExecution(focal.CarrierId, bounded, plan, commit.RuntimeResult,
                    commit.Succeeded ? LocalizedDomainExecutionRejectionReason.None : LocalizedDomainExecutionRejectionReason.RuntimeCommitRejected)
            }.AsReadOnly(), LocalizedDomainExecutionRejectionReason.None);
        }

        private static DomainOrchestrationContextSet BuildUnaryContexts(LocalizedDomainCarrier carrier,
            LocalizedDomainRuntimeTarget source, IReadOnlyList<LocalizedDomainRuntimeTarget> allTargets)
        {
            if (carrier.BoundLaw.Law.TerritoryPrinciple != LawTerritoryPrinciple.Catalysis)
                return DomainOrchestrationContextSet.Empty;

            List<PhenomenonCarrierSnapshot> recipients = new List<PhenomenonCarrierSnapshot>();
            for (int index = 0; index < allTargets.Count; index++)
            {
                LocalizedDomainRuntimeTarget target = allTargets[index];
                if (target == null || target.CarrierId.Equals(source.CarrierId)) continue;
                recipients.Add(new PhenomenonCarrierSnapshot(target.CarrierId, carrier.BoundLaw.Law.Phenomenon, target.Snapshot));
            }
            PhenomenonCarrierSnapshot sourceSnapshot = new PhenomenonCarrierSnapshot(source.CarrierId,
                carrier.BoundLaw.Law.Phenomenon, source.Snapshot);
            CatalysisOrchestrationContext context = new CatalysisOrchestrationContext(
                new PhenomenonCarrierTopology(sourceSnapshot, recipients));
            return new DomainOrchestrationContextSet(catalysis: new[]
            {
                new CatalysisContextBinding(carrier.BoundLaw.Law, source.CarrierId, context)
            });
        }
    }

    public sealed class LocalizedDomainTargetExecution
    {
        internal LocalizedDomainTargetExecution(PhenomenonCarrierId target, DomainBoundedOrchestrationResult bounded,
            DomainCommitPlan plan, DomainCommitResult commit, LocalizedDomainExecutionRejectionReason rejection)
        { Target = target; Bounded = bounded; Plan = plan; Commit = commit; RejectionReason = rejection; }
        public PhenomenonCarrierId Target { get; }
        public DomainBoundedOrchestrationResult Bounded { get; }
        public DomainCommitPlan Plan { get; }
        public DomainCommitResult Commit { get; }
        public LocalizedDomainExecutionRejectionReason RejectionReason { get; }
        public bool Succeeded => RejectionReason == LocalizedDomainExecutionRejectionReason.None;
    }

    /// <summary>One uncommitted planned effect. It is deliberately a thin
    /// envelope over the established bounded result and conflict participant.</summary>
    public sealed class LocalizedDomainPreparedProposal
    {
        internal LocalizedDomainPreparedProposal(LocalizedDomainCarrier carrier, PhenomenonCarrierId target,
            DomainBoundedOrchestrationResult bounded, DomainCommitPlan plan, DomainConflictParticipant participant)
        { Carrier = carrier; Target = target; Bounded = bounded; Plan = plan; Participant = participant; }
        public LocalizedDomainCarrier Carrier { get; }
        public PhenomenonCarrierId Target { get; }
        public DomainBoundedOrchestrationResult Bounded { get; }
        public DomainCommitPlan Plan { get; }
        public DomainConflictParticipant Participant { get; }
    }

    public sealed class LocalizedDomainPreparationResult
    {
        internal LocalizedDomainPreparationResult(LocalizedDomainCarrier carrier, IReadOnlyList<LocalizedDomainPreparedProposal> proposals,
            LocalizedDomainExecutionRejectionReason rejection)
        { Carrier = carrier; Proposals = proposals; RejectionReason = rejection; }
        public LocalizedDomainCarrier Carrier { get; }
        public IReadOnlyList<LocalizedDomainPreparedProposal> Proposals { get; }
        public LocalizedDomainExecutionRejectionReason RejectionReason { get; }
        public bool Succeeded => RejectionReason == LocalizedDomainExecutionRejectionReason.None;
        internal static LocalizedDomainPreparationResult Rejected(LocalizedDomainCarrier carrier, LocalizedDomainExecutionRejectionReason rejection)
        { return new LocalizedDomainPreparationResult(carrier, new List<LocalizedDomainPreparedProposal>().AsReadOnly(), rejection); }
    }

    public sealed class LocalizedDomainExecutionResult
    {
        internal LocalizedDomainExecutionResult(LocalizedDomainCarrier carrier, IReadOnlyList<LocalizedDomainTargetExecution> targets,
            LocalizedDomainExecutionRejectionReason rejection)
        { Carrier = carrier; Targets = targets; RejectionReason = rejection; }
        public LocalizedDomainCarrier Carrier { get; }
        public IReadOnlyList<LocalizedDomainTargetExecution> Targets { get; }
        public LocalizedDomainExecutionRejectionReason RejectionReason { get; }
        public bool Succeeded => RejectionReason == LocalizedDomainExecutionRejectionReason.None;
        internal static LocalizedDomainExecutionResult Rejected(LocalizedDomainCarrier carrier, LocalizedDomainExecutionRejectionReason rejection)
        { return new LocalizedDomainExecutionResult(carrier, new List<LocalizedDomainTargetExecution>().AsReadOnly(), rejection); }
    }
}
