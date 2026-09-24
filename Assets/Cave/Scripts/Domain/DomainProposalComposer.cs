using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    public enum ProposalCompositionPolicy { None = 0, AdditiveDelta = 1, CatalysisCoalescence = 2 }
    public enum ProposalCompositionReason { None = 0, InvalidCollision = 1, MissingTransition = 2, TransitionMismatch = 3, DuplicateTransitionId = 4, InsufficientIndependentProposals = 5, BaselineMismatch = 6, UnsupportedGeneration = 7, UnsupportedOperation = 8, UnsupportedOrigin = 9, BaseResolutionRejected = 10, OpposedCatalysisDestinations = 11, CatalysisResultMismatch = 12, InvalidCatalysisDestination = 13 }
    public sealed class ProposalCompositionComponent
    { public ProposalCompositionComponent(NormalizedSemanticTransition transition, float signedDelta) { Transition = transition; SignedDelta = signedDelta; } public NormalizedSemanticTransition Transition { get; } public float SignedDelta { get; } }
    public sealed class DomainProposalCompositionResult
    {
        internal DomainProposalCompositionResult(ProposalCollision collision, bool resolved, ProposalCompositionPolicy policy, ProposalCompositionReason reason, PhenomenonSemanticSnapshot before, PhenomenonSemanticSnapshot after, IReadOnlyList<ProposalCompositionComponent> components, float netDelta, bool hasEffectiveOperation, PhenomenonOperationKind operation, float magnitude, PhenomenonSemanticRegion? destination, bool noOp)
        { Collision = collision; Resolved = resolved; Policy = policy; Reason = reason; CommonBefore = before; ResultingAfter = after; Components = components; NetSignedDelta = netDelta; HasEffectiveOperation = hasEffectiveOperation; EffectiveOperation = operation; EffectiveMagnitude = magnitude; DestinationRegion = destination; IsNoOp = noOp; }
        public ProposalCollision Collision { get; } public bool Resolved { get; } public bool Unresolved => !Resolved; public ProposalCompositionPolicy Policy { get; } public ProposalCompositionReason Reason { get; }
        public PhenomenonCarrierId CarrierId => Collision != null ? Collision.CarrierId : default(PhenomenonCarrierId); public LawPhenomenon Phenomenon => Collision != null ? Collision.Phenomenon : default(LawPhenomenon); public DomainOrchestrationGeneration Generation => Collision != null ? Collision.Generation : default(DomainOrchestrationGeneration);
        public PhenomenonSemanticSnapshot CommonBefore { get; } public PhenomenonSemanticSnapshot ResultingAfter { get; } public IReadOnlyList<ProposalCompositionComponent> Components { get; } public float NetSignedDelta { get; } public bool HasEffectiveOperation { get; } public PhenomenonOperationKind EffectiveOperation { get; } public float EffectiveMagnitude { get; } public PhenomenonSemanticRegion? DestinationRegion { get; } public bool IsNoOp { get; }
    }
    public static class DomainProposalComposer
    {
        public static DomainProposalCompositionResult TryCompose(ProposalCollision collision, IReadOnlyList<NormalizedSemanticTransition> transitions)
        {
            List<ProposalCompositionComponent> components = new List<ProposalCompositionComponent>(); PhenomenonSemanticSnapshot before;
            ProposalCompositionReason validation = Validate(collision, transitions, components, out before);
            if (validation != ProposalCompositionReason.None) return Rejected(collision, validation, before, components);
            if (collision.Generation == DomainOrchestrationGeneration.Generation1) return ComposeGenerationOne(collision, before, components);
            if (collision.Generation == DomainOrchestrationGeneration.Generation2) return ComposeGenerationTwo(collision, before, components);
            return Rejected(collision, ProposalCompositionReason.UnsupportedGeneration, before, components);
        }
        private static ProposalCompositionReason Validate(ProposalCollision collision, IReadOnlyList<NormalizedSemanticTransition> all, List<ProposalCompositionComponent> components, out PhenomenonSemanticSnapshot before)
        {
            before = null; if (collision == null || !collision.IsUnresolved || all == null || collision.InvolvedTransitionIds == null || collision.InvolvedTransitionIds.Count < 2) return ProposalCompositionReason.InvalidCollision;
            List<DomainTransitionSequenceId> seenIds = new List<DomainTransitionSequenceId>(); List<Branch> branches = new List<Branch>();
            for (int index = 0; index < collision.InvolvedTransitionIds.Count; index++)
            {
                DomainTransitionSequenceId id = collision.InvolvedTransitionIds[index]; if (seenIds.Contains(id)) return ProposalCompositionReason.DuplicateTransitionId; seenIds.Add(id);
                NormalizedSemanticTransition transition = Find(all, id); if (transition == null) return ProposalCompositionReason.MissingTransition;
                if (transition.CarrierId.Equals(collision.CarrierId) == false || transition.Phenomenon != collision.Phenomenon || transition.Generation != collision.Generation) return ProposalCompositionReason.TransitionMismatch;
                if (before == null) before = transition.Before; else if (!Same(before, transition.Before)) return ProposalCompositionReason.BaselineMismatch;
                components.Add(new ProposalCompositionComponent(transition, Signed(transition)));
                Branch branch = new Branch(transition.OriginatingLaw, transition.ParentTransitionId); if (!branches.Contains(branch)) branches.Add(branch);
            }
            return branches.Count < 2 ? ProposalCompositionReason.InsufficientIndependentProposals : ProposalCompositionReason.None;
        }
        private static DomainProposalCompositionResult ComposeGenerationOne(ProposalCollision collision, PhenomenonSemanticSnapshot before, List<ProposalCompositionComponent> components)
        {
            float net = 0f; for (int index = 0; index < components.Count; index++) { NormalizedSemanticTransition t = components[index].Transition; if ((t.Origin != DomainTransitionOrigin.Propagation && t.Origin != DomainTransitionOrigin.Synchronization) || (t.Operation != PhenomenonOperationKind.Add && t.Operation != PhenomenonOperationKind.Remove)) return Rejected(collision, t.Operation == PhenomenonOperationKind.Transfer ? ProposalCompositionReason.UnsupportedOperation : ProposalCompositionReason.UnsupportedOrigin, before, components); net += components[index].SignedDelta; }
            PhenomenonOperationKind operation = net < 0f ? PhenomenonOperationKind.Remove : PhenomenonOperationKind.Add; float magnitude = net < 0f ? -net : net;
            PhenomenonResolutionResult resolved = PhenomenonOperationResolver.Resolve(new PhenomenonOperationRequest(operation, collision.Phenomenon, magnitude, before));
            if (!resolved.Succeeded) return new DomainProposalCompositionResult(collision, false, ProposalCompositionPolicy.AdditiveDelta, ProposalCompositionReason.BaseResolutionRejected, before, null, components.AsReadOnly(), net, true, operation, magnitude, null, magnitude == 0f);
            return new DomainProposalCompositionResult(collision, true, ProposalCompositionPolicy.AdditiveDelta, ProposalCompositionReason.None, before, resolved.TargetAfter, components.AsReadOnly(), net, true, operation, magnitude, null, magnitude == 0f);
        }
        private static DomainProposalCompositionResult ComposeGenerationTwo(ProposalCollision collision, PhenomenonSemanticSnapshot before, List<ProposalCompositionComponent> components)
        {
            PhenomenonSemanticSnapshot canonical = null; PhenomenonSemanticRegion? destination = null;
            for (int index = 0; index < components.Count; index++)
            { NormalizedSemanticTransition t = components[index].Transition; if (t.Origin != DomainTransitionOrigin.Catalysis || t.Operation == PhenomenonOperationKind.Transfer) return Rejected(collision, ProposalCompositionReason.UnsupportedOrigin, before, components); if (t.After == null || (t.After.Region != PhenomenonSemanticRegion.High && t.After.Region != PhenomenonSemanticRegion.Low)) return Rejected(collision, ProposalCompositionReason.InvalidCatalysisDestination, before, components); if (!destination.HasValue) destination = t.After.Region; else if (destination.Value != t.After.Region) return new DomainProposalCompositionResult(collision, false, ProposalCompositionPolicy.CatalysisCoalescence, ProposalCompositionReason.OpposedCatalysisDestinations, before, null, components.AsReadOnly(), 0f, false, default(PhenomenonOperationKind), 0f, null, false); if (canonical == null) canonical = t.After; else if (!Same(canonical, t.After)) return new DomainProposalCompositionResult(collision, false, ProposalCompositionPolicy.CatalysisCoalescence, ProposalCompositionReason.CatalysisResultMismatch, before, null, components.AsReadOnly(), 0f, false, default(PhenomenonOperationKind), 0f, destination, false); }
            return new DomainProposalCompositionResult(collision, true, ProposalCompositionPolicy.CatalysisCoalescence, ProposalCompositionReason.None, before, canonical, components.AsReadOnly(), 0f, false, default(PhenomenonOperationKind), 0f, destination, false);
        }
        private static DomainProposalCompositionResult Rejected(ProposalCollision collision, ProposalCompositionReason reason, PhenomenonSemanticSnapshot before, List<ProposalCompositionComponent> components) { return new DomainProposalCompositionResult(collision, false, ProposalCompositionPolicy.None, reason, before, null, components.AsReadOnly(), 0f, false, default(PhenomenonOperationKind), 0f, null, false); }
        private static float Signed(NormalizedSemanticTransition transition) { return transition.Operation == PhenomenonOperationKind.Add ? transition.Magnitude : transition.Operation == PhenomenonOperationKind.Remove ? -transition.Magnitude : 0f; }
        private static NormalizedSemanticTransition Find(IReadOnlyList<NormalizedSemanticTransition> values, DomainTransitionSequenceId id) { for (int i = 0; i < values.Count; i++) if (values[i].SequenceId.Equals(id)) return values[i]; return null; }
        private static bool Same(PhenomenonSemanticSnapshot left, PhenomenonSemanticSnapshot right) { return left != null && right != null && left.Phenomenon == right.Phenomenon && left.SemanticValue == right.SemanticValue && left.Region == right.Region; }
        private sealed class Branch { public Branch(DomainLaw law, DomainTransitionSequenceId? parent) { Law = law; Parent = parent; } public DomainLaw Law; public DomainTransitionSequenceId? Parent; public override bool Equals(object obj) { Branch other = obj as Branch; return other != null && Equals(Law, other.Law) && Nullable.Equals(Parent, other.Parent); } public override int GetHashCode() { return (Law != null ? Law.GetHashCode() : 0) ^ (Parent.HasValue ? Parent.Value.GetHashCode() : 0); } }
    }
}
