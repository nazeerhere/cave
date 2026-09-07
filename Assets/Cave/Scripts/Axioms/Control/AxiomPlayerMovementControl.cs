using Cave.Axioms.Elemental;
using Cave.Player;
using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Axioms.Control
{
    /// <summary>
    /// The single player-side consumer of passive movement telemetry. It does not
    /// write Rigidbody values; PlayerController and PlayerDash retain that ownership.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovementTelemetry), typeof(AxiomRuntimeState))]
    public sealed class AxiomPlayerMovementControl : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float coherentAlignment = .45f;
        [SerializeField, Range(0f, 1f)] private float uncontrolledReversalBurden = .06f;
        [SerializeField, Range(0f, .5f)] private float maximumKnockbackResistance = .25f;

        private PlayerMovementTelemetry telemetry;
        private AxiomRuntimeState runtime;
        private AxiomDynamicsState dynamics;
        private AxiomControlState control;
        private PlayerDash dash;
        private AxiomMasteryState mastery;

        public float LastMovementControlQuality { get; private set; }
        public float CurrentMassKnockbackMultiplier { get; private set; } = 1f;
        public float CurrentMassImpactMeasure { get; private set; }

        private void Awake()
        {
            telemetry = GetComponent<PlayerMovementTelemetry>();
            runtime = GetComponent<AxiomRuntimeState>();
            if (runtime == null)
            {
                runtime = gameObject.AddComponent<AxiomRuntimeState>();
            }

            dynamics = AxiomDynamicsState.EnsureOn(gameObject);
            control = AxiomControlState.EnsureOn(gameObject);
            dash = GetComponent<PlayerDash>();
            mastery = AxiomMasteryState.EnsureOn(gameObject);
        }

        private void FixedUpdate()
        {
            if (telemetry == null)
            {
                return;
            }

            float now = Time.fixedTime;
            control.ResolveExpired(runtime, now);
            UpdateFlow(now);
            UpdateMass(now);
        }

        public float GetIncomingKnockbackMultiplier()
        {
            return CurrentMassKnockbackMultiplier;
        }

        private void UpdateFlow(float timestamp)
        {
            AxiomDynamicResponse response;
            if (!HasStacks(AxiomKind.Flow, timestamp, out response))
            {
                return;
            }

            bool coherent = telemetry.HasDirectionAlignment
                && telemetry.DirectionAlignment >= coherentAlignment
                && telemetry.HasMeaningfulDirection;
            bool dashAssistedReversal = telemetry.IsReversing && dash != null && dash.IsDashing;
            LastMovementControlQuality = dashAssistedReversal
                ? .9f
                : coherent ? Mathf.Clamp01((telemetry.DirectionAlignment + 1f) * .5f) : 0f;

            AxiomControlOpportunity opportunity;
            if ((dashAssistedReversal || coherent)
                && control.TryGetActiveOpportunity(AxiomKind.Flow, timestamp, out opportunity))
            {
                control.TryIntervene(
                    runtime,
                    dynamics,
                    AxiomKind.Flow,
                    -opportunity.Error.SignedDifference,
                    LastMovementControlQuality,
                    timestamp,
                    gameObject,
                    gameObject);
            }
            else if (telemetry.IsReversing && !dashAssistedReversal)
            {
                AxiomDynamicResponse ignored;
                dynamics.ApplyCounterBurden(
                    AxiomKind.Flow,
                    uncontrolledReversalBurden * Mathf.Clamp01(telemetry.DeltaVelocity.magnitude),
                    timestamp,
                    out ignored);
            }

            control.EvaluateAndRefresh(runtime, AxiomKind.Flow, timestamp, gameObject, gameObject);
        }

        private void UpdateMass(float timestamp)
        {
            AxiomDynamicResponse response;
            if (!HasStacks(AxiomKind.Mass, timestamp, out response))
            {
                CurrentMassKnockbackMultiplier = 1f;
                CurrentMassImpactMeasure = 0f;
                return;
            }

            float deltaV = telemetry.DeltaVelocity.magnitude;
            CurrentMassImpactMeasure = Mathf.Clamp01(deltaV / 20f) * response.ResponseStrength;
            float effectiveCounter = response.Counter * mastery.GetCounterFactor(AxiomKind.Mass);
            float resistance = maximumKnockbackResistance * Mathf.Clamp01(response.Desirable - effectiveCounter * .25f);
            // Inertia lowers retained efficiency during abrupt, uncontrolled turns.
            if (telemetry.IsReversing && (dash == null || !dash.IsDashing))
            {
                resistance *= 1f - (.5f * response.Counter);
                AxiomDynamicResponse ignored;
                dynamics.ApplyCounterBurden(AxiomKind.Mass, .04f * Mathf.Clamp01(deltaV), timestamp, out ignored);
            }

            CurrentMassKnockbackMultiplier = 1f - Mathf.Clamp(resistance, 0f, maximumKnockbackResistance);
            AxiomControlOpportunity opportunity;
            if (telemetry.HasMeaningfulDirection
                && telemetry.HasDirectionAlignment
                && telemetry.DirectionAlignment >= coherentAlignment
                && control.TryGetActiveOpportunity(AxiomKind.Mass, timestamp, out opportunity))
            {
                control.TryIntervene(
                    runtime,
                    dynamics,
                    AxiomKind.Mass,
                    -opportunity.Error.SignedDifference,
                    Mathf.Clamp01((telemetry.DirectionAlignment + 1f) * .5f),
                    timestamp,
                    gameObject,
                    gameObject);
            }

            control.EvaluateAndRefresh(runtime, AxiomKind.Mass, timestamp, gameObject, gameObject);
        }

        private bool HasStacks(AxiomKind kind, float timestamp, out AxiomDynamicResponse response)
        {
            response = default(AxiomDynamicResponse);
            AxiomTrajectoryState trajectory;
            if (!runtime.TryGetTrajectory(kind, out trajectory) || trajectory.CurrentValue <= 0f)
            {
                return false;
            }

            return dynamics.TryGetResponse(kind, timestamp, out response);
        }
    }
}
