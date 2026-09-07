using System;
using Cave.Axioms.Elemental;
using UnityEngine;

namespace Cave.Axioms.Control
{
    [Serializable]
    public struct AxiomControlReferenceProfile
    {
        public AxiomKind Kind;
        public AxiomTrajectoryReference Reference;
        public float OpportunityLifetime;
        public float MinimumErrorMagnitude;
        public float RefreshMagnitudeDelta;
        public float MinimumControlQuality;
        public float InterventionScale;

        public static AxiomControlReferenceProfile DefaultFor(AxiomKind kind)
        {
            AxiomTrajectoryReference reference = new AxiomTrajectoryReference
            {
                EvaluateState = true,
                State = 3f,
                StateTolerance = 1f,
                EvaluateRate = true,
                Rate = 0f,
                RateTolerance = .75f,
                EvaluateAcceleration = true,
                Acceleration = 0f,
                AccelerationTolerance = 3f
            };
            return new AxiomControlReferenceProfile
            {
                Kind = kind,
                Reference = reference,
                OpportunityLifetime = 1.1f,
                MinimumErrorMagnitude = .05f,
                RefreshMagnitudeDelta = .25f,
                MinimumControlQuality = .45f,
                InterventionScale = .12f
            };
        }
    }

    public struct AxiomControlOpportunity
    {
        public AxiomControlOpportunity(
            AxiomKind kind,
            AxiomErrorState error,
            AxiomControlReferenceProfile profile,
            float createdAt)
        {
            Kind = kind;
            Error = error;
            Profile = profile;
            CreatedAt = createdAt;
            ExpiresAt = createdAt + Mathf.Max(.05f, profile.OpportunityLifetime);
        }

        public AxiomKind Kind { get; }
        public AxiomErrorState Error { get; }
        public AxiomControlReferenceProfile Profile { get; }
        public float CreatedAt { get; }
        public float ExpiresAt { get; }
        public bool IsActiveAt(float timestamp) => timestamp < ExpiresAt;
    }

