using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Deterministic contracts-only verification. It intentionally calls no Territory evaluator or operation resolver.</summary>
    public static class DomainOrchestrationContractsVerification
    {
        public static bool TryRunAll(out string failure)
        {
            PhenomenonSemanticSnapshot heat; PhenomenonSemanticSnapshot potential;
            PhenomenonSemanticResult semantic;
            PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat, 1f, out heat, out semantic);
            PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Potential, 1f, out potential, out semantic);
            PhenomenonCarrierSnapshot a = new PhenomenonCarrierSnapshot(new PhenomenonCarrierId("A"), LawPhenomenon.Heat, heat);
            PhenomenonCarrierSnapshot b = new PhenomenonCarrierSnapshot(new PhenomenonCarrierId("B"), LawPhenomenon.Heat, heat);
            PhenomenonCarrierSnapshot c = new PhenomenonCarrierSnapshot(new PhenomenonCarrierId("C"), LawPhenomenon.Heat, heat);
            PhenomenonCarrierSnapshot wrong = new PhenomenonCarrierSnapshot(new PhenomenonCarrierId("P"), LawPhenomenon.Potential, potential);
            DomainLaw propagation; DomainLaw accumulation; DomainLaw synchronization; DomainLaw interference; DomainLaw catalysis;
            LawValidationResult lawValidation;
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, out propagation, out lawValidation);
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Accumulation, out accumulation, out lawValidation);
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Synchronization, out synchronization, out lawValidation);
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Interference, out interference, out lawValidation);
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Catalysis, out catalysis, out lawValidation);
            DomainComposition composition = DomainComposition.Empty;
            composition = DomainCompositionEditor.Add(composition, propagation).Resulting;
            PhenomenonRelationshipGroup group = CreateGroup(a.CarrierId, b.CarrierId);
            PropagationOrchestrationContext propagationContext = new PropagationOrchestrationContext(new PhenomenonCarrierTopology(a, new[] { b }), 1);
            AccumulationOrchestrationContext accumulationContext = new AccumulationOrchestrationContext(new PhenomenonConvergenceTopology(b, new[] { a }), 1);
            SynchronizationOrchestrationContext synchronizationContext = new SynchronizationOrchestrationContext(group, new[] { a, b });
            InterferenceOrchestrationContext interferenceContext = new InterferenceOrchestrationContext(a, new[] { c }, b, 1);
            CatalysisOrchestrationContext catalysisB = new CatalysisOrchestrationContext(new PhenomenonCarrierTopology(b, new[] { a }));
            CatalysisOrchestrationContext catalysisC = new CatalysisOrchestrationContext(new PhenomenonCarrierTopology(c, new[] { a }));
            DomainOrchestrationContextSet contexts = new DomainOrchestrationContextSet(
                new[] { new DomainLawContextBinding<PropagationOrchestrationContext>(propagation, propagationContext) },
                new[] { new DomainLawContextBinding<AccumulationOrchestrationContext>(accumulation, accumulationContext) },
                new[] { new DomainLawContextBinding<SynchronizationOrchestrationContext>(synchronization, synchronizationContext) },
                new[] { new DomainLawContextBinding<InterferenceOrchestrationContext>(interference, interferenceContext) },
                new[] { new CatalysisContextBinding(catalysis, b.CarrierId, catalysisB), new CatalysisContextBinding(catalysis, c.CarrierId, catalysisC) });
            LawExpressionContext expression = new LawExpressionContext(true, LawExpression.Projectile, false);
            UnaryDomainOrchestrationRequest unary = new UnaryDomainOrchestrationRequest(composition, expression, LawPhenomenon.Heat, PhenomenonOperationKind.Add, 1f, a, contexts);
            TransferDomainOrchestrationRequest transfer = new TransferDomainOrchestrationRequest(composition, expression, LawPhenomenon.Heat, 1f, a, b, contexts);
            PropagationOrchestrationContext foundPropagation; AccumulationOrchestrationContext foundAccumulation; SynchronizationOrchestrationContext foundSynchronization; InterferenceOrchestrationContext foundInterference; CatalysisOrchestrationContext foundCatalysis;
            bool contextPass = contexts.TryGetPropagation(propagation, out foundPropagation) && ReferenceEquals(foundPropagation, propagationContext)
                && contexts.TryGetAccumulation(accumulation, out foundAccumulation) && ReferenceEquals(foundAccumulation, accumulationContext)
                && contexts.TryGetSynchronization(synchronization, out foundSynchronization) && ReferenceEquals(foundSynchronization, synchronizationContext)
                && contexts.TryGetInterference(interference, out foundInterference) && ReferenceEquals(foundInterference, interferenceContext)
                && contexts.TryGetCatalysisForSource(catalysis, b.CarrierId, out foundCatalysis) && ReferenceEquals(foundCatalysis, catalysisB)
                && contexts.TryGetCatalysisForSource(catalysis, c.CarrierId, out foundCatalysis) && ReferenceEquals(foundCatalysis, catalysisC);
            EffectiveUnaryDomainIntent effective = new EffectiveUnaryDomainIntent(LawPhenomenon.Heat, PhenomenonOperationKind.Add, 1f, PhenomenonOperationKind.Remove, 3f, true, true, 3);
            NormalizedSemanticTransition propagationTransition = new NormalizedSemanticTransition(new DomainTransitionSequenceId(1), DomainOrchestrationGeneration.Generation1, new DomainTransitionSequenceId(0), b.CarrierId, LawPhenomenon.Heat, heat, heat, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, propagation, true);
            NormalizedSemanticTransition transferSource = new NormalizedSemanticTransition(new DomainTransitionSequenceId(2), DomainOrchestrationGeneration.Generation0, null, a.CarrierId, LawPhenomenon.Heat, heat, heat, PhenomenonOperationKind.Transfer, 1f, DomainTransitionOrigin.Base, null, true);
            NormalizedSemanticTransition transferTarget = new NormalizedSemanticTransition(new DomainTransitionSequenceId(3), DomainOrchestrationGeneration.Generation0, null, b.CarrierId, LawPhenomenon.Heat, heat, heat, PhenomenonOperationKind.Transfer, 1f, DomainTransitionOrigin.Base, null, true);
            ProposalCollision collision = new ProposalCollision(b.CarrierId, LawPhenomenon.Heat, DomainOrchestrationGeneration.Generation1, new[] { new DomainTransitionSequenceId(4), new DomainTransitionSequenceId(5) }, new[] { propagation, synchronization });
            bool validUnary = DomainOrchestrationRequestValidator.Validate(unary).IsValid;
            bool transferRejectedForUnary = DomainOrchestrationRequestValidator.Validate(new UnaryDomainOrchestrationRequest(composition, expression, LawPhenomenon.Heat, PhenomenonOperationKind.Transfer, 1f, a, contexts)).RejectionReason == DomainOrchestrationRequestRejectionReason.InvalidOperation;
            bool validTransfer = DomainOrchestrationRequestValidator.Validate(transfer).IsValid;
            bool mismatchRejected = DomainOrchestrationRequestValidator.Validate(new UnaryDomainOrchestrationRequest(composition, expression, LawPhenomenon.Heat, PhenomenonOperationKind.Add, 1f, wrong, contexts)).RejectionReason == DomainOrchestrationRequestRejectionReason.CarrierPhenomenonMismatch;
            bool negativeRejected = DomainOrchestrationRequestValidator.Validate(new UnaryDomainOrchestrationRequest(composition, expression, LawPhenomenon.Heat, PhenomenonOperationKind.Add, -1f, a, contexts)).RejectionReason == DomainOrchestrationRequestRejectionReason.InvalidMagnitude;
            bool generationsBounded = (int)DomainOrchestrationGeneration.Generation0 == 0 && (int)DomainOrchestrationGeneration.Generation1 == 1 && (int)DomainOrchestrationGeneration.Generation2 == 2;
            bool propagationRecord = propagationTransition.SequenceId.Value == 1 && propagationTransition.Origin == DomainTransitionOrigin.Propagation && propagationTransition.Generation == DomainOrchestrationGeneration.Generation1;
            bool transferRecords = transferSource.SequenceId.Value != transferTarget.SequenceId.Value && !transferSource.CarrierId.Equals(transferTarget.CarrierId);
            bool collisionContract = collision.IsUnresolved && collision.InvolvedTransitionIds.Count == 2 && collision.OriginatingLaws.Count == 2;
            bool pass = validUnary && transferRejectedForUnary && validTransfer && mismatchRejected && negativeRejected && contextPass && effective.IsStructurallyValid && generationsBounded && propagationRecord && transferRecords && collisionContract;
            failure = pass ? null : "Orchestration contracts verification failed: unary=" + validUnary + ", unaryTransfer=" + transferRejectedForUnary + ", transfer=" + validTransfer + ", mismatch=" + mismatchRejected + ", negative=" + negativeRejected + ", context=" + contextPass + ", effective=" + effective.IsStructurallyValid + ", generation=" + generationsBounded + ", propagation=" + propagationRecord + ", transferRecords=" + transferRecords + ", collision=" + collisionContract;
            return pass;
        }

        private static PhenomenonRelationshipGroup CreateGroup(PhenomenonCarrierId first, PhenomenonCarrierId second)
        {
            return new PhenomenonRelationshipGroup(new PhenomenonRelationshipGroupId("sync"), LawPhenomenon.Heat, new[] { first, second });
        }
    }
}
