using System;

namespace Cave.Axioms.Elemental
{
    public struct AxiomDynamicResponse
    {
        public AxiomDynamicResponse(
            AxiomKind kind,
            float stack,
            float desirable,
            float counter,
            float input,
            float latestInputAmount,
            float lastEvaluationTime)
        {
            Kind = kind;
            Stack = stack;
            Desirable = desirable;
            Counter = counter;
            Input = input;
            LatestInputAmount = latestInputAmount;
            LastEvaluationTime = lastEvaluationTime;
        }

        public AxiomKind Kind { get; }
        public float Stack { get; }
        public float Desirable { get; }
        public float Counter { get; }
        public float Input { get; }
        public float LatestInputAmount { get; }
        public float LastEvaluationTime { get; }
        public float ResponseStrength => Clamp01(Desirable - (Counter * .25f));

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>
    /// One bounded, allocation-free response channel. It uses the same law for all
    /// elemental Axioms and advances only at application/query time.
    /// </summary>
    public sealed class AxiomDynamicsChannel
    {
        private readonly AxiomKind kind;
        private readonly AxiomDynamicsParameters parameters;
        private float stack;
        private float desirable;
        private float counter;
        private float inputRate;
        private float inputEndsAt;
        private float latestInputAmount;
        private float lastEvaluationTime;
        private bool hasEvaluationTime;

        public AxiomDynamicsChannel(AxiomKind kind, AxiomDynamicsParameters parameters)
        {
            this.kind = kind;
            this.parameters = parameters.Sanitized();
        }

        public AxiomKind Kind => kind;

        public void ApplyInput(float currentStack, float amount, float timestamp)
        {
            AdvanceTo(timestamp);
            stack = NonNegativeFinite(currentStack);
            latestInputAmount = NonNegativeFinite(amount);
            inputRate = latestInputAmount / parameters.InputPulseDuration;
            inputEndsAt = timestamp + parameters.InputPulseDuration;
        }

        public AxiomDynamicResponse AdvanceTo(float timestamp)
        {
            if (!hasEvaluationTime)
            {
                lastEvaluationTime = timestamp;
                hasEvaluationTime = true;
                return Snapshot(timestamp);
            }

            if (!IsFinite(timestamp) || timestamp <= lastEvaluationTime)
            {
                return Snapshot(lastEvaluationTime);
            }

            float requestedDuration = timestamp - lastEvaluationTime;
            float duration = requestedDuration > parameters.MaximumAdvanceSeconds
                ? parameters.MaximumAdvanceSeconds
                : requestedDuration;
            float start = timestamp - duration;
            if (requestedDuration > parameters.MaximumAdvanceSeconds)
            {
                // A long unobserved gap is intentionally compressed into a bounded
                // relaxation horizon so one inactive actor never performs thousands
                // of catch-up steps.
                inputRate = 0f;
                inputEndsAt = start;
            }

            int steps = (int)Math.Ceiling(duration / parameters.MaximumSubstepSeconds);
            if (steps < 1)
            {
                steps = 1;
            }
            else if (steps > parameters.MaximumSubsteps)
            {
                steps = parameters.MaximumSubsteps;
            }

            float step = duration / steps;
            float cursor = start;
            for (int index = 0; index < steps; index++)
            {
                float localStep = step;
                if (cursor < inputEndsAt && cursor + localStep > inputEndsAt)
                {
                    localStep = inputEndsAt - cursor;
                }

                Integrate(localStep, cursor < inputEndsAt ? inputRate : 0f);
                cursor += localStep;
                if (localStep < step)
                {
                    Integrate(step - localStep, 0f);
                    cursor += step - localStep;
                }
            }

            if (timestamp >= inputEndsAt)
            {
                inputRate = 0f;
            }

            lastEvaluationTime = timestamp;
            return Snapshot(timestamp);
        }

        public AxiomDynamicResponse Snapshot(float timestamp)
        {
            return new AxiomDynamicResponse(
                kind,
                stack,
                desirable,
                counter,
                timestamp < inputEndsAt ? inputRate : 0f,
                latestInputAmount,
                hasEvaluationTime ? lastEvaluationTime : timestamp);
        }

        /// <summary>Bounded control-only adjustment; it deliberately never changes S or u.</summary>
        public AxiomDynamicResponse ApplyControlCorrection(
            float desirableDelta,
            float counterReduction,
            float timestamp)
        {
            AdvanceTo(timestamp);
            desirable = ClampFinite(desirable + NonNegativeFinite(desirableDelta), parameters.MaximumResponse);
            counter = ClampFinite(counter - NonNegativeFinite(counterReduction), parameters.MaximumResponse);
            return Snapshot(timestamp);
        }

        /// <summary>Small bounded burden for an uncontrolled movement event; no stack mutation.</summary>
        public AxiomDynamicResponse ApplyCounterBurden(float counterDelta, float timestamp)
        {
            AdvanceTo(timestamp);
            counter = ClampFinite(counter + NonNegativeFinite(counterDelta), parameters.MaximumResponse);
            return Snapshot(timestamp);
        }

        private void Integrate(float deltaTime, float input)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // dB/dt = a*u + b*S - c*C*B
            // dC/dt = d*B - e*C
            float desirableDerivative = (parameters.InputGain * input)
                + (parameters.StackGain * stack)
                - (parameters.CounterCoupling * counter * desirable);
            float counterDerivative = (parameters.CounterGain * desirable)
                - (parameters.CounterDecay * counter);
            desirable = ClampFinite(desirable + desirableDerivative * deltaTime, parameters.MaximumResponse);
            counter = ClampFinite(counter + counterDerivative * deltaTime, parameters.MaximumResponse);
        }

        private static float NonNegativeFinite(float value)
        {
            return IsFinite(value) && value > 0f ? value : 0f;
        }

        private static float ClampFinite(float value, float maximum)
        {
            if (!IsFinite(value) || value < 0f)
            {
                return 0f;
            }

            return value > maximum ? maximum : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
