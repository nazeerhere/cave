using Cave.Axioms;

namespace Cave.Domain
{
    public enum AxiomDomainSemanticCapability { Unsupported = 0, ReadOnly = 1, ReadWrite = 2 }
    public enum AxiomDomainSemanticCodecRejection
    {
        None = 0, UnsupportedPhenomenon = 1, SemanticMappingNotEstablished = 2,
        InvalidSemanticValue = 3, MissingRuntimeState = 4, TrajectoryUnavailable = 5,
        StateOutsideRuntimeBounds = 6, MissingNaturalMassBaseline = 7, MissingPatternContext = 8
    }

    /// <summary>
    /// The sole Domain-facing conversion boundary for live Axiom S/R/A. It
    /// projects immutable semantic snapshots and resolves approved semantic
    /// final values back to S only; Rate and Acceleration remain untouched.
    /// </summary>
    public static class AxiomDomainSemanticCodec
    {
        public static AxiomDomainSemanticCapability GetCapability(LawPhenomenon phenomenon)
        {
            switch (phenomenon)
            {
                case LawPhenomenon.Heat:
                case LawPhenomenon.Flow:
                case LawPhenomenon.Compression:
                case LawPhenomenon.Potential:
                case LawPhenomenon.Mass:
                case LawPhenomenon.Order:
                case LawPhenomenon.Resonance:
                case LawPhenomenon.Phase:
                    return AxiomDomainSemanticCapability.ReadWrite;
                default:
                    return AxiomDomainSemanticCapability.Unsupported;
            }
        }

        public static bool TryMapKind(AxiomKind kind, out LawPhenomenon phenomenon)
        {
            switch (kind)
            {
                case AxiomKind.Heat: phenomenon = LawPhenomenon.Heat; return true;
                case AxiomKind.Flow: phenomenon = LawPhenomenon.Flow; return true;
                case AxiomKind.Mass: phenomenon = LawPhenomenon.Mass; return true;
                case AxiomKind.Compression: phenomenon = LawPhenomenon.Compression; return true;
                case AxiomKind.Potential: phenomenon = LawPhenomenon.Potential; return true;
                case AxiomKind.Resonance: phenomenon = LawPhenomenon.Resonance; return true;
                case AxiomKind.Phase: phenomenon = LawPhenomenon.Phase; return true;
                case AxiomKind.Order: phenomenon = LawPhenomenon.Order; return true;
                default: phenomenon = default(LawPhenomenon); return false;
            }
        }

        public static bool TryMapPhenomenon(LawPhenomenon phenomenon, out AxiomKind kind)
        {
            switch (phenomenon)
            {
                case LawPhenomenon.Heat: kind = AxiomKind.Heat; return true;
                case LawPhenomenon.Flow: kind = AxiomKind.Flow; return true;
                case LawPhenomenon.Mass: kind = AxiomKind.Mass; return true;
                case LawPhenomenon.Compression: kind = AxiomKind.Compression; return true;
                case LawPhenomenon.Potential: kind = AxiomKind.Potential; return true;
                case LawPhenomenon.Resonance: kind = AxiomKind.Resonance; return true;
                case LawPhenomenon.Phase: kind = AxiomKind.Phase; return true;
                case LawPhenomenon.Order: kind = AxiomKind.Order; return true;
                default: kind = default(AxiomKind); return false;
            }
        }

        public static bool TryProject(AxiomRuntimeState runtime, LawPhenomenon phenomenon, float timestamp,
            out PhenomenonSemanticSnapshot snapshot, out AxiomDomainSemanticCodecRejection rejection)
        {
            snapshot = null;
            if (runtime == null) { rejection = AxiomDomainSemanticCodecRejection.MissingRuntimeState; return false; }
            AxiomKind kind;
            if (!TryMapPhenomenon(phenomenon, out kind)) { rejection = AxiomDomainSemanticCodecRejection.UnsupportedPhenomenon; return false; }
            if (GetCapability(phenomenon) == AxiomDomainSemanticCapability.Unsupported)
            { rejection = AxiomDomainSemanticCodecRejection.SemanticMappingNotEstablished; return false; }
            if (phenomenon == LawPhenomenon.Mass && !runtime.HasNaturalMassBaseline())
            { rejection = AxiomDomainSemanticCodecRejection.MissingNaturalMassBaseline; return false; }

            AxiomTrajectoryState trajectory;
            if (!runtime.TryGetTrajectory(kind, timestamp, out trajectory))
            { rejection = AxiomDomainSemanticCodecRejection.TrajectoryUnavailable; return false; }
            PhenomenonSemanticResult semantic;
            if (!PhenomenonSemanticAdapters.TryFromAxiomTrajectory(trajectory, out snapshot, out semantic)
                || snapshot == null || snapshot.Phenomenon != phenomenon)
            { snapshot = null; rejection = AxiomDomainSemanticCodecRejection.InvalidSemanticValue; return false; }
            if (phenomenon == LawPhenomenon.Resonance || phenomenon == LawPhenomenon.Phase)
            {
                string identity;
                uint revision;
                if (!runtime.TryReadPatternContext(kind, out identity, out revision))
                {
                    snapshot = null;
                    rejection = AxiomDomainSemanticCodecRejection.MissingPatternContext;
                    return false;
                }
                snapshot = new PhenomenonSemanticSnapshot(phenomenon, snapshot.SemanticValue, snapshot.Region,
                    new PhenomenonPatternContext(identity, revision));
            }
            rejection = AxiomDomainSemanticCodecRejection.None;
            return true;
        }

        public static bool TryResolveCommittedState(AxiomRuntimeState runtime, LawPhenomenon phenomenon, float finalSemanticValue,
            out float resultingState, out AxiomDomainSemanticCodecRejection rejection)
        {
            resultingState = 0f;
            if (runtime == null)
            { rejection = AxiomDomainSemanticCodecRejection.MissingRuntimeState; return false; }
            AxiomKind ignoredKind;
            if (!TryMapPhenomenon(phenomenon, out ignoredKind))
            { rejection = AxiomDomainSemanticCodecRejection.UnsupportedPhenomenon; return false; }
            if (GetCapability(phenomenon) != AxiomDomainSemanticCapability.ReadWrite)
            { rejection = AxiomDomainSemanticCodecRejection.SemanticMappingNotEstablished; return false; }
            if ((phenomenon == LawPhenomenon.Resonance || phenomenon == LawPhenomenon.Phase)
                && !runtime.TryReadPatternContext(ignoredKind, out _, out _))
            { rejection = AxiomDomainSemanticCodecRejection.MissingPatternContext; return false; }
            if (phenomenon == LawPhenomenon.Mass && !runtime.HasNaturalMassBaseline())
            { rejection = AxiomDomainSemanticCodecRejection.MissingNaturalMassBaseline; return false; }
            PhenomenonSemanticSnapshot ignoredSnapshot; PhenomenonSemanticResult semantic;
            if (!PhenomenonSemanticClassifier.TryCreateSnapshot(phenomenon, finalSemanticValue,
                out ignoredSnapshot, out semantic))
            { rejection = AxiomDomainSemanticCodecRejection.InvalidSemanticValue; return false; }
            float minimum;
            float maximum;
            if (!runtime.TryGetDomainStateBounds(ignoredKind, out minimum, out maximum)
                || finalSemanticValue < minimum || finalSemanticValue > maximum)
            { rejection = AxiomDomainSemanticCodecRejection.StateOutsideRuntimeBounds; return false; }
            resultingState = finalSemanticValue;
            rejection = AxiomDomainSemanticCodecRejection.None;
            return true;
        }
    }
}
