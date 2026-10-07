using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Locks state-family metadata and operation eligibility independently of downstream statuses or stacks.</summary>
    public static class PhenomenonStateModelVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyProfiles(out failure)
                && VerifyAdditiveOperations(out failure)
                && VerifyRelativeMassContract(out failure)
                && VerifyPatternOperationContract(out failure)
                && VerifyRuntimeOperationRejection(out failure);
        }

        private static bool VerifyProfiles(out string failure)
        {
            LawPhenomenon[] all = { LawPhenomenon.Heat, LawPhenomenon.Flow, LawPhenomenon.Mass,
                LawPhenomenon.Compression, LawPhenomenon.Potential, LawPhenomenon.Resonance,
                LawPhenomenon.Phase, LawPhenomenon.Order };
            for (int index = 0; index < all.Length; index++)
            {
                PhenomenonSemanticProfile profile;
                if (!PhenomenonSemanticClassifier.TryGetProfile(all[index], out profile))
                { failure = "Missing state-model profile for " + all[index] + "."; return false; }
            }
            PhenomenonSemanticProfile resonance; PhenomenonSemanticProfile phase; PhenomenonSemanticProfile order;
            bool valid = PhenomenonSemanticClassifier.TryGetProfile(LawPhenomenon.Resonance, out resonance)
                && resonance.StateModelKind == PhenomenonStateModelKind.Pattern
                && PhenomenonSemanticClassifier.TryGetProfile(LawPhenomenon.Phase, out phase)
                && phase.StateModelKind == PhenomenonStateModelKind.Pattern
                && PhenomenonSemanticClassifier.TryGetProfile(LawPhenomenon.Order, out order)
                && order.StateModelKind == PhenomenonStateModelKind.Additive
                && order.Operations.SupportsAdd && order.Operations.SupportsRemove;
            failure = valid ? null : "Pattern or signed Order state family was misclassified.";
            return valid;
        }

        private static bool VerifyAdditiveOperations(out string failure)
        {
            PhenomenonSemanticProfile heat;
            bool valid = PhenomenonSemanticClassifier.TryGetProfile(LawPhenomenon.Heat, out heat)
                && heat.StateModelKind == PhenomenonStateModelKind.Additive
                && heat.NeutralSemanticValue == 0f
                && heat.Operations.SupportsAdd && heat.Operations.SupportsRemove && heat.Operations.SupportsTransfer
                && PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Flow, PhenomenonOperationKind.Add)
                && !PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Flow, PhenomenonOperationKind.Transfer);
            failure = valid ? null : "Additive semantic operation metadata was inconsistent.";
            return valid;
        }

        private static bool VerifyRelativeMassContract(out string failure)
        {
            PhenomenonSemanticProfile mass;
            bool valid = PhenomenonSemanticClassifier.TryGetProfile(LawPhenomenon.Mass, out mass)
                && mass.StateModelKind == PhenomenonStateModelKind.Relative
                && mass.NeutralSemanticValue == 1f
                && mass.RequiresCarrierBaseline
                && mass.Operations.SupportsAdd && mass.Operations.SupportsRemove && !mass.Operations.SupportsTransfer;
            failure = valid ? null : "Mass was not represented as a baseline-relative semantic state.";
            return valid;
        }

        private static bool VerifyPatternOperationContract(out string failure)
        {
            bool valid = PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Resonance, PhenomenonOperationKind.Add)
                && PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Resonance, PhenomenonOperationKind.Remove)
                && PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Phase, PhenomenonOperationKind.Add)
                && PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Phase, PhenomenonOperationKind.Remove)
                && !PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Resonance, PhenomenonOperationKind.Transfer)
                && !PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Phase, PhenomenonOperationKind.Transfer)
                && !PhenomenonSemanticClassifier.SupportsOperation(LawPhenomenon.Order, PhenomenonOperationKind.Transfer);
            failure = valid ? null : "Pattern operation metadata did not require contextual Add/Remove and reject Transfer.";
            return valid;
        }

        private static bool VerifyRuntimeOperationRejection(out string failure)
        {
            PhenomenonSemanticSnapshot mass;
            PhenomenonSemanticResult semantic;
            PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Mass, 1f, out mass, out semantic);
            DomainLaw law; LawValidationResult validation;
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Mass, LawTerritoryPrinciple.Accumulation, out law, out validation);
            DomainComposition composition = DomainCompositionEditor.Add(DomainComposition.Empty, law).Resulting;
            DomainOrchestrationRequestValidation result = DomainOrchestrationRequestValidator.Validate(
                new TransferDomainOrchestrationRequest(composition, new LawExpressionContext(true, LawExpression.Projectile, false),
                    LawPhenomenon.Mass, 1f, new PhenomenonCarrierSnapshot(new PhenomenonCarrierId("a"), LawPhenomenon.Mass, mass),
                    new PhenomenonCarrierSnapshot(new PhenomenonCarrierId("b"), LawPhenomenon.Mass, mass), DomainOrchestrationContextSet.Empty));
            bool valid = !result.IsValid && result.RejectionReason == DomainOrchestrationRequestRejectionReason.OperationUnsupportedForPhenomenonStateModel;
            failure = valid ? null : "Unsupported relative Transfer reached the Domain resolver.";
            return valid;
        }
    }
}
