using System.Collections.Generic;

namespace Cave.Domain
{
    public enum DomainCommitPlanRejectionReason { None = 0, BoundedResultRequired = 1, UnresolvedCollision = 2, UnresolvedKey = 3, InvalidAuthority = 4, InvalidFinalState = 5, MissingCausalBeforeState = 6 }
    public sealed class DomainCommitEntry
    {
        internal DomainCommitEntry(PhenomenonCarrierId carrierId, LawPhenomenon phenomenon, PhenomenonSemanticSnapshot expectedBefore, PhenomenonSemanticSnapshot finalAfter, IReadOnlyList<DomainTransitionSequenceId> causalIds)
        { CarrierId=carrierId;Phenomenon=phenomenon;ExpectedBefore=expectedBefore;FinalAfter=finalAfter;CausalTransitionIds=causalIds; }
        public PhenomenonCarrierId CarrierId { get; } public LawPhenomenon Phenomenon { get; } public PhenomenonSemanticSnapshot ExpectedBefore { get; } public PhenomenonSemanticSnapshot FinalAfter { get; } public IReadOnlyList<DomainTransitionSequenceId> CausalTransitionIds { get; }
        public bool IsNoOp => DomainCommitSnapshot.Equal(ExpectedBefore,FinalAfter);
    }
    public sealed class DomainCommitPlan
    {
        internal DomainCommitPlan(DomainBoundedOrchestrationResult source,IReadOnlyList<DomainCommitEntry> entries,DomainCommitPlanRejectionReason rejection)
        { Source=source;Entries=entries;RejectionReason=rejection; }
        public DomainBoundedOrchestrationResult Source { get; } public IReadOnlyList<DomainCommitEntry> Entries { get; } public DomainCommitPlanRejectionReason RejectionReason { get; } public bool IsCommittable => RejectionReason==DomainCommitPlanRejectionReason.None;
        public int AffectedKeyCount => Entries.Count; public int WriteCount { get { int count=0;for(int i=0;i<Entries.Count;i++)if(!Entries[i].IsNoOp)count++;return count; } } public int NoOpCount => Entries.Count-WriteCount;
    }
    public static class DomainCommitPlanBuilder
    {
        public static DomainCommitPlan Build(DomainBoundedOrchestrationResult bounded)
        {
            DomainCommitPlanRejectionReason rejection=Validate(bounded); if(rejection!=DomainCommitPlanRejectionReason.None)return Rejected(bounded,rejection);
            List<DomainCommitEntry> entries=new List<DomainCommitEntry>();
            for(int stateIndex=0;stateIndex<bounded.FinalStates.Count;stateIndex++)
            {
                DomainFinalSemanticState final=bounded.FinalStates[stateIndex]; if(final==null||final.IsUnresolved||final.Snapshot==null)return Rejected(bounded,DomainCommitPlanRejectionReason.InvalidFinalState);
                List<DomainAuthoritativeTransition> causal=Find(bounded.AllAuthoritativeTransitions,final.CarrierId,final.Phenomenon); if(causal.Count==0||causal[0].Before==null)return Rejected(bounded,DomainCommitPlanRejectionReason.MissingCausalBeforeState);
                List<DomainTransitionSequenceId> ids=new List<DomainTransitionSequenceId>();for(int i=0;i<causal.Count;i++)ids.Add(causal[i].SequenceId); entries.Add(new DomainCommitEntry(final.CarrierId,final.Phenomenon,causal[0].Before,final.Snapshot,ids.AsReadOnly()));
            }
            entries.Sort((left,right)=>left.CausalTransitionIds[0].CompareTo(right.CausalTransitionIds[0]));return new DomainCommitPlan(bounded,entries.AsReadOnly(),DomainCommitPlanRejectionReason.None);
        }
        private static DomainCommitPlanRejectionReason Validate(DomainBoundedOrchestrationResult bounded){if(bounded==null||!bounded.Succeeded||!bounded.IsCausallyTerminal)return DomainCommitPlanRejectionReason.BoundedResultRequired;if(bounded.HasUnresolvedProposalCollisions)return DomainCommitPlanRejectionReason.UnresolvedCollision;if(bounded.FinalUnresolvedKeys==null||bounded.FinalUnresolvedKeys.Count>0)return DomainCommitPlanRejectionReason.UnresolvedKey;if(bounded.AllAuthoritativeTransitions==null||bounded.FinalStates==null)return DomainCommitPlanRejectionReason.InvalidAuthority;return DomainCommitPlanRejectionReason.None;}
        private static List<DomainAuthoritativeTransition> Find(IReadOnlyList<DomainAuthoritativeTransition> values,PhenomenonCarrierId carrier,LawPhenomenon phenomenon){List<DomainAuthoritativeTransition> matches=new List<DomainAuthoritativeTransition>();for(int i=0;i<values.Count;i++)if(values[i]!=null&&values[i].CarrierId.Equals(carrier)&&values[i].Phenomenon==phenomenon)matches.Add(values[i]);matches.Sort((left,right)=>left.SequenceId.CompareTo(right.SequenceId));return matches;}
        private static DomainCommitPlan Rejected(DomainBoundedOrchestrationResult source,DomainCommitPlanRejectionReason reason){return new DomainCommitPlan(source,new List<DomainCommitEntry>().AsReadOnly(),reason);}
    }
    internal static class DomainCommitSnapshot
    { internal static bool Equal(PhenomenonSemanticSnapshot left,PhenomenonSemanticSnapshot right){return left!=null&&right!=null&&left.Phenomenon==right.Phenomenon&&left.SemanticValue==right.SemanticValue&&left.Region==right.Region;} }
}
