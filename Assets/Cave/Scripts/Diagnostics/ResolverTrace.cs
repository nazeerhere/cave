using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cave.Domain;
using Cave.Interactions;

namespace Cave.Diagnostics
{
    public enum ResolverTraceMode { Off = 0, Summary = 1, Full = 2 }
    public enum ResolverTraceRejectionCategory
    {
        None = 0, ExpressionMismatch = 1, PhenomenonMismatch = 2, TerritoryMismatch = 3, ScopeMismatch = 4,
        InactiveDomain = 5, InsufficientAuthority = 6, InvalidTarget = 7, ConditionFailed = 8,
        Superseded = 9, InvalidEvidence = 10, InvalidRequest = 11, ContextUnavailable = 12, OperationUnsupported = 13
    }

    public readonly struct ResolverTraceLaw
    {
        public ResolverTraceLaw(LawExpression expression, LawPhenomenon phenomenon, LawTerritoryPrinciple territory, bool eligible, ResolverTraceRejectionCategory rejection, string detail)
        { Expression = expression; Phenomenon = phenomenon; Territory = territory; Eligible = eligible; Rejection = rejection; Detail = detail; }
        public LawExpression Expression { get; } public LawPhenomenon Phenomenon { get; } public LawTerritoryPrinciple Territory { get; }
        public bool Eligible { get; } public ResolverTraceRejectionCategory Rejection { get; } public string Detail { get; }
    }

    public readonly struct ResolverTraceTransition
    {
        public ResolverTraceTransition(NormalizedSemanticTransition transition)
        {
            CarrierId = transition != null ? transition.CarrierId.ToString() : string.Empty;
            Phenomenon = transition != null ? transition.Phenomenon : default;
            Operation = transition != null ? transition.Operation : default;
            BeforeValue = transition != null && transition.Before != null ? transition.Before.SemanticValue : 0f;
            AfterValue = transition != null && transition.After != null ? transition.After.SemanticValue : 0f;
            BeforeRegion = transition != null && transition.Before != null ? transition.Before.Region : default;
            AfterRegion = transition != null && transition.After != null ? transition.After.Region : default;
        }
        public ResolverTraceTransition(DomainAuthoritativeTransition transition)
        {
            CarrierId = transition != null ? transition.CarrierId.ToString() : string.Empty;
            Phenomenon = transition != null ? transition.Phenomenon : default;
            Operation = transition != null && transition.RawTransition != null ? transition.RawTransition.Operation : default;
            BeforeValue = transition != null && transition.Before != null ? transition.Before.SemanticValue : 0f;
            AfterValue = transition != null && transition.After != null ? transition.After.SemanticValue : 0f;
            BeforeRegion = transition != null && transition.Before != null ? transition.Before.Region : default;
            AfterRegion = transition != null && transition.After != null ? transition.After.Region : default;
        }
        public string CarrierId { get; } public LawPhenomenon Phenomenon { get; } public PhenomenonOperationKind Operation { get; }
        public float BeforeValue { get; } public float AfterValue { get; }
        public PhenomenonSemanticRegion BeforeRegion { get; } public PhenomenonSemanticRegion AfterRegion { get; }
    }

    public sealed class ResolverTraceRecord
    {
        internal ResolverTraceRecord(ulong id, ulong? parentId, ulong interactionSequence, double timestamp, string requestKind,
            LawExpression? expression, LawPhenomenon phenomenon, PhenomenonOperationKind operation, float magnitude,
            bool succeeded, ResolverTraceRejectionCategory rejection, IReadOnlyList<ResolverTraceLaw> laws,
            IReadOnlyList<ResolverTraceTransition> transitions, IReadOnlyList<DomainConflictTrace> conflicts)
        { ResolutionId=id; ParentResolutionId=parentId; OriginatingInteractionSequence=interactionSequence; Timestamp=timestamp; RequestKind=requestKind; Expression=expression; Phenomenon=phenomenon; Operation=operation; Magnitude=magnitude; Succeeded=succeeded; Rejection=rejection; Laws=laws; Transitions=transitions; Conflicts=conflicts; }
        public ulong ResolutionId { get; } public ulong? ParentResolutionId { get; } public ulong OriginatingInteractionSequence { get; }
        public double Timestamp { get; } public string RequestKind { get; } public LawExpression? Expression { get; }
        public LawPhenomenon Phenomenon { get; } public PhenomenonOperationKind Operation { get; } public float Magnitude { get; }
        public bool Succeeded { get; } public ResolverTraceRejectionCategory Rejection { get; }
        public IReadOnlyList<ResolverTraceLaw> Laws { get; } public IReadOnlyList<ResolverTraceTransition> Transitions { get; }
        public IReadOnlyList<DomainConflictTrace> Conflicts { get; }
    }

