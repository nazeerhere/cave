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

    /// <summary>The meaning of a phenomenon's continuous semantic coordinate, never a UI/status-stack category.</summary>
    public enum PhenomenonStateModelKind
    {
        Undetermined = 0,
        Additive = 1,
        Relative = 2,
        Pattern = 3
    }

    /// <summary>Operation eligibility in semantic-coordinate space, not physical-unit arithmetic.</summary>
    public struct PhenomenonOperationCapabilities
    {
        public PhenomenonOperationCapabilities(bool supportsAdd, bool supportsRemove, bool supportsTransfer)
        { SupportsAdd = supportsAdd; SupportsRemove = supportsRemove; SupportsTransfer = supportsTransfer; }
        public bool SupportsAdd { get; }
        public bool SupportsRemove { get; }
        public bool SupportsTransfer { get; }
        public bool Supports(PhenomenonOperationKind operation)
        {
            return operation == PhenomenonOperationKind.Add ? SupportsAdd
                : operation == PhenomenonOperationKind.Remove ? SupportsRemove
                : operation == PhenomenonOperationKind.Transfer && SupportsTransfer;
        }
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
    /// Immutable, Unity-reference-free identity of the qualifying relationship
    /// behind a Pattern phenomenon. Revision changes invalidate snapshots even
    /// when their scalar S happens to be numerically unchanged.
    /// </summary>
    public struct PhenomenonPatternContext : IEquatable<PhenomenonPatternContext>
    {
        public PhenomenonPatternContext(string identity, uint revision)
        {
            Identity = identity;
            Revision = revision;
        }

        public string Identity { get; }
        public uint Revision { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Identity);
        public bool Equals(PhenomenonPatternContext other)
        {
            return Revision == other.Revision && string.Equals(Identity, other.Identity, StringComparison.Ordinal);
        }
        public override bool Equals(object other) => other is PhenomenonPatternContext && Equals((PhenomenonPatternContext)other);
        public override int GetHashCode() => (Identity == null ? 0 : Identity.GetHashCode()) ^ (int)Revision;
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
        private readonly PhenomenonPatternContext patternContext;

        internal PhenomenonSemanticSnapshot(
            LawPhenomenon phenomenon,
            float semanticValue,
            PhenomenonSemanticRegion region)
        {
            this.phenomenon = phenomenon;
            this.semanticValue = semanticValue;
            this.region = region;
            patternContext = default(PhenomenonPatternContext);
        }

        internal PhenomenonSemanticSnapshot(
            LawPhenomenon phenomenon,
            float semanticValue,
            PhenomenonSemanticRegion region,
            PhenomenonPatternContext patternContext)
        {
            this.phenomenon = phenomenon;
            this.semanticValue = semanticValue;
            this.region = region;
            this.patternContext = patternContext;
        }

        public LawPhenomenon Phenomenon => phenomenon;
        public float SemanticValue => semanticValue;
        public PhenomenonSemanticRegion Region => region;
        public PhenomenonPatternContext PatternContext => patternContext;
    }

    /// <summary>
    /// Centralized provisional region boundaries. Values at or below the low
    /// boundary remain Low; values at or above the high boundary remain High.
    /// The classifier never clamps or mutates a source value.
    /// </summary>
    public struct PhenomenonSemanticProfile
    {
        public PhenomenonSemanticProfile(float lowMaximum, float highMinimum,
            PhenomenonStateModelKind stateModelKind, float neutralSemanticValue,
            PhenomenonOperationCapabilities operations, bool requiresCarrierBaseline = false)
        {
            LowMaximum = lowMaximum;
            HighMinimum = highMinimum;
            StateModelKind = stateModelKind;
            NeutralSemanticValue = neutralSemanticValue;
            Operations = operations;
            RequiresCarrierBaseline = requiresCarrierBaseline;
        }

        public float LowMaximum { get; }
        public float HighMinimum { get; }
        public PhenomenonStateModelKind StateModelKind { get; }
        public float NeutralSemanticValue { get; }
        public PhenomenonOperationCapabilities Operations { get; }
        public bool RequiresCarrierBaseline { get; }
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
                    profile = new PhenomenonSemanticProfile(-2f, 2f, PhenomenonStateModelKind.Additive, 0f,
                        new PhenomenonOperationCapabilities(true, true, true));
                    return true;

                case LawPhenomenon.Flow:
                    profile = new PhenomenonSemanticProfile(1f, 3f, PhenomenonStateModelKind.Additive, 0f,
                        new PhenomenonOperationCapabilities(true, true, false));
                    return true;

                case LawPhenomenon.Mass:
                    profile = new PhenomenonSemanticProfile(.75f, 1.25f, PhenomenonStateModelKind.Relative, 1f,
                        new PhenomenonOperationCapabilities(true, true, false), true);
                    return true;

                case LawPhenomenon.Compression:
                case LawPhenomenon.Potential:
                    profile = new PhenomenonSemanticProfile(-2f, 2f, PhenomenonStateModelKind.Additive, 0f,
                        new PhenomenonOperationCapabilities(true, true, false));
                    return true;

                case LawPhenomenon.Resonance:
                    profile = new PhenomenonSemanticProfile(1f, 3f, PhenomenonStateModelKind.Pattern, 0f,
                        new PhenomenonOperationCapabilities(true, true, false));
                    return true;

                case LawPhenomenon.Phase:
                    profile = new PhenomenonSemanticProfile(1f, 4f, PhenomenonStateModelKind.Pattern, 0f,
                        new PhenomenonOperationCapabilities(true, true, false));
                    return true;

                case LawPhenomenon.Order:
                    profile = new PhenomenonSemanticProfile(-2f, 2f, PhenomenonStateModelKind.Additive, 0f,
                        new PhenomenonOperationCapabilities(true, true, false));
                    return true;

                default:
                    profile = default(PhenomenonSemanticProfile);
                    return false;
            }
        }

        public static bool SupportsOperation(LawPhenomenon phenomenon, PhenomenonOperationKind operation)
        {
            PhenomenonSemanticProfile profile;
            return TryGetProfile(phenomenon, out profile) && profile.Operations.Supports(operation);
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
        /// Axiom trajectories provide the semantic scalar for all established
        /// state models. Pattern identities and Mass's physical baseline are
        /// validated by the codec, not fabricated by this scalar adapter.
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
                case AxiomKind.Compression:
                    phenomenon = LawPhenomenon.Compression;
                    break;
                case AxiomKind.Potential:
                    phenomenon = LawPhenomenon.Potential;
                    break;
                case AxiomKind.Mass:
                    phenomenon = LawPhenomenon.Mass;
                    break;
                case AxiomKind.Order:
                    phenomenon = LawPhenomenon.Order;
                    break;
                case AxiomKind.Resonance:
                    phenomenon = LawPhenomenon.Resonance;
                    break;
                case AxiomKind.Phase:
                    phenomenon = LawPhenomenon.Phase;
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
