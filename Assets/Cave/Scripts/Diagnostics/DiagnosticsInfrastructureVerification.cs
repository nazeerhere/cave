using Cave.Domain;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Diagnostics
{
    /// <summary>Pure contracts for diagnostics: observation modes and policy decisions never participate in resolution truth.</summary>
    public static class DiagnosticsInfrastructureVerification
    {
        public static bool TryRunAll(out string failure)
        {
            ResolverTraceService.Clear();
            DomainBoundedOrchestrationResult off=Resolve(ResolverTraceMode.Off);
            int offCount=ResolverTraceService.Count;
            DomainBoundedOrchestrationResult summary=Resolve(ResolverTraceMode.Summary);
            int summaryCount=ResolverTraceService.Count;
            DomainBoundedOrchestrationResult full=Resolve(ResolverTraceMode.Full);
            ResolverTraceRecord fullRecord=ResolverTraceService.Snapshot()[ResolverTraceService.Count-1];
            bool sameTruth=off.Succeeded==summary.Succeeded&&summary.Succeeded==full.Succeeded&&off.AllAuthoritativeTransitions.Count==summary.AllAuthoritativeTransitions.Count&&summary.AllAuthoritativeTransitions.Count==full.AllAuthoritativeTransitions.Count;
            bool modes=offCount==0&&summaryCount==1&&fullRecord.Transitions.Count>0&&fullRecord.ResolutionId>0;
            ResolverTraceService.Clear(); ResolverTraceService.Mode=ResolverTraceMode.Summary;
            ResolverTraceScope parent=ResolverTraceService.Begin(); DomainBoundedOrchestrationResult childResult=Resolve(ResolverTraceMode.Summary); parent.Complete(childResult); parent.Dispose();
            IReadOnlyList<ResolverTraceRecord> causalRecords=ResolverTraceService.Snapshot();
            bool causal=causalRecords.Count==2&&causalRecords[0].ParentResolutionId==causalRecords[1].ResolutionId;
            for(int index=0;index<ResolverTraceService.Capacity+9;index++)Resolve(ResolverTraceMode.Summary);
            bool bounded=ResolverTraceService.Count==ResolverTraceService.Capacity;
            ResolverTraceService.ConfigureCapacity(16);
            for(int index=0;index<24;index++)Resolve(ResolverTraceMode.Summary);
            bool configurableBound=ResolverTraceService.Capacity==16&&ResolverTraceService.Count==16;
            ResolverTraceService.ConfigureCapacity(384);

            ResourceGovernorPolicy policy=new ResourceGovernorPolicy { SustainedPressureSeconds=1f, SustainedRecoverySeconds=1f, DegradedEntryMilliseconds=20f, CriticalEntryMilliseconds=30f, DegradedRecoveryMilliseconds=15f, CriticalRecoveryMilliseconds=25f };
            bool normal=policy.Decide(new RuntimeQosRequest(RuntimeWorkCategory.Vfx,RuntimeQosPriority.P3Cosmetic,1f,1f))==RuntimeQosLevel.Full;
            policy.Evaluate(0f,21f); policy.Evaluate(1.1f,21f); bool degraded=policy.Mode==ResourceGovernorMode.Degraded&&policy.Decide(new RuntimeQosRequest(RuntimeWorkCategory.Vfx,RuntimeQosPriority.P2Presentation,1f,1f))==RuntimeQosLevel.Reduced;
            policy.Evaluate(2f,31f); policy.Evaluate(3.1f,31f); bool critical=policy.Mode==ResourceGovernorMode.Critical&&policy.Decide(new RuntimeQosRequest(RuntimeWorkCategory.Projectile,RuntimeQosPriority.P3Cosmetic,1f,1f,true))==RuntimeQosLevel.Full&&policy.Decide(new RuntimeQosRequest(RuntimeWorkCategory.Vfx,RuntimeQosPriority.P3Cosmetic,1f,1f))==RuntimeQosLevel.Denied;
            policy.Evaluate(4f,24f); bool hysteretic=policy.Mode==ResourceGovernorMode.Critical; policy.Evaluate(5.1f,24f); bool recovered=policy.Mode==ResourceGovernorMode.Degraded;
            RuntimeObjectPool<object> pool=new RuntimeObjectPool<object>(RuntimeWorkCategory.Impact,()=>new object()); object pooled; bool acquired=pool.TryAcquire(out pooled); pool.Return(pooled); bool poolContract=acquired&&pool.Telemetry.Active==0&&pool.Telemetry.Available==1&&pool.Telemetry.Misses==1&&pool.Telemetry.Expansions==1;
            GameObject governorObject=new GameObject("Governor verification"); ResourceGovernor governor=governorObject.AddComponent<ResourceGovernor>(); governor.Control=ResourceGovernorControl.ForceCritical; bool forcedImmediately=governor.Mode==ResourceGovernorMode.Critical&&governor.Decide(new RuntimeQosRequest(RuntimeWorkCategory.Vfx,RuntimeQosPriority.P3Cosmetic,1f,1f))==RuntimeQosLevel.Denied; UnityEngine.Object.DestroyImmediate(governorObject);
            bool pass=sameTruth&&modes&&causal&&bounded&&configurableBound&&normal&&degraded&&critical&&hysteretic&&recovered&&poolContract&&forcedImmediately;
            failure=pass?null:"Diagnostics infrastructure verification failed: truth="+sameTruth+", modes="+modes+", causal="+causal+", bounded="+bounded+", configurable="+configurableBound+", normal="+normal+", degraded="+degraded+", critical="+critical+", hysteresis="+hysteretic+", recovery="+recovered+", pool="+poolContract+", forced="+forcedImmediately;
            ResolverTraceService.Mode=ResolverTraceMode.Off; ResolverTraceService.Clear();
            return pass;
        }

        private static DomainBoundedOrchestrationResult Resolve(ResolverTraceMode mode)
        {
            ResolverTraceService.Mode=mode;
            PhenomenonSemanticSnapshot snapshot; PhenomenonSemanticResult semantic;
            PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat,0f,out snapshot,out semantic);
            return DomainBoundedOrchestrator.Execute(new UnaryDomainOrchestrationRequest(DomainComposition.Empty,new LawExpressionContext(true,LawExpression.Projectile,false),LawPhenomenon.Heat,PhenomenonOperationKind.Add,0f,new PhenomenonCarrierSnapshot(new PhenomenonCarrierId("diagnostic"),LawPhenomenon.Heat,snapshot),DomainOrchestrationContextSet.Empty));
        }
    }
}
