using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for authored Domain composition and Complexity.</summary>
    public static class DomainCompositionVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyEmptyAndOneLaw(out failure)
                && VerifyDiversityAndExpressionExclusion(out failure)
                && VerifyPairwiseGrowth(out failure)
                && VerifyMutationAndOrder(out failure)
                && VerifyPolicyAndImmutability(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyEmptyAndOneLaw(out string failure)
        {
            DomainComplexityReport empty = DomainComplexityCalculator.Calculate(DomainComposition.Empty, DomainComplexityPolicy.Default);
            DomainLaw a = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            DomainCompositionMutationResult add = DomainCompositionEditor.Add(DomainComposition.Empty, a);
            DomainComplexityReport one = DomainComplexityCalculator.Calculate(add.Resulting, DomainComplexityPolicy.Default);
            return Expect(empty.LawCount == 0 && Approximately(empty.TotalComplexity, 0f)
                && add.Succeeded && add.Resulting.Count == 1
                && one.LawCount == 1 && one.UniquePhenomenonCount == 1 && one.UniqueTerritoryPrincipleCount == 1
                && Approximately(one.BaseLawCost, 1f) && Approximately(one.LawInteractionCost, 0f)
                && Approximately(one.PhenomenonDiversityCost, 0f) && Approximately(one.TerritoryDiversityCost, 0f)
                && Approximately(one.TotalComplexity, 1f),
                "Empty or one-Law composition Complexity was not deterministic.", out failure);
        }

        private static bool VerifyDiversityAndExpressionExclusion(out string failure)
        {
            DomainLaw heatPropagation = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            DomainLaw heatCatalysisProjectile = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Catalysis);
            DomainLaw heatCatalysisFrenzy = Law(LawExpression.Frenzy, LawPhenomenon.Heat, LawTerritoryPrinciple.Catalysis);
            DomainLaw potentialCatalysis = Law(LawExpression.Projectile, LawPhenomenon.Potential, LawTerritoryPrinciple.Catalysis);
            DomainComplexityReport samePhenomenon = Report(heatPropagation, heatCatalysisProjectile);
            DomainComplexityReport expressionVariant = Report(heatPropagation, heatCatalysisFrenzy);
            DomainComplexityReport diverse = Report(heatPropagation, potentialCatalysis);
            return Expect(samePhenomenon.LawCount == 2
                && Approximately(samePhenomenon.BaseLawCost, 2f)
                && Approximately(samePhenomenon.LawInteractionCost, 1f)
                && Approximately(samePhenomenon.PhenomenonDiversityCost, 0f)
                && Approximately(samePhenomenon.TerritoryDiversityCost, 1f)
                && Approximately(samePhenomenon.TotalComplexity, expressionVariant.TotalComplexity)
                && expressionVariant.UniqueExpressionCount == 2
                && diverse.TotalComplexity > samePhenomenon.TotalComplexity,
                "Expression diversity was charged or phenomenon/territory diversity was miscalculated.", out failure);
        }

        private static bool VerifyPairwiseGrowth(out string failure)
        {
            DomainComposition threePhenomena = Composition(
                Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation),
                Law(LawExpression.Projectile, LawPhenomenon.Flow, LawTerritoryPrinciple.Propagation),
                Law(LawExpression.Projectile, LawPhenomenon.Potential, LawTerritoryPrinciple.Propagation));
            DomainComposition threeTerritories = Composition(
                Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation),
                Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Catalysis),
                Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal));
            DomainComplexityReport phenomena = DomainComplexityCalculator.Calculate(threePhenomena, DomainComplexityPolicy.Default);
            DomainComplexityReport territories = DomainComplexityCalculator.Calculate(threeTerritories, DomainComplexityPolicy.Default);
            return Expect(phenomena.LawCount == 3 && Approximately(phenomena.LawInteractionCost, 3f)
                && phenomena.UniquePhenomenonCount == 3 && Approximately(phenomena.PhenomenonDiversityCost, 3f)
                && territories.UniqueTerritoryPrincipleCount == 3 && Approximately(territories.TerritoryDiversityCost, 3f),
                "Pairwise Complexity growth was not applied to Laws, phenomena, and territories.", out failure);
        }

        private static bool VerifyMutationAndOrder(out string failure)
        {
            DomainLaw a = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            DomainLaw b = Law(LawExpression.Frenzy, LawPhenomenon.Flow, LawTerritoryPrinciple.Catalysis);
            DomainLaw c = Law(LawExpression.Projectile, LawPhenomenon.Potential, LawTerritoryPrinciple.Reversal);
            DomainComposition first = Composition(a, c, b);
            DomainCompositionMutationResult duplicate = DomainCompositionEditor.Add(first, a);
            DomainCompositionMutationResult removed = DomainCompositionEditor.Remove(first, c);
            DomainCompositionMutationResult missing = DomainCompositionEditor.Remove(first,
                Law(LawExpression.Projectile, LawPhenomenon.Order, LawTerritoryPrinciple.Interference));
            DomainCompositionMutationResult empty = DomainCompositionEditor.Remove(Composition(a), a);
            return Expect(first.OrderedLaws[0].Equals(a) && first.OrderedLaws[1].Equals(c) && first.OrderedLaws[2].Equals(b)
                && !duplicate.Succeeded && duplicate.RejectionReason == DomainCompositionMutationRejectionReason.DuplicateLaw
                && duplicate.Resulting == first
                && removed.Succeeded && removed.Resulting.Count == 2
                && removed.Resulting.OrderedLaws[0].Equals(a) && removed.Resulting.OrderedLaws[1].Equals(b)
                && !missing.Succeeded && missing.RejectionReason == DomainCompositionMutationRejectionReason.LawNotFound
                && empty.Succeeded && empty.Resulting.Count == 0,
                "Composition add/remove did not preserve ordered immutable Law grammar.", out failure);
        }

        private static bool VerifyPolicyAndImmutability(out string failure)
        {
            DomainLaw a = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            DomainLaw b = Law(LawExpression.Frenzy, LawPhenomenon.Heat, LawTerritoryPrinciple.Catalysis);
            DomainComposition original = Composition(a);
            DomainCompositionMutationResult added = DomainCompositionEditor.Add(original, b);
            DomainComplexityReport defaultReport = DomainComplexityCalculator.Calculate(added.Resulting, DomainComplexityPolicy.Default);
            DomainComplexityReport custom = DomainComplexityCalculator.Calculate(added.Resulting,
                new DomainComplexityPolicy(2f, 3f, 5f, 7f));
            return Expect(original.Count == 1 && added.Resulting.Count == 2
                && defaultReport.UniqueExpressionCount == 2
                && defaultReport.OrderedLawBaseCosts.Count == 2
                && Approximately(custom.BaseLawCost, 4f)
                && Approximately(custom.LawInteractionCost, 3f)
                && Approximately(custom.PhenomenonDiversityCost, 0f)
                && Approximately(custom.TerritoryDiversityCost, 7f)
                && Approximately(custom.TotalComplexity, 14f),
                "Custom Complexity policy changed composition or failed to alter only report costs.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string f1; string f2; string f3; string f4; string f5; string f6; string f7; string f8; string f9; string f10;
            bool passed = DomainLawVerification.TryRunAll(out f1)
                & PhenomenonSemanticsVerification.TryRunAll(out f2)
                & PhenomenonOperationsVerification.TryRunAll(out f3)
                & DomainLawEvaluationVerification.TryRunAll(out f4)
                & PhenomenonTopologyVerification.TryRunAll(out f5)
                & CatalysisEvaluationVerification.TryRunAll(out f6)
                & AccumulationEvaluationVerification.TryRunAll(out f7)
                & PhenomenonRelationshipGroupVerification.TryRunAll(out f8)
                & SynchronizationEvaluationVerification.TryRunAll(out f9)
                & InterferenceEvaluationVerification.TryRunAll(out f10);
            return Expect(passed, "Earlier Domain verification regressed.", out failure);
        }

        private static DomainComposition Composition(params DomainLaw[] laws)
        {
            DomainComposition composition = DomainComposition.Empty;
            for (int index = 0; index < laws.Length; index++) composition = DomainCompositionEditor.Add(composition, laws[index]).Resulting;
            return composition;
        }

        private static DomainComplexityReport Report(params DomainLaw[] laws)
        {
            return DomainComplexityCalculator.Calculate(Composition(laws), DomainComplexityPolicy.Default);
        }

        private static DomainLaw Law(LawExpression expression, LawPhenomenon phenomenon, LawTerritoryPrinciple territory)
        {
            DomainLaw law; LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, territory, out law, out validation))
                throw new InvalidOperationException("Verification could not create Law: " + validation.RejectionReason);
            return law;
        }

        private static bool Approximately(float left, float right) { return Math.Abs(left - right) <= Tolerance; }
        private static bool Expect(bool condition, string message, out string failure) { failure = condition ? null : message; return condition; }
    }
}
