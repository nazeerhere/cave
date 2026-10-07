using System;
using UnityEngine;

namespace Cave.Gambit
{
    /// <summary>Deterministic coverage for the production Oblivion Disk-to-Gambit adapter contracts.</summary>
    public static class SovereignGambitTrapPieceVerification
    {
        private sealed class ValidPlacement : ISovereignAnchorPlacementValidator
        {
            public SovereignAnchorPlacementRejection Validate(SovereignAnchorPlacementRequest request) => SovereignAnchorPlacementRejection.None;
        }

        public static bool TryRunAll(out string failure)
        {
            try
            {
                VerifyPhysicalInfluenceAndArming();
                VerifyChainCutAndReconnect();
                VerifyRedundantCycleAndOverlap();
                VerifyModeIsolationAndDeterminism();
                failure=null;return true;
            }
            catch(Exception exception) { failure=exception.Message;return false; }
        }

        private static void VerifyPhysicalInfluenceAndArming()
        {
            SovereignGambitRuntime runtime=CreateRuntime("arming");
            StrategicNodeId root=runtime.TerritoryGraph.RootId,first=new StrategicNodeId("arming:oblivion-disk:1");
            runtime.RegisterOrUpdateNode(new StrategicTerritoryNode(first,GambitSide.Player,new Vector2(2f,0f),true));
            runtime.ConnectNodes(root,first);
            StrategicInfluence inactive=Physical("arming:one",first,new Vector2(2f,0f),4f,false,true,runtime);
            Require(inactive.Category==StrategicInfluenceSourceCategory.PhysicalControl&&inactive.Region.Radius==4f&&!inactive.LawMetadata.IsValid,"Physical influence did not preserve the actual pulse footprint or remained Law-free.");
            runtime.RegisterInfluence(inactive);runtime.Evaluate(0f);
            Require(!runtime.EnemySovereignInCheck&&runtime.EnemySovereignProtectionActive,"An unarmed disk contributed strategic authority.");
            runtime.RegisterInfluence(Physical("arming:one",first,new Vector2(2f,0f),4f,true,true,runtime));runtime.Evaluate(1f);
            GambitCheckResolutionResult check=runtime.ExplainEnemySovereignCheck();
            Require(runtime.EnemySovereignInCheck&&!runtime.EnemySovereignProtectionActive&&check.Trace.EffectiveThreatSourceIds.Count==1,"An armed connected physical disk did not establish Check exactly once.");
        }

        private static void VerifyChainCutAndReconnect()
        {
            SovereignGambitRuntime runtime=CreateRuntime("chain");
            StrategicNodeId root=runtime.TerritoryGraph.RootId,a=Node("chain",1),b=Node("chain",2),c=Node("chain",3),bridge=Node("chain",4);
            Add(runtime,a,1f);Add(runtime,b,2f);Add(runtime,c,3f);
            runtime.ConnectNodes(root,a);runtime.ConnectNodes(a,b);runtime.ConnectNodes(b,c);
            runtime.RegisterInfluence(Physical("chain:c",c,new Vector2(3f,0f),2.6f,true,true,runtime));runtime.Evaluate(0f);
            Require(runtime.EnemySovereignInCheck,"A real disk physical influence did not reach the existing Check resolver.");
            int version=runtime.TerritoryGraph.Version;runtime.RemoveNode(a);runtime.Evaluate(1f);
            StrategicTerritorySnapshot cut=runtime.TerritorySnapshot;
            Require(!cut.IsConnectedToRoot(b)&&!cut.IsConnectedToRoot(c)&&!runtime.EnemySovereignInCheck&&runtime.EnemySovereignProtectionActive&&cut.Version>version,"A network cut did not suppress only disconnected strategic authority and restore protection.");
            Add(runtime,bridge,.5f);runtime.ConnectNodes(root,bridge);runtime.ConnectNodes(bridge,b);runtime.Evaluate(2f);
            Require(runtime.TerritorySnapshot.IsConnectedToRoot(c)&&runtime.EnemySovereignInCheck,"A surviving disk failed to regain authority through a new route.");
        }

