using System.Collections.Generic;
namespace Cave.Domain
{
    public static class DomainProposalComposerVerification
    {
        public static bool TryRunAll(out string failure)
        {
            DomainLaw propagation = Law(LawTerritoryPrinciple.Propagation); DomainLaw synchronization = Law(LawTerritoryPrinciple.Synchronization); DomainLaw catalysis = Law(LawTerritoryPrinciple.Catalysis);
            PhenomenonSemanticSnapshot zero = Snapshot(0f); PhenomenonSemanticSnapshot one = Snapshot(1f); PhenomenonSemanticSnapshot two = Snapshot(2f); PhenomenonSemanticSnapshot three = Snapshot(3f); PhenomenonSemanticSnapshot low = Snapshot(-2f);
            NormalizedSemanticTransition addOne = Transition(1, 0, propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Propagation);
            NormalizedSemanticTransition addOneSync = Transition(2, 0, synchronization, zero, one, PhenomenonOperationKind.Add, 1f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Synchronization);
            DomainProposalCompositionResult additive = DomainProposalComposer.TryCompose(Collision(DomainOrchestrationGeneration.Generation1, addOne, addOneSync), new[] { addOne, addOneSync });
            NormalizedSemanticTransition addThree = Transition(3, 0, propagation, zero, three, PhenomenonOperationKind.Add, 3f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Propagation);
            NormalizedSemanticTransition removeOne = Transition(4, 0, synchronization, zero, Snapshot(-1f), PhenomenonOperationKind.Remove, 1f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Synchronization);
            DomainProposalCompositionResult mixed = DomainProposalComposer.TryCompose(Collision(DomainOrchestrationGeneration.Generation1, addThree, removeOne), new[] { addThree, removeOne });
            NormalizedSemanticTransition removeTwo = Transition(5, 0, synchronization, zero, Snapshot(-2f), PhenomenonOperationKind.Remove, 2f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Synchronization);
            DomainProposalCompositionResult cancellation = DomainProposalComposer.TryCompose(Collision(DomainOrchestrationGeneration.Generation1, Transition(6, 0, propagation, zero, two, PhenomenonOperationKind.Add, 2f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Propagation), removeTwo), new[] { Transition(6, 0, propagation, zero, two, PhenomenonOperationKind.Add, 2f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Propagation), removeTwo });
            NormalizedSemanticTransition baselineDifferent = Transition(7, 0, synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainOrchestrationGeneration.Generation1, DomainTransitionOrigin.Synchronization);
            DomainProposalCompositionResult baseline = DomainProposalComposer.TryCompose(Collision(DomainOrchestrationGeneration.Generation1, addOne, baselineDifferent), new[] { addOne, baselineDifferent });
            NormalizedSemanticTransition highA = Transition(8, 1, catalysis, zero, two, PhenomenonOperationKind.Add, 2f, DomainOrchestrationGeneration.Generation2, DomainTransitionOrigin.Catalysis);
            NormalizedSemanticTransition highB = Transition(9, 2, catalysis, zero, two, PhenomenonOperationKind.Add, 2f, DomainOrchestrationGeneration.Generation2, DomainTransitionOrigin.Catalysis);
            ProposalCollision highCollision = Collision(DomainOrchestrationGeneration.Generation2, highA, highB);
            DomainProposalCompositionResult coalesced = DomainProposalComposer.TryCompose(highCollision, new[] { highA, highB });
            NormalizedSemanticTransition lowB = Transition(10, 2, catalysis, zero, low, PhenomenonOperationKind.Remove, 2f, DomainOrchestrationGeneration.Generation2, DomainTransitionOrigin.Catalysis);
            DomainProposalCompositionResult opposed = DomainProposalComposer.TryCompose(Collision(DomainOrchestrationGeneration.Generation2, highA, lowB), new[] { highA, lowB });
            NormalizedSemanticTransition inconsistentHigh = Transition(11, 2, catalysis, zero, three, PhenomenonOperationKind.Add, 3f, DomainOrchestrationGeneration.Generation2, DomainTransitionOrigin.Catalysis);
            DomainProposalCompositionResult mismatch = DomainProposalComposer.TryCompose(Collision(DomainOrchestrationGeneration.Generation2, highA, inconsistentHigh), new[] { highA, inconsistentHigh });
            DomainProposalCompositionResult missing = DomainProposalComposer.TryCompose(Collision(DomainOrchestrationGeneration.Generation1, addOne, addOneSync), new[] { addOne });
            bool pass = additive.Resolved && additive.Policy == ProposalCompositionPolicy.AdditiveDelta && additive.NetSignedDelta == 2f && additive.EffectiveOperation == PhenomenonOperationKind.Add && additive.EffectiveMagnitude == 2f && additive.ResultingAfter.SemanticValue == 2f
                && mixed.Resolved && mixed.NetSignedDelta == 2f && mixed.ResultingAfter.SemanticValue == 2f
                && cancellation.Resolved && cancellation.IsNoOp && cancellation.NetSignedDelta == 0f && cancellation.ResultingAfter.SemanticValue == 0f
                && !baseline.Resolved && baseline.Reason == ProposalCompositionReason.BaselineMismatch
                && coalesced.Resolved && coalesced.Policy == ProposalCompositionPolicy.CatalysisCoalescence && coalesced.DestinationRegion == PhenomenonSemanticRegion.High && coalesced.ResultingAfter.SemanticValue == 2f && coalesced.Components.Count == 2
                && !opposed.Resolved && opposed.Reason == ProposalCompositionReason.OpposedCatalysisDestinations && !mismatch.Resolved && mismatch.Reason == ProposalCompositionReason.CatalysisResultMismatch
                && !missing.Resolved && missing.Reason == ProposalCompositionReason.MissingTransition && !highA.IsUnambiguous && highCollision.IsUnresolved;
            failure = pass ? null : "Proposal composition verification failed."; return pass;
        }
        private static DomainLaw Law(LawTerritoryPrinciple territory) { DomainLaw law; LawValidationResult validation; DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, territory, out law, out validation); return law; }
        private static PhenomenonSemanticSnapshot Snapshot(float value) { PhenomenonSemanticSnapshot snapshot; PhenomenonSemanticResult result; PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat, value, out snapshot, out result); return snapshot; }
        private static NormalizedSemanticTransition Transition(int id, int parent, DomainLaw law, PhenomenonSemanticSnapshot before, PhenomenonSemanticSnapshot after, PhenomenonOperationKind operation, float magnitude, DomainOrchestrationGeneration generation, DomainTransitionOrigin origin) { return new NormalizedSemanticTransition(new DomainTransitionSequenceId(id), generation, new DomainTransitionSequenceId(parent), new PhenomenonCarrierId("B"), LawPhenomenon.Heat, before, after, operation, magnitude, origin, law, false); }
        private static ProposalCollision Collision(DomainOrchestrationGeneration generation, params NormalizedSemanticTransition[] transitions) { List<DomainTransitionSequenceId> ids = new List<DomainTransitionSequenceId>(); List<DomainLaw> laws = new List<DomainLaw>(); for (int i = 0; i < transitions.Length; i++) { ids.Add(transitions[i].SequenceId); if (!laws.Contains(transitions[i].OriginatingLaw)) laws.Add(transitions[i].OriginatingLaw); } return new ProposalCollision(new PhenomenonCarrierId("B"), LawPhenomenon.Heat, generation, ids, laws); }
    }
}
