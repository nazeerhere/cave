using System;
using Cave.Axioms;

namespace Cave.Domain
{
    /// <summary>
    /// Semantic position of an ordinary phenomenon. This is intentionally
    /// distinct from Axiom trajectory direction, rate intensity, curvature, and
    /// any gameplay status.
    /// </summary>
    public enum PhenomenonSemanticRegion
    {
        Low = 1,
        Regular = 2,
        High = 3
    }

    /// <summary>Deterministic reasons a semantic snapshot cannot be produced.</summary>
    public enum PhenomenonSemanticRejectionReason
    {
        None = 0,
        InvalidPhenomenon = 1,
        InvalidSemanticValue = 2,
        InvalidNaturalMassBaseline = 3,
        UnsupportedAxiomKind = 4
    }

    /// <summary>Immutable result of semantic classification or adapter validation.</summary>
    public struct PhenomenonSemanticResult
    {
        private readonly bool isValid;
        private readonly PhenomenonSemanticRejectionReason rejectionReason;

        private PhenomenonSemanticResult(bool isValid, PhenomenonSemanticRejectionReason rejectionReason)
        {
            this.isValid = isValid;
            this.rejectionReason = rejectionReason;
        }

        public bool IsValid => isValid;
        public PhenomenonSemanticRejectionReason RejectionReason => rejectionReason;

        public static PhenomenonSemanticResult Valid()
        {
            return new PhenomenonSemanticResult(true, PhenomenonSemanticRejectionReason.None);
        }

        public static PhenomenonSemanticResult Rejected(PhenomenonSemanticRejectionReason reason)
        {
            return new PhenomenonSemanticResult(false, reason);
        }
    }

    /// <summary>
    /// Immutable scalar snapshot for future pure Law resolution. Its value is a
    /// semantic value: Mass uses a natural-baseline ratio; every other current
    /// phenomenon uses its documented scalar axis. It owns no history or rate.
    /// </summary>
    public sealed class PhenomenonSemanticSnapshot
    {
        private readonly LawPhenomenon phenomenon;
        private readonly float semanticValue;
        private readonly PhenomenonSemanticRegion region;

        internal PhenomenonSemanticSnapshot(
            LawPhenomenon phenomenon,
            float semanticValue,
            PhenomenonSemanticRegion region)
        {
            this.phenomenon = phenomenon;
            this.semanticValue = semanticValue;
            this.region = region;
        }

        public LawPhenomenon Phenomenon => phenomenon;
        public float SemanticValue => semanticValue;
        public PhenomenonSemanticRegion Region => region;
    }

    /// <summary>
    /// Centralized provisional region boundaries. Values at or below the low
    /// boundary remain Low; values at or above the high boundary remain High.
    /// The classifier never clamps or mutates a source value.
    /// </summary>
    public struct PhenomenonSemanticProfile
    {
        public PhenomenonSemanticProfile(float lowMaximum, float highMinimum)
        {
            LowMaximum = lowMaximum;
            HighMinimum = highMinimum;
        }

        public float LowMaximum { get; }
        public float HighMinimum { get; }
    }

    /// <summary>
    /// Pure, centralized conversion from a valid ordinary phenomenon scalar to
    /// its semantic region. It neither records history nor changes gameplay.
    /// </summary>
    public static class PhenomenonSemanticClassifier
    {
        public static bool TryGetProfile(LawPhenomenon phenomenon, out PhenomenonSemanticProfile profile)
        {
            switch (phenomenon)
            {
                case LawPhenomenon.Heat:
                case LawPhenomenon.Compression:
                case LawPhenomenon.Potential:
                case LawPhenomenon.Order:
                    profile = new PhenomenonSemanticProfile(-2f, 2f);
                    return true;

                case LawPhenomenon.Flow:
                case LawPhenomenon.Resonance:
                    profile = new PhenomenonSemanticProfile(1f, 3f);
                    return true;

                case LawPhenomenon.Phase:
                    profile = new PhenomenonSemanticProfile(1f, 4f);
                    return true;

                case LawPhenomenon.Mass:
                    profile = new PhenomenonSemanticProfile(.75f, 1.25f);
                    return true;

                default:
                    profile = default(PhenomenonSemanticProfile);
                    return false;
            }
        }