        private static void VerifyRedundantCycleAndOverlap()
        {
            SovereignGambitRuntime runtime=CreateRuntime("cycle");
            StrategicNodeId root=runtime.TerritoryGraph.RootId,a=Node("cycle",1),b=Node("cycle",2),c=Node("cycle",3);
            Add(runtime,a,1f);Add(runtime,b,1.5f);Add(runtime,c,3f);
            runtime.ConnectNodes(root,a);runtime.ConnectNodes(root,b);runtime.ConnectNodes(a,c);runtime.ConnectNodes(b,c);runtime.ConnectNodes(a,b);
            runtime.RegisterInfluence(Physical("cycle:c",c,new Vector2(3f,0f),2.6f,true,true,runtime));
            runtime.RegisterInfluence(Physical("cycle:b-overlap",b,new Vector2(3f,0f),2.6f,true,true,runtime));runtime.Evaluate(0f);
            Require(runtime.ExplainEnemySovereignCheck().Trace.EffectiveThreatSourceIds.Count==2,"Overlapping disks were not preserved as distinct strategic influence sources.");
            runtime.RemoveNode(a);runtime.Evaluate(1f);
            Require(runtime.TerritorySnapshot.IsConnectedToRoot(c)&&runtime.EnemySovereignInCheck,"A redundant route falsely disconnected a surviving disk or broke Check.");
            string trace=GambitTrapNetworkDiagnostics.BuildBoundedTrace(runtime.TerritorySnapshot,16);
            Require(!string.IsNullOrEmpty(trace)&&trace.Contains("cycle:oblivion-disk:3"),"Bounded cycle diagnostics were not stable.");
        }

        private static void VerifyModeIsolationAndDeterminism()
        {
            // No runtime is globally discoverable: constructing ordinary physical influence does not register it anywhere.
            StrategicInfluence ordinary=GambitTrapPhysicalInfluenceFactory.Create("ordinary",new StrategicNodeId("ordinary:1"),Vector2.zero,1f,true,false,default,default);
            Require(ordinary.Owner==GambitSide.Player&&!ordinary.Target.IsValid&&!ordinary.AuthorityConnected,"Non-Gambit disk state leaked strategic authority.");
            bool first=RunDeterministicScenario("deterministic-a"),second=RunDeterministicScenario("deterministic-b");
            Require(first&&second,"Identical trap topology scenarios produced different Check results.");
            SovereignGambitRuntime cleanup=CreateRuntime("cleanup");cleanup.End();
            Require(cleanup.TerritorySnapshot.Nodes.Count==0&&!cleanup.EnemySovereignTarget.IsValid,"Match cleanup retained trap/topology state.");
        }

        private static bool RunDeterministicScenario(string match)
        {
            SovereignGambitRuntime runtime=CreateRuntime(match);StrategicNodeId node=Node(match,1);Add(runtime,node,2f);runtime.ConnectNodes(runtime.TerritoryGraph.RootId,node);
            runtime.RegisterInfluence(Physical(match+":physical",node,new Vector2(2f,0f),4f,true,true,runtime));runtime.Evaluate(0f);return runtime.EnemySovereignInCheck;
        }

        private static SovereignGambitRuntime CreateRuntime(string match)
        {
            SovereignGambitRuntime runtime=new SovereignGambitRuntime(match);
            Require(runtime.TryPlaceAnchor(new SovereignAnchorPlacementRequest(match,new StrategicNodeId(match+":anchor"),Vector2.zero,Vector2.one),new ValidPlacement()).Accepted,"Anchor placement failed.");
            Require(runtime.RegisterEnemySovereign(new StrategicTargetId(match+":sovereign"),new StrategicInfluenceRegion(new Vector2(6f,0f),.5f)),"Sovereign registration failed.");
            Require(runtime.Activate(),"Match activation failed.");return runtime;
        }
        private static StrategicNodeId Node(string match,int index) => new StrategicNodeId(match+":oblivion-disk:"+index);
        private static void Add(SovereignGambitRuntime runtime,StrategicNodeId id,float x) => Require(runtime.RegisterOrUpdateNode(new StrategicTerritoryNode(id,GambitSide.Player,new Vector2(x,0f),true)),"Trap node registration failed: "+id);
        private static StrategicInfluence Physical(string source,StrategicNodeId id,Vector2 position,float radius,bool armed,bool connected,SovereignGambitRuntime runtime)
        { StrategicInfluenceRegion target;runtime.TryGetEnemySovereignRegion(out target);return GambitTrapPhysicalInfluenceFactory.Create(source,id,position,radius,armed,connected,runtime.EnemySovereignTarget,target); }
        private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    }
}
