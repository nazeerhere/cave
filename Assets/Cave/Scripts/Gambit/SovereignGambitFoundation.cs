using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Gambit
{
    public enum GambitMatchPhase { Setup = 0, Active = 1, Victory = 2, Defeat = 3, Ended = 4 }
    public enum GambitSide { None = 0, Player = 1, Enemy = 2 }
    public enum SovereignAnchorLifecycle { Unplaced = 0, PlacementRequested = 1, Placed = 2, Active = 3, Invalidated = 4, MatchEnded = 5 }
    public enum SovereignAnchorPlacementRejection { None = 0, NoGround = 1, Blocked = 2, InsufficientFootprint = 3, InvalidSurface = 4, RuntimeUnavailable = 5, AlreadyPlaced = 6, InvalidRequest = 7 }
    public enum GambitStrategicEventKind { SovereignThreatened, SovereignCheckStarted, SovereignCheckBroken, PlayerAnchorThreatened, PlayerAnchorCheckStarted, PlayerAnchorCheckBroken, StrategicNodeLost, StrategicConnectionBroken, Checkmate, InvalidSovereignDefeat }

    public readonly struct StrategicNodeId : IEquatable<StrategicNodeId>, IComparable<StrategicNodeId>
    {
        public StrategicNodeId(string value) { Value = value; }
        public string Value { get; } public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(StrategicNodeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object value) => value is StrategicNodeId && Equals((StrategicNodeId)value);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(StrategicNodeId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct StrategicTargetId : IEquatable<StrategicTargetId>, IComparable<StrategicTargetId>
    {
        public StrategicTargetId(string value) { Value = value; }
        public string Value { get; } public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(StrategicTargetId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object value) => value is StrategicTargetId && Equals((StrategicTargetId)value);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(StrategicTargetId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct SovereignAnchorPlacementRequest
    {
        public SovereignAnchorPlacementRequest(string matchId, StrategicNodeId rootId, Vector2 position, Vector2 footprint)
        { MatchId=matchId; RootId=rootId; Position=position; Footprint=new Vector2(Mathf.Max(.01f,footprint.x),Mathf.Max(.01f,footprint.y)); }
        public string MatchId { get; } public StrategicNodeId RootId { get; } public Vector2 Position { get; } public Vector2 Footprint { get; }
    }
    public readonly struct SovereignAnchorPlacementResult
    {
        public SovereignAnchorPlacementResult(bool accepted,SovereignAnchorPlacementRejection rejection) { Accepted=accepted; Rejection=rejection; }
        public bool Accepted { get; } public SovereignAnchorPlacementRejection Rejection { get; }
    }
    public interface ISovereignAnchorPlacementValidator { SovereignAnchorPlacementRejection Validate(SovereignAnchorPlacementRequest request); }

    public sealed class StrategicTerritoryNode
    {
        public StrategicTerritoryNode(StrategicNodeId id,GambitSide owner,Vector2 position,bool active=true) { Id=id;Owner=owner;Position=position;Active=active; }
        public StrategicNodeId Id { get; } public GambitSide Owner { get; } public Vector2 Position { get; } public bool Active { get; }
        public StrategicTerritoryNode WithActive(bool active) => new StrategicTerritoryNode(Id,Owner,Position,active);
    }
    public readonly struct StrategicTerritoryEdge : IEquatable<StrategicTerritoryEdge>
    {
        public StrategicTerritoryEdge(StrategicNodeId first,StrategicNodeId second)
        { if(first.CompareTo(second)<=0){First=first;Second=second;}else{First=second;Second=first;} }
        public StrategicNodeId First { get; } public StrategicNodeId Second { get; }
        public bool Equals(StrategicTerritoryEdge other) => First.Equals(other.First)&&Second.Equals(other.Second);
        public override bool Equals(object value) => value is StrategicTerritoryEdge&&Equals((StrategicTerritoryEdge)value);
        public override int GetHashCode() => First.GetHashCode()*397^Second.GetHashCode();
    }
    /// <summary>Future carriers provide link legality; the graph never invents a radius or chess-board rule.</summary>
    public interface IStrategicConnectionPolicy { bool CanConnect(StrategicTerritoryNode first, StrategicTerritoryNode second); }

    /// <summary>Pure, explicit graph. Edges are supplied by a future connection policy; no radius or scene scan is implicit.</summary>
    public sealed class StrategicTerritoryGraph
    {
        private readonly Dictionary<StrategicNodeId,StrategicTerritoryNode> nodes=new Dictionary<StrategicNodeId,StrategicTerritoryNode>();
        private readonly HashSet<StrategicTerritoryEdge> edges=new HashSet<StrategicTerritoryEdge>();
        private StrategicNodeId root;
        public int Version { get; private set; }
        public StrategicNodeId RootId => root;
        public bool HasRoot => root.IsValid&&nodes.ContainsKey(root)&&nodes[root].Active;
        public bool TrySetRoot(StrategicTerritoryNode node)
        { if(node==null||!node.Id.IsValid||node.Owner!=GambitSide.Player||HasRoot)return false; nodes[node.Id]=node;root=node.Id;Version++;return true; }
        public bool AddOrUpdate(StrategicTerritoryNode node) { if(node==null||!node.Id.IsValid)return false; StrategicTerritoryNode old; if(nodes.TryGetValue(node.Id,out old)&&old.Owner==node.Owner&&old.Position==node.Position&&old.Active==node.Active)return false;nodes[node.Id]=node;Version++;return true; }
        public bool SetActive(StrategicNodeId id,bool active) { StrategicTerritoryNode node;if(!nodes.TryGetValue(id,out node)||node.Active==active)return false;nodes[id]=node.WithActive(active);Version++;return true; }
        public bool Remove(StrategicNodeId id) { if(!nodes.Remove(id))return false;List<StrategicTerritoryEdge> remove=new List<StrategicTerritoryEdge>();foreach(StrategicTerritoryEdge edge in edges)if(edge.First.Equals(id)||edge.Second.Equals(id))remove.Add(edge);for(int i=0;i<remove.Count;i++)edges.Remove(remove[i]);if(root.Equals(id))root=default;Version++;return true; }
        public bool Connect(StrategicNodeId first,StrategicNodeId second,IStrategicConnectionPolicy policy=null) { StrategicTerritoryNode firstNode,secondNode;if(!first.IsValid||!second.IsValid||first.Equals(second)||!nodes.TryGetValue(first,out firstNode)||!nodes.TryGetValue(second,out secondNode)||(policy!=null&&!policy.CanConnect(firstNode,secondNode)))return false;bool added=edges.Add(new StrategicTerritoryEdge(first,second));if(added)Version++;return added; }
        public bool Disconnect(StrategicNodeId first,StrategicNodeId second) { bool removed=edges.Remove(new StrategicTerritoryEdge(first,second));if(removed)Version++;return removed; }
        public StrategicTerritorySnapshot Snapshot()
        {
            List<StrategicTerritoryNode> orderedNodes=new List<StrategicTerritoryNode>(nodes.Values);orderedNodes.Sort((a,b)=>a.Id.CompareTo(b.Id));
            List<StrategicTerritoryEdge> orderedEdges=new List<StrategicTerritoryEdge>(edges);orderedEdges.Sort((a,b)=>a.First.Equals(b.First)?a.Second.CompareTo(b.Second):a.First.CompareTo(b.First));
            HashSet<StrategicNodeId> connected=ResolveConnected();
            return new StrategicTerritorySnapshot(root,Version,orderedNodes.AsReadOnly(),orderedEdges.AsReadOnly(),new List<StrategicNodeId>(connected).AsReadOnly());
        }
        public void Clear() { nodes.Clear();edges.Clear();root=default;Version++; }
        private HashSet<StrategicNodeId> ResolveConnected()
        { HashSet<StrategicNodeId> found=new HashSet<StrategicNodeId>();if(!HasRoot)return found;Queue<StrategicNodeId> pending=new Queue<StrategicNodeId>();pending.Enqueue(root);found.Add(root);while(pending.Count>0){StrategicNodeId current=pending.Dequeue();foreach(StrategicTerritoryEdge edge in edges){StrategicNodeId next;if(edge.First.Equals(current))next=edge.Second;else if(edge.Second.Equals(current))next=edge.First;else continue;StrategicTerritoryNode node;if(nodes.TryGetValue(next,out node)&&node.Active&&found.Add(next))pending.Enqueue(next);}}return found; }
    }
    public sealed class StrategicTerritorySnapshot
    {
        public StrategicTerritorySnapshot(StrategicNodeId root,int version,IReadOnlyList<StrategicTerritoryNode> nodes,IReadOnlyList<StrategicTerritoryEdge> edges,IReadOnlyList<StrategicNodeId> connected) { RootId=root;Version=version;Nodes=nodes;Edges=edges;ConnectedNodeIds=connected; }
        public StrategicNodeId RootId { get; } public int Version { get; } public IReadOnlyList<StrategicTerritoryNode> Nodes { get; } public IReadOnlyList<StrategicTerritoryEdge> Edges { get; } public IReadOnlyList<StrategicNodeId> ConnectedNodeIds { get; }
        public bool IsConnectedToRoot(StrategicNodeId id) { for(int i=0;i<ConnectedNodeIds.Count;i++)if(ConnectedNodeIds[i].Equals(id))return true;return false; }
    }

    /// <summary>Presentation-neutral source classification. It does not prescribe a chess role or a Law effect.</summary>
    public enum StrategicInfluenceSourceCategory { Unspecified = 0, PhysicalControl = 1, LocalizedDomain = 2 }

    /// <summary>A small, explicit strategic footprint. Consumers decide whether a footprint is relevant; this type has no physics queries.</summary>
    public readonly struct StrategicInfluenceRegion
    {
        public StrategicInfluenceRegion(Vector2 center,float radius) { Center=center;Radius=Mathf.Max(0f,radius); }
        public Vector2 Center { get; } public float Radius { get; } public bool IsValid => Radius>0f;
        public bool Intersects(StrategicInfluenceRegion other)
        {
            if(!IsValid||!other.IsValid)return false;
            float distance=(Center-other.Center).sqrMagnitude;float combined=Radius+other.Radius;
            return distance<=combined*combined;
        }
    }

    /// <summary>Immutable descriptive metadata for an influence produced by an already-authoritative Domain runtime.</summary>
    public readonly struct StrategicLawMetadata
    {
        public StrategicLawMetadata(string lawId,Cave.Domain.LawExpression expression,Cave.Domain.LawPhenomenon phenomenon,Cave.Domain.LawTerritoryPrinciple territory)
        { LawId=lawId;Expression=expression;Phenomenon=phenomenon;TerritoryPrinciple=territory; }
        public string LawId { get; } public Cave.Domain.LawExpression Expression { get; } public Cave.Domain.LawPhenomenon Phenomenon { get; } public Cave.Domain.LawTerritoryPrinciple TerritoryPrinciple { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(LawId);
    }

    public readonly struct StrategicInfluence
    {
        public StrategicInfluence(string sourceId,StrategicNodeId sourceNodeId,GambitSide owner,StrategicTargetId target,bool active,bool authorityConnected,string tag=null)
            : this(sourceId,sourceNodeId,owner,target,active,authorityConnected,tag,default,StrategicInfluenceSourceCategory.Unspecified,default) { }
        public StrategicInfluence(string sourceId,StrategicNodeId sourceNodeId,GambitSide owner,StrategicTargetId target,bool active,bool authorityConnected,string tag,StrategicInfluenceRegion region,StrategicInfluenceSourceCategory category,StrategicLawMetadata lawMetadata)
        { SourceId=sourceId;SourceNodeId=sourceNodeId;Owner=owner;Target=target;Active=active;AuthorityConnected=authorityConnected;Tag=tag;Region=region;Category=category;LawMetadata=lawMetadata; }
        public string SourceId { get; } public StrategicNodeId SourceNodeId { get; } public GambitSide Owner { get; } public StrategicTargetId Target { get; } public bool Active { get; } public bool AuthorityConnected { get; } public string Tag { get; }
        public StrategicInfluenceRegion Region { get; } public StrategicInfluenceSourceCategory Category { get; } public StrategicLawMetadata LawMetadata { get; }
    }
    public readonly struct StrategicDefenseCoverage
    {
        public StrategicDefenseCoverage(string blockerId,GambitSide owner,StrategicTargetId target,bool active) { BlockerId=blockerId;Owner=owner;Target=target;Active=active; }
        public string BlockerId { get; } public GambitSide Owner { get; } public StrategicTargetId Target { get; } public bool Active { get; }
    }
    public sealed class GambitCheckTrace
    {
        internal GambitCheckTrace(bool anchorValid,int connectedThreats,int targetedThreats,int blockers,IReadOnlyList<string> effective) { AnchorValid=anchorValid;ConnectedThreatSources=connectedThreats;TargetedThreatSources=targetedThreats;DefenseBlockers=blockers;EffectiveThreatSourceIds=effective; }
        public bool AnchorValid { get; } public int ConnectedThreatSources { get; } public int TargetedThreatSources { get; } public int DefenseBlockers { get; } public IReadOnlyList<string> EffectiveThreatSourceIds { get; }
    }
    public sealed class GambitCheckResolutionResult
    {
        internal GambitCheckResolutionResult(bool check,GambitCheckTrace trace) { IsCheck=check;Trace=trace; }
        public bool IsCheck { get; } public GambitCheckTrace Trace { get; }
    }
    public sealed class GambitBattlefieldSnapshot
    {
        public GambitBattlefieldSnapshot(bool playerAnchorValid,StrategicTargetId playerAnchor,StrategicTargetId enemySovereign,StrategicTerritorySnapshot topology,IReadOnlyList<StrategicInfluence> influences,IReadOnlyList<StrategicDefenseCoverage> blockers)
        { PlayerAnchorValid=playerAnchorValid;PlayerAnchor=playerAnchor;EnemySovereign=enemySovereign;Topology=topology;Influences=influences??Array.Empty<StrategicInfluence>();Blockers=blockers??Array.Empty<StrategicDefenseCoverage>(); }
        public bool PlayerAnchorValid { get; } public StrategicTargetId PlayerAnchor { get; } public StrategicTargetId EnemySovereign { get; } public StrategicTerritorySnapshot Topology { get; } public IReadOnlyList<StrategicInfluence> Influences { get; } public IReadOnlyList<StrategicDefenseCoverage> Blockers { get; }
    }
    /// <summary>One pure authority for both simultaneous threat queries. It has no Laws, damage, CTC, or scene access.</summary>
    public static class SovereignGambitCheckResolver
    {
        public static GambitCheckResolutionResult ResolveEnemySovereign(GambitBattlefieldSnapshot snapshot) => Resolve(snapshot,GambitSide.Player,snapshot!=null?snapshot.EnemySovereign:default,true);
        public static GambitCheckResolutionResult ResolvePlayerAnchor(GambitBattlefieldSnapshot snapshot) => Resolve(snapshot,GambitSide.Enemy,snapshot!=null?snapshot.PlayerAnchor:default,snapshot!=null&&snapshot.PlayerAnchorValid);
        private static GambitCheckResolutionResult Resolve(GambitBattlefieldSnapshot snapshot,GambitSide attacker,StrategicTargetId target,bool targetValid)
        {
            List<string> effective=new List<string>();int connected=0,targeted=0,blockers=0;if(snapshot==null||!targetValid||!target.IsValid)return new GambitCheckResolutionResult(false,new GambitCheckTrace(false,0,0,0,effective.AsReadOnly()));
            for(int i=0;i<snapshot.Blockers.Count;i++)if(snapshot.Blockers[i].Active&&snapshot.Blockers[i].Target.Equals(target))blockers++;
            for(int i=0;i<snapshot.Influences.Count;i++){StrategicInfluence influence=snapshot.Influences[i];if(!influence.Active||influence.Owner!=attacker)continue;bool linked=influence.AuthorityConnected;if(attacker==GambitSide.Player&&snapshot.Topology!=null)linked=linked&&snapshot.Topology.IsConnectedToRoot(influence.SourceNodeId);if(!linked)continue;connected++;if(!influence.Target.Equals(target))continue;targeted++;if(blockers==0)effective.Add(influence.SourceId??string.Empty);}
            return new GambitCheckResolutionResult(effective.Count>0,new GambitCheckTrace(true,connected,targeted,blockers,effective.AsReadOnly()));
        }
    }
}