        public static bool TryClassify(
            LawPhenomenon phenomenon,
            float semanticValue,
            out PhenomenonSemanticRegion region,
            out PhenomenonSemanticResult result)
        {
            if (!TryGetProfile(phenomenon, out PhenomenonSemanticProfile profile))
            {
                region = default(PhenomenonSemanticRegion);
                result = PhenomenonSemanticResult.Rejected(PhenomenonSemanticRejectionReason.InvalidPhenomenon);
                return false;
            }

            if (!IsFinite(semanticValue))
            {
                region = default(PhenomenonSemanticRegion);
                result = PhenomenonSemanticResult.Rejected(PhenomenonSemanticRejectionReason.InvalidSemanticValue);
                return false;
            }

            region = semanticValue <= profile.LowMaximum
                ? PhenomenonSemanticRegion.Low
                : semanticValue >= profile.HighMinimum
                    ? PhenomenonSemanticRegion.High
                    : PhenomenonSemanticRegion.Regular;
            result = PhenomenonSemanticResult.Valid();
            return true;
        }

        public static bool TryCreateSnapshot(
            LawPhenomenon phenomenon,
            float semanticValue,
            out PhenomenonSemanticSnapshot snapshot,
            out PhenomenonSemanticResult result)
        {
            PhenomenonSemanticRegion region;
            if (!TryClassify(phenomenon, semanticValue, out region, out result))
            {
                snapshot = null;
                return false;
            }

            snapshot = new PhenomenonSemanticSnapshot(phenomenon, semanticValue, region);
            return true;
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Narrow, read-only adapters from existing heterogeneous state into the
    /// ordinary semantic layer. They never mutate their sources or imply that
    /// current combat state already has the full locked semantic meaning.
    /// </summary>
    public static class PhenomenonSemanticAdapters
    {
        /// <summary>
        /// Heat and Flow currently expose scalar Axiom trajectories whose raw
        /// values can be classified directly. Order is intentionally excluded:
        /// existing Order application is nonnegative and has no instability side.
        /// Mass is intentionally excluded because it requires baseline ratio.
        /// </summary>
        public static bool TryFromAxiomTrajectory(
            AxiomTrajectoryState trajectory,
            out PhenomenonSemanticSnapshot snapshot,
            out PhenomenonSemanticResult result)
        {
            LawPhenomenon phenomenon;
            switch (trajectory.Kind)
            {
                case AxiomKind.Heat:
                    phenomenon = LawPhenomenon.Heat;
                    break;
                case AxiomKind.Flow:
                    phenomenon = LawPhenomenon.Flow;
                    break;
                default:
                    snapshot = null;
                    result = PhenomenonSemanticResult.Rejected(PhenomenonSemanticRejectionReason.UnsupportedAxiomKind);
                    return false;
            }

            return PhenomenonSemanticClassifier.TryCreateSnapshot(
                phenomenon,
                trajectory.CurrentValue,
                out snapshot,
                out result);
        }

        /// <summary>
        /// Converts absolute effective Mass through an explicit natural baseline.
        /// No baseline is fabricated when the source has not defined one.
        /// </summary>
        public static bool TryFromMassRelativeToBaseline(
            float effectiveMass,
            float naturalMassBaseline,
            out PhenomenonSemanticSnapshot snapshot,
            out PhenomenonSemanticResult result)
        {
            if (!PhenomenonSemanticClassifier.IsFinite(effectiveMass)
                || !PhenomenonSemanticClassifier.IsFinite(naturalMassBaseline)
                || naturalMassBaseline <= 0f)
            {
                snapshot = null;
                result = PhenomenonSemanticResult.Rejected(PhenomenonSemanticRejectionReason.InvalidNaturalMassBaseline);
                return false;
            }

            return PhenomenonSemanticClassifier.TryCreateSnapshot(
                LawPhenomenon.Mass,
                effectiveMass / naturalMassBaseline,
                out snapshot,
                out result);
        }

        public static bool TryFromPhaseLatentStacks(
            int latentStacks,
            out PhenomenonSemanticSnapshot snapshot,
            out PhenomenonSemanticResult result)
        {
            return PhenomenonSemanticClassifier.TryCreateSnapshot(
                LawPhenomenon.Phase,
                latentStacks,
                out snapshot,
                out result);
        }

        public static bool TryFromResonanceProgress(
            int progress,
            out PhenomenonSemanticSnapshot snapshot,
            out PhenomenonSemanticResult result)
        {
            return PhenomenonSemanticClassifier.TryCreateSnapshot(
                LawPhenomenon.Resonance,
                progress,
                out snapshot,
                out result);
        }
    }
}
