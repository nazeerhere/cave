using System;
using Cave.Axioms;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Production S/R/A trajectory for one actor and phenomenon. The legacy
    /// response channel remains available only for historical verifier coverage;
    /// gameplay reads this channel through AxiomDynamicsState/AxiomRuntimeState.
    /// </summary>
    public sealed class ThreeStateAxiomDynamicsChannel
    {
        private readonly AxiomKind kind;
        private readonly AxiomDynamicsParameters parameters;
        private float state;
        private float rate;
        private float acceleration;
        private float inputPressure;
        private float inputEndsAt;
        private float lastTime;
        private bool hasTime;

        public ThreeStateAxiomDynamicsChannel(AxiomKind kind, AxiomDynamicsParameters parameters)
        {
            this.kind = kind;
            this.parameters = parameters.Sanitized();
            state = ResolvedDesiredState;
            rate = ResolvedDesiredRate;
            acceleration = ResolvedDesiredAcceleration;
        }

        public bool IsActive => Abs(state - ResolvedDesiredState) > parameters.ActiveEpsilon
            || Abs(rate - ResolvedDesiredRate) > parameters.ActiveEpsilon
            || Abs(acceleration - ResolvedDesiredAcceleration) > parameters.ActiveEpsilon
            || inputPressure != 0f;

        public AxiomDynamicsParameters Parameters => parameters;

        public void ApplyInput(float amount, float timestamp)
        {
            AdvanceTo(timestamp);
            if (!Finite(amount) || amount == 0f) return;
            inputPressure = Clamp(inputPressure + amount / parameters.InputPulseDuration,
                -parameters.MaximumAbsoluteAcceleration, parameters.MaximumAbsoluteAcceleration);
            inputEndsAt = timestamp + parameters.InputPulseDuration;
        }

        public void AdvanceTo(float timestamp)
        {
            if (!hasTime)
            {
                lastTime = timestamp;
                hasTime = true;
                return;
            }

            if (!Finite(timestamp) || timestamp <= lastTime) return;
            float requested = timestamp - lastTime;
            float duration = requested > parameters.MaximumAdvanceSeconds ? parameters.MaximumAdvanceSeconds : requested;
            float cursor = timestamp - duration;
            if (requested > parameters.MaximumAdvanceSeconds)
            {
                inputPressure = 0f;
                inputEndsAt = cursor;
            }

            int steps = (int)Math.Ceiling(duration / parameters.MaximumSubstepSeconds);
            steps = steps < 1 ? 1 : steps > parameters.MaximumSubsteps ? parameters.MaximumSubsteps : steps;
            float step = duration / steps;
            for (int index = 0; index < steps; index++)
            {
                float first = cursor < inputEndsAt && cursor + step > inputEndsAt ? inputEndsAt - cursor : step;
                Integrate(first, cursor < inputEndsAt ? inputPressure : 0f);
                cursor += first;
                if (first < step)
                {
                    Integrate(step - first, 0f);
                    cursor += step - first;
                }
            }

            if (timestamp >= inputEndsAt) inputPressure = 0f;
            lastTime = timestamp;
            if (!IsActive)
            {
                state = ResolvedDesiredState;
                rate = ResolvedDesiredRate;
                acceleration = ResolvedDesiredAcceleration;
            }
        }

        public AxiomTrajectoryState GetActualTrajectory(float timestamp, AxiomTrajectoryThresholds thresholds)
        {
            AdvanceTo(timestamp);
            return Trajectory(state, rate, acceleration, timestamp, thresholds);
        }

        public AxiomTrajectoryState GetDesiredTrajectory(float timestamp, AxiomTrajectoryThresholds thresholds)
        {
            return Trajectory(ResolvedDesiredState, ResolvedDesiredRate, ResolvedDesiredAcceleration, timestamp, thresholds);
        }

        /// <summary>
        /// The dynamics profile owns target and, once versioned, the three
        /// tolerance bands. Older serialized profiles preserve their existing
        /// control-reference bands until explicitly upgraded.
        /// </summary>
        public AxiomTrajectoryReference BuildControlReference(AxiomTrajectoryReference legacy)
        {
            AxiomTrajectoryReference result = legacy;
            result.State = ResolvedDesiredState;
            result.Rate = ResolvedDesiredRate;
            result.Acceleration = ResolvedDesiredAcceleration;
            if (parameters.ControlSemanticsVersion >= 1)
            {
                result.StateTolerance = parameters.StateTolerance;
                result.RateTolerance = parameters.RateTolerance;
                result.AccelerationTolerance = parameters.AccelerationTolerance;
            }

            return result;
        }

        public void ApplyControlCorrection(AxiomErrorKind dimension, float requiredDirection, float magnitude, float timestamp)
        {
            AdvanceTo(timestamp);
            float impulse = Clamp(Abs(magnitude), 0f, 10f) * (requiredDirection >= 0f ? 1f : -1f);
            switch (dimension)
            {
                case AxiomErrorKind.State:
                    state = Clamp(state + impulse, parameters.MinimumState, parameters.MaximumState);
                    break;
                case AxiomErrorKind.Rate:
                    rate = Clamp(rate + impulse, -parameters.MaximumAbsoluteRate, parameters.MaximumAbsoluteRate);
                    break;
                case AxiomErrorKind.Acceleration:
                    acceleration = Clamp(acceleration + impulse, -parameters.MaximumAbsoluteAcceleration, parameters.MaximumAbsoluteAcceleration);
                    break;
            }
        }

        public void ApplyCounterBurden(float amount, float timestamp)
        {
            AdvanceTo(timestamp);
            acceleration = Clamp(acceleration + Abs(amount),
                -parameters.MaximumAbsoluteAcceleration, parameters.MaximumAbsoluteAcceleration);
        }

        /// <summary>Domain commit seam: replaces only S after advancing current dynamics; R and A remain authoritative and continuous.</summary>
        public bool TrySetStatePreservingDynamics(float approvedState,float timestamp)
        {
            if(!Finite(approvedState))return false;
            AdvanceTo(timestamp);
            state=Clamp(approvedState,parameters.MinimumState,parameters.MaximumState);
            return true;
        }

        public AxiomDynamicResponse Snapshot(float timestamp)
        {
            float input = Abs(inputPressure);
            float accelerationMagnitude = Abs(acceleration);
            return new AxiomDynamicResponse(kind, state, Abs(rate), accelerationMagnitude,
                accelerationMagnitude, input, input, hasTime ? lastTime : timestamp);
        }

        private void Integrate(float deltaTime, float input)
        {
            if (deltaTime <= 0f) return;
            // dS/dt = R; dR/dt = A; dA/dt = input - restoring/damping terms.
            float jerk = (parameters.ThreeStateInputGain * input)
                - parameters.StateRestoration * (state - ResolvedDesiredState)
                - parameters.RateDamping * (rate - ResolvedDesiredRate)
                - parameters.AccelerationDamping * (acceleration - ResolvedDesiredAcceleration);
            acceleration = Clamp(acceleration + jerk * deltaTime,
                -parameters.MaximumAbsoluteAcceleration, parameters.MaximumAbsoluteAcceleration);
            rate = Clamp(rate + acceleration * deltaTime, -parameters.MaximumAbsoluteRate, parameters.MaximumAbsoluteRate);
            state = Clamp(state + rate * deltaTime, parameters.MinimumState, parameters.MaximumState);
        }

        private AxiomTrajectoryState Trajectory(float s, float r, float a, float timestamp, AxiomTrajectoryThresholds thresholds)
        {
            float absRate = Abs(r);
            AxiomTrajectoryDirection direction = absRate <= thresholds.StableRateThreshold ? AxiomTrajectoryDirection.Stable : r > 0f ? AxiomTrajectoryDirection.Rising : AxiomTrajectoryDirection.Falling;
            AxiomRateIntensity intensity = absRate <= thresholds.StableRateThreshold ? AxiomRateIntensity.Slow : absRate >= thresholds.FastRateThreshold ? AxiomRateIntensity.Fast : AxiomRateIntensity.Moderate;
            AxiomCurvature curvature = Abs(a) <= thresholds.StableAccelerationThreshold ? AxiomCurvature.StableRate : a > 0f ? AxiomCurvature.Accelerating : AxiomCurvature.Decelerating;
            return new AxiomTrajectoryState(kind, s, r, r, a, true, true, true, direction, intensity, curvature, timestamp);
        }

        private float ResolvedDesiredState
        {
            get
            {
                float desired = parameters.DesiredState;
                if (parameters.EnforceMinimumDesiredState && desired < parameters.MinimumDesiredState)
                {
                    desired = parameters.MinimumDesiredState;
                }

                return Clamp(desired, parameters.MinimumState, parameters.MaximumState);
            }
        }

        private float ResolvedDesiredRate => Clamp(
            parameters.DesiredRate,
            -parameters.MaximumAbsoluteRate,
            parameters.MaximumAbsoluteRate);

        private float ResolvedDesiredAcceleration => Clamp(
            parameters.DesiredAcceleration,
            -parameters.MaximumAbsoluteAcceleration,
            parameters.MaximumAbsoluteAcceleration);

        private static float Abs(float value) => value < 0f ? -value : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float minimum, float maximum)
        {
            if (!Finite(value)) return 0f;
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
