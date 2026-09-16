using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Read-only bridge from current-run Axiom mastery into future Domain UI and
    /// construction. It does not change the existing evidence model or persist
    /// its values.
    /// </summary>
    public static class DomainMasteryQuery
    {
        public const float DefaultEligibilityThreshold = 0.65f;

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

        private static float eligibilityThreshold = DefaultEligibilityThreshold;

        /// <summary>
        /// Centralized threshold for Domain-potential presentation. A future
        /// balance/config asset may set this once; it is never stored as mastery.
        /// </summary>
        public static float EligibilityThreshold => eligibilityThreshold;
        public static int PhenomenonCount => DomainPhenomena.Length;

        public static MasteryDomain GetPhenomenon(int index)
        {
            return DomainPhenomena[index];
        }

        public static void SetEligibilityThreshold(float threshold)
        {
            eligibilityThreshold = Mathf.Clamp01(threshold);
        }

        public static float GetMastery(AxiomMasteryState mastery, MasteryDomain phenomenon)
        {
            return mastery != null ? mastery.Get(phenomenon) : 0f;
        }

        public static bool IsEligible(AxiomMasteryState mastery, MasteryDomain phenomenon)
        {
            return GetMastery(mastery, phenomenon) >= eligibilityThreshold;
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
