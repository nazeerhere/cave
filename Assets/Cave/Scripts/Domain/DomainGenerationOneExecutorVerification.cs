namespace Cave.Domain
{
    public static class DomainGenerationOneExecutorVerification
    {
        public static bool TryRunAll(out string failure)
        {
            LawExpressionContext expression = new LawExpressionContext(true, LawExpression.Projectile, false);
            DomainLaw propagation = Law(LawTerritoryPrinciple.Propagation); DomainLaw synchronization = Law(LawTerritoryPrinciple.Synchronization);
            DomainLaw accumulation = Law(LawTerritoryPrinciple.Accumulation); DomainLaw reversal = Law(LawTerritoryPrinciple.Reversal); DomainLaw interference = Law(LawTerritoryPrinciple.Interference);
            PhenomenonCarrierSnapshot a = Carrier("A", 0f); PhenomenonCarrierSnapshot b = Carrier("B", 0f); PhenomenonCarrierSnapshot c = Carrier("C", 1f);
            UnaryDomainGenerationZeroResult noneZero = ZeroUnary(DomainComposition.Empty, expression, a, DomainOrchestrationContextSet.Empty, 1f);
            UnaryDomainGenerationOneResult none = DomainGenerationOneExecutor.Execute(noneZero);
            DomainOrchestrationContextSet propagationContexts = new DomainOrchestrationContextSet(new[] { new DomainLawContextBinding<PropagationOrchestrationContext>(propagation, new PropagationOrchestrationContext(new PhenomenonCarrierTopology(a, new[] { b, c }), 2)) });
            UnaryDomainGenerationOneResult propagated = DomainGenerationOneExecutor.Execute(ZeroUnary(Composition(propagation), expression, a, propagationContexts, 2f));
            DomainOrchestrationContextSet mismatchPropagation = new DomainOrchestrationContextSet(new[] { new DomainLawContextBinding<PropagationOrchestrationContext>(propagation, new PropagationOrchestrationContext(new PhenomenonCarrierTopology(Carrier("Other", 0f), new[] { b }), 1)) });
            UnaryDomainGenerationOneResult propagationMismatch = DomainGenerationOneExecutor.Execute(ZeroUnary(Composition(propagation), expression, a, mismatchPropagation, 1f));
            PhenomenonRelationshipGroup group = new PhenomenonRelationshipGroup(new PhenomenonRelationshipGroupId("g"), LawPhenomenon.Heat, new[] { a.CarrierId, b.CarrierId, c.CarrierId });
            DomainOrchestrationContextSet synchronizationContexts = new DomainOrchestrationContextSet(null, null, new[] { new DomainLawContextBinding<SynchronizationOrchestrationContext>(synchronization, new SynchronizationOrchestrationContext(group, new[] { a, b, c })) });
            UnaryDomainGenerationOneResult synchronized = DomainGenerationOneExecutor.Execute(ZeroUnary(Composition(synchronization), expression, a, synchronizationContexts, 1f));
            DomainOrchestrationContextSet transformedContexts = new DomainOrchestrationContextSet(
                new[] { new DomainLawContextBinding<PropagationOrchestrationContext>(propagation, new PropagationOrchestrationContext(new PhenomenonCarrierTopology(a, new[] { b }), 1)) }, null, null,
                new[] { new DomainLawContextBinding<InterferenceOrchestrationContext>(interference, new InterferenceOrchestrationContext(Carrier("R", 0f), new[] { Carrier("I", 0f) }, a, 2)) });
            UnaryDomainGenerationOneResult transformed = DomainGenerationOneExecutor.Execute(ZeroUnary(Composition(reversal, interference, propagation), expression, a, transformedContexts, 1f));
            DomainOrchestrationContextSet collisionContexts = new DomainOrchestrationContextSet(
                new[] { new DomainLawContextBinding<PropagationOrchestrationContext>(propagation, new PropagationOrchestrationContext(new PhenomenonCarrierTopology(a, new[] { b }), 1)) }, null,
                new[] { new DomainLawContextBinding<SynchronizationOrchestrationContext>(synchronization, new SynchronizationOrchestrationContext(new PhenomenonRelationshipGroup(new PhenomenonRelationshipGroupId("collision"), LawPhenomenon.Heat, new[] { a.CarrierId, b.CarrierId }), new[] { a, b })) });
            UnaryDomainGenerationOneResult collision = DomainGenerationOneExecutor.Execute(ZeroUnary(Composition(propagation, synchronization), expression, a, collisionContexts, 1f));
            TransferDomainGenerationZeroResult transferZero = DomainGenerationZeroExecutor.Execute(new TransferDomainOrchestrationRequest(Composition(accumulation), expression, LawPhenomenon.Heat, 1f, Carrier("S", 2f), Carrier("T", 0f),
                new DomainOrchestrationContextSet(null, new[] { new DomainLawContextBinding<AccumulationOrchestrationContext>(accumulation, new AccumulationOrchestrationContext(new PhenomenonConvergenceTopology(Carrier("T", 0f), new[] { Carrier("S", 2f), Carrier("X", 2f), Carrier("Y", 2f) }), 3)) })));
            TransferDomainGenerationOneResult accumulated = DomainGenerationOneExecutor.Execute(transferZero);
            UnaryDomainGenerationZeroResult invalidZero = DomainGenerationZeroExecutor.Execute(new UnaryDomainOrchestrationRequest(DomainComposition.Empty, expression, LawPhenomenon.Heat, PhenomenonOperationKind.Transfer, 1f, a, DomainOrchestrationContextSet.Empty));
            UnaryDomainGenerationOneResult blocked = DomainGenerationOneExecutor.Execute(invalidZero);

            bool pass = none.Succeeded && none.Transitions.Count == 0 && none.ProposalCollisions.Count == 0 && none.MaximumGeneration == DomainOrchestrationGeneration.Generation0
                && propagated.Succeeded && propagated.Transitions.Count == 2 && propagated.Transitions[0].SequenceId.Value == 1 && propagated.Transitions[1].SequenceId.Value == 2 && Value(propagated.Transitions[0].After) == 2f && Value(propagated.Transitions[1].After) == 3f && propagated.Transitions[0].ParentTransitionId.Value.Value == 0
                && propagationMismatch.Succeeded && propagationMismatch.Transitions.Count == 0 && propagationMismatch.RelationalOutcomes[0].Reason == DomainLawOutcomeReason.ContextMismatch
                && synchronized.Succeeded && synchronized.Transitions.Count == 2 && synchronized.Transitions[0].ParentTransitionId.Value.Value == 0 && Value(synchronized.Transitions[0].After) == 1f && Value(synchronized.Transitions[1].After) == 2f
                && transformed.Succeeded && transformed.Transitions.Count == 1 && transformed.Transitions[0].Operation == PhenomenonOperationKind.Remove && transformed.Transitions[0].Magnitude == 2f && Value(transformed.Transitions[0].After) == -2f
                && collision.Succeeded && collision.ProposalCollisions.Count == 1 && !collision.Transitions[0].IsUnambiguous && !collision.Transitions[1].IsUnambiguous && collision.ProposalCollisions[0].InvolvedTransitionIds.Count == 2
                && accumulated.Succeeded && accumulated.Transitions.Count == 4 && accumulated.Transitions[0].SequenceId.Value == 2 && accumulated.Transitions[1].SequenceId.Value == 3 && accumulated.Transitions[2].SequenceId.Value == 4 && accumulated.Transitions[3].SequenceId.Value == 5 && Value(accumulated.Transitions[1].Before) == 1f && Value(accumulated.Transitions[1].After) == 2f && Value(accumulated.Transitions[3].Before) == 2f && Value(accumulated.Transitions[3].After) == 3f && accumulated.Transitions[0].ParentTransitionId.Value.Value == 1 && accumulated.Transitions[2].ParentTransitionId.Value.Value == 3 && accumulated.ProposalCollisions.Count == 0 && accumulated.OriginalTransferSourceSkips.Count == 1 && accumulated.OriginalTransferSourceSkips[0].CarrierId.Equals(new PhenomenonCarrierId("S"))
                && !blocked.Succeeded && blocked.RejectionReason == DomainGenerationOneRejectionReason.GenerationZeroRequired;
            failure = pass ? null : "Generation-1 orchestration verification failed.";
            return pass;
        }
        private static UnaryDomainGenerationZeroResult ZeroUnary(DomainComposition composition, LawExpressionContext expression, PhenomenonCarrierSnapshot target, DomainOrchestrationContextSet contexts, float magnitude)
        { return DomainGenerationZeroExecutor.Execute(new UnaryDomainOrchestrationRequest(composition, expression, LawPhenomenon.Heat, PhenomenonOperationKind.Add, magnitude, target, contexts)); }
        private static DomainLaw Law(LawTerritoryPrinciple territory) { DomainLaw law; LawValidationResult validation; DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, territory, out law, out validation); return law; }
        private static DomainComposition Composition(params DomainLaw[] laws) { DomainComposition composition = DomainComposition.Empty; for (int index = 0; index < laws.Length; index++) composition = DomainCompositionEditor.Add(composition, laws[index]).Resulting; return composition; }
        private static PhenomenonCarrierSnapshot Carrier(string id, float value) { PhenomenonSemanticSnapshot snapshot; PhenomenonSemanticResult result; PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat, value, out snapshot, out result); return new PhenomenonCarrierSnapshot(new PhenomenonCarrierId(id), LawPhenomenon.Heat, snapshot); }
        private static float Value(PhenomenonSemanticSnapshot snapshot) { return snapshot != null ? snapshot.SemanticValue : float.NaN; }
    }
}
