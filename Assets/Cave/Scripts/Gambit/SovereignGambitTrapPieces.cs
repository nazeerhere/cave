using System;
using System.Collections.Generic;
using System.Text;
using Cave.Domain;
using Cave.FieldControl;
using Cave.Player;
using UnityEngine;

namespace Cave.Gambit
{
    /// <summary>Inspectable, match-local facts for one ordinary Oblivion Disk acting as a Gambit piece.</summary>
    public readonly struct GambitTrapPieceSnapshot
    {
        public GambitTrapPieceSnapshot(StrategicNodeId nodeId,bool armed,bool enhanced,bool connected,bool physicalOperational,bool strategicInfluenceActive,bool checkContributor,float physicalRadius)
        { NodeId=nodeId;Armed=armed;Enhanced=enhanced;Connected=connected;PhysicalOperational=physicalOperational;StrategicInfluenceActive=strategicInfluenceActive;CheckContributor=checkContributor;PhysicalRadius=physicalRadius; }
        public StrategicNodeId NodeId { get; } public bool Armed { get; } public bool Enhanced { get; } public bool Connected { get; }
        public bool PhysicalOperational { get; } public bool StrategicInfluenceActive { get; } public bool CheckContributor { get; } public float PhysicalRadius { get; }
    }

    /// <summary>
    /// Thin observation adapter over PlayerLandmine. It neither owns nor changes arming, pulses, Potential, Reserve, VFX, or destruction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLandmine))]
    public sealed class SovereignGambitTrapPiece : MonoBehaviour
    {
        private PlayerLandmine landmine;
        private SovereignGambitRuntime runtime;
        private StrategicNodeId nodeId;
        private string physicalInfluenceId;
        private string localizedDomainInfluenceId;
        private bool registered;

        public PlayerLandmine PhysicalTrap => landmine;
        public StrategicNodeId NodeId => nodeId;
        public string InfluenceProviderId => physicalInfluenceId;
        public bool IsRegistered => registered;
        public bool IsArmed => landmine != null && landmine.IsArmed;
        public bool IsEnhanced => landmine!=null&&landmine.GetComponent<TrapDomainEnhancement>()?.IsEnhanced==true;
        public bool IsPhysicallyOperational => landmine != null && landmine.IsOperational;
        public bool IsConnectedToAnchor => runtime != null && runtime.TerritorySnapshot.IsConnectedToRoot(nodeId);

        internal bool Bind(SovereignGambitRuntime match,StrategicNodeId identity)
        {
            if(match==null||!identity.IsValid)return false;
            landmine=landmine!=null?landmine:GetComponent<PlayerLandmine>();
            if(landmine==null)return false;
            runtime=match;nodeId=identity;physicalInfluenceId=identity.Value+":physical-control";localizedDomainInfluenceId=identity.Value+":localized-domain";
            return RefreshNode();
        }

        internal bool RefreshNode()
        {
            if(runtime==null||landmine==null||!nodeId.IsValid)return false;
            bool changed=runtime.RegisterOrUpdateNode(new StrategicTerritoryNode(nodeId,GambitSide.Player,transform.position,IsPhysicallyOperational));
            registered=registered||changed||runtime.TerritorySnapshot.IsConnectedToRoot(nodeId)||ContainsNode(runtime.TerritorySnapshot,nodeId);
            return changed;
        }