    /// <summary>Opt-in bounded observer. Resolver code never reads its decisions and remains identical while this is Off.</summary>
    public static class ResolverTraceService
    {
        private const int DefaultCapacity = 384;
        private static ResolverTraceRecord[] records = new ResolverTraceRecord[DefaultCapacity];
        private static int nextIndex;
        private static int count;
        private static ulong nextResolutionId;
        [ThreadStatic] private static ulong? currentResolutionId;
        public static ResolverTraceMode Mode { get; set; } = ResolverTraceMode.Off;
        public static int Capacity => records.Length;
        public static int Count => count;
        public static IReadOnlyList<ResolverTraceRecord> Snapshot()
        {
            List<ResolverTraceRecord> values = new List<ResolverTraceRecord>(count);
            int start = count == records.Length ? nextIndex : 0;
            for (int index=0;index<count;index++) { ResolverTraceRecord record=records[(start+index)%records.Length]; if(record!=null) values.Add(record); }
            return values.AsReadOnly();
        }
        public static void Clear() { Array.Clear(records,0,records.Length); nextIndex=0; count=0; }
        /// <summary>Developer setup only. Resizing retires old observations deterministically; it never reaches resolver state.</summary>
        public static void ConfigureCapacity(int capacity)
        {
            capacity=Math.Max(16,Math.Min(4096,capacity)); if(capacity==records.Length)return;
            IReadOnlyList<ResolverTraceRecord> previous=Snapshot(); records=new ResolverTraceRecord[capacity]; nextIndex=0; count=0;
            int first=Math.Max(0,previous.Count-capacity); for(int index=first;index<previous.Count;index++)Add(previous[index]);
        }
        public static ResolverTraceScope Begin()
        {
            if (Mode == ResolverTraceMode.Off) return default;
            ulong id=++nextResolutionId; ResolverTraceScope scope=new ResolverTraceScope(id,currentResolutionId); currentResolutionId=id; return scope;
        }
        internal static void End(ResolverTraceScope scope) { if(scope.IsActive && currentResolutionId==scope.ResolutionId) currentResolutionId=scope.ParentResolutionId; }
        internal static void Complete(ResolverTraceScope scope, DomainBoundedOrchestrationResult result)
        {
            if(!scope.IsActive || Mode==ResolverTraceMode.Off)return;
            DomainResolutionPlan plan=result!=null?result.ResolutionPlan:null;
            DomainResolutionIntent intent=plan!=null?plan.Intent:null;
            List<ResolverTraceLaw> laws=Mode==ResolverTraceMode.Full?BuildLaws(plan):Empty<ResolverTraceLaw>();
            List<ResolverTraceTransition> transitions=BuildTransitions(result);
            Add(new ResolverTraceRecord(scope.ResolutionId,scope.ParentResolutionId,InteractionEventBus.CurrentDispatchSequence,Now(),
                result!=null&&result.OriginalTransferRequest!=null?"Transfer":"Unary", intent!=null&&intent.ExpressionContext!=null&&intent.ExpressionContext.HasExpression?(LawExpression?)intent.ExpressionContext.Expression:null,
                intent!=null?intent.Phenomenon:default, intent!=null?intent.Operation:default, intent!=null?intent.Magnitude:0f,
                result!=null&&result.Succeeded, result!=null&&result.Succeeded?ResolverTraceRejectionCategory.None:ResolverTraceRejectionCategory.InvalidRequest,
                laws.AsReadOnly(),transitions.AsReadOnly(),Empty<DomainConflictTrace>().AsReadOnly()));
        }
        public static void RecordArbitration(CrossDomainArbitrationResult arbitration)
        {
            if(Mode==ResolverTraceMode.Off||arbitration==null)return;
            ResolverTraceScope scope=Begin();
            try
            {
                List<DomainConflictTrace> conflicts=new List<DomainConflictTrace>();
                if(Mode==ResolverTraceMode.Full) for(int index=0;index<arbitration.Outcomes.Count;index++) if(arbitration.Outcomes[index].Trace!=null) conflicts.Add(arbitration.Outcomes[index].Trace);
                Add(new ResolverTraceRecord(scope.ResolutionId,scope.ParentResolutionId,InteractionEventBus.CurrentDispatchSequence,Now(),"CrossDomainArbitration",null,default,default,0f,arbitration.Succeeded,
                    arbitration.Succeeded?ResolverTraceRejectionCategory.None:ResolverTraceRejectionCategory.InvalidEvidence,Empty<ResolverTraceLaw>().AsReadOnly(),Empty<ResolverTraceTransition>().AsReadOnly(),conflicts.AsReadOnly()));
            }
            finally { scope.Dispose(); }
        }
        private static void Add(ResolverTraceRecord record) { records[nextIndex]=record; nextIndex=(nextIndex+1)%records.Length; count=Math.Min(records.Length,count+1); }
        private static List<ResolverTraceLaw> BuildLaws(DomainResolutionPlan plan)
        {
            List<ResolverTraceLaw> values=new List<ResolverTraceLaw>(); if(plan==null||plan.Laws==null)return values;
            for(int index=0;index<plan.Laws.Count;index++) { PlannedDomainLaw law=plan.Laws[index]; if(law==null||law.CandidateLaw==null)continue; values.Add(new ResolverTraceLaw(law.CandidateLaw.Expression,law.CandidateLaw.Phenomenon,law.CandidateLaw.TerritoryPrinciple,law.SharedEligible,Map(law.EligibilityReason),law.EligibilityReason.ToString())); }
            return values;
        }
        private static List<ResolverTraceTransition> BuildTransitions(DomainBoundedOrchestrationResult result)
        {
            List<ResolverTraceTransition> values=new List<ResolverTraceTransition>(); if(result==null||result.AllAuthoritativeTransitions==null)return values;
            for(int index=0;index<result.AllAuthoritativeTransitions.Count;index++) { DomainAuthoritativeTransition value=result.AllAuthoritativeTransitions[index]; if(value!=null)values.Add(new ResolverTraceTransition(value)); }
            return values;
        }
        private static ResolverTraceRejectionCategory Map(LawEvaluationRejectionReason reason)
        {
            if(reason==LawEvaluationRejectionReason.ExpressionMismatch)return ResolverTraceRejectionCategory.ExpressionMismatch;
            if(reason==LawEvaluationRejectionReason.PhenomenonMismatch)return ResolverTraceRejectionCategory.PhenomenonMismatch;
            if(reason==LawEvaluationRejectionReason.ExpressionUnavailable||reason==LawEvaluationRejectionReason.FrenzyInactive)return ResolverTraceRejectionCategory.InactiveDomain;
            if(reason==LawEvaluationRejectionReason.NoDefinedInverse||reason==LawEvaluationRejectionReason.TerritoryBehaviorNotImplemented)return ResolverTraceRejectionCategory.OperationUnsupported;
            return reason==LawEvaluationRejectionReason.None?ResolverTraceRejectionCategory.None:ResolverTraceRejectionCategory.ConditionFailed;
        }
        private static List<T> Empty<T>() { return new List<T>(0); }
        private static double Now() { return Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency; }
    }

    public struct ResolverTraceScope : IDisposable
    {
        internal ResolverTraceScope(ulong id,ulong? parent){ResolutionId=id;ParentResolutionId=parent;IsActive=true;}
        public ulong ResolutionId { get; } public ulong? ParentResolutionId { get; } public bool IsActive { get; }
        public void Complete(DomainBoundedOrchestrationResult result) { ResolverTraceService.Complete(this,result); }
        public void Dispose() { ResolverTraceService.End(this); }
    }
}
