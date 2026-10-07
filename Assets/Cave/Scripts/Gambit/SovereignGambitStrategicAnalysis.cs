using System;
using System.Collections.Generic;

namespace Cave.Gambit
{
    /// <summary>Read-only, version-keyed strategic facts.  Analysis never
    /// mutates the live graph to test a hypothetical node removal.</summary>
    public sealed class SovereignGambitStrategicAnalysis
    {
        internal SovereignGambitStrategicAnalysis(int topologyVersion, IReadOnlyList<StrategicConnectorFact> connectors,
            IReadOnlyList<StrategicCheckContributorFact> contributors)
        { TopologyVersion = topologyVersion; Connectors = connectors; CheckContributors = contributors; }
        public int TopologyVersion { get; }
        public IReadOnlyList<StrategicConnectorFact> Connectors { get; }
        public IReadOnlyList<StrategicCheckContributorFact> CheckContributors { get; }
        public bool IsCritical(StrategicNodeId id) { for (int i=0;i<Connectors.Count;i++) if (Connectors[i].NodeId.Equals(id)) return Connectors[i].IsCriticalConnector; return false; }
    }

    public readonly struct StrategicConnectorFact
    {
        public StrategicConnectorFact(StrategicNodeId id, bool connected, bool critical, int dependentCount)
        { NodeId=id; IsConnected=connected; IsCriticalConnector=critical; DependentConnectedNodeCount=dependentCount; }
        public StrategicNodeId NodeId { get; } public bool IsConnected { get; } public bool IsCriticalConnector { get; }
        public int DependentConnectedNodeCount { get; } public bool HasAlternateAnchorRoute => IsConnected && !IsCriticalConnector;
    }
    public readonly struct StrategicCheckContributorFact
    {
        public StrategicCheckContributorFact(string sourceId, StrategicNodeId nodeId, StrategicLawMetadata law, bool critical)
        { SourceId=sourceId; NodeId=nodeId; LawMetadata=law; IsCriticalConnector=critical; }
        public string SourceId { get; } public StrategicNodeId NodeId { get; } public StrategicLawMetadata LawMetadata { get; } public bool IsCriticalConnector { get; }
    }

    public static class SovereignGambitStrategicAnalyzer
    {
        public static SovereignGambitStrategicAnalysis Analyze(GambitBattlefieldSnapshot snapshot)
        {
            StrategicTerritorySnapshot topology = snapshot != null ? snapshot.Topology : null;
            List<StrategicConnectorFact> facts = new List<StrategicConnectorFact>();
            if (topology == null) return new SovereignGambitStrategicAnalysis(0, facts.AsReadOnly(), new List<StrategicCheckContributorFact>().AsReadOnly());
            HashSet<StrategicNodeId> connected = new HashSet<StrategicNodeId>(topology.ConnectedNodeIds);
            List<StrategicTerritoryNode> nodes = new List<StrategicTerritoryNode>(topology.Nodes); nodes.Sort((a,b)=>a.Id.CompareTo(b.Id));
            for (int index=0; index<nodes.Count; index++)
            {
                StrategicTerritoryNode node = nodes[index]; bool isConnected=connected.Contains(node.Id);
                int dependent=0;
                if (isConnected && !node.Id.Equals(topology.RootId))
                {
                    HashSet<StrategicNodeId> without = Reachable(topology, node.Id);
                    foreach (StrategicNodeId candidate in connected) if (!candidate.Equals(node.Id) && !without.Contains(candidate)) dependent++;
                }
                facts.Add(new StrategicConnectorFact(node.Id,isConnected,dependent>0,dependent));
            }
            List<StrategicCheckContributorFact> contributors = new List<StrategicCheckContributorFact>();
            GambitCheckResolutionResult check=SovereignGambitCheckResolver.ResolveEnemySovereign(snapshot);
            for(int sourceIndex=0;sourceIndex<check.Trace.EffectiveThreatSourceIds.Count;sourceIndex++)
            {
                string source=check.Trace.EffectiveThreatSourceIds[sourceIndex];
                for(int influenceIndex=0;influenceIndex<snapshot.Influences.Count;influenceIndex++)
                {
                    StrategicInfluence influence=snapshot.Influences[influenceIndex];
                    if(!string.Equals(source,influence.SourceId,StringComparison.Ordinal))continue;
                    bool critical=false;for(int factIndex=0;factIndex<facts.Count;factIndex++)if(facts[factIndex].NodeId.Equals(influence.SourceNodeId)){critical=facts[factIndex].IsCriticalConnector;break;}
                    contributors.Add(new StrategicCheckContributorFact(source,influence.SourceNodeId,influence.LawMetadata,critical)); break;
                }
            }
            contributors.Sort((a,b)=>string.CompareOrdinal(a.SourceId,b.SourceId));
            return new SovereignGambitStrategicAnalysis(topology.Version,facts.AsReadOnly(),contributors.AsReadOnly());
        }

        private static HashSet<StrategicNodeId> Reachable(StrategicTerritorySnapshot snapshot, StrategicNodeId omitted)
        {
            HashSet<StrategicNodeId> found=new HashSet<StrategicNodeId>();
            if(!snapshot.RootId.IsValid||snapshot.RootId.Equals(omitted))return found;
            Queue<StrategicNodeId> queue=new Queue<StrategicNodeId>();queue.Enqueue(snapshot.RootId);found.Add(snapshot.RootId);
            while(queue.Count>0){StrategicNodeId current=queue.Dequeue();for(int index=0;index<snapshot.Edges.Count;index++){StrategicTerritoryEdge edge=snapshot.Edges[index];StrategicNodeId next;if(edge.First.Equals(current))next=edge.Second;else if(edge.Second.Equals(current))next=edge.First;else continue;if(next.Equals(omitted)||!found.Add(next))continue;queue.Enqueue(next);}}
            return found;
        }
    }
}
