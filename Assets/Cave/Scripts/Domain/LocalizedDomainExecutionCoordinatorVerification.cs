using Cave.Axioms;
using Cave.FieldControl;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Focused editor-safe coverage of the real queue/flush path:
    /// sibling requests share one generation and reach arbitration together.</summary>
    public static class LocalizedDomainExecutionCoordinatorVerification
    {
        public static bool TryRunAll(out string failure)
        {
            GameObject owner = new GameObject("LocalizedCoordinatorOwner");
            GameObject target = new GameObject("LocalizedCoordinatorTarget");
            GameObject peer = new GameObject("LocalizedCoordinatorPeer");
            GameObject networkObject = new GameObject("LocalizedCoordinatorFieldNetwork");
            try
            {
                AxiomRuntimeState runtime = target.AddComponent<AxiomRuntimeState>();
                peer.AddComponent<AxiomRuntimeState>();
                peer.transform.position = new Vector3(20f, 0f, 0f);
                DomainRuntimeCarrierIdentity identity = DomainRuntimeCarrierIdentity.EnsureOn(target);
                DomainRuntimeCarrierIdentity peerIdentity = DomainRuntimeCarrierIdentity.EnsureOn(peer);
                if (identity != null) DomainRuntimeCarrierRegistry.Register(identity);
                if (peerIdentity != null) DomainRuntimeCarrierRegistry.Register(peerIdentity);
                DomainLaw law; LawValidationResult validation;
                if (runtime == null || identity == null || !DomainLaw.TryCreate(LawExpression.Trap, LawPhenomenon.Heat,
                    LawTerritoryPrinciple.Reversal, out law, out validation))
                { failure = "Could not establish a live target and immutable Trap law."; return false; }
                LocalizedDomainExecutionCoordinator coordinator = LocalizedDomainExecutionCoordinator.EnsureOn(PlayerDomainReserve.EnsureOn(owner));
                LocalizedDomainCarrier first = new LocalizedDomainCarrier("child-a", "parent", "owner-a", new BoundTrapLaw("law", law), Vector2.zero, 3f, 10f);
                LocalizedDomainCarrier second = new LocalizedDomainCarrier("child-b", "parent", "owner-b", new BoundTrapLaw("law", law), Vector2.zero, 3f, 10f);
                bool queued = coordinator.Enqueue(first) && coordinator.Enqueue(second) && coordinator.PendingRequestCount == 2;
                LocalizedDomainExecutionBatchResult batch = coordinator.Flush(1f);
                bool shared = batch != null && batch.RequestCount == 2 && coordinator.PendingRequestCount == 0
                    && first.ExecutionGenerationId == batch.GenerationId && second.ExecutionGenerationId == batch.GenerationId;
                bool together = batch.Arbitration != null && batch.Arbitration.Participants.Count == 2
                    && batch.Arbitration.AuthoritativeWrites.Count == 1 && batch.Commit != null && batch.Commit.Succeeded;
                PhenomenonRelationshipGroup group = new PhenomenonRelationshipGroup(new PhenomenonRelationshipGroupId("coordinator-group"),
                    LawPhenomenon.Heat, new[] { identity.CarrierId, peerIdentity.CarrierId });
                PhenomenonRelationshipGroup resolvedGroup; System.Collections.Generic.IReadOnlyList<PhenomenonCarrierSnapshot> members;
                bool synchronization = SynchronizationRuntimeRegistry.Register(group)
                    && SynchronizationRuntimeRegistry.TryGetLiveMembers(identity.CarrierId, LawPhenomenon.Heat, 1f, out resolvedGroup, out members)
                    && resolvedGroup == group && members.Count == 2;

                peer.transform.position = Vector3.right;
                DomainLaw interferenceLaw;
                bool interferenceLawValid = DomainLaw.TryCreate(LawExpression.Trap, LawPhenomenon.Heat,
                    LawTerritoryPrinciple.Interference, out interferenceLaw, out validation);
                LocalizedDomainCarrier interferenceCarrier = new LocalizedDomainCarrier("child-interference", "parent", "owner-c",
                    new BoundTrapLaw("interference", interferenceLaw), Vector2.zero, 3f, 10f,
                    new LocalizedDomainActivationContext(identity.CarrierId, new[] { identity.CarrierId, peerIdentity.CarrierId }));
                bool interferenceQueued = interferenceLawValid && coordinator.Enqueue(interferenceCarrier);
                LocalizedDomainExecutionBatchResult interferenceBatch = coordinator.Flush(2f);
                bool interference = interferenceQueued && interferenceBatch != null
                    && interferenceBatch.Proposals.Count == 1 && interferenceBatch.Commit != null && interferenceBatch.Commit.Succeeded;

                FieldNetwork network = networkObject.AddComponent<FieldNetwork>();
                network.Configure(FieldOwnerTeam.Player, ~0, 3, 10f, 1f, 1f, 1f);
                target.transform.position = Vector3.zero;
                FieldNode firstNode = target.AddComponent<FieldNode>();
                FieldNode secondNode = peer.AddComponent<FieldNode>();
                firstNode.Configure(network, FieldOwnerTeam.Player, 1, 1f, 0f, 1);
                secondNode.Configure(network, FieldOwnerTeam.Player, 1, 1f, 0f, 2);
                PropagationOrchestrationContext propagation;
                bool fieldBridge = FieldNetworkDomainCarrierBinding.Register(firstNode, identity.CarrierId)
                    && FieldNetworkDomainCarrierBinding.Register(secondNode, peerIdentity.CarrierId)
                    && FieldNetworkDomainCarrierBinding.TryBuildPropagationContext(identity.CarrierId, LawPhenomenon.Heat, 1f, out propagation)
                    && propagation.Topology.OrderedRecipients.Count == 1
                    && propagation.Topology.OrderedRecipients[0].CarrierId.Equals(peerIdentity.CarrierId);
                failure = queued && shared && together && synchronization && interference && fieldBridge ? null
                    : "Localized coordinator/relationship/topology bridge verification failed (queued=" + queued
                    + ", shared=" + shared + ", together=" + together + ", synchronization=" + synchronization + ", interference=" + interference
                    + ", fieldBridge=" + fieldBridge + ").";
                return failure == null;
            }
            finally
            {
                SynchronizationRuntimeRegistry.Remove(new PhenomenonRelationshipGroupId("coordinator-group"));
                Object.DestroyImmediate(owner); Object.DestroyImmediate(target); Object.DestroyImmediate(peer); Object.DestroyImmediate(networkObject);
            }
        }
    }
}
