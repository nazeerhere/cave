namespace Cave.Domain
{
    public static class DomainGenerationZeroExecutorVerification
    {
        public static bool TryRunAll(out string failure)
        {
            DomainLaw reversal = Law(LawTerritoryPrinciple.Reversal);
            DomainLaw interference = Law(LawTerritoryPrinciple.Interference);
            DomainLaw propagation = Law(LawTerritoryPrinciple.Propagation);
            DomainLaw synchronization = Law(LawTerritoryPrinciple.Synchronization);
            DomainLaw accumulation = Law(LawTerritoryPrinciple.Accumulation);
            DomainLaw catalysis = Law(LawTerritoryPrinciple.Catalysis);
            PhenomenonCarrierSnapshot target = Carrier("T", 0f);
            PhenomenonCarrierSnapshot reference = Carrier("R", 0f);
            PhenomenonCarrierSnapshot contributorOne = Carrier("C1", 0f);
            PhenomenonCarrierSnapshot contributorTwo = Carrier("C2", 0f);
            LawExpressionContext expression = new LawExpressionContext(true, LawExpression.Projectile, false);
            DomainOrchestrationContextSet coherentContexts = Contexts(interference, reference, target, new[] { contributorOne, contributorTwo });

            UnaryDomainGenerationZeroResult baseOnly = DomainGenerationZeroExecutor.Execute(Unary(DomainComposition.Empty, expression, 1f, target, DomainOrchestrationContextSet.Empty));
            UnaryDomainGenerationZeroResult reversalOnly = DomainGenerationZeroExecutor.Execute(Unary(Composition(reversal), expression, 1f, target, DomainOrchestrationContextSet.Empty));
            UnaryDomainGenerationZeroResult removeReversal = DomainGenerationZeroExecutor.Execute(Unary(Composition(reversal), expression, 2f, target, DomainOrchestrationContextSet.Empty, PhenomenonOperationKind.Remove));
            UnaryDomainGenerationZeroResult interferenceOnly = DomainGenerationZeroExecutor.Execute(Unary(Composition(interference), expression, 1f, target, coherentContexts));
            UnaryDomainGenerationZeroResult bothA = DomainGenerationZeroExecutor.Execute(Unary(Composition(reversal, interference), expression, 1f, target, coherentContexts));
            UnaryDomainGenerationZeroResult bothB = DomainGenerationZeroExecutor.Execute(Unary(Composition(interference, reversal), expression, 1f, target, coherentContexts));
            UnaryDomainGenerationZeroResult missing = DomainGenerationZeroExecutor.Execute(Unary(Composition(interference), expression, 1f, target, DomainOrchestrationContextSet.Empty));
            DomainOrchestrationContextSet ambiguous = new DomainOrchestrationContextSet(null, null, null, new[] {
                new DomainLawContextBinding<InterferenceOrchestrationContext>(interference, new InterferenceOrchestrationContext(reference, new[] { contributorOne }, target, 2)),
                new DomainLawContextBinding<InterferenceOrchestrationContext>(interference, new InterferenceOrchestrationContext(reference, new[] { contributorTwo }, target, 2)) });
            UnaryDomainGenerationZeroResult ambiguousResult = DomainGenerationZeroExecutor.Execute(Unary(Composition(interference), expression, 1f, target, ambiguous));
            UnaryDomainGenerationZeroResult mismatch = DomainGenerationZeroExecutor.Execute(Unary(Composition(interference), expression, 1f, target,
                Contexts(interference, reference, Carrier("Other", 0f), new[] { contributorOne })));
            UnaryDomainGenerationZeroResult relationalPresent = DomainGenerationZeroExecutor.Execute(Unary(Composition(propagation, synchronization, catalysis), expression, 1f, target, DomainOrchestrationContextSet.Empty));
            UnaryDomainGenerationZeroResult catalysisCrossing = DomainGenerationZeroExecutor.Execute(Unary(DomainComposition.Empty, expression, 1f, Carrier("High", 1f), DomainOrchestrationContextSet.Empty));
            TransferDomainGenerationZeroResult transfer = DomainGenerationZeroExecutor.Execute(new TransferDomainOrchestrationRequest(Composition(reversal, interference, accumulation), expression, LawPhenomenon.Heat, 1f, Carrier("A", 2f), Carrier("B", 0f), coherentContexts));
            UnaryDomainGenerationZeroResult invalidUnary = DomainGenerationZeroExecutor.Execute(new UnaryDomainOrchestrationRequest(DomainComposition.Empty, expression, LawPhenomenon.Heat, PhenomenonOperationKind.Transfer, 1f, target, DomainOrchestrationContextSet.Empty));
            TransferDomainGenerationZeroResult invalidTransfer = DomainGenerationZeroExecutor.Execute(new TransferDomainOrchestrationRequest(DomainComposition.Empty, expression, LawPhenomenon.Heat, -1f, Carrier("A", 2f), Carrier("B", 0f), DomainOrchestrationContextSet.Empty));

            bool pass = baseOnly.Succeeded && baseOnly.EffectiveIntent.EffectiveOperation == PhenomenonOperationKind.Add && Value(baseOnly.PrimaryTransition.After) == 1f && baseOnly.PrimaryTransition.SequenceId.Value == 0
                && reversalOnly.Succeeded && reversalOnly.EffectiveIntent.EffectiveOperation == PhenomenonOperationKind.Remove && Value(reversalOnly.PrimaryTransition.After) == -1f && reversalOnly.ReversalOutcome.Applied
                && removeReversal.Succeeded && removeReversal.EffectiveIntent.EffectiveOperation == PhenomenonOperationKind.Add && Value(removeReversal.PrimaryTransition.After) == 2f
                && interferenceOnly.Succeeded && interferenceOnly.EffectiveIntent.EffectiveMagnitude == 3f && interferenceOnly.InterferenceOutcome.AcceptedContributorCount == 3 && Value(interferenceOnly.PrimaryTransition.After) == 3f
                && bothA.Succeeded && bothB.Succeeded && bothA.EffectiveIntent.EffectiveOperation == bothB.EffectiveIntent.EffectiveOperation && bothA.EffectiveIntent.EffectiveMagnitude == bothB.EffectiveIntent.EffectiveMagnitude && Value(bothA.PrimaryTransition.After) == Value(bothB.PrimaryTransition.After) && Value(bothA.PrimaryTransition.After) == -3f
                && missing.Succeeded && missing.EffectiveIntent.EffectiveMagnitude == 1f && missing.InterferenceOutcome.Reason == DomainLawOutcomeReason.ContextUnavailable
                && ambiguousResult.Succeeded && ambiguousResult.EffectiveIntent.EffectiveMagnitude == 1f && ambiguousResult.InterferenceOutcome.Reason == DomainLawOutcomeReason.ContextUnavailable
                && mismatch.Succeeded && mismatch.EffectiveIntent.EffectiveMagnitude == 1f && mismatch.InterferenceOutcome.Reason == DomainLawOutcomeReason.ContextMismatch
                && reference.SemanticState.SemanticValue == 0f && contributorOne.SemanticState.SemanticValue == 0f && contributorTwo.SemanticState.SemanticValue == 0f
                && relationalPresent.Succeeded && relationalPresent.ResolutionPlan.Laws.Count == 3 && relationalPresent.TransformOutcomes.Count == 0 && relationalPresent.MaximumGeneration == DomainOrchestrationGeneration.Generation0
                && catalysisCrossing.Succeeded && catalysisCrossing.PrimaryTransition.Before.Region == PhenomenonSemanticRegion.Regular && catalysisCrossing.PrimaryTransition.After.Region == PhenomenonSemanticRegion.High
                && transfer.Succeeded && transfer.PrimaryResult.SourceDelta.Amount + transfer.PrimaryResult.TargetDelta.Amount == 0f && transfer.SourceTransition.SequenceId.Value == 0 && transfer.TargetTransition.SequenceId.Value == 1 && transfer.SourceTransition.Operation == PhenomenonOperationKind.Transfer && transfer.TargetTransition.Operation == PhenomenonOperationKind.Transfer && Value(transfer.SourceTransition.After) == 1f && Value(transfer.TargetTransition.After) == 1f && transfer.MaximumGeneration == DomainOrchestrationGeneration.Generation0
                && transfer.TransformOutcomes.Count == 2 && transfer.TransformOutcomes[0].Reason == DomainLawOutcomeReason.NoDefinedInverse && transfer.TransformOutcomes[1].Reason == DomainLawOutcomeReason.OperationUnsupported
                && !invalidUnary.Succeeded && invalidUnary.RejectionReason == DomainGenerationZeroRejectionReason.InvalidRequest
                && !invalidTransfer.Succeeded && invalidTransfer.RejectionReason == DomainGenerationZeroRejectionReason.InvalidRequest;
            failure = pass ? null : "Generation-0 orchestration verification failed.";
            return pass;
        }

        private static UnaryDomainOrchestrationRequest Unary(DomainComposition composition, LawExpressionContext expression, float magnitude, PhenomenonCarrierSnapshot target, DomainOrchestrationContextSet contexts, PhenomenonOperationKind operation = PhenomenonOperationKind.Add)
        { return new UnaryDomainOrchestrationRequest(composition, expression, LawPhenomenon.Heat, operation, magnitude, target, contexts); }
        private static DomainLaw Law(LawTerritoryPrinciple territory) { DomainLaw law; LawValidationResult validation; DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, territory, out law, out validation); return law; }
        private static DomainComposition Composition(params DomainLaw[] laws) { DomainComposition composition = DomainComposition.Empty; for (int index = 0; index < laws.Length; index++) composition = DomainCompositionEditor.Add(composition, laws[index]).Resulting; return composition; }
        private static PhenomenonCarrierSnapshot Carrier(string id, float value) { PhenomenonSemanticSnapshot snapshot; PhenomenonSemanticResult result; PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat, value, out snapshot, out result); return new PhenomenonCarrierSnapshot(new PhenomenonCarrierId(id), LawPhenomenon.Heat, snapshot); }
        private static DomainOrchestrationContextSet Contexts(DomainLaw law, PhenomenonCarrierSnapshot reference, PhenomenonCarrierSnapshot focal, PhenomenonCarrierSnapshot[] contributors)
        { return new DomainOrchestrationContextSet(null, null, null, new[] { new DomainLawContextBinding<InterferenceOrchestrationContext>(law, new InterferenceOrchestrationContext(reference, contributors, focal, 3)) }); }
        private static float Value(PhenomenonSemanticSnapshot snapshot) { return snapshot != null ? snapshot.SemanticValue : float.NaN; }
    }
}
