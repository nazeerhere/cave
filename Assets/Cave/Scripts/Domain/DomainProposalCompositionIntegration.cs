using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Authoritative outcome of one resolved raw collision; its lineage is explicitly multi-parent.</summary>
    public sealed class DomainComposedTransition
    {
        internal DomainComposedTransition(DomainTransitionSequenceId id, DomainProposalCompositionResult composition) { SequenceId=id; Composition=composition; }
        public DomainTransitionSequenceId SequenceId { get; } public DomainProposalCompositionResult Composition { get; }
        public DomainOrchestrationGeneration Generation => Composition.Generation; public PhenomenonCarrierId CarrierId => Composition.CarrierId; public LawPhenomenon Phenomenon => Composition.Phenomenon;
        public PhenomenonSemanticSnapshot Before => Composition.CommonBefore; public PhenomenonSemanticSnapshot After => Composition.ResultingAfter; public bool IsNoOp => Composition.IsNoOp;
        public ProposalCompositionPolicy Policy => Composition.Policy; public IReadOnlyList<ProposalCompositionComponent> Contributors => Composition.Components;
        public IReadOnlyList<DomainTransitionSequenceId> ParentTransitionIds => Composition.Collision.InvolvedTransitionIds; public IReadOnlyList<DomainLaw> OriginatingLaws => Composition.Collision.OriginatingLaws;
        public bool IsAuthoritative => Composition.Resolved; public bool IsTerminal => Generation == DomainOrchestrationGeneration.Generation2;
    }

    public enum DomainGenerationTwoCompositionRejectionReason { None = 0, GenerationTwoRequired = 1 }
    public sealed class DomainUnresolvedSemanticKey
    { public DomainUnresolvedSemanticKey(PhenomenonCarrierId carrierId, LawPhenomenon phenomenon) { CarrierId=carrierId; Phenomenon=phenomenon; } public PhenomenonCarrierId CarrierId { get; } public LawPhenomenon Phenomenon { get; } }
    public sealed class DomainFinalSemanticState
    { internal DomainFinalSemanticState(PhenomenonCarrierId carrierId, LawPhenomenon phenomenon, PhenomenonSemanticSnapshot snapshot, bool unresolved) { CarrierId=carrierId;Phenomenon=phenomenon;Snapshot=snapshot;IsUnresolved=unresolved; } public PhenomenonCarrierId CarrierId { get; } public LawPhenomenon Phenomenon { get; } public PhenomenonSemanticSnapshot Snapshot { get; } public bool IsUnresolved { get; } }
    public sealed class DomainAuthoritativeTransition
    {
        internal DomainAuthoritativeTransition(NormalizedSemanticTransition raw) { RawTransition=raw; }
        internal DomainAuthoritativeTransition(DomainAuthoritativeGenerationOneTransition generationOne) { GenerationOneTransition=generationOne; }
        internal DomainAuthoritativeTransition(DomainComposedTransition composed) { ComposedTransition=composed; }
        public NormalizedSemanticTransition RawTransition { get; } public DomainAuthoritativeGenerationOneTransition GenerationOneTransition { get; } public DomainComposedTransition ComposedTransition { get; }
        public bool IsComposed => ComposedTransition != null; public DomainTransitionSequenceId SequenceId => IsComposed ? ComposedTransition.SequenceId : GenerationOneTransition != null ? GenerationOneTransition.SequenceId : RawTransition.SequenceId;
        public DomainOrchestrationGeneration Generation => IsComposed ? ComposedTransition.Generation : GenerationOneTransition != null ? GenerationOneTransition.Generation : RawTransition.Generation;
        public PhenomenonCarrierId CarrierId => IsComposed ? ComposedTransition.CarrierId : GenerationOneTransition != null ? GenerationOneTransition.CarrierId : RawTransition.CarrierId;
        public LawPhenomenon Phenomenon => IsComposed ? ComposedTransition.Phenomenon : GenerationOneTransition != null ? GenerationOneTransition.Phenomenon : RawTransition.Phenomenon;
        public PhenomenonSemanticSnapshot Before => IsComposed ? ComposedTransition.Before : GenerationOneTransition != null ? GenerationOneTransition.Before : RawTransition.Before;
        public PhenomenonSemanticSnapshot After => IsComposed ? ComposedTransition.After : GenerationOneTransition != null ? GenerationOneTransition.After : RawTransition.After;
    }

    /// <summary>Pure terminal G2 composition and final state boundary; raw history is preserved by reference.</summary>
    public sealed class DomainProposalCompositionIntegrationResult
    {
        internal DomainProposalCompositionIntegrationResult(DomainGenerationTwoResult raw, IReadOnlyList<DomainProposalCompositionResult> g2, IReadOnlyList<DomainComposedTransition> nodes, IReadOnlyList<DomainAuthoritativeTransition> authority, IReadOnlyList<DomainFinalSemanticState> states, IReadOnlyList<DomainUnresolvedSemanticKey> unresolved, int next, DomainGenerationTwoCompositionRejectionReason rejection)
        { RawGenerationTwo=raw; ResolvedGenerationOne=raw!=null?raw.ResolvedGenerationOne:null; GenerationOneCompositions=ResolvedGenerationOne!=null?ResolvedGenerationOne.CompositionResults:Empty<DomainProposalCompositionResult>(); GenerationTwoCompositions=g2; GenerationOneComposedTransitions=ResolvedGenerationOne!=null?ResolvedGenerationOne.ComposedTransitions:Empty<DomainComposedTransition>(); GenerationTwoComposedTransitions=nodes; AuthoritativeGenerationTwoTransitions=authority; FinalStates=states; UnresolvedKeys=unresolved; NextTransitionId=new DomainTransitionSequenceId(next); RejectionReason=rejection; }
        public DomainGenerationTwoResult RawGenerationTwo { get; } public DomainResolvedGenerationOneResult ResolvedGenerationOne { get; }
        public IReadOnlyList<DomainProposalCompositionResult> GenerationOneCompositions { get; } public IReadOnlyList<DomainProposalCompositionResult> GenerationTwoCompositions { get; }
        public IReadOnlyList<DomainComposedTransition> GenerationOneComposedTransitions { get; } public IReadOnlyList<DomainComposedTransition> GenerationTwoComposedTransitions { get; }
        public IReadOnlyList<DomainAuthoritativeTransition> AuthoritativeGenerationTwoTransitions { get; } public IReadOnlyList<DomainFinalSemanticState> FinalStates { get; } public IReadOnlyList<DomainUnresolvedSemanticKey> UnresolvedKeys { get; }
        public DomainTransitionSequenceId NextTransitionId { get; } public DomainGenerationTwoCompositionRejectionReason RejectionReason { get; } public bool Succeeded => RejectionReason == DomainGenerationTwoCompositionRejectionReason.None;
        public bool HasRawProposalCollisions => ResolvedGenerationOne != null && (ResolvedGenerationOne.CompositionResults.Count > 0 || RawGenerationTwo.ProposalCollisions.Count > 0);
        public bool HasUnresolvedProposalCollisions => HasUnresolved(GenerationOneCompositions) || HasUnresolved(GenerationTwoCompositions);
        public bool TryGetFinalState(PhenomenonCarrierId carrierId, LawPhenomenon phenomenon, out DomainFinalSemanticState state) { for(int i=0;i<FinalStates.Count;i++)if(FinalStates[i].CarrierId.Equals(carrierId)&&FinalStates[i].Phenomenon==phenomenon){state=FinalStates[i];return true;}state=null;return false; }
        private static bool HasUnresolved(IReadOnlyList<DomainProposalCompositionResult> values) { for(int i=0;i<values.Count;i++)if(!values[i].Resolved)return true;return false; }
        private static IReadOnlyList<T> Empty<T>() { return new List<T>().AsReadOnly(); }
    }

    public static class DomainProposalCompositionIntegrator
    {
        public static DomainProposalCompositionIntegrationResult ResolveGenerationTwo(DomainGenerationTwoResult raw)
        {
            if (!IsValid(raw)) return Rejected(raw);
            List<ProposalCollision> ordered = new List<ProposalCollision>(raw.ProposalCollisions); ordered.Sort((left,right)=>Min(left).CompareTo(Min(right)));
            List<DomainProposalCompositionResult> compositions = new List<DomainProposalCompositionResult>(); for(int i=0;i<ordered.Count;i++) compositions.Add(DomainProposalComposer.TryCompose(ordered[i],raw.Transitions));
            int next=Max(raw.Transitions)+1; List<DomainComposedTransition> nodes=new List<DomainComposedTransition>(); for(int i=0;i<compositions.Count;i++)if(compositions[i].Resolved)nodes.Add(new DomainComposedTransition(new DomainTransitionSequenceId(next++),compositions[i]));
            List<DomainAuthoritativeTransition> authority=new List<DomainAuthoritativeTransition>(); for(int i=0;i<raw.Transitions.Count;i++)if(raw.Transitions[i].IsUnambiguous)authority.Add(new DomainAuthoritativeTransition(raw.Transitions[i]));for(int i=0;i<nodes.Count;i++)authority.Add(new DomainAuthoritativeTransition(nodes[i]));authority.Sort((left,right)=>left.SequenceId.CompareTo(right.SequenceId));
            List<DomainUnresolvedSemanticKey> unresolved=Unresolved(raw.ResolvedGenerationOne,compositions); List<DomainFinalSemanticState> states=States(raw.ResolvedGenerationOne,authority,unresolved);
            return new DomainProposalCompositionIntegrationResult(raw,compositions.AsReadOnly(),nodes.AsReadOnly(),authority.AsReadOnly(),states.AsReadOnly(),unresolved.AsReadOnly(),next,DomainGenerationTwoCompositionRejectionReason.None);
        }
        private static DomainProposalCompositionIntegrationResult Rejected(DomainGenerationTwoResult raw) { return new DomainProposalCompositionIntegrationResult(raw,Empty<DomainProposalCompositionResult>(),Empty<DomainComposedTransition>(),Empty<DomainAuthoritativeTransition>(),Empty<DomainFinalSemanticState>(),Empty<DomainUnresolvedSemanticKey>(),-1,DomainGenerationTwoCompositionRejectionReason.GenerationTwoRequired); }
        private static bool IsValid(DomainGenerationTwoResult raw) { return raw!=null&&raw.Succeeded&&raw.ResolvedGenerationOne!=null&&raw.ResolvedGenerationOne.Succeeded&&raw.Transitions!=null&&raw.ProposalCollisions!=null&&NoNulls(raw.Transitions)&&NoNulls(raw.ProposalCollisions)&&raw.ResolvedGenerationOne.AuthoritativeTransitions!=null&&raw.ResolvedGenerationOne.PostGenerationOneStates!=null&&raw.ResolvedGenerationOne.UnresolvedKeys!=null; }
        private static bool NoNulls<T>(IReadOnlyList<T> values) where T:class { for(int i=0;i<values.Count;i++)if(values[i]==null)return false;return true; }
        private static List<DomainUnresolvedSemanticKey> Unresolved(DomainResolvedGenerationOneResult resolved,IReadOnlyList<DomainProposalCompositionResult> compositions) { List<DomainUnresolvedSemanticKey> keys=new List<DomainUnresolvedSemanticKey>();for(int i=0;i<resolved.UnresolvedKeys.Count;i++)Add(keys,resolved.UnresolvedKeys[i].CarrierId,resolved.UnresolvedKeys[i].Phenomenon);for(int i=0;i<compositions.Count;i++)if(!compositions[i].Resolved)Add(keys,compositions[i].CarrierId,compositions[i].Phenomenon);return keys; }
        private static List<DomainFinalSemanticState> States(DomainResolvedGenerationOneResult resolved,IReadOnlyList<DomainAuthoritativeTransition> authority,IReadOnlyList<DomainUnresolvedSemanticKey> unresolved) { List<DomainFinalSemanticState> values=new List<DomainFinalSemanticState>();for(int i=0;i<resolved.PostGenerationOneStates.Count;i++){DomainPostGenerationOneState state=resolved.PostGenerationOneStates[i];Put(values,state.CarrierId,state.Phenomenon,state.Snapshot,state.IsUnresolved);}for(int i=0;i<authority.Count;i++)Put(values,authority[i].CarrierId,authority[i].Phenomenon,authority[i].After);for(int i=0;i<unresolved.Count;i++)Put(values,unresolved[i].CarrierId,unresolved[i].Phenomenon,null,true);return values; }
        private static void Add(List<DomainUnresolvedSemanticKey> values,PhenomenonCarrierId carrier,LawPhenomenon phenomenon){for(int i=0;i<values.Count;i++)if(values[i].CarrierId.Equals(carrier)&&values[i].Phenomenon==phenomenon)return;values.Add(new DomainUnresolvedSemanticKey(carrier,phenomenon));}
        private static void Put(List<DomainFinalSemanticState> values,PhenomenonCarrierId carrier,LawPhenomenon phenomenon,PhenomenonSemanticSnapshot snapshot,bool unresolved=false){for(int i=0;i<values.Count;i++)if(values[i].CarrierId.Equals(carrier)&&values[i].Phenomenon==phenomenon){values[i]=new DomainFinalSemanticState(carrier,phenomenon,snapshot,unresolved);return;}values.Add(new DomainFinalSemanticState(carrier,phenomenon,snapshot,unresolved));}
        private static int Min(ProposalCollision collision){int min=int.MaxValue;for(int i=0;i<collision.InvolvedTransitionIds.Count;i++)if(collision.InvolvedTransitionIds[i].Value<min)min=collision.InvolvedTransitionIds[i].Value;return min;}
        private static int Max(IReadOnlyList<NormalizedSemanticTransition> values){int max=-1;for(int i=0;i<values.Count;i++)if(values[i].SequenceId.Value>max)max=values[i].SequenceId.Value;return max;}
        private static IReadOnlyList<T> Empty<T>(){return new List<T>().AsReadOnly();}
    }
}
