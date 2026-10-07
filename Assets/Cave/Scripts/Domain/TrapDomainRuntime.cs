using System;
using System.Collections.Generic;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    public enum DomainReserveAllocationRejection { None=0, InvalidRequest=1, InsufficientReserve=2, DuplicateAllocation=3, AlreadyReclaimed=4 }
    public readonly struct DomainReserveAllocation
    {
        internal DomainReserveAllocation(string id,string owner,float cost) { Id=id;OwnerId=owner;Cost=cost; }
        public string Id { get; } public string OwnerId { get; } public float Cost { get; }
    }
    /// <summary>
    /// One player-owned Domain energy ledger. Reserve is an allocation inside
    /// this same pool: allocating a carrier consumes current Charge and reduces
    /// available capacity; reclaiming restores capacity only.
    /// </summary>
    public sealed class DomainReserveLedger
    {
        private readonly Dictionary<string,DomainReserveAllocation> allocations=new Dictionary<string,DomainReserveAllocation>();
        private readonly HashSet<string> awardedEvents=new HashSet<string>();
        public event Action Changed;
        public DomainReserveLedger(float baseMaximumEnergy)
        {
            BaseMaximumEnergy=Mathf.Max(0f,baseMaximumEnergy);
            CurrentCharge=BaseMaximumEnergy;
        }
        public float BaseMaximumEnergy { get; }
        /// <summary>Maximum usable Charge after active Reserve allocations.</summary>
        public float Capacity => Mathf.Max(0f,BaseMaximumEnergy-Committed);
        public float CurrentCharge { get; private set; }
        public float Committed { get; private set; }
        /// <summary>Unfilled usable capacity, not a second resource.</summary>
        public float Available => Mathf.Max(0f,Capacity-CurrentCharge);
        public int AllocationCount => allocations.Count;

        public bool TryAward(string eventId,float amount)
        {
            if(string.IsNullOrWhiteSpace(eventId)||amount<=0f||float.IsNaN(amount)||float.IsInfinity(amount))return false;
            if(!awardedEvents.Add(eventId))return false;
            float before=CurrentCharge;
            CurrentCharge=Mathf.Min(Capacity,CurrentCharge+amount);
            if(Mathf.Approximately(before,CurrentCharge))return false;
            Changed?.Invoke();return true;
        }
        public bool TrySpend(float amount)
        {
            if(amount<=0f||float.IsNaN(amount)||float.IsInfinity(amount)||CurrentCharge+.0001f<amount)return false;
            CurrentCharge=Mathf.Max(0f,CurrentCharge-amount);Changed?.Invoke();return true;
        }
        public void ResetCharge()
        {
            awardedEvents.Clear();
            if(Mathf.Approximately(CurrentCharge,0f))return;
            CurrentCharge=0f;Changed?.Invoke();
        }
        public DomainReserveAllocationRejection TryAllocate(string id,string owner,float cost,out DomainReserveAllocation allocation)
        {
            allocation=default;if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(owner)||cost<=0f||float.IsNaN(cost)||float.IsInfinity(cost))return DomainReserveAllocationRejection.InvalidRequest;
            if(allocations.ContainsKey(id))return DomainReserveAllocationRejection.DuplicateAllocation;
            if(CurrentCharge+.0001f<cost||Capacity+.0001f<cost)return DomainReserveAllocationRejection.InsufficientReserve;
            allocation=new DomainReserveAllocation(id,owner,cost);allocations.Add(id,allocation);Committed+=cost;
            CurrentCharge=Mathf.Min(Capacity,Mathf.Max(0f,CurrentCharge-cost));
            Changed?.Invoke();return DomainReserveAllocationRejection.None;
        }
        public DomainReserveAllocationRejection Reclaim(string id)
        {
            DomainReserveAllocation allocation;if(!allocations.TryGetValue(id,out allocation))return DomainReserveAllocationRejection.AlreadyReclaimed;
            allocations.Remove(id);Committed=Mathf.Max(0f,Committed-allocation.Cost);Changed?.Invoke();return DomainReserveAllocationRejection.None;
        }
        public bool TryGet(string id,out DomainReserveAllocation allocation) => allocations.TryGetValue(id,out allocation);
    }

    [DisallowMultipleComponent]
    public sealed class PlayerDomainReserve : MonoBehaviour
    {
        [SerializeField,Min(0f)] private float baseMaximumEnergy=100f;
        [SerializeField,Min(.01f)] private float defaultTrapAllocation=15f;
        private DomainReserveLedger ledger;
        public event Action ReserveChanged;
        public DomainReserveLedger Ledger
        {
            get
            {
                if(ledger!=null)return ledger;
                ledger=new DomainReserveLedger(baseMaximumEnergy);
                ledger.Changed+=HandleLedgerChanged;
                return ledger;
            }
        }
        public float BaseMaximumEnergy=>Ledger.BaseMaximumEnergy;
        public float MaximumAvailableCharge=>Ledger.Capacity;
        public float CurrentCharge=>Ledger.CurrentCharge;
        public float TotalReserve=>Ledger.Capacity;
        public float CommittedReserve=>Ledger.Committed;
        public float AvailableReserve=>Ledger.Available;
        public float DefaultTrapAllocation=>defaultTrapAllocation;
        public bool TryAwardCharge(string eventId,float amount)=>Ledger.TryAward(eventId,amount);
        public bool TrySpendCharge(float amount)=>Ledger.TrySpend(amount);
        public void ResetExpeditionCharge()=>Ledger.ResetCharge();
        public static PlayerDomainReserve EnsureOn(GameObject owner) => owner==null?null:owner.GetComponent<PlayerDomainReserve>()??owner.AddComponent<PlayerDomainReserve>();
        private void HandleLedgerChanged(){ReserveChanged?.Invoke();}
        private void OnDestroy(){if(ledger!=null)ledger.Changed-=HandleLedgerChanged;}
    }

    public enum TrapDomainEnhancementState { Ordinary=0, EnhancementPending=1, Enhanced=2, LocalizedDomainActive=3, Cleared=4 }
    public enum TrapDomainEnhancementRejection { None=0, TrapUnavailable=1, InvalidTrapLaw=2, AlreadyEnhanced=3, ReserveUnavailable=4, ReserveRejected=5 }
    public sealed class BoundTrapLaw
    {
        internal BoundTrapLaw(string id,DomainLaw law) { Id=id;Law=law; }
        public string Id { get; } public DomainLaw Law { get; }
    }
    /// <summary>Transaction-local activation facts sourced by the ordinary disk interaction, never discovered heuristically.</summary>
    public sealed class LocalizedDomainActivationContext
    {
        public LocalizedDomainActivationContext(PhenomenonCarrierId focalCarrierId, IEnumerable<PhenomenonCarrierId> affectedCarrierIds)
        {
            FocalCarrierId = focalCarrierId;
            List<PhenomenonCarrierId> values = affectedCarrierIds != null ? new List<PhenomenonCarrierId>(affectedCarrierIds) : new List<PhenomenonCarrierId>();
            values.Sort((left, right) => left.CompareTo(right));
            AffectedCarrierIds = values.AsReadOnly();
        }
        public PhenomenonCarrierId FocalCarrierId { get; }
        public IReadOnlyList<PhenomenonCarrierId> AffectedCarrierIds { get; }
        public bool HasFocalCarrier => FocalCarrierId.IsValid;
    }
    /// <summary>Immutable child Domain descriptor. Resolution planning uses the shared Domain planner and Trap expression context.</summary>
    public sealed class LocalizedDomainCarrier
    {
        internal LocalizedDomainCarrier(string id,string parent,string owner,BoundTrapLaw law,Vector2 center,float radius,float endsAt,LocalizedDomainActivationContext activationContext=null)
        { Id=id;ParentAuthorityId=parent;OwnerId=owner;BoundLaw=law;Center=center;Radius=radius;EndsAt=endsAt;
          DomainCompositionMutationResult add=DomainCompositionEditor.Add(DomainComposition.Empty,law.Law);
          Composition=add.Resulting;
          ActivationContext=activationContext;
          Plan=DomainResolutionPlanner.Plan(new DomainResolutionIntent(Composition,new LawExpressionContext(true,LawExpression.Trap,false),law.Law.Phenomenon,PhenomenonOperationKind.Add,1f)); }
        public string Id { get; } public string ParentAuthorityId { get; } public string OwnerId { get; } public BoundTrapLaw BoundLaw { get; }
        public Vector2 Center { get; } public float Radius { get; } public float EndsAt { get; } public DomainComposition Composition { get; } public DomainResolutionPlan Plan { get; }
        public LocalizedDomainActivationContext ActivationContext { get; }
        private string executionGenerationId;
        /// <summary>Bound once by the shared localized execution coordinator.
        /// The legacy per-carrier value is retained only for direct compatibility
        /// callers that have not entered a coordinated flush.</summary>
        public string ExecutionGenerationId => executionGenerationId ?? (Id + ":activation");
        internal bool TryBindExecutionGeneration(string generationId)
        {
            if (string.IsNullOrWhiteSpace(generationId)) return false;
            if (executionGenerationId != null) return string.Equals(executionGenerationId, generationId, StringComparison.Ordinal);
            executionGenerationId = generationId;
            return true;
        }
        public DomainExecutionProvenance CreateProvenance(PhenomenonOperationKind effectiveOperation)
        {
            return new DomainExecutionProvenance(OwnerId, ParentAuthorityId, Id,
                new PhenomenonCarrierId("trap:" + OwnerId), BoundLaw.Id, BoundLaw.Law.Expression,
                BoundLaw.Law.Phenomenon, BoundLaw.Law.TerritoryPrinciple, ExecutionGenerationId, effectiveOperation);
        }
        public bool IsActive(float now) => now<EndsAt;
    }

    /// <summary>Per-disk enhancement owner. It preserves ordinary disk behavior and only observes successful ordinary pulse activation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLandmine))]
    public sealed class TrapDomainEnhancement : MonoBehaviour
    {
        [SerializeField,Min(.05f)] private float localizedRadius=1f;
        [SerializeField,Min(.05f)] private float localizedLifetime=1.5f;
        [SerializeField,Min(.01f)] private float reserveCost=15f;
        private PlayerLandmine trap; private PlayerDomainReserve reserve; private BoundTrapLaw boundLaw; private DomainReserveAllocation allocation; private LocalizedDomainCarrier carrier; private string parentAuthorityId; private bool reclaimed; private LocalizedDomainExecutionCoordinator coordinator;
        public event Action<TrapDomainEnhancement> Changed;
        public TrapDomainEnhancementState State { get; private set; }=TrapDomainEnhancementState.Ordinary;
        public bool IsEnhanced => boundLaw!=null&&!reclaimed; public bool IsLocalizedDomainActive => carrier!=null&&carrier.IsActive(Time.time);
        public BoundTrapLaw BoundLaw=>boundLaw; public DomainReserveAllocation Allocation=>allocation; public LocalizedDomainCarrier Carrier=>carrier;
        public TrapDomainEnhancementRejection TryEnhance(DomainAuthoredLaw authored,PlayerDomainReserve reserveOwner,string parentId)
        {
            trap=trap!=null?trap:GetComponent<PlayerLandmine>();
            if(trap==null||!trap.IsOperational)return TrapDomainEnhancementRejection.TrapUnavailable;
            if(IsEnhanced)return TrapDomainEnhancementRejection.AlreadyEnhanced;
            if(authored==null||authored.Law==null||authored.Law.Expression!=LawExpression.Trap)return TrapDomainEnhancementRejection.InvalidTrapLaw;
            if(reserveOwner==null||string.IsNullOrWhiteSpace(parentId))return TrapDomainEnhancementRejection.ReserveUnavailable;
            State=TrapDomainEnhancementState.EnhancementPending;
            // The established player ledger owns the single configurable
            // allocation profile. The retained field is legacy serialization
            // only and cannot create a competing Trap currency.
            DomainReserveAllocation next;DomainReserveAllocationRejection result=reserveOwner.Ledger.TryAllocate("trap-domain:"+authored.Id+":"+trap.CreationOrder,trap.CreationOrder.ToString(),reserveOwner.DefaultTrapAllocation,out next);
            if(result!=DomainReserveAllocationRejection.None){State=TrapDomainEnhancementState.Ordinary;return TrapDomainEnhancementRejection.ReserveRejected;}
            reserve=reserveOwner;allocation=next;boundLaw=new BoundTrapLaw(authored.Id,authored.Law);parentAuthorityId=parentId;reclaimed=false;State=TrapDomainEnhancementState.Enhanced;Subscribe();trap.NotifyObservedStateChanged();Changed?.Invoke(this);return TrapDomainEnhancementRejection.None;
        }
        private void Awake(){trap=GetComponent<PlayerLandmine>();}
        private void Subscribe(){if(trap==null)return;trap.MeaningfulPulseResolved-=HandleMeaningfulPulse;trap.MeaningfulPulseResolved+=HandleMeaningfulPulse;trap.Removed-=HandleRemoved;trap.Removed+=HandleRemoved;}
        private void HandleMeaningfulPulse(PlayerLandmine source,PlayerLandminePulseContext pulse){if(source!=trap||!IsEnhanced||carrier!=null)return;carrier=new LocalizedDomainCarrier("localized-trap:"+trap.CreationOrder,parentAuthorityId,trap.CreationOrder.ToString(),boundLaw,transform.position,Mathf.Min(localizedRadius,trap.LocalPulseRadius),Time.time+localizedLifetime,BuildActivationContext(pulse));RegisterFieldBinding();(coordinator??(coordinator=LocalizedDomainExecutionCoordinator.EnsureOn(reserve))).Enqueue(carrier);State=TrapDomainEnhancementState.LocalizedDomainActive;trap.NotifyObservedStateChanged();Changed?.Invoke(this);}
        private static LocalizedDomainActivationContext BuildActivationContext(PlayerLandminePulseContext pulse)
        {
            if(pulse==null)return new LocalizedDomainActivationContext(default(PhenomenonCarrierId),null);
            DomainRuntimeCarrierIdentity focal=pulse.TriggeringTarget!=null?pulse.TriggeringTarget.GetComponent<DomainRuntimeCarrierIdentity>():null;
            List<PhenomenonCarrierId> affectedIds=new List<PhenomenonCarrierId>();
            for(int index=0;index<pulse.AffectedTargets.Count;index++){DomainRuntimeCarrierIdentity identity=pulse.AffectedTargets[index]!=null?pulse.AffectedTargets[index].GetComponent<DomainRuntimeCarrierIdentity>():null;if(identity!=null)affectedIds.Add(identity.CarrierId);}
            return new LocalizedDomainActivationContext(focal!=null?focal.CarrierId:default(PhenomenonCarrierId),affectedIds);
        }
        private void RegisterFieldBinding(){if(trap==null||trap.FieldNode==null||carrier==null||carrier.ActivationContext==null||!carrier.ActivationContext.HasFocalCarrier)return;FieldNetworkDomainCarrierBinding.Register(trap.FieldNode,carrier.ActivationContext.FocalCarrierId);}
        private void Update(){if(carrier!=null&&!carrier.IsActive(Time.time)){if(trap!=null)FieldNetworkDomainCarrierBinding.Unregister(trap.FieldNode);carrier=null;Reclaim();State=TrapDomainEnhancementState.Cleared;trap?.NotifyObservedStateChanged();Changed?.Invoke(this);}}
        private void HandleRemoved(PlayerLandmine _){if(trap!=null)FieldNetworkDomainCarrierBinding.Unregister(trap.FieldNode);carrier=null;Reclaim();State=TrapDomainEnhancementState.Cleared;Changed?.Invoke(this);}
        private void Reclaim(){if(reclaimed||reserve==null)return;reserve.Ledger.Reclaim(allocation.Id);reclaimed=true;}
        private void OnDestroy(){if(trap!=null){trap.MeaningfulPulseResolved-=HandleMeaningfulPulse;trap.Removed-=HandleRemoved;FieldNetworkDomainCarrierBinding.Unregister(trap.FieldNode);}Reclaim();}
    }
}