        internal void RefreshInfluence()
        {
            if(runtime==null||landmine==null||string.IsNullOrEmpty(physicalInfluenceId))return;
            StrategicInfluenceRegion region=new StrategicInfluenceRegion(transform.position,landmine.LocalPulseRadius);
            StrategicInfluenceRegion sovereignRegion;
            runtime.TryGetEnemySovereignRegion(out sovereignRegion);
            runtime.RegisterInfluence(GambitTrapPhysicalInfluenceFactory.Create(
                physicalInfluenceId,nodeId,transform.position,landmine.LocalPulseRadius,IsPhysicallyOperational,
                IsConnectedToAnchor,runtime.EnemySovereignTarget,sovereignRegion));
            TrapDomainEnhancement enhancement=landmine.GetComponent<TrapDomainEnhancement>();
            if(enhancement!=null&&enhancement.IsLocalizedDomainActive&&enhancement.Carrier!=null&&enhancement.BoundLaw!=null)
            {
                StrategicInfluenceRegion domainRegion=new StrategicInfluenceRegion(enhancement.Carrier.Center,enhancement.Carrier.Radius);
                StrategicTargetId domainTarget=sovereignRegion.IsValid&&domainRegion.Intersects(sovereignRegion)?runtime.EnemySovereignTarget:default;
                DomainLaw law=enhancement.BoundLaw.Law;
                runtime.RegisterInfluence(new StrategicInfluence(localizedDomainInfluenceId,nodeId,GambitSide.Player,domainTarget,true,IsConnectedToAnchor,
                    "oblivion-disk:localized-domain",domainRegion,StrategicInfluenceSourceCategory.LocalizedDomain,
                    new StrategicLawMetadata(enhancement.BoundLaw.Id,law.Expression,law.Phenomenon,law.TerritoryPrinciple)));
            }
            else runtime.RemoveInfluence(localizedDomainInfluenceId);
        }

        internal void Unregister()
        {
            if(runtime==null)return;
            runtime.RemoveInfluence(physicalInfluenceId);
            runtime.RemoveInfluence(localizedDomainInfluenceId);
            runtime.RemoveNode(nodeId);
            registered=false;
        }

        public GambitTrapPieceSnapshot Snapshot()
        {
            bool connected=IsConnectedToAnchor;
            bool checkContributor=false;
            if(runtime!=null&&!string.IsNullOrEmpty(physicalInfluenceId))
            {
                GambitCheckResolutionResult trace=runtime.ExplainEnemySovereignCheck();
                for(int index=0;index<trace.Trace.EffectiveThreatSourceIds.Count;index++)
                    if(string.Equals(trace.Trace.EffectiveThreatSourceIds[index],physicalInfluenceId,StringComparison.Ordinal)){checkContributor=true;break;}
            }
            return new GambitTrapPieceSnapshot(nodeId,IsArmed,IsEnhanced,connected,IsPhysicallyOperational,IsPhysicallyOperational&&connected,checkContributor,landmine!=null?landmine.LocalPulseRadius:0f);
        }

        private static bool ContainsNode(StrategicTerritorySnapshot snapshot,StrategicNodeId id)
        {
            for(int index=0;index<snapshot.Nodes.Count;index++)if(snapshot.Nodes[index].Id.Equals(id))return true;
            return false;
        }
    }

    /// <summary>Production influence construction shared by the runtime bridge and deterministic verification.</summary>
    public static class GambitTrapPhysicalInfluenceFactory
    {
        public static StrategicInfluence Create(string sourceId,StrategicNodeId sourceNodeId,Vector2 position,float actualPulseRadius,bool operational,bool authorityConnected,StrategicTargetId enemySovereign,StrategicInfluenceRegion enemySovereignRegion)
        {
            StrategicInfluenceRegion region=new StrategicInfluenceRegion(position,actualPulseRadius);
            StrategicTargetId target=enemySovereignRegion.IsValid&&region.Intersects(enemySovereignRegion)?enemySovereign:default;
            return new StrategicInfluence(sourceId,sourceNodeId,GambitSide.Player,target,operational,authorityConnected,
                "oblivion-disk:physical-control",region,StrategicInfluenceSourceCategory.PhysicalControl,default);
        }
    }

    /// <summary>
    /// Match opt-in host. It observes completed disk deployment and maps the real FieldNetwork links into the generic Gambit graph.
    /// No global current-match state is created and no disk receives Gambit behavior outside a bound match.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLandmineInventory))]
    public sealed class SovereignGambitTrapMatchBridge : MonoBehaviour
    {
        private readonly Dictionary<PlayerLandmine,SovereignGambitTrapPiece> pieces=new Dictionary<PlayerLandmine,SovereignGambitTrapPiece>();
        private readonly HashSet<StrategicTerritoryEdge> appliedEdges=new HashSet<StrategicTerritoryEdge>();
        private PlayerLandmineInventory inventory;
        private SovereignGambitRuntime runtime;
        private bool subscribed;
        private bool rebuilding;

