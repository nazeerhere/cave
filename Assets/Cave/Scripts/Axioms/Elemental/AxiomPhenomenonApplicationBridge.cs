using Cave.Axioms.Control;
using UnityEngine;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Typed source boundary for non-elemental phenomenon interactions. It owns no
    /// mastery policy: source actions change runtime state only; control remains
    /// responsible for any later successful-intervention evidence.
    /// </summary>
    public static class AxiomPhenomenonApplicationBridge
    {
        public static bool Apply(GameObject actor, AxiomKind kind, float amount, GameObject source, GameObject target, float timestamp)
        {
            if (actor == null || amount <= 0f) return false;
            AxiomRuntimeState runtime = actor.GetComponent<AxiomRuntimeState>() ?? actor.AddComponent<AxiomRuntimeState>();
            AxiomDynamicsState dynamics = AxiomDynamicsState.EnsureOn(actor);
            AxiomControlState control = AxiomControlState.EnsureOn(actor);
            runtime.ApplyDelta(kind, amount, source, target, timestamp);
            return RefreshAfterRuntimeMutation(runtime, dynamics, control, kind, amount, source, target, timestamp);
        }

        public static bool RefreshAfterRuntimeMutation(AxiomRuntimeState runtime, AxiomDynamicsState dynamics,
            AxiomControlState control, AxiomKind kind, float inputAmount, GameObject source, GameObject target, float timestamp)
        {
            if (runtime == null || dynamics == null || control == null || inputAmount < 0f) return false;
            AxiomTrajectoryState actual;
            if (!runtime.TryGetTrajectory(kind, timestamp, out actual)) return false;
            dynamics.ApplySuccessfulInput(kind, actual.CurrentValue, inputAmount, timestamp);
            control.RecordSuccessfulApplication(dynamics, kind, inputAmount, timestamp);
            control.EvaluateAndRefresh(runtime, dynamics, kind, AxiomOpportunityCandidateSource.PlayerDisturbance, timestamp, source, target);
            return true;
        }

        /// <summary>Explicit future Guard-pattern seam. Callers must establish the relationship first.</summary>
        public static bool ApplyEstablishedGuardResonance(GameObject actor, bool relationshipEstablished, GameObject target, float timestamp)
        {
            return relationshipEstablished && Apply(actor, AxiomKind.Resonance, 1f, actor, target, timestamp);
        }
    }
}
