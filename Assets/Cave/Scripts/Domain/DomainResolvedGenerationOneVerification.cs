using System.Collections.Generic;

namespace Cave.Domain
{
    public static class DomainResolvedGenerationOneVerification
    {
        public static bool TryRunAll(out string failure)
        {
            DomainLaw propagation = Law(LawTerritoryPrinciple.Propagation);
            DomainLaw synchronization = Law(LawTerritoryPrinciple.Synchronization);
            DomainLaw accumulation = Law(LawTerritoryPrinciple.Accumulation);
            UnaryDomainGenerationZeroResult unaryZero = ZeroUnary();
            PhenomenonSemanticSnapshot zero = Snapshot(0f);
            PhenomenonSemanticSnapshot one = Snapshot(1f);
            PhenomenonSemanticSnapshot two = Snapshot(2f);
            PhenomenonSemanticSnapshot three = Snapshot(3f);

            UnaryDomainGenerationOneResult cleanRaw = Raw(unaryZero, Read(Transition(1, "Clean", propagation, zero, three, PhenomenonOperationKind.Add, 3f, DomainTransitionOrigin.Propagation, true)), Empty<ProposalCollision>());
            DomainResolvedGenerationOneResult clean = DomainResolvedGenerationOneBuilder.Resolve(cleanRaw);

            NormalizedSemanticTransition basicPropagation = Transition(1, "B", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition basicSynchronization = Transition(2, "B", synchronization, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            ProposalCollision basicCollision = Collision("B", basicPropagation, basicSynchronization);
            UnaryDomainGenerationOneResult basicRaw = Raw(unaryZero, Read(basicPropagation, basicSynchronization), Read(basicCollision));
            DomainResolvedGenerationOneResult basic = DomainResolvedGenerationOneBuilder.Resolve(basicRaw);

            NormalizedSemanticTransition mixedAdd = Transition(1, "M", propagation, zero, three, PhenomenonOperationKind.Add, 3f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition mixedRemove = Transition(2, "M", synchronization, zero, Snapshot(-1f), PhenomenonOperationKind.Remove, 1f, DomainTransitionOrigin.Synchronization, false);
            DomainResolvedGenerationOneResult mixed = DomainResolvedGenerationOneBuilder.Resolve(Raw(unaryZero, Read(mixedAdd, mixedRemove), Read(Collision("M", mixedAdd, mixedRemove))));

            NormalizedSemanticTransition zeroAdd = Transition(1, "Z", propagation, zero, two, PhenomenonOperationKind.Add, 2f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition zeroRemove = Transition(2, "Z", synchronization, zero, Snapshot(-2f), PhenomenonOperationKind.Remove, 2f, DomainTransitionOrigin.Synchronization, false);
            DomainResolvedGenerationOneResult zeroNet = DomainResolvedGenerationOneBuilder.Resolve(Raw(unaryZero, Read(zeroAdd, zeroRemove), Read(Collision("Z", zeroAdd, zeroRemove))));

            NormalizedSemanticTransition firstA = Transition(1, "B", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition firstB = Transition(2, "B", synchronization, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            NormalizedSemanticTransition secondA = Transition(3, "C", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition secondB = Transition(4, "C", synchronization, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            ProposalCollision firstCollision = Collision("B", firstA, firstB);
            ProposalCollision secondCollision = Collision("C", secondA, secondB);
            UnaryDomainGenerationOneResult orderedRaw = Raw(unaryZero, Read(firstA, firstB, secondA, secondB), Read(secondCollision, firstCollision));
            DomainResolvedGenerationOneResult ordered = DomainResolvedGenerationOneBuilder.Resolve(orderedRaw);
            DomainResolvedGenerationOneResult orderedAgain = DomainResolvedGenerationOneBuilder.Resolve(orderedRaw);

            NormalizedSemanticTransition mismatchA = Transition(1, "U", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition mismatchB = Transition(2, "U", synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            DomainResolvedGenerationOneResult mismatch = DomainResolvedGenerationOneBuilder.Resolve(Raw(unaryZero, Read(mismatchA, mismatchB), Read(Collision("U", mismatchA, mismatchB))));

            NormalizedSemanticTransition partialA = Transition(1, "P", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition partialB = Transition(2, "P", synchronization, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            NormalizedSemanticTransition partialBadA = Transition(3, "Q", propagation, zero, one, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Propagation, false);
            NormalizedSemanticTransition partialBadB = Transition(4, "Q", synchronization, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Synchronization, false);
            DomainResolvedGenerationOneResult partial = DomainResolvedGenerationOneBuilder.Resolve(Raw(unaryZero, Read(partialA, partialB, partialBadA, partialBadB), Read(Collision("Q", partialBadA, partialBadB), Collision("P", partialA, partialB))));

            TransferDomainGenerationZeroResult transferZero = ZeroTransfer();
            NormalizedSemanticTransition accumulationOne = Transition(1, "T", accumulation, one, two, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Accumulation, true);
            NormalizedSemanticTransition accumulationTwo = Transition(2, "T", accumulation, two, three, PhenomenonOperationKind.Add, 1f, DomainTransitionOrigin.Accumulation, true);
            DomainResolvedGenerationOneResult accumulationResult = DomainResolvedGenerationOneBuilder.Resolve(TransferRaw(transferZero, Read(accumulationOne, accumulationTwo), Empty<ProposalCollision>()));

            UnaryDomainGenerationOneResult failedRaw = new UnaryDomainGenerationOneResult(null, Empty<DomainLawOutcomeTrace>(), Empty<PropagationEvaluationResult>(), Empty<SynchronizationEvaluationResult>(), Empty<NormalizedSemanticTransition>(), Empty<ProposalCollision>(), DomainGenerationOneRejectionReason.GenerationZeroRequired);
            UnaryDomainGenerationZeroResult failedZero = DomainGenerationZeroExecutor.Execute(new UnaryDomainOrchestrationRequest(DomainComposition.Empty, new LawExpressionContext(true, LawExpression.Projectile, false), LawPhenomenon.Heat, PhenomenonOperationKind.Transfer, 1f, Carrier("A", 0f), DomainOrchestrationContextSet.Empty));
            UnaryDomainGenerationOneResult failedFoundation = Raw(failedZero, Empty<NormalizedSemanticTransition>(), Empty<ProposalCollision>());
            DomainResolvedGenerationOneResult rejected = DomainResolvedGenerationOneBuilder.Resolve(failedRaw);
            DomainResolvedGenerationOneResult rejectedFoundation = DomainResolvedGenerationOneBuilder.Resolve(failedFoundation);

            DomainPostGenerationOneState state;
            bool cleanState = clean.TryGetPostGenerationOneState(new PhenomenonCarrierId("Clean"), LawPhenomenon.Heat, out state) && !state.IsUnresolved && Value(state.Snapshot) == 3f;
            bool basicState = basic.TryGetPostGenerationOneState(new PhenomenonCarrierId("B"), LawPhenomenon.Heat, out state) && !state.IsUnresolved && Value(state.Snapshot) == 2f;
            bool mismatchState = mismatch.TryGetPostGenerationOneState(new PhenomenonCarrierId("U"), LawPhenomenon.Heat, out state) && state.IsUnresolved && state.Snapshot == null;
            bool partialStates = partial.TryGetPostGenerationOneState(new PhenomenonCarrierId("P"), LawPhenomenon.Heat, out state) && !state.IsUnresolved && Value(state.Snapshot) == 2f
                && partial.TryGetPostGenerationOneState(new PhenomenonCarrierId("Q"), LawPhenomenon.Heat, out state) && state.IsUnresolved && state.Snapshot == null;
            bool accumulationState = accumulationResult.TryGetPostGenerationOneState(new PhenomenonCarrierId("T"), LawPhenomenon.Heat, out state) && !state.IsUnresolved && Value(state.Snapshot) == 3f;

            bool pass = clean.Succeeded && clean.CompositionResults.Count == 0 && clean.ComposedTransitions.Count == 0 && clean.AuthoritativeTransitions.Count == 1 && !clean.AuthoritativeTransitions[0].IsComposed && clean.UnresolvedKeys.Count == 0 && clean.NextTransitionId.Value == 2 && cleanState
                && basic.Succeeded && basic.CompositionResults.Count == 1 && basic.CompositionResults[0].Resolved && basic.CompositionResults[0].NetSignedDelta == 2f && basic.ComposedTransitions.Count == 1 && basic.ComposedTransitions[0].SequenceId.Value == 3 && basic.AuthoritativeTransitions.Count == 1 && basic.AuthoritativeTransitions[0].IsComposed && basic.ComposedTransitions[0].Contributors.Count == 2 && basic.ComposedTransitions[0].ParentTransitionIds.Count == 2 && basic.ComposedTransitions[0].OriginatingLaws.Count == 2 && basic.NextTransitionId.Value == 4 && basicState
                && !basicPropagation.IsUnambiguous && !basicSynchronization.IsUnambiguous && basicRaw.Transitions[0] == basicPropagation && basicRaw.ProposalCollisions[0] == basicCollision
                && mixed.Succeeded && mixed.ComposedTransitions.Count == 1 && Value(mixed.ComposedTransitions[0].After) == 2f
                && zeroNet.Succeeded && zeroNet.ComposedTransitions.Count == 1 && zeroNet.ComposedTransitions[0].IsNoOp && Value(zeroNet.ComposedTransitions[0].Before) == 0f && Value(zeroNet.ComposedTransitions[0].After) == 0f
                && ordered.Succeeded && ordered.ComposedTransitions.Count == 2 && ordered.ComposedTransitions[0].SequenceId.Value == 5 && ordered.ComposedTransitions[1].SequenceId.Value == 6 && ordered.ComposedTransitions[0].CarrierId.Equals(new PhenomenonCarrierId("B")) && ordered.ComposedTransitions[1].CarrierId.Equals(new PhenomenonCarrierId("C")) && ordered.NextTransitionId.Value == 7
                && orderedAgain.ComposedTransitions[0].SequenceId.Value == ordered.ComposedTransitions[0].SequenceId.Value && orderedAgain.ComposedTransitions[1].SequenceId.Value == ordered.ComposedTransitions[1].SequenceId.Value && orderedAgain.NextTransitionId.Value == ordered.NextTransitionId.Value
                && mismatch.Succeeded && mismatch.ComposedTransitions.Count == 0 && mismatch.AuthoritativeTransitions.Count == 0 && mismatch.UnresolvedKeys.Count == 1 && mismatch.UnresolvedKeys[0].CarrierId.Equals(new PhenomenonCarrierId("U")) && mismatchState
                && partial.Succeeded && partial.ComposedTransitions.Count == 1 && partial.UnresolvedKeys.Count == 1 && partialStates
                && accumulationResult.Succeeded && accumulationResult.ComposedTransitions.Count == 0 && accumulationResult.AuthoritativeTransitions.Count == 2 && accumulationResult.NextTransitionId.Value == 3 && accumulationState
                && !rejected.Succeeded && rejected.RejectionReason == DomainResolvedGenerationOneRejectionReason.GenerationOneRequired && rejected.CompositionResults.Count == 0 && rejected.ComposedTransitions.Count == 0 && rejected.AuthoritativeTransitions.Count == 0 && rejected.PostGenerationOneStates.Count == 0 && rejected.UnresolvedKeys.Count == 0 && !rejected.NextTransitionId.IsValid
                && !rejectedFoundation.Succeeded && rejectedFoundation.ComposedTransitions.Count == 0 && rejectedFoundation.PostGenerationOneStates.Count == 0 && !rejectedFoundation.NextTransitionId.IsValid
                && AllGenerationOne(ordered) && AllGenerationOne(basic) && AllGenerationOne(zeroNet);
            failure = pass ? null : "Resolved Generation-1 verification failed.";
            return pass;
        }

        private static UnaryDomainGenerationOneResult Raw(UnaryDomainGenerationZeroResult zero, IReadOnlyList<NormalizedSemanticTransition> transitions, IReadOnlyList<ProposalCollision> collisions) { return new UnaryDomainGenerationOneResult(zero, Empty<DomainLawOutcomeTrace>(), Empty<PropagationEvaluationResult>(), Empty<SynchronizationEvaluationResult>(), transitions, collisions, DomainGenerationOneRejectionReason.None); }
        private static TransferDomainGenerationOneResult TransferRaw(TransferDomainGenerationZeroResult zero, IReadOnlyList<NormalizedSemanticTransition> transitions, IReadOnlyList<ProposalCollision> collisions) { return new TransferDomainGenerationOneResult(zero, Empty<DomainLawOutcomeTrace>(), Empty<AccumulationEvaluationResult>(), Empty<OriginalTransferSourceSkip>(), transitions, collisions, DomainGenerationOneRejectionReason.None); }
        private static UnaryDomainGenerationZeroResult ZeroUnary() { return DomainGenerationZeroExecutor.Execute(new UnaryDomainOrchestrationRequest(DomainComposition.Empty, new LawExpressionContext(true, LawExpression.Projectile, false), LawPhenomenon.Heat, PhenomenonOperationKind.Add, 0f, Carrier("A", 0f), DomainOrchestrationContextSet.Empty)); }
        private static TransferDomainGenerationZeroResult ZeroTransfer() { return DomainGenerationZeroExecutor.Execute(new TransferDomainOrchestrationRequest(DomainComposition.Empty, new LawExpressionContext(true, LawExpression.Projectile, false), LawPhenomenon.Heat, 0f, Carrier("S", 0f), Carrier("T", 0f), DomainOrchestrationContextSet.Empty)); }
        private static DomainLaw Law(LawTerritoryPrinciple territory) { DomainLaw law; LawValidationResult result; DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, territory, out law, out result); return law; }
        private static PhenomenonCarrierSnapshot Carrier(string id, float value) { return new PhenomenonCarrierSnapshot(new PhenomenonCarrierId(id), LawPhenomenon.Heat, Snapshot(value)); }
        private static PhenomenonSemanticSnapshot Snapshot(float value) { PhenomenonSemanticSnapshot snapshot; PhenomenonSemanticResult result; PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat, value, out snapshot, out result); return snapshot; }
        private static NormalizedSemanticTransition Transition(int id, string carrier, DomainLaw law, PhenomenonSemanticSnapshot before, PhenomenonSemanticSnapshot after, PhenomenonOperationKind operation, float magnitude, DomainTransitionOrigin origin, bool unambiguous) { return new NormalizedSemanticTransition(new DomainTransitionSequenceId(id), DomainOrchestrationGeneration.Generation1, new DomainTransitionSequenceId(0), new PhenomenonCarrierId(carrier), LawPhenomenon.Heat, before, after, operation, magnitude, origin, law, unambiguous); }
        private static ProposalCollision Collision(string carrier, params NormalizedSemanticTransition[] transitions) { List<DomainTransitionSequenceId> ids = new List<DomainTransitionSequenceId>(); List<DomainLaw> laws = new List<DomainLaw>(); for (int i = 0; i < transitions.Length; i++) { ids.Add(transitions[i].SequenceId); if (!laws.Contains(transitions[i].OriginatingLaw)) laws.Add(transitions[i].OriginatingLaw); } return new ProposalCollision(new PhenomenonCarrierId(carrier), LawPhenomenon.Heat, DomainOrchestrationGeneration.Generation1, ids, laws); }
        private static bool AllGenerationOne(DomainResolvedGenerationOneResult result) { for (int i = 0; i < result.ComposedTransitions.Count; i++) if (result.ComposedTransitions[i].Generation != DomainOrchestrationGeneration.Generation1) return false; return true; }
        private static float Value(PhenomenonSemanticSnapshot snapshot) { return snapshot != null ? snapshot.SemanticValue : float.NaN; }
        private static IReadOnlyList<T> Read<T>(params T[] values) { return new List<T>(values).AsReadOnly(); }
        private static IReadOnlyList<T> Empty<T>() { return new List<T>().AsReadOnly(); }
    }
}
