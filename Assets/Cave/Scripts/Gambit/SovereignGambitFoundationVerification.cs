using System;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Gambit
{
    public static class SovereignGambitFoundationVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyPlacementAndRoot(out failure)&&VerifyTopology(out failure)&&VerifyChecksAndProtection(out failure)&&VerifyLifecycleAndDeterminism(out failure);
        }
        private static bool VerifyPlacementAndRoot(out string failure)
        {
            SovereignGambitRuntime match=new SovereignGambitRuntime("match");
            SovereignAnchorPlacementRequest request=new SovereignAnchorPlacementRequest("match",new StrategicNodeId("anchor"),new Vector2(17.25f,-3.5f),new Vector2(1f,1f));
            SovereignAnchorPlacementResult invalid=match.TryPlaceAnchor(request,new Validator(SovereignAnchorPlacementRejection.NoGround));
            SovereignAnchorPlacementResult valid=match.TryPlaceAnchor(request,new Validator(SovereignAnchorPlacementRejection.None));
            bool pass=match.Phase==GambitMatchPhase.Setup&&!invalid.Accepted&&invalid.Rejection==SovereignAnchorPlacementRejection.NoGround&&valid.Accepted&&match.TerritoryGraph.HasRoot&&match.TerritoryGraph.RootId.Equals(request.RootId)&&match.PlayerAnchorTarget.Equals(new StrategicTargetId("anchor"));
            failure=pass?null:"Placement/root contract failed.";return pass;
        }
        private static bool VerifyTopology(out string failure)
        {
            SovereignGambitRuntime match=Active("topology");StrategicNodeId a=new StrategicNodeId("A"),b=new StrategicNodeId("B"),c=new StrategicNodeId("C");
            int before=match.TerritoryGraph.Version;match.RegisterOrUpdateNode(Node(a));match.RegisterOrUpdateNode(Node(b));match.ConnectNodes(match.TerritoryGraph.RootId,a);match.ConnectNodes(a,b);
            StrategicTerritorySnapshot connected=match.TerritoryGraph.Snapshot();match.RemoveNode(a);StrategicTerritorySnapshot disconnected=match.TerritoryGraph.Snapshot();match.RegisterOrUpdateNode(Node(c));match.ConnectNodes(match.TerritoryGraph.RootId,c);match.ConnectNodes(c,b);StrategicTerritorySnapshot reconnected=match.TerritoryGraph.Snapshot();
            bool pass=connected.IsConnectedToRoot(a)&&connected.IsConnectedToRoot(b)&&!disconnected.IsConnectedToRoot(b)&&Contains(disconnected,b)&&reconnected.IsConnectedToRoot(b)&&match.TerritoryGraph.Version>before;
            failure=pass?null:"Topology did not root/disconnect/reconnect deterministically.";return pass;
        }
        private static bool VerifyChecksAndProtection(out string failure)
        {
            SovereignGambitRuntime match=Active("check");StrategicNodeId node=new StrategicNodeId("threat");match.RegisterOrUpdateNode(Node(node));match.ConnectNodes(match.TerritoryGraph.RootId,node);
            match.RegisterInfluence(new StrategicInfluence("player-threat",node,GambitSide.Player,match.EnemySovereignTarget,true,true));match.Evaluate(0f);bool check=match.EnemySovereignInCheck&&!match.EnemySovereignProtectionActive&&match.ExplainEnemySovereignCheck().Trace.EffectiveThreatSourceIds.Count==1;
            match.RegisterBlocker(new StrategicDefenseCoverage("cover",GambitSide.Enemy,match.EnemySovereignTarget,true));match.Evaluate(0f);bool blocked=!match.EnemySovereignInCheck&&match.EnemySovereignProtectionActive&&match.ExplainEnemySovereignCheck().Trace.DefenseBlockers==1;
            match.RemoveBlocker("cover");match.RegisterInfluence(new StrategicInfluence("enemy-threat",default,GambitSide.Enemy,match.PlayerAnchorTarget,true,true));match.Evaluate(0f);bool simultaneous=match.EnemySovereignInCheck&&match.PlayerAnchorInCheck&&match.Phase==GambitMatchPhase.Active;
            match.DisconnectNodes(match.TerritoryGraph.RootId,node);match.Evaluate(0f);bool broken=!match.EnemySovereignInCheck&&match.EnemySovereignProtectionActive&&match.PlayerAnchorInCheck;
            bool pass=check&&blocked&&simultaneous&&broken;failure=pass?null:"Check/protection/simultaneous threat contract failed.";return pass;
        }
        private static bool VerifyLifecycleAndDeterminism(out string failure)
        {
            SovereignGambitRuntime invalid=Active("invalid");bool noVictory=!invalid.ReportEnemySovereignDefeated()&&invalid.Phase==GambitMatchPhase.Active;
            SovereignGambitRuntime valid=Active("valid");StrategicNodeId node=new StrategicNodeId("n");valid.RegisterOrUpdateNode(Node(node));valid.ConnectNodes(valid.TerritoryGraph.RootId,node);valid.RegisterInfluence(new StrategicInfluence("threat",node,GambitSide.Player,valid.EnemySovereignTarget,true,true));valid.Evaluate(0f);bool checkmate=valid.ReportEnemySovereignDefeated()&&valid.Phase==GambitMatchPhase.Victory&&!valid.ReportEnemySovereignDefeated();
            SovereignGambitRuntime first=Checked("determinism-a"),second=Checked("determinism-b");bool deterministic=first.ExplainEnemySovereignCheck().IsCheck==second.ExplainEnemySovereignCheck().IsCheck&&first.ExplainEnemySovereignCheck().Trace.EffectiveThreatSourceIds[0]==second.ExplainEnemySovereignCheck().Trace.EffectiveThreatSourceIds[0];
            valid.End();bool cleanup=valid.Phase==GambitMatchPhase.Ended&&!valid.PlayerAnchorValid&&valid.TerritoryGraph.Snapshot().Nodes.Count==0&&!valid.EnemySovereignInCheck&&!valid.PlayerAnchorInCheck;
            bool pass=noVictory&&checkmate&&deterministic&&cleanup;failure=pass?null:"Checkmate/cleanup/determinism contract failed.";return pass;
        }
        private static SovereignGambitRuntime Active(string id) { SovereignGambitRuntime match=new SovereignGambitRuntime(id);SovereignAnchorPlacementResult placed=match.TryPlaceAnchor(new SovereignAnchorPlacementRequest(id,new StrategicNodeId("anchor"),Vector2.zero,Vector2.one),new Validator(SovereignAnchorPlacementRejection.None));if(!placed.Accepted||!match.RegisterEnemySovereign(new StrategicTargetId("sovereign"))||!match.Activate())throw new InvalidOperationException("Test match could not activate.");return match; }
        private static SovereignGambitRuntime Checked(string id) { SovereignGambitRuntime match=Active(id);StrategicNodeId node=new StrategicNodeId("node");match.RegisterOrUpdateNode(Node(node));match.ConnectNodes(match.TerritoryGraph.RootId,node);match.RegisterInfluence(new StrategicInfluence("threat",node,GambitSide.Player,match.EnemySovereignTarget,true,true));match.Evaluate(0f);return match; }
        private static StrategicTerritoryNode Node(StrategicNodeId id) => new StrategicTerritoryNode(id,GambitSide.Player,Vector2.one,true);
        private static bool Contains(StrategicTerritorySnapshot snapshot,StrategicNodeId id) { for(int i=0;i<snapshot.Nodes.Count;i++)if(snapshot.Nodes[i].Id.Equals(id))return true;return false; }
        private sealed class Validator : ISovereignAnchorPlacementValidator { private readonly SovereignAnchorPlacementRejection rejection; public Validator(SovereignAnchorPlacementRejection value){rejection=value;} public SovereignAnchorPlacementRejection Validate(SovereignAnchorPlacementRequest request)=>rejection; }
    }
}
