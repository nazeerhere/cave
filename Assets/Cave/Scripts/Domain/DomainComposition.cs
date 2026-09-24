using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Immutable authored Law collection. Its order is display/trace order, never execution priority.</summary>
    public sealed class DomainComposition
    {
        private readonly IReadOnlyList<DomainLaw> orderedLaws;

        internal DomainComposition(IEnumerable<DomainLaw> laws)
        {
            orderedLaws = new List<DomainLaw>(laws).AsReadOnly();
        }

        public static DomainComposition Empty { get; } = new DomainComposition(new DomainLaw[0]);
        public IReadOnlyList<DomainLaw> OrderedLaws => orderedLaws;
        public int Count => orderedLaws.Count;

        public bool Contains(DomainLaw law)
        {
            if (law == null) return false;
            for (int index = 0; index < orderedLaws.Count; index++)
            {
                if (orderedLaws[index].Equals(law)) return true;
            }
            return false;
        }

        public int UniquePhenomenonCount => CountDistinctPhenomena();
        public int UniqueTerritoryPrincipleCount => CountDistinctTerritories();
        public int UniqueExpressionCount => CountDistinctExpressions();

        private int CountDistinctPhenomena()
        {
            HashSet<LawPhenomenon> values = new HashSet<LawPhenomenon>();
            for (int index = 0; index < orderedLaws.Count; index++) values.Add(orderedLaws[index].Phenomenon);
            return values.Count;
        }

        private int CountDistinctTerritories()
        {
            HashSet<LawTerritoryPrinciple> values = new HashSet<LawTerritoryPrinciple>();
            for (int index = 0; index < orderedLaws.Count; index++) values.Add(orderedLaws[index].TerritoryPrinciple);
            return values.Count;
        }

        private int CountDistinctExpressions()
        {
            HashSet<LawExpression> values = new HashSet<LawExpression>();
            for (int index = 0; index < orderedLaws.Count; index++) values.Add(orderedLaws[index].Expression);
            return values.Count;
        }
    }

    public enum DomainCompositionMutationAction { Add = 1, Remove = 2 }
    public enum DomainCompositionMutationRejectionReason { None = 0, InvalidLaw = 1, DuplicateLaw = 2, LawNotFound = 3 }

    /// <summary>Immutable outcome for authoring a Law collection.</summary>
    public sealed class DomainCompositionMutationResult
    {
        internal DomainCompositionMutationResult(DomainComposition previous, DomainComposition resulting,
            DomainCompositionMutationAction action, DomainLaw law, bool succeeded,
            DomainCompositionMutationRejectionReason rejection)
        {
            Previous = previous;
            Resulting = resulting;
            Action = action;
            Law = law;
            Succeeded = succeeded;
            RejectionReason = rejection;
        }

        public DomainComposition Previous { get; }
        public DomainComposition Resulting { get; }
        public DomainCompositionMutationAction Action { get; }
        public DomainLaw Law { get; }
        public bool Succeeded { get; }
        public DomainCompositionMutationRejectionReason RejectionReason { get; }
        public bool Changed => Succeeded && Previous != Resulting;
    }

    /// <summary>Pure immutable authoring operations; they do not check runtime eligibility or capacity.</summary>
    public static class DomainCompositionEditor
    {
        public static DomainCompositionMutationResult Add(DomainComposition composition, DomainLaw law)
        {
            DomainComposition previous = composition ?? DomainComposition.Empty;
            if (!IsValidLaw(law)) return Rejected(previous, DomainCompositionMutationAction.Add, law,
                DomainCompositionMutationRejectionReason.InvalidLaw);
            if (previous.Contains(law)) return Rejected(previous, DomainCompositionMutationAction.Add, law,
                DomainCompositionMutationRejectionReason.DuplicateLaw);

            List<DomainLaw> laws = new List<DomainLaw>(previous.OrderedLaws);
            laws.Add(law);
            return new DomainCompositionMutationResult(previous, new DomainComposition(laws),
                DomainCompositionMutationAction.Add, law, true, DomainCompositionMutationRejectionReason.None);
        }

        public static DomainCompositionMutationResult Remove(DomainComposition composition, DomainLaw law)
        {
            DomainComposition previous = composition ?? DomainComposition.Empty;
            if (!IsValidLaw(law)) return Rejected(previous, DomainCompositionMutationAction.Remove, law,
                DomainCompositionMutationRejectionReason.InvalidLaw);
            int index = -1;
            for (int candidateIndex = 0; candidateIndex < previous.OrderedLaws.Count; candidateIndex++)
            {
                if (previous.OrderedLaws[candidateIndex].Equals(law))
                {
                    index = candidateIndex;
                    break;
                }
            }
            if (index < 0) return Rejected(previous, DomainCompositionMutationAction.Remove, law,
                DomainCompositionMutationRejectionReason.LawNotFound);

            List<DomainLaw> laws = new List<DomainLaw>(previous.OrderedLaws);
            laws.RemoveAt(index);
            return new DomainCompositionMutationResult(previous, new DomainComposition(laws),
                DomainCompositionMutationAction.Remove, law, true, DomainCompositionMutationRejectionReason.None);
        }

        private static bool IsValidLaw(DomainLaw law)
        {
            if (law == null) return false;
            return DomainLaw.Validate(law.Expression, law.Phenomenon, law.TerritoryPrinciple).IsValid;
        }

        private static DomainCompositionMutationResult Rejected(DomainComposition previous,
            DomainCompositionMutationAction action, DomainLaw law, DomainCompositionMutationRejectionReason rejection)
        {
            return new DomainCompositionMutationResult(previous, previous, action, law, false, rejection);
        }
    }

    /// <summary>Centralized provisional complexity coefficients; Expression diversity is intentionally absent.</summary>
    public sealed class DomainComplexityPolicy
    {
        public DomainComplexityPolicy(float baseLawCost, float lawInteractionCoefficient,
            float phenomenonDiversityCoefficient, float territoryDiversityCoefficient)
        {
            BaseLawCost = baseLawCost;
            LawInteractionCoefficient = lawInteractionCoefficient;
            PhenomenonDiversityCoefficient = phenomenonDiversityCoefficient;
            TerritoryDiversityCoefficient = territoryDiversityCoefficient;
        }

        public static DomainComplexityPolicy Default { get; } = new DomainComplexityPolicy(1f, 1f, 1f, 1f);
        public float BaseLawCost { get; }
        public float LawInteractionCoefficient { get; }
        public float PhenomenonDiversityCoefficient { get; }
        public float TerritoryDiversityCoefficient { get; }
    }

    /// <summary>Immutable inspectable provisional committed Complexity report, not a capacity decision.</summary>
    public sealed class DomainComplexityReport
    {
        internal DomainComplexityReport(int lawCount, int phenomenonCount, int territoryCount, int expressionCount,
            IReadOnlyList<float> orderedLawBaseCosts, float baseCost, float lawInteractionCost,
            float phenomenonDiversityCost, float territoryDiversityCost)
        {
            LawCount = lawCount;
            UniquePhenomenonCount = phenomenonCount;
            UniqueTerritoryPrincipleCount = territoryCount;
            UniqueExpressionCount = expressionCount;
            OrderedLawBaseCosts = orderedLawBaseCosts;
            BaseLawCost = baseCost;
            LawInteractionCost = lawInteractionCost;
            PhenomenonDiversityCost = phenomenonDiversityCost;
            TerritoryDiversityCost = territoryDiversityCost;
        }

        public int LawCount { get; }
        public int UniquePhenomenonCount { get; }
        public int UniqueTerritoryPrincipleCount { get; }
        public int UniqueExpressionCount { get; }
        public IReadOnlyList<float> OrderedLawBaseCosts { get; }
        public float BaseLawCost { get; }
        public float LawInteractionCost { get; }
        public float PhenomenonDiversityCost { get; }
        public float TerritoryDiversityCost { get; }
        public float TotalComplexity => BaseLawCost + LawInteractionCost + PhenomenonDiversityCost + TerritoryDiversityCost;
    }

    /// <summary>Pure calculator only; it executes no Law and makes no Complexity-capacity decision.</summary>
    public static class DomainComplexityCalculator
    {
        public static DomainComplexityReport Calculate(DomainComposition composition, DomainComplexityPolicy policy)
        {
            DomainComposition source = composition ?? DomainComposition.Empty;
            DomainComplexityPolicy activePolicy = policy ?? DomainComplexityPolicy.Default;
            List<float> perLawCosts = new List<float>();
            float baseCost = 0f;
            for (int index = 0; index < source.OrderedLaws.Count; index++)
            {
                float cost = ResolveBaseLawCost(source.OrderedLaws[index], activePolicy);
                perLawCosts.Add(cost);
                baseCost += cost;
            }
            float lawInteraction = activePolicy.LawInteractionCoefficient * PairCount(source.Count);
            float phenomenonDiversity = activePolicy.PhenomenonDiversityCoefficient * PairCount(source.UniquePhenomenonCount);
            float territoryDiversity = activePolicy.TerritoryDiversityCoefficient * PairCount(source.UniqueTerritoryPrincipleCount);
            return new DomainComplexityReport(source.Count, source.UniquePhenomenonCount,
                source.UniqueTerritoryPrincipleCount, source.UniqueExpressionCount, perLawCosts.AsReadOnly(),
                baseCost, lawInteraction, phenomenonDiversity, territoryDiversity);
        }

        // Single seam for future per-Law metadata or a cost provider; Sprint 11 intentionally uses uniform policy cost.
        private static float ResolveBaseLawCost(DomainLaw law, DomainComplexityPolicy policy)
        {
            return policy.BaseLawCost;
        }

        private static float PairCount(int count)
        {
            return count * (count - 1) / 2f;
        }
    }
}
