using System;
using System.Collections.Generic;
using Cave.FieldControl;

namespace Cave.Domain
{
    /// <summary>
    /// Identity-only bridge from an authoritative physical FieldNode to an
    /// already-registered live Domain carrier. FieldNetwork owns links; the
    /// Domain registry owns semantic snapshots and writable state.
    /// </summary>
    public static class FieldNetworkDomainCarrierBinding
    {
        private static readonly Dictionary<FieldNode, PhenomenonCarrierId> byNode =
            new Dictionary<FieldNode, PhenomenonCarrierId>();
        private static readonly Dictionary<PhenomenonCarrierId, FieldNode> byCarrier =
            new Dictionary<PhenomenonCarrierId, FieldNode>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntime() { byNode.Clear(); byCarrier.Clear(); }

        public static bool Register(FieldNode node, PhenomenonCarrierId carrier)
        {
            if (node == null || !node.isActiveAndEnabled || !carrier.IsValid) return false;
            DomainRuntimeCarrierIdentity identity;
            if (!DomainRuntimeCarrierRegistry.TryGet(carrier, out identity)) return false;
            PhenomenonCarrierId existingCarrier;
            if (byNode.TryGetValue(node, out existingCarrier) && !existingCarrier.Equals(carrier)) return false;
            FieldNode existingNode;
            if (byCarrier.TryGetValue(carrier, out existingNode) && !ReferenceEquals(existingNode, node)) return false;
            byNode[node] = carrier;
            byCarrier[carrier] = node;
            return true;
        }

        public static void Unregister(FieldNode node)
        {
            if (node == null) return;
            PhenomenonCarrierId carrier;
            if (!byNode.TryGetValue(node, out carrier)) return;
            byNode.Remove(node);
            FieldNode mapped;
            if (byCarrier.TryGetValue(carrier, out mapped) && ReferenceEquals(mapped, node)) byCarrier.Remove(carrier);
        }

        public static bool TryGetCarrier(FieldNode node, out PhenomenonCarrierId carrier)
        {
            Prune();
            carrier = default(PhenomenonCarrierId);
            return node != null && byNode.TryGetValue(node, out carrier);
        }

        public static bool TryGetNode(PhenomenonCarrierId carrier, out FieldNode node)
        {
            Prune();
            node = null;
            return byCarrier.TryGetValue(carrier, out node) && node != null;
        }

        /// <summary>Builds a bounded one-hop Domain topology from actual current
        /// FieldNetwork links. Unbound physical nodes are not invented as
        /// semantic recipients.</summary>
        public static bool TryBuildPropagationContext(PhenomenonCarrierId sourceCarrier,
            LawPhenomenon phenomenon, float timestamp, out PropagationOrchestrationContext context)
        {
            context = null;
            FieldNode sourceNode;
            if (!TryGetNode(sourceCarrier, out sourceNode) || sourceNode.Network == null) return false;
            IDomainPhenomenonRuntimeTarget sourceTarget;
            PhenomenonSemanticSnapshot sourceSnapshot;
            if (!DomainRuntimeCarrierRegistry.TryResolve(sourceCarrier, phenomenon, timestamp, out sourceTarget)
                || !sourceTarget.TryRead(out sourceSnapshot) || sourceSnapshot == null) return false;

            Queue<FieldNode> frontier = new Queue<FieldNode>();
            HashSet<FieldNode> visited = new HashSet<FieldNode>();
            frontier.Enqueue(sourceNode);
            visited.Add(sourceNode);
            List<PhenomenonCarrierSnapshot> recipients = new List<PhenomenonCarrierSnapshot>();
            while (frontier.Count > 0)
            {
                FieldNode current = frontier.Dequeue();
                IReadOnlyList<FieldNode> linked = sourceNode.Network.SnapshotLinkedNodes(current);
                for (int index = 0; index < linked.Count; index++)
                {
                    if (!visited.Add(linked[index])) continue;
                    frontier.Enqueue(linked[index]);
                PhenomenonCarrierId recipientId;
                IDomainPhenomenonRuntimeTarget recipientTarget;
                PhenomenonSemanticSnapshot recipientSnapshot;
                if (!TryGetCarrier(linked[index], out recipientId)
                    || !DomainRuntimeCarrierRegistry.TryResolve(recipientId, phenomenon, timestamp, out recipientTarget)
                    || !recipientTarget.TryRead(out recipientSnapshot) || recipientSnapshot == null) continue;
                recipients.Add(new PhenomenonCarrierSnapshot(recipientId, phenomenon, recipientSnapshot));
                }
            }
            if (recipients.Count == 0) return false;
            recipients.Sort((left, right) => left.CarrierId.CompareTo(right.CarrierId));
            context = new PropagationOrchestrationContext(
                new PhenomenonCarrierTopology(new PhenomenonCarrierSnapshot(sourceCarrier, phenomenon, sourceSnapshot), recipients), recipients.Count);
            return true;
        }

        private static void Prune()
        {
            List<FieldNode> dead = new List<FieldNode>();
            foreach (KeyValuePair<FieldNode, PhenomenonCarrierId> pair in byNode)
            {
                DomainRuntimeCarrierIdentity identity;
                if (pair.Key == null || !pair.Key.isActiveAndEnabled || !DomainRuntimeCarrierRegistry.TryGet(pair.Value, out identity)) dead.Add(pair.Key);
            }
            for (int index = 0; index < dead.Count; index++) Unregister(dead[index]);
        }
    }
}
