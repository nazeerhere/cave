using System.Collections.Generic;

namespace Cave.Domain
{
    public enum DomainRuntimeTargetRejectionReason { None = 0, UnsupportedPhenomenon = 1, MissingNaturalMassBaseline = 2, InvalidFinalState = 3 }
    public enum DomainCommitRejectionReason { None = 0, PlanNotCommittable = 1, PlanAlreadyCommitted = 2, MissingRuntimeTarget = 3, UnsupportedPhenomenon = 4, RuntimeStateUnavailable = 5, RuntimeStateStale = 6, FinalStateNotWritable = 7 }
    public interface IDomainPhenomenonRuntimeAccessor { bool TryResolve(PhenomenonCarrierId carrierId,LawPhenomenon phenomenon,out IDomainPhenomenonRuntimeTarget target); }
    /// <summary>A bound runtime target; Apply is a validated in-memory assignment with no gameplay side effects or failure mode.</summary>
    public interface IDomainPhenomenonRuntimeTarget { bool TryRead(out PhenomenonSemanticSnapshot snapshot); bool TryValidateWrite(PhenomenonSemanticSnapshot finalSnapshot,out DomainRuntimeTargetRejectionReason rejection); void Apply(PhenomenonSemanticSnapshot finalSnapshot); }
    public sealed class DomainCommitResult
    {
        internal DomainCommitResult(DomainCommitPlan plan,bool succeeded,DomainCommitRejectionReason rejection,int committed,int noOps,DomainCommitEntry failedEntry,DomainRuntimeTargetRejectionReason targetRejection)
        { Plan=plan;Succeeded=succeeded;RejectionReason=rejection;CommittedEntryCount=committed;NoOpCount=noOps;FailedEntry=failedEntry;TargetRejection=targetRejection; }
        public DomainCommitPlan Plan { get; } public bool Succeeded { get; } public DomainCommitRejectionReason RejectionReason { get; } public int CommittedEntryCount { get; } public int NoOpCount { get; } public DomainCommitEntry FailedEntry { get; } public DomainRuntimeTargetRejectionReason TargetRejection { get; } public PhenomenonCarrierId? FailedCarrierId => FailedEntry!=null?(PhenomenonCarrierId?)FailedEntry.CarrierId:null;
    }
    public sealed class DomainRuntimeCommitter
    {
        private readonly HashSet<DomainCommitPlan> committedPlans=new HashSet<DomainCommitPlan>();
        public DomainCommitResult Commit(DomainCommitPlan plan,IDomainPhenomenonRuntimeAccessor accessor)
        {
            if(plan==null||!plan.IsCommittable)return Failed(plan,DomainCommitRejectionReason.PlanNotCommittable,null,DomainRuntimeTargetRejectionReason.None);
            if(committedPlans.Contains(plan))return Failed(plan,DomainCommitRejectionReason.PlanAlreadyCommitted,null,DomainRuntimeTargetRejectionReason.None);
            if(accessor==null)return Failed(plan,DomainCommitRejectionReason.MissingRuntimeTarget,null,DomainRuntimeTargetRejectionReason.None);
            List<ValidatedWrite> writes=new List<ValidatedWrite>(); int noOps=0;
            for(int i=0;i<plan.Entries.Count;i++)
            {
                DomainCommitEntry entry=plan.Entries[i]; IDomainPhenomenonRuntimeTarget target;
                if(!accessor.TryResolve(entry.CarrierId,entry.Phenomenon,out target)||target==null)return Failed(plan,DomainCommitRejectionReason.MissingRuntimeTarget,entry,DomainRuntimeTargetRejectionReason.None);
                PhenomenonSemanticSnapshot actual;if(!target.TryRead(out actual)||actual==null)return Failed(plan,DomainCommitRejectionReason.RuntimeStateUnavailable,entry,DomainRuntimeTargetRejectionReason.None);
                if(!DomainCommitSnapshot.Equal(entry.ExpectedBefore,actual))return Failed(plan,DomainCommitRejectionReason.RuntimeStateStale,entry,DomainRuntimeTargetRejectionReason.None);
                DomainRuntimeTargetRejectionReason validation;if(!target.TryValidateWrite(entry.FinalAfter,out validation))return Failed(plan,validation==DomainRuntimeTargetRejectionReason.UnsupportedPhenomenon?DomainCommitRejectionReason.UnsupportedPhenomenon:DomainCommitRejectionReason.FinalStateNotWritable,entry,validation);
                if(entry.IsNoOp)noOps++;else writes.Add(new ValidatedWrite(entry,target));
            }
            for(int i=0;i<writes.Count;i++)writes[i].Target.Apply(writes[i].Entry.FinalAfter);committedPlans.Add(plan);return new DomainCommitResult(plan,true,DomainCommitRejectionReason.None,writes.Count,noOps,null,DomainRuntimeTargetRejectionReason.None);
        }
        private static DomainCommitResult Failed(DomainCommitPlan plan,DomainCommitRejectionReason rejection,DomainCommitEntry entry,DomainRuntimeTargetRejectionReason target){return new DomainCommitResult(plan,false,rejection,0,0,entry,target);}
        private sealed class ValidatedWrite { public ValidatedWrite(DomainCommitEntry entry,IDomainPhenomenonRuntimeTarget target){Entry=entry;Target=target;} public DomainCommitEntry Entry;public IDomainPhenomenonRuntimeTarget Target; }
    }
}
