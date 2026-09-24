using System;
using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Single permanent-progression entry point for the Domain Seed. PlayerPrefs
    /// is supported by Unity WebGL browser storage; the Seed and maximum
    /// Complexity Capacity tier are stored here. Current-run mastery and Domain
    /// configuration are not saved.
    /// </summary>
    public static class DomainProgression
    {
        public const string DomainSeedKey = "Cave.Domain.Seed.Acquired";
        public const string ComplexityCapacityTierKey = "Cave.Domain.ComplexityCapacityTier";

        public static event Action DomainSeedGranted;
        public static event Action<DomainComplexityCapacityTier> ComplexityCapacityTierUnlocked;

        public static bool HasDomainSeed => PlayerPrefs.GetInt(DomainSeedKey, 0) != 0;

        public static DomainSeed CurrentSeed => new DomainSeed(HasDomainSeed);

        public static DomainComplexityCapacityTier CurrentComplexityCapacityTier
        {
            get
            {
                DomainComplexityCapacityTier stored = (DomainComplexityCapacityTier)Mathf.Clamp(
                    PlayerPrefs.GetInt(ComplexityCapacityTierKey, 0),
                    (int)DomainComplexityCapacityTier.None,
                    (int)DomainComplexityCapacityProgression.HighestTier);
                if (!HasDomainSeed)
                {
                    return DomainComplexityCapacityTier.None;
                }

                return stored < DomainComplexityCapacityTier.Seed
                    ? DomainComplexityCapacityTier.Seed
                    : stored;
            }
        }

        public static float CurrentComplexityCapacity => DomainComplexityCapacityProgression.GetCapacity(
            CurrentComplexityCapacityTier,
            DomainComplexityPolicy.Default);

        public static DomainComplexityCapacityEvaluation EvaluateComplexityCapacity(
            PlayerMasteryEvidenceState currentRunEvidence,
            PlayerMasteryPolicy masteryPolicy)
        {
            return DomainComplexityCapacityProgression.Evaluate(
                HasDomainSeed,
                currentRunEvidence,
                masteryPolicy,
                CurrentComplexityCapacityTier);
        }

        /// <summary>Persists only a newly earned maximum tier; current-run evidence remains run-local.</summary>
        public static DomainComplexityCapacityEvaluation TryAdvanceComplexityCapacity(
            PlayerMasteryEvidenceState currentRunEvidence,
            PlayerMasteryPolicy masteryPolicy)
        {
            DomainComplexityCapacityEvaluation evaluation = EvaluateComplexityCapacity(currentRunEvidence, masteryPolicy);
            DomainComplexityCapacityTier committed = DomainComplexityCapacityProgression.Commit(
                CurrentComplexityCapacityTier,
                evaluation);
            if (committed > CurrentComplexityCapacityTier)
            {
                PlayerPrefs.SetInt(ComplexityCapacityTierKey, (int)committed);
                PlayerPrefs.Save();
                ComplexityCapacityTierUnlocked?.Invoke(committed);
                return EvaluateComplexityCapacity(currentRunEvidence, masteryPolicy);
            }

            return evaluation;
        }

        /// <summary>Builds the existing authoritative authoring context with real persistent capacity.</summary>
        public static DomainAuthoringContext CreateAuthoringContext(
            PlayerMasteryEvidenceState currentRunEvidence,
            PlayerMasteryPolicy masteryPolicy,
            DomainComplexityPolicy complexityPolicy)
        {
            DomainComplexityPolicy activeComplexityPolicy = complexityPolicy ?? DomainComplexityPolicy.Default;
            return new DomainAuthoringContext(
                HasDomainSeed,
                currentRunEvidence,
                DomainComplexityCapacityProgression.GetCapacity(CurrentComplexityCapacityTier, activeComplexityPolicy),
                masteryPolicy ?? PlayerMasteryPolicy.Default,
                activeComplexityPolicy);
        }

        /// <returns>True only when this call newly grants the persistent seed.</returns>
        public static bool GrantDomainSeed()
        {
            if (HasDomainSeed)
            {
                return false;
            }

            PlayerPrefs.SetInt(DomainSeedKey, 1);
            PlayerPrefs.Save();
            DomainSeedGranted?.Invoke();
            return true;
        }
    }
}
