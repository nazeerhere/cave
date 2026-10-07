using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Read-only familiarity bridge for legacy Axiom presentation. It is not
    /// Domain authoring authority; that belongs to DomainControlMasteryQuery.
    /// </summary>
    public static class DomainMasteryQuery
    {
        public const float DefaultFamiliarityHighlightThreshold = 0.65f;
        /// <summary>Legacy compatibility alias; this is not authoring eligibility.</summary>
        public const float DefaultEligibilityThreshold = DefaultFamiliarityHighlightThreshold;

        private static readonly MasteryDomain[] DomainPhenomena =
        {
            MasteryDomain.Heat,
            MasteryDomain.Order,
            MasteryDomain.Flow,
            MasteryDomain.Mass,
            MasteryDomain.Phase,
            MasteryDomain.Resonance,
            MasteryDomain.StoneglassPrecision
        };

        private static float familiarityHighlightThreshold = DefaultFamiliarityHighlightThreshold;

        /// <summary>
        /// Centralized legacy-presentation threshold. It has no authoring role.
        /// </summary>
        public static float FamiliarityHighlightThreshold => familiarityHighlightThreshold;
        /// <summary>Legacy compatibility alias; this is not authoring eligibility.</summary>
        public static float EligibilityThreshold => FamiliarityHighlightThreshold;
        public static int PhenomenonCount => DomainPhenomena.Length;

        public static MasteryDomain GetPhenomenon(int index)
        {
            return DomainPhenomena[index];
        }

        public static void SetFamiliarityHighlightThreshold(float threshold)
        {
            familiarityHighlightThreshold = Mathf.Clamp01(threshold);
        }

        /// <summary>Legacy compatibility alias; this is not authoring eligibility.</summary>
        public static void SetEligibilityThreshold(float threshold)
        {
            SetFamiliarityHighlightThreshold(threshold);
        }

        public static float GetFamiliarity(AxiomMasteryState mastery, MasteryDomain phenomenon)
        {
            return mastery != null ? mastery.Get(phenomenon) : 0f;
        }

        /// <summary>Legacy compatibility alias for ordinary-use familiarity.</summary>
        public static float GetMastery(AxiomMasteryState mastery, MasteryDomain phenomenon)
        {
            return GetFamiliarity(mastery, phenomenon);
        }

        public static bool IsFamiliarityHighlighted(AxiomMasteryState mastery, MasteryDomain phenomenon)
        {
            return GetFamiliarity(mastery, phenomenon) >= familiarityHighlightThreshold;
        }

        /// <summary>Legacy compatibility alias; this is not authoring eligibility.</summary>
        public static bool IsEligible(AxiomMasteryState mastery, MasteryDomain phenomenon)
        {
            return IsFamiliarityHighlighted(mastery, phenomenon);
        }

        public static string GetDisplayName(MasteryDomain phenomenon)
        {
            switch (phenomenon)
            {
                case MasteryDomain.StoneglassPrecision:
                    return "STONEGLASS PRECISION";
                default:
                    return phenomenon.ToString().ToUpperInvariant();
            }
        }
    }
}