        public SovereignGambitRuntime Runtime => runtime;
        public int RegisteredPieceCount => pieces.Count;
        public float ConnectionDistance => inventory!=null?inventory.FieldLinkDistance:0f;

        /// <summary>Begins observing this player's actual disks for one already-created Gambit match.</summary>
        public bool Bind(SovereignGambitRuntime match)
        {
            if(match==null)return false;
            Unbind();runtime=match;inventory=inventory!=null?inventory:GetComponent<PlayerLandmineInventory>();
            if(inventory==null)return false;
            inventory.LandminePlaced+=HandleLandminePlaced;
            if(inventory.FieldNetwork!=null)inventory.FieldNetwork.TopologyChanged+=HandleFieldTopologyChanged;
            runtime.PhaseChanged+=HandlePhaseChanged;subscribed=true;
            IReadOnlyList<PlayerLandmine> existing=inventory.ActiveMines;
            for(int index=0;index<existing.Count;index++)Track(existing[index]);
            RebuildTopology();
            return true;
        }

        /// <summary>Call after an externally-driven disk movement or a match phase transition. There is no per-frame trap scan.</summary>
        public void RefreshTopology()
        {
            if(inventory!=null&&runtime!=null&&runtime.Phase==GambitMatchPhase.Active)
            {
                IReadOnlyList<PlayerLandmine> existing=inventory.ActiveMines;
                for(int index=0;index<existing.Count;index++)Track(existing[index]);
            }
            RebuildTopology();
        }

        public void Unbind()
        {
            if(subscribed&&inventory!=null)inventory.LandminePlaced-=HandleLandminePlaced;
            if(subscribed&&inventory!=null&&inventory.FieldNetwork!=null)inventory.FieldNetwork.TopologyChanged-=HandleFieldTopologyChanged;
            if(subscribed&&runtime!=null)runtime.PhaseChanged-=HandlePhaseChanged;
            subscribed=false;
            foreach(KeyValuePair<PlayerLandmine,SovereignGambitTrapPiece> pair in pieces)
            {
                if(pair.Key!=null){pair.Key.StateChanged-=HandleLandmineStateChanged;pair.Key.Removed-=HandleLandmineRemoved;}
                pair.Value?.Unregister();
            }
            pieces.Clear();appliedEdges.Clear();runtime=null;
        }

        public string BuildBoundedNetworkTrace(int maximumNodes=32)
        {
            return GambitTrapNetworkDiagnostics.BuildBoundedTrace(runtime!=null?runtime.TerritorySnapshot:null,Mathf.Max(1,maximumNodes));
        }

        private void Awake() { inventory=GetComponent<PlayerLandmineInventory>(); }
        private void OnDisable() { Unbind(); }

        private void HandleLandminePlaced(PlayerLandmine mine) { if(Track(mine))RebuildTopology(); }
        private void HandlePhaseChanged(GambitMatchPhase phase) { if(phase==GambitMatchPhase.Active)RefreshTopology(); }
        private void HandleFieldTopologyChanged() { RebuildTopology(); }
        private void HandleLandmineStateChanged(PlayerLandmine _) { RebuildTopology(); }
        private void HandleLandmineRemoved(PlayerLandmine mine)
        {
            SovereignGambitTrapPiece piece;
            if(!pieces.TryGetValue(mine,out piece))return;
            mine.StateChanged-=HandleLandmineStateChanged;mine.Removed-=HandleLandmineRemoved;
            piece?.Unregister();pieces.Remove(mine);RebuildTopology();
        }

        private bool Track(PlayerLandmine mine)
        {
            if(runtime==null||mine==null||pieces.ContainsKey(mine)||runtime.Phase!=GambitMatchPhase.Active)return false;
            if(mine.CreationOrder==0UL)return false;
            SovereignGambitTrapPiece piece=mine.GetComponent<SovereignGambitTrapPiece>();
            if(piece==null)piece=mine.gameObject.AddComponent<SovereignGambitTrapPiece>();
            StrategicNodeId id=new StrategicNodeId(runtime.MatchId+":oblivion-disk:"+mine.CreationOrder);
            if(!piece.Bind(runtime,id))return false;
            mine.StateChanged+=HandleLandmineStateChanged;mine.Removed+=HandleLandmineRemoved;pieces.Add(mine,piece);return true;
        }

