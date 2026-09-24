using System;
using System.Collections.Generic;
using Cave.Axioms.Mastery;

namespace Cave.Domain
{
    /// <summary>Permanent maximum structural capacity tiers for personal Domain authoring.</summary>
    public enum DomainComplexityCapacityTier
    {
        None = 0,
        Seed = 1,
        TierII = 2,
        TierIII = 3,
        TierIV = 4
    }

    public sealed class DomainComplexityCapacityRequirement
    {
        internal DomainComplexityCapacityRequirement(string description, bool isSatisfied)
        {
            Description = description;
            IsSatisfied = isSatisfied;
        }

        public string Description { get; }
        public bool IsSatisfied { get; }
    }

    /// <summary>Read-only result. Querying it never changes a player's unlocked tier.</summary>
    public sealed class DomainComplexityCapacityEvaluation
    {
        internal DomainComplexityCapacityEvaluation(
            bool hasDomainSeed,
            DomainComplexityCapacityTier currentTier,
            DomainComplexityCapacityTier qualifiedTier,
            DomainComplexityCapacityTier nextTier,
            float capacity,
            IReadOnlyList<DomainComplexityCapacityRequirement> requirements)
        {
            HasDomainSeed = hasDomainSeed;
            CurrentTier = currentTier;
            QualifiedTier = qualifiedTier;
            NextTier = nextTier;
            Capacity = capacity;
            Requirements = requirements;

            List<DomainComplexityCapacityRequirement> satisfied = new List<DomainComplexityCapacityRequirement>();
            List<DomainComplexityCapacityRequirement> unsatisfied = new List<DomainComplexityCapacityRequirement>();
            for (int index = 0; index < requirements.Count; index++)
            {
                if (requirements[index].IsSatisfied) satisfied.Add(requirements[index]);
                else unsatisfied.Add(requirements[index]);
            }

            SatisfiedRequirements = satisfied.AsReadOnly();
            UnsatisfiedRequirements = unsatisfied.AsReadOnly();
        }

        public bool HasDomainSeed { get; }
        public DomainComplexityCapacityTier CurrentTier { get; }
        public DomainComplexityCapacityTier QualifiedTier { get; }
        public DomainComplexityCapacityTier NextTier { get; }
        public float Capacity { get; }
        public IReadOnlyList<DomainComplexityCapacityRequirement> Requirements { get; }
        public IReadOnlyList<DomainComplexityCapacityRequirement> SatisfiedRequirements { get; }
        public IReadOnlyList<DomainComplexityCapacityRequirement> UnsatisfiedRequirements { get; }
        public bool IsAdvancementNewlyEarned => QualifiedTier > CurrentTier;
    }

    /// <summary>
    /// Pure tier qualification and capacity math. It derives every capacity from
    /// the active structural Complexity policy; it neither stores mastery nor
    /// mutates permanent progression.
    /// </summary>
    public static class DomainComplexityCapacityProgression
    {
        public static DomainComplexityCapacityTier HighestTier => DomainComplexityCapacityTier.TierIV;

        public static DomainComplexityCapacityEvaluation Evaluate(
            bool hasDomainSeed,
            PlayerMasteryEvidenceState mastery,
            PlayerMasteryPolicy masteryPolicy,
            DomainComplexityCapacityTier permanentlyUnlockedTier)
        {
            PlayerMasteryPolicy activePolicy = masteryPolicy ?? PlayerMasteryPolicy.Default;
            PlayerMasteryEvidenceState activeMastery = mastery ?? PlayerMasteryEvidenceState.Empty;
            DomainComplexityCapacityTier stored = ClampTier(permanentlyUnlockedTier);

            if (!hasDomainSeed)
            {
                IReadOnlyList<DomainComplexityCapacityRequirement> noSeedRequirements = RequirementsFor(
                    DomainComplexityCapacityTier.Seed,
                    false,
                    activeMastery,
                    activePolicy);
                return new DomainComplexityCapacityEvaluation(
                    false,
                    DomainComplexityCapacityTier.None,
                    DomainComplexityCapacityTier.None,
                    DomainComplexityCapacityTier.Seed,
                    0f,
                    noSeedRequirements);
            }

            DomainComplexityCapacityTier current = stored < DomainComplexityCapacityTier.Seed
                ? DomainComplexityCapacityTier.Seed
                : stored;
            DomainComplexityCapacityTier qualified = DomainComplexityCapacityTier.Seed;
            for (DomainComplexityCapacityTier candidate = DomainComplexityCapacityTier.TierII;
                candidate <= HighestTier;
                candidate++)
            {
                if (!AreRequirementsSatisfied(candidate, activeMastery, activePolicy)) break;
                qualified = candidate;
            }

            DomainComplexityCapacityTier next = current < HighestTier
                ? current + 1
                : DomainComplexityCapacityTier.None;
            return new DomainComplexityCapacityEvaluation(
                true,
                current,
                qualified,
                next,
                GetCapacity(current, DomainComplexityPolicy.Default),
                RequirementsFor(next, true, activeMastery, activePolicy));
        }