    /// <summary>
    /// Actor-local, event/query-driven control layer over existing scalar
    /// trajectories. It owns at most one fresh opportunity per supported Axiom.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomControlState : MonoBehaviour
    {
        [SerializeField] private AxiomControlReferenceProfile[] profileOverrides = new AxiomControlReferenceProfile[0];

        private AxiomControlOpportunity? heat;
        private AxiomControlOpportunity? order;
        private AxiomControlOpportunity? flow;
        private AxiomControlOpportunity? mass;

        public float LastInterventionQuality { get; private set; }
        public event Action<AxiomKind, AxiomErrorState, float, float> InterventionSucceeded;
        public bool TryGetActiveOpportunity(AxiomKind kind, float timestamp, out AxiomControlOpportunity opportunity)
        {
            AxiomControlOpportunity? current = GetOpportunity(kind);
            if (!current.HasValue)
            {
                opportunity = default(AxiomControlOpportunity);
                return false;
            }

            if (!current.Value.IsActiveAt(timestamp))
            {
                ClearOpportunity(kind);
                opportunity = default(AxiomControlOpportunity);
                return false;
            }

            opportunity = current.Value;
            return true;
        }

        public static AxiomControlState EnsureOn(GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            AxiomControlState state = actor.GetComponent<AxiomControlState>();
            return state != null ? state : actor.AddComponent<AxiomControlState>();
        }

        public void EvaluateAndRefresh(
            AxiomRuntimeState runtime,
            AxiomKind kind,
            float timestamp,
            GameObject source,
            GameObject target)
        {
            if (runtime == null || !IsSupported(kind))
            {
                return;
            }

            AxiomTrajectoryState trajectory;
            if (!runtime.TryGetTrajectory(kind, out trajectory))
            {
                return;
            }

            AxiomControlReferenceProfile profile = ResolveProfile(kind);
            AxiomErrorState error = AxiomErrorEvaluator.Evaluate(trajectory, profile.Reference, timestamp);
            if (!error.HasError || error.Magnitude < profile.MinimumErrorMagnitude)
            {
                return;
            }

            AxiomControlOpportunity current;
            bool hasCurrent = TryGetActiveOpportunity(kind, timestamp, out current);
            if (hasCurrent
                && current.Error.ErrorKind == error.ErrorKind
                && Abs(current.Error.Magnitude - error.Magnitude) < profile.RefreshMagnitudeDelta)
            {
                return;
            }

            bool refreshed = hasCurrent;
            SetOpportunity(kind, new AxiomControlOpportunity(kind, error, profile, timestamp));
            runtime.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                refreshed ? AxiomFeedbackType.ControlOpportunityRefreshed : AxiomFeedbackType.ControlOpportunityOpened,
                error.Magnitude,
                source,
                target,
                timestamp));
        }

        public bool TryIntervene(
            AxiomRuntimeState runtime,
            AxiomDynamicsState dynamics,
            AxiomKind kind,
            float correctionDirection,
            float quality,
            float timestamp,
            GameObject source,
            GameObject target)
        {
            LastInterventionQuality = Mathf.Clamp01(quality);
            AxiomControlOpportunity opportunity;
            if (runtime == null || dynamics == null || !TryGetActiveOpportunity(kind, timestamp, out opportunity))
            {
                return false;
            }

            bool correctDirection = correctionDirection * opportunity.Error.SignedDifference < 0f;
            if (!correctDirection || LastInterventionQuality < opportunity.Profile.MinimumControlQuality)
            {
                runtime.RaiseFeedback(new AxiomFeedbackEvent(
                    kind,
                    AxiomFeedbackType.ControlInterventionFailed,
                    LastInterventionQuality,
                    source,
                    target,
                    timestamp));
                return false;
            }

            float normalizedError = Mathf.Clamp01(opportunity.Error.Magnitude / (1f + opportunity.Error.Magnitude));
            float correction = opportunity.Profile.InterventionScale
                * (.5f + normalizedError)
                * LastInterventionQuality;
            AxiomDynamicResponse response;
            dynamics.ApplyControlCorrection(kind, correction, correction * .75f, timestamp, out response);
            ClearOpportunity(kind);
            runtime.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                AxiomFeedbackType.ControlInterventionSucceeded,
                correction,
                source,
                target,
                timestamp));
            InterventionSucceeded?.Invoke(kind, opportunity.Error, LastInterventionQuality, timestamp);
            return true;
        }

        public void ResolveExpired(AxiomRuntimeState runtime, float timestamp)
        {
            ResolveExpiry(runtime, AxiomKind.Heat, timestamp);
            ResolveExpiry(runtime, AxiomKind.Order, timestamp);
            ResolveExpiry(runtime, AxiomKind.Flow, timestamp);
            ResolveExpiry(runtime, AxiomKind.Mass, timestamp);
        }

        private void ResolveExpiry(AxiomRuntimeState runtime, AxiomKind kind, float timestamp)
        {
            AxiomControlOpportunity? opportunity = GetOpportunity(kind);
            if (!opportunity.HasValue || opportunity.Value.IsActiveAt(timestamp))
            {
                return;
            }

            ClearOpportunity(kind);
            runtime?.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                AxiomFeedbackType.ControlOpportunityExpired,
                opportunity.Value.Error.Magnitude,
                gameObject,
                gameObject,
                timestamp));
        }

        private AxiomControlReferenceProfile ResolveProfile(AxiomKind kind)
        {
            if (profileOverrides != null)
            {
                for (int index = 0; index < profileOverrides.Length; index++)
                {
                    if (profileOverrides[index].Kind == kind)
                    {
                        return profileOverrides[index];
                    }
                }
            }

            return AxiomControlReferenceProfile.DefaultFor(kind);
        }

        private AxiomControlOpportunity? GetOpportunity(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat: return heat;
                case AxiomKind.Order: return order;
                case AxiomKind.Flow: return flow;
                case AxiomKind.Mass: return mass;
                default: return null;
            }
        }

        private void SetOpportunity(AxiomKind kind, AxiomControlOpportunity value)
        {
            switch (kind)
            {
                case AxiomKind.Heat: heat = value; break;
                case AxiomKind.Order: order = value; break;
                case AxiomKind.Flow: flow = value; break;
                case AxiomKind.Mass: mass = value; break;
            }
        }

        private void ClearOpportunity(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat: heat = null; break;
                case AxiomKind.Order: order = null; break;
                case AxiomKind.Flow: flow = null; break;
                case AxiomKind.Mass: mass = null; break;
            }
        }

        private static bool IsSupported(AxiomKind kind)
        {
            return kind == AxiomKind.Heat || kind == AxiomKind.Order
                || kind == AxiomKind.Flow || kind == AxiomKind.Mass;
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