        private void RebuildTopology()
        {
            if(rebuilding||runtime==null||runtime.Phase!=GambitMatchPhase.Active)return;
            rebuilding=true;
            try
            {
                List<SovereignGambitTrapPiece> ordered=OrderedPieces();
                for(int index=0;index<ordered.Count;index++)ordered[index].RefreshNode();
                StrategicTerritorySnapshot before=runtime.TerritorySnapshot;
                StrategicTerritoryNode root=FindRoot(before);
                HashSet<StrategicTerritoryEdge> desired=new HashSet<StrategicTerritoryEdge>();
                if(root!=null)
                {
                    for(int index=0;index<ordered.Count;index++)
                    {
                        SovereignGambitTrapPiece piece=ordered[index];
                        if(!piece.IsPhysicallyOperational)continue;
                        if(IsWithinAnchorRange(root,piece))desired.Add(new StrategicTerritoryEdge(root.Id,piece.NodeId));
                        for(int other=index+1;other<ordered.Count;other++)
                            if(UsesRealFieldLink(piece,ordered[other]))desired.Add(new StrategicTerritoryEdge(piece.NodeId,ordered[other].NodeId));
                    }
                }
                List<StrategicTerritoryEdge> removals=new List<StrategicTerritoryEdge>();
                foreach(StrategicTerritoryEdge edge in appliedEdges)if(!desired.Contains(edge))removals.Add(edge);
                for(int index=0;index<removals.Count;index++){runtime.DisconnectNodes(removals[index].First,removals[index].Second);appliedEdges.Remove(removals[index]);}
                foreach(StrategicTerritoryEdge edge in desired)if(appliedEdges.Add(edge))runtime.ConnectNodes(edge.First,edge.Second);
                for(int index=0;index<ordered.Count;index++)ordered[index].RefreshInfluence();
                runtime.Evaluate(Time.time);
            }
            finally { rebuilding=false; }
        }

        private bool IsWithinAnchorRange(StrategicTerritoryNode root,SovereignGambitTrapPiece piece)
        {
            float range=ConnectionDistance;
            return range>0f&&piece.IsPhysicallyOperational&&((Vector2)piece.transform.position-root.Position).sqrMagnitude<=range*range;
        }

        private bool UsesRealFieldLink(SovereignGambitTrapPiece first,SovereignGambitTrapPiece second)
        {
            return first!=null&&second!=null&&first.IsPhysicallyOperational&&second.IsPhysicallyOperational
                &&inventory!=null&&inventory.FieldNetwork!=null
                &&inventory.FieldNetwork.AreLinked(first.PhysicalTrap.FieldNode,second.PhysicalTrap.FieldNode);
        }

        private List<SovereignGambitTrapPiece> OrderedPieces()
        {
            List<SovereignGambitTrapPiece> ordered=new List<SovereignGambitTrapPiece>(pieces.Count);
            foreach(KeyValuePair<PlayerLandmine,SovereignGambitTrapPiece> pair in pieces)if(pair.Key!=null&&pair.Value!=null)ordered.Add(pair.Value);
            ordered.Sort((left,right)=>left.NodeId.CompareTo(right.NodeId));return ordered;
        }
        private static StrategicTerritoryNode FindRoot(StrategicTerritorySnapshot snapshot)
        {
            if(snapshot==null)return null;
            for(int index=0;index<snapshot.Nodes.Count;index++)if(snapshot.Nodes[index].Id.Equals(snapshot.RootId))return snapshot.Nodes[index];
            return null;
        }

