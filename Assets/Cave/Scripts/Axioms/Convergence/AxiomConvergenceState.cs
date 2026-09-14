using System;
using Cave.Axioms.Control;
using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Axioms.Convergence
{
    public struct AxiomConvergenceEvent
    {
        public AxiomConvergenceEvent(GameObject player, GameObject target, int chargeTier, float quality, float timestamp)
        {
            Player = player;
            Target = target;
            ChargeTier = chargeTier;
            Quality = quality;
            Timestamp = timestamp;
        }

        public GameObject Player { get; }
        public GameObject Target { get; }
        public int ChargeTier { get; }
        public float Quality { get; }
        public float Timestamp { get; }
    }

    /// <summary>Pure predicate retained separately so deterministic coverage cannot drift from runtime gating.</summary>
    public static class AxiomConvergenceRules
    {
        public static bool Qualifies(bool frenzyActive, bool targetHasPhaseExposure, bool strongControl, int chargeTier)
        {
            return frenzyActive && targetHasPhaseExposure && strongControl && chargeTier >= 2;
        }
    }

    /// <summary>Bounded action-id gate: a committed action can recognise Convergence once at most.</summary>
    public sealed class AxiomConvergenceActionGate
    {
        private int lastRecognizedAction = int.MinValue;

        public bool TryConsume(int committedActionId)
        {
            if (committedActionId == lastRecognizedAction) return false;
            lastRecognizedAction = committedActionId;
            return true;
        }
    }

    /// <summary>
    /// Player-local Convergence recognition only. Its approved payoff is currently
    /// feedback/evidence; it deliberately has no damage or state multiplier.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomConvergenceState : MonoBehaviour
    {
        private readonly AxiomConvergenceActionGate actionGate = new AxiomConvergenceActionGate();
        public event Action<AxiomConvergenceEvent> Recognized;

        public static bool TryRecognize(
            GameObject player,
            GameObject target,
            int chargeTier,
            bool frenzyActive,
            bool targetHasPhaseExposure,
            int committedActionId,
            float timestamp)
        {
            if (player == null || target == null) return false;
            float quality;
            bool strongControl = ResolveStrongControl(player, timestamp, out quality);
            if (!AxiomConvergenceRules.Qualifies(frenzyActive, targetHasPhaseExposure, strongControl, chargeTier))
            {
                return false;
            }

            AxiomConvergenceState state = player.GetComponent<AxiomConvergenceState>();
            if (state == null) state = player.AddComponent<AxiomConvergenceState>();
            return state.TryRecognize(target, chargeTier, quality, committedActionId, timestamp);
        }

        private bool TryRecognize(GameObject target, int chargeTier, float quality, int committedActionId, float timestamp)
        {
            if (!actionGate.TryConsume(committedActionId)) return false;

            AxiomConvergenceEvent convergence = new AxiomConvergenceEvent(gameObject, target, chargeTier, quality, timestamp);
            Recognized?.Invoke(convergence);
            AxiomRuntimeState runtime = GetComponent<AxiomRuntimeState>();
            if (runtime == null) runtime = gameObject.AddComponent<AxiomRuntimeState>();
            runtime.RaiseFeedback(new AxiomFeedbackEvent(
                AxiomKind.Phase,
                AxiomFeedbackType.ConvergenceRecognized,
                quality,
                gameObject,
                target,
                timestamp));
            AxiomMasteryState.EnsureOn(gameObject).Record(new MasteryEvidence(
                MasteryDomain.Phase,
                MasteryEvidenceKind.Convergence,
                quality,
                Mathf.Max(1f, chargeTier),
                timestamp,
                target.GetInstanceID()));
            return true;
        }

        private static bool ResolveStrongControl(GameObject player, float timestamp, out float quality)
        {
            quality = 0f;
            AxiomPlayerMovementControl movement = player.GetComponent<AxiomPlayerMovementControl>();
            if (movement != null)
            {
                quality = Mathf.Max(quality, movement.LastMovementControlQuality);
            }

            AxiomControlState control = player.GetComponent<AxiomControlState>();
            bool temporal = control != null && control.HasStrongCurrentControl(timestamp);
            if (temporal) quality = Mathf.Max(quality, Mathf.Max(.8f, control.LastInterventionQuality));
            return quality >= .75f;
        }
    }
}