        /// <summary>Returns the monotonic tier to persist after an evaluated advancement.</summary>
        public static DomainComplexityCapacityTier Commit(
            DomainComplexityCapacityTier permanentlyUnlockedTier,
            DomainComplexityCapacityEvaluation evaluation)
        {
            DomainComplexityCapacityTier current = ClampTier(permanentlyUnlockedTier);
            if (evaluation == null || !evaluation.HasDomainSeed) return current;
            return evaluation.QualifiedTier > current ? evaluation.QualifiedTier : current;
        }

        public static float GetCapacity(DomainComplexityCapacityTier tier, DomainComplexityPolicy policy)
        {
            int lawCount = (int)ClampTier(tier);
            if (lawCount <= 0) return 0f;

            DomainComplexityPolicy activePolicy = policy ?? DomainComplexityPolicy.Default;
            float pairs = lawCount * (lawCount - 1) / 2f;
            return Math.Max(0f,
                lawCount * activePolicy.BaseLawCost
                + pairs * activePolicy.LawInteractionCoefficient
                + pairs * activePolicy.PhenomenonDiversityCoefficient
                + pairs * activePolicy.TerritoryDiversityCoefficient);
        }

        public static string GetDisplayName(DomainComplexityCapacityTier tier)
        {
            switch (ClampTier(tier))
            {
                case DomainComplexityCapacityTier.Seed: return "CAPACITY TIER I";
                case DomainComplexityCapacityTier.TierII: return "CAPACITY TIER II";
                case DomainComplexityCapacityTier.TierIII: return "CAPACITY TIER III";
                case DomainComplexityCapacityTier.TierIV: return "CAPACITY TIER IV";
                default: return "CAPACITY LOCKED";
            }
        }

        private static bool AreRequirementsSatisfied(
            DomainComplexityCapacityTier tier,
            PlayerMasteryEvidenceState mastery,
            PlayerMasteryPolicy policy)
        {
            IReadOnlyList<DomainComplexityCapacityRequirement> requirements = RequirementsFor(tier, true, mastery, policy);
            for (int index = 0; index < requirements.Count; index++)
            {
                if (!requirements[index].IsSatisfied) return false;
            }

            return true;
        }

        private static IReadOnlyList<DomainComplexityCapacityRequirement> RequirementsFor(
            DomainComplexityCapacityTier tier,
            bool hasDomainSeed,
            PlayerMasteryEvidenceState mastery,
            PlayerMasteryPolicy policy)
        {
            List<DomainComplexityCapacityRequirement> requirements = new List<DomainComplexityCapacityRequirement>();
            if (tier == DomainComplexityCapacityTier.None) return requirements.AsReadOnly();
            if (tier == DomainComplexityCapacityTier.Seed)
            {
                requirements.Add(new DomainComplexityCapacityRequirement("Awaken a Domain Seed", hasDomainSeed));
                return requirements.AsReadOnly();
            }

            int requiredPhenomena = tier == DomainComplexityCapacityTier.TierII ? 2
                : tier == DomainComplexityCapacityTier.TierIII ? 3
                : 4;
            int masteredPhenomena = CountMasteredPhenomena(mastery, policy);
            requirements.Add(new DomainComplexityCapacityRequirement(
                "Master " + requiredPhenomena + " phenomena",
                masteredPhenomena >= requiredPhenomena));
            requirements.Add(new DomainComplexityCapacityRequirement(
                "Master Projectile expression",
                mastery.GetProjectileReport(policy).IsMastered));

            if (tier >= DomainComplexityCapacityTier.TierIII)
            {
                requirements.Add(new DomainComplexityCapacityRequirement(
                    "Master Frenzy expression",
                    mastery.IsFrenzyMastered(policy)));
            }

            return requirements.AsReadOnly();
        }

        private static int CountMasteredPhenomena(PlayerMasteryEvidenceState mastery, PlayerMasteryPolicy policy)
        {
            int count = 0;
            foreach (LawPhenomenon phenomenon in (LawPhenomenon[])Enum.GetValues(typeof(LawPhenomenon)))
            {
                if (mastery.GetPhenomenonReport(phenomenon, policy).IsMastered) count++;
            }

            return count;
        }

        private static DomainComplexityCapacityTier ClampTier(DomainComplexityCapacityTier tier)
        {
            if (tier < DomainComplexityCapacityTier.None) return DomainComplexityCapacityTier.None;
            return tier > HighestTier ? HighestTier : tier;
        }
    }
}