        private void OnDrawGizmos()
        {
            if(runtime==null)return;
            StrategicTerritorySnapshot snapshot=runtime.TerritorySnapshot;
            for(int index=0;index<snapshot.Edges.Count;index++)
            {
                StrategicTerritoryNode first=Find(snapshot,snapshot.Edges[index].First),second=Find(snapshot,snapshot.Edges[index].Second);
                if(first==null||second==null)continue;
                Gizmos.color=snapshot.IsConnectedToRoot(first.Id)&&snapshot.IsConnectedToRoot(second.Id)?Color.green:new Color(1f,.25f,.1f,1f);
                Gizmos.DrawLine(first.Position,second.Position);
            }
            for(int index=0;index<snapshot.Nodes.Count;index++)
            {
                StrategicTerritoryNode node=snapshot.Nodes[index];
                Gizmos.color=node.Id.Equals(snapshot.RootId)?Color.cyan:(snapshot.IsConnectedToRoot(node.Id)?Color.green:Color.red);
                Gizmos.DrawWireSphere(node.Position,.18f);
            }
            foreach(KeyValuePair<PlayerLandmine,SovereignGambitTrapPiece> pair in pieces)
            {
                if(pair.Value==null||pair.Value.PhysicalTrap==null)continue;
                GambitTrapPieceSnapshot info=pair.Value.Snapshot();
                Gizmos.color=info.CheckContributor?Color.magenta:Color.yellow;
                Gizmos.DrawWireSphere(pair.Value.transform.position,info.PhysicalRadius);
            }
        }
        private static StrategicTerritoryNode Find(StrategicTerritorySnapshot snapshot,StrategicNodeId id)
        { for(int index=0;index<snapshot.Nodes.Count;index++)if(snapshot.Nodes[index].Id.Equals(id))return snapshot.Nodes[index];return null; }
    }

    /// <summary>Stable bounded diagnostics for development hosts; it intentionally does not emit logs by itself.</summary>
    public static class GambitTrapNetworkDiagnostics
    {
        public static string BuildBoundedTrace(StrategicTerritorySnapshot snapshot,int maximumNodes)
        {
            if(snapshot==null||!snapshot.RootId.IsValid)return "Gambit network unavailable.";
            Dictionary<StrategicNodeId,List<StrategicNodeId>> adjacency=new Dictionary<StrategicNodeId,List<StrategicNodeId>>();
            for(int index=0;index<snapshot.Nodes.Count;index++)adjacency[snapshot.Nodes[index].Id]=new List<StrategicNodeId>();
            for(int index=0;index<snapshot.Edges.Count;index++)
            {
                StrategicTerritoryEdge edge=snapshot.Edges[index];List<StrategicNodeId> first,second;
                if(adjacency.TryGetValue(edge.First,out first)&&adjacency.TryGetValue(edge.Second,out second)){first.Add(edge.Second);second.Add(edge.First);}
            }
            foreach(List<StrategicNodeId> links in adjacency.Values)links.Sort((left,right)=>left.CompareTo(right));
            StringBuilder result=new StringBuilder();Queue<StrategicNodeId> pending=new Queue<StrategicNodeId>();HashSet<StrategicNodeId> visited=new HashSet<StrategicNodeId>();
            pending.Enqueue(snapshot.RootId);visited.Add(snapshot.RootId);int emitted=0;
            while(pending.Count>0&&emitted<maximumNodes)
            {
                StrategicNodeId current=pending.Dequeue();result.Append(current).Append('\n');emitted++;
                List<StrategicNodeId> links; if(!adjacency.TryGetValue(current,out links))continue;
                for(int index=0;index<links.Count;index++)if(snapshot.IsConnectedToRoot(links[index])&&visited.Add(links[index]))pending.Enqueue(links[index]);
            }
            List<StrategicNodeId> disconnected=new List<StrategicNodeId>();
            for(int index=0;index<snapshot.Nodes.Count;index++)if(!snapshot.Nodes[index].Id.Equals(snapshot.RootId)&&!snapshot.IsConnectedToRoot(snapshot.Nodes[index].Id))disconnected.Add(snapshot.Nodes[index].Id);
            disconnected.Sort((left,right)=>left.CompareTo(right));
            if(disconnected.Count>0){result.Append("Disconnected:");for(int index=0;index<disconnected.Count&&index<maximumNodes;index++)result.Append(' ').Append(disconnected[index]);}
            return result.ToString();
        }
    }
}
