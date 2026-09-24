using System.Collections.Generic;

namespace Cave.Domain
{
    public static class DomainGenerationTwoExecutorVerification
    {
        public static bool TryRunAll(out string failure)
        {
            DomainLaw propagation = Law(LawTerritoryPrinciple.Propagation); DomainLaw synchronization = Law(LawTerritoryPrinciple.Synchronization); DomainLaw accumulation = Law(LawTerritoryPrinciple.Accumulation); DomainLaw catalysis = Law(LawTerritoryPrinciple.Catalysis);
            PhenomenonSemanticSnapshot zero = Snapshot(0f); PhenomenonSemanticSnapshot one = Snapshot(1f); PhenomenonSemanticSnapshot two = Snapshot(2f); PhenomenonSemanticSnapshot three = Snapshot(3f);

            DomainBoundedOrchestrationResult g0Trigger = DomainBoundedOrchestrator.Execute(new UnaryDomainOrchestrationRequest(Composition(catalysis), Expression(), LawPhenomenon.Heat, PhenomenonOperationKind.Add, 1f, Carrier("A", 1f), Context(catalysis, "A", Carrier("D", 0f))));
            UnaryDomainGenerationZeroResult cleanZero = Zero(Composition(catalysis), Context(catalysis, "C", Carrier("D", 0f)));
            NormalizedSemanticTransition clean = Transition(1, "C", propagation, one, three, PhenomenonOperationKind.Add, 2f, DomainTransitionOrigin.Propagation, true);
            DomainResolvedGenerationOneResult cleanResolved = Resolve(cleanZero, Read(clean), Empty<ProposalCollision>());
            DomainGenerationTwoResult cleanTwo = DomainGenerationTwoExecutor.Execute(cleanResolved);

            UnaryDomainGenerationZeroResult composedZero = Zero(Composition(propagation, synchronization, catalysis), Context(catalysis, "B", Carrier("D", 0f)));
            NormalizedSemanticTransition composedA = Transition(1, "B", propagation, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition composedB = Transition(2, "B", synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            NormalizedSemanticTransition otherA = Transition(3, "E", propagation, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition otherB = Transition(4, "E", synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            UnaryDomainGenerationOneResult composedRaw = Raw(composedZero, Read(composedA, composedB, otherA, otherB), Read(Collision("E", otherA, otherB), Collision("B", composedA, composedB)));
            DomainResolvedGenerationOneResult composedResolved = DomainResolvedGenerationOneBuilder.Resolve(composedRaw);
            DomainGenerationTwoResult composedTwo = DomainGenerationTwoExecutor.Execute(composedResolved);
            DomainGenerationTwoResult composedAgain = DomainGenerationTwoExecutor.Execute(composedResolved);

            DomainOrchestrationContextSet recipientContexts = Context(catalysis, "C", Carrier("B", 0f));
            UnaryDomainGenerationZeroResult recipientZero = Zero(Composition(propagation, synchronization, catalysis), recipientContexts);
            NormalizedSemanticTransition recipientA = Transition(1, "B", propagation, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition recipientB = Transition(2, "B", synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            NormalizedSemanticTransition recipientSource = Transition(3, "C", propagation, one, three, PhenomenonOperationKind.Add, 2f, DomainTransitionOrigin.Propagation, true);
            DomainGenerationTwoResult resolvedRecipientTwo = DomainGenerationTwoExecutor.Execute(Resolve(recipientZero, Read(recipientA, recipientB, recipientSource), Read(Collision("B", recipientA, recipientB))));

            UnaryDomainGenerationZeroResult unresolvedZero = Zero(Composition(propagation, synchronization, catalysis), Context(catalysis, "B", Carrier("D", 0f)));
            NormalizedSemanticTransition unresolvedA = Transition(1, "B", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition unresolvedB = Transition(2, "B", synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            DomainResolvedGenerationOneResult unresolvedSource = Resolve(unresolvedZero, Read(unresolvedA, unresolvedB), Read(Collision("B", unresolvedA, unresolvedB)));
            DomainGenerationTwoResult unresolvedSourceTwo = DomainGenerationTwoExecutor.Execute(unresolvedSource);

            UnaryDomainGenerationZeroResult unresolvedRecipientZero = Zero(Composition(propagation, synchronization, catalysis), Context(catalysis, "C", Carrier("B", 0f)));
            NormalizedSemanticTransition unresolvedRecipientA = Transition(1, "B", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition unresolvedRecipientB = Transition(2, "B", synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            NormalizedSemanticTransition unresolvedRecipientSource = Transition(3, "C", propagation, one, three, PhenomenonOperationKind.Add, 2f, DomainTransitionOrigin.Propagation, true);
            DomainGenerationTwoResult unresolvedRecipientTwo = DomainGenerationTwoExecutor.Execute(Resolve(unresolvedRecipientZero, Read(unresolvedRecipientA, unresolvedRecipientB, unresolvedRecipientSource), Read(Collision("B", unresolvedRecipientA, unresolvedRecipientB))));

            UnaryDomainGenerationZeroResult cleanRecipientZero = Zero(Composition(propagation, catalysis), Context(catalysis, "C", Carrier("B", 0f)));
            NormalizedSemanticTransition cleanRecipient = Transition(1, "B", propagation, one, three, PhenomenonOperationKind.Add, 2f, DomainTransitionOrigin.Propagation, true);
            NormalizedSemanticTransition cleanRecipientSource = Transition(2, "C", propagation, one, three, PhenomenonOperationKind.Add, 2f, DomainTransitionOrigin.Propagation, true);
            DomainGenerationTwoResult cleanRecipientTwo = DomainGenerationTwoExecutor.Execute(Resolve(cleanRecipientZero, Read(cleanRecipient, cleanRecipientSource), Empty<ProposalCollision>()));

            TransferDomainGenerationZeroResult accumulationZero = ZeroTransfer(Composition(accumulation, catalysis), Context(catalysis, "C", Carrier("B", 0f)));
            NormalizedSemanticTransition accumulationOne = Transition(2, "B", accumulation, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Accumulation, true);
            NormalizedSemanticTransition accumulationTwo = Transition(3, "B", accumulation, two, three, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Accumulation, true);
            NormalizedSemanticTransition accumulationSource = Transition(4, "C", accumulation, one, three, PhenomenonOperationKind.Add, 2f, DomainTransitionOrigin.Accumulation, true);
            DomainGenerationTwoResult accumulationTwoResult = DomainGenerationTwoExecutor.Execute(Resolve(accumulationZero, Read(accumulationOne, accumulationTwo, accumulationSource), Empty<ProposalCollision>()));

            UnaryDomainGenerationZeroResult lowHighZero = Zero(Composition(catalysis), Context(catalysis, "L", Carrier("D", 0f)));
            NormalizedSemanticTransition lowHigh = Transition(1, "L", propagation, Snapshot(-2f), three, PhenomenonOperationKind.Add, 5f, DomainTransitionOrigin.Propagation, true);
            DomainGenerationTwoResult lowHighTwo = DomainGenerationTwoExecutor.Execute(Resolve(lowHighZero, Read(lowHigh), Empty<ProposalCollision>()));

            DomainBoundedOrchestrationResult g2Collision = DomainBoundedOrchestrator.Execute(new UnaryDomainOrchestrationRequest(Composition(propagation, catalysis), Expression(), LawPhenomenon.Heat, PhenomenonOperationKind.Add, 1f, Carrier("A", 1f), new DomainOrchestrationContextSet(
                new[] { new DomainLawContextBinding<PropagationOrchestrationContext>(propagation, new PropagationOrchestrationContext(new PhenomenonCarrierTopology(Carrier("A", 1f), new[] { Carrier("B", 1f) }), 1)) }, null, null, null,
                new[] { new CatalysisContextBinding(catalysis, new PhenomenonCarrierId("A"), new CatalysisOrchestrationContext(new PhenomenonCarrierTopology(Carrier("A", 1f), new[] { Carrier("D", 0f) }))), new CatalysisContextBinding(catalysis, new PhenomenonCarrierId("B"), new CatalysisOrchestrationContext(new PhenomenonCarrierTopology(Carrier("B", 1f), new[] { Carrier("D", 0f) }))) })));

            DomainGenerationTwoResult rejectedNull = DomainGenerationTwoExecutor.Execute(null);
            DomainResolvedGenerationOneResult invalidNext = new DomainResolvedGenerationOneResult(cleanResolved.UnaryRaw, null, Empty<DomainProposalCompositionResult>(), Empty<DomainComposedTransition>(), cleanResolved.AuthoritativeTransitions, cleanResolved.PostGenerationOneStates, Empty<DomainUnresolvedGenerationOneKey>(), -1);
            DomainGenerationTwoResult rejectedNext = DomainGenerationTwoExecutor.Execute(invalidNext);
            DomainResolvedGenerationOneResult failedResolved = new DomainResolvedGenerationOneResult(cleanResolved.UnaryRaw, null, Empty<DomainProposalCompositionResult>(), Empty<DomainComposedTransition>(), Empty<DomainAuthoritativeGenerationOneTransition>(), Empty<DomainPostGenerationOneState>(), Empty<DomainUnresolvedGenerationOneKey>(), -1, DomainResolvedGenerationOneRejectionReason.GenerationOneRequired);
            DomainResolvedGenerationOneResult malformedResolved = new DomainResolvedGenerationOneResult(cleanResolved.UnaryRaw, null, Empty<DomainProposalCompositionResult>(), Empty<DomainComposedTransition>(), null, Empty<DomainPostGenerationOneState>(), Empty<DomainUnresolvedGenerationOneKey>(), 2);
            DomainGenerationTwoResult rejectedFailed = DomainGenerationTwoExecutor.Execute(failedResolved);
            DomainGenerationTwoResult rejectedMalformed = DomainGenerationTwoExecutor.Execute(malformedResolved);

            CatalysisObservationTrace trace;
            bool composedObserved = Find(composedTwo, 5, out trace) && trace.SourceCarrierId.Equals(new PhenomenonCarrierId("B")) && trace.Triggered && trace.GeneratedTransitionCount == 1;
            bool rawSuppressed = !Find(composedTwo, 1, out trace) && !Find(composedTwo, 2, out trace);
            bool resolvedRecipientState = Find(resolvedRecipientTwo, 3, out trace) && trace.RecipientBlocks.Count == 0 && resolvedRecipientTwo.EvaluationResults.Count == 1 && resolvedRecipientTwo.EvaluationResults[0].RecipientResults[0].RejectionReason == CatalysisRecipientRejectionReason.AlreadyHigh;
            bool unresolvedRecipientBlocked = Find(unresolvedRecipientTwo, 3, out trace) && trace.RecipientBlocks.Count == 1 && trace.RecipientBlocks[0].CarrierId.Equals(new PhenomenonCarrierId("B"));
            bool cleanRecipientState = Find(cleanRecipientTwo, 2, out trace) && trace.RecipientBlocks.Count == 0 && cleanRecipientTwo.EvaluationResults[0].RecipientResults[0].RejectionReason == CatalysisRecipientRejectionReason.AlreadyHigh;
            bool accumulationRecipientState = accumulationTwoResult.EvaluationResults.Count == 1 && accumulationTwoResult.EvaluationResults[0].RecipientResults[0].RejectionReason == CatalysisRecipientRejectionReason.AlreadyHigh;
            bool g0Ok = g0Trigger.Succeeded && g0Trigger.GenerationTwo.Transitions.Count == 1 && g0Trigger.GenerationTwo.Transitions[0].SequenceId.Value == 1 && g0Trigger.GenerationTwo.Transitions[0].ParentTransitionId.Value.Value == 0;
            bool cleanOk = cleanResolved.Succeeded && cleanTwo.Succeeded && cleanTwo.Transitions.Count == 1 && cleanTwo.Transitions[0].SequenceId.Value == cleanResolved.NextTransitionId.Value && cleanTwo.Transitions[0].ParentTransitionId.Value.Value == 1;
            bool composedOk = composedResolved.ComposedTransitions.Count == 2 && composedResolved.ComposedTransitions[0].SequenceId.Value == 5 && composedResolved.ComposedTransitions[1].SequenceId.Value == 6 && composedResolved.NextTransitionId.Value == 7 && composedTwo.Succeeded && composedTwo.Transitions.Count == 1 && composedTwo.Transitions[0].SequenceId.Value == 7 && composedTwo.Transitions[0].ParentTransitionId.Value.Value == 5 && composedObserved && rawSuppressed;
            bool deterministicOk = composedAgain.Transitions.Count == composedTwo.Transitions.Count && composedAgain.Transitions[0].SequenceId.Value == composedTwo.Transitions[0].SequenceId.Value && composedAgain.Transitions[0].ParentTransitionId.Value.Value == composedTwo.Transitions[0].ParentTransitionId.Value.Value && composedRaw.Transitions[0] == composedA && composedResolved.CompositionResults.Count == 2;
            bool recipientOk = resolvedRecipientState && !Find(resolvedRecipientTwo, 1, out trace) && !Find(resolvedRecipientTwo, 2, out trace);
            bool unresolvedOk = unresolvedSource.UnresolvedKeys.Count == 1 && !Find(unresolvedSourceTwo, 1, out trace) && !Find(unresolvedSourceTwo, 2, out trace) && unresolvedSourceTwo.EvaluationResults.Count == 0 && unresolvedRecipientBlocked;
            bool stateOk = cleanRecipientState && accumulationRecipientState;
            bool lowOk = lowHighTwo.Succeeded && Find(lowHighTwo, 1, out trace) && !trace.Triggered && lowHighTwo.Transitions.Count == 0;
            bool collisionOk = g2Collision.Succeeded && g2Collision.GenerationTwo.Transitions.Count == 2 && g2Collision.GenerationTwo.ProposalCollisions.Count == 1 && !g2Collision.GenerationTwo.Transitions[0].IsUnambiguous && !g2Collision.GenerationTwo.Transitions[1].IsUnambiguous && g2Collision.GenerationTwo.Transitions[0].ParentTransitionId.Value.Value != g2Collision.GenerationTwo.Transitions[1].ParentTransitionId.Value.Value && g2Collision.FinalResolution.Succeeded && g2Collision.ComposedGenerationTwoTransitions.Count == 1 && g2Collision.AllAuthoritativeTransitions.Count == 3 && g2Collision.AllAuthoritativeTransitions[2].IsComposed && g2Collision.HasRawProposalCollisions && !g2Collision.HasUnresolvedProposalCollisions;
            bool rejectOk = !rejectedNull.Succeeded && rejectedNull.RejectionReason == DomainGenerationTwoRejectionReason.ResolvedGenerationOneRequired && rejectedNull.Transitions.Count == 0 && rejectedNull.Observations.Count == 0 && !rejectedNext.Succeeded && rejectedNext.Transitions.Count == 0 && rejectedNext.Observations.Count == 0 && !rejectedFailed.Succeeded && rejectedFailed.Observations.Count == 0 && !rejectedMalformed.Succeeded && rejectedMalformed.Transitions.Count == 0;
            bool pass = g0Ok && cleanOk && composedOk && deterministicOk && recipientOk && unresolvedOk && stateOk && lowOk && collisionOk && rejectOk;
            failure = pass ? null : "Generation-2 orchestration verification failed. g0=" + g0Ok + " clean=" + cleanOk + " composed=" + composedOk + " deterministic=" + deterministicOk + " recipient=" + recipientOk + " unresolved=" + unresolvedOk + " state=" + stateOk + " low=" + lowOk + " collision=" + collisionOk + " reject=" + rejectOk;
            return pass;
        }

        private static DomainResolvedGenerationOneResult Resolve(UnaryDomainGenerationZeroResult zero, IReadOnlyList<NormalizedSemanticTransition> transitions, IReadOnlyList<ProposalCollision> collisions) { return DomainResolvedGenerationOneBuilder.Resolve(Raw(zero, transitions, collisions)); }
        private static DomainResolvedGenerationOneResult Resolve(TransferDomainGenerationZeroResult zero, IReadOnlyList<NormalizedSemanticTransition> transitions, IReadOnlyList<ProposalCollision> collisions) { return DomainResolvedGenerationOneBuilder.Resolve(new TransferDomainGenerationOneResult(zero, Empty<DomainLawOutcomeTrace>(), Empty<AccumulationEvaluationResult>(), Empty<OriginalTransferSourceSkip>(), transitions, collisions, DomainGenerationOneRejectionReason.None)); }
        private static UnaryDomainGenerationOneResult Raw(UnaryDomainGenerationZeroResult zero, IReadOnlyList<NormalizedSemanticTransition> transitions, IReadOnlyList<ProposalCollision> collisions) { return new UnaryDomainGenerationOneResult(zero, Empty<DomainLawOutcomeTrace>(), Empty<PropagationEvaluationResult>(), Empty<SynchronizationEvaluationResult>(), transitions, collisions, DomainGenerationOneRejectionReason.None); }
        private static UnaryDomainGenerationZeroResult Zero(DomainComposition composition, DomainOrchestrationContextSet contexts) { return DomainGenerationZeroExecutor.Execute(new UnaryDomainOrchestrationRequest(composition, Expression(), LawPhenomenon.Heat, PhenomenonOperationKind.Add, 0f, Carrier("A", 0f), contexts)); }
        private static TransferDomainGenerationZeroResult ZeroTransfer(DomainComposition composition, DomainOrchestrationContextSet contexts) { return DomainGenerationZeroExecutor.Execute(new TransferDomainOrchestrationRequest(composition, Expression(), LawPhenomenon.Heat, 0f, Carrier("S", 0f), Carrier("T", 0f), contexts)); }
        private static DomainOrchestrationContextSet Context(DomainLaw law, string source, params PhenomenonCarrierSnapshot[] recipients) { return new DomainOrchestrationContextSet(null, null, null, null, new[] { new CatalysisContextBinding(law, new PhenomenonCarrierId(source), new CatalysisOrchestrationContext(new PhenomenonCarrierTopology(Carrier(source, 0f), recipients))) }); }
        private static LawExpressionContext Expression() { return new LawExpressionContext(true, LawExpression.Projectile, false); }
        private static DomainLaw Law(LawTerritoryPrinciple territory) { DomainLaw law; LawValidationResult validation; DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, territory, out law, out validation); return law; }
        private static DomainComposition Composition(params DomainLaw[] laws) { DomainComposition composition = DomainComposition.Empty; for (int index = 0; index < laws.Length; index++) composition = DomainCompositionEditor.Add(composition, laws[index]).Resulting; return composition; }
        private static PhenomenonCarrierSnapshot Carrier(string id, float value) { return new PhenomenonCarrierSnapshot(new PhenomenonCarrierId(id), LawPhenomenon.Heat, Snapshot(value)); }
        private static PhenomenonSemanticSnapshot Snapshot(float value) { PhenomenonSemanticSnapshot snapshot; PhenomenonSemanticResult result; PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat, value, out snapshot, out result); return snapshot; }
        private static NormalizedSemanticTransition Transition(int id, string carrier, DomainLaw law, PhenomenonSemanticSnapshot before, PhenomenonSemanticSnapshot after, PhenomenonOperationKind operation, float magnitude, DomainTransitionOrigin origin, bool unambiguous) { return new NormalizedSemanticTransition(new DomainTransitionSequenceId(id), DomainOrchestrationGeneration.Generation1, new DomainTransitionSequenceId(0), new PhenomenonCarrierId(carrier), LawPhenomenon.Heat, before, after, operation, magnitude, origin, law, unambiguous); }
        private static ProposalCollision Collision(string carrier, params NormalizedSemanticTransition[] transitions) { List<DomainTransitionSequenceId> ids = new List<DomainTransitionSequenceId>(); List<DomainLaw> laws = new List<DomainLaw>(); for (int i = 0; i < transitions.Length; i++) { ids.Add(transitions[i].SequenceId); if (!laws.Contains(transitions[i].OriginatingLaw)) laws.Add(transitions[i].OriginatingLaw); } return new ProposalCollision(new PhenomenonCarrierId(carrier), LawPhenomenon.Heat, DomainOrchestrationGeneration.Generation1, ids, laws); }
        private static bool Find(DomainGenerationTwoResult result, int id, out CatalysisObservationTrace trace) { for (int i = 0; i < result.Observations.Count; i++) if (result.Observations[i].SourceTransitionId.Value == id) { trace = result.Observations[i]; return true; } trace = null; return false; }
        private static IReadOnlyList<T> Read<T>(params T[] values) { return new List<T>(values).AsReadOnly(); }
        private static IReadOnlyList<T> Empty<T>() { return new List<T>().AsReadOnly(); }
    }
}
