using Cave.Axioms;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Minimal Domain runtime-target adapter over the authoritative Axiom S/R/A owner. Unsupported semantic inverses fail closed.</summary>
    public sealed class AxiomRuntimeDomainTarget : IDomainPhenomenonRuntimeTarget
    {
        private readonly AxiomRuntimeState runtime;
        private readonly LawPhenomenon phenomenon;
        private readonly float timestamp;
        public AxiomRuntimeDomainTarget(AxiomRuntimeState runtime,LawPhenomenon phenomenon,float timestamp)
        { this.runtime=runtime;this.phenomenon=phenomenon;this.timestamp=timestamp; }
        public bool TryRead(out PhenomenonSemanticSnapshot snapshot)
        {
            snapshot=null;
            return runtime!=null&&runtime.isActiveAndEnabled&&runtime.TryReadDomainSemantic(phenomenon,timestamp,out snapshot);
        }
        public bool TryValidateWrite(PhenomenonSemanticSnapshot finalSnapshot,out DomainRuntimeTargetRejectionReason rejection)
        {
            if(finalSnapshot==null||finalSnapshot.Phenomenon!=phenomenon){rejection=DomainRuntimeTargetRejectionReason.InvalidFinalState;return false;}
            float ignoredState; AxiomDomainSemanticCodecRejection codec;
            bool valid=AxiomDomainSemanticCodec.TryResolveCommittedState(
                runtime,phenomenon,finalSnapshot.SemanticValue,out ignoredState,out codec);
            rejection=valid?DomainRuntimeTargetRejectionReason.None:
                codec==AxiomDomainSemanticCodecRejection.SemanticMappingNotEstablished
                    ?DomainRuntimeTargetRejectionReason.UnsupportedPhenomenon
                    :codec==AxiomDomainSemanticCodecRejection.MissingNaturalMassBaseline
                        ?DomainRuntimeTargetRejectionReason.MissingNaturalMassBaseline
                        :codec==AxiomDomainSemanticCodecRejection.MissingPatternContext
                            ?DomainRuntimeTargetRejectionReason.MissingPatternContext
                        :DomainRuntimeTargetRejectionReason.InvalidFinalState;
            return valid;
        }
        public void Apply(PhenomenonSemanticSnapshot finalSnapshot) { runtime?.TryCommitDomainSemantic(finalSnapshot,timestamp); }
    }
}
