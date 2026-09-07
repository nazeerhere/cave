namespace Cave.Axioms
{
    public enum AxiomKind
    {
        Heat,
        Order,
        Flow,
        Mass,
        Phase,
        StoneglassPrecision
    }

    public enum AxiomTrajectoryDirection
    {
        Unavailable,
        Falling,
        Stable,
        Rising
    }

    public enum AxiomRateIntensity
    {
        Unavailable,
        Slow,
        Moderate,
        Fast
    }

    public enum AxiomCurvature
    {
        Unavailable,
        Decelerating,
        StableRate,
        Accelerating
    }

    public enum AxiomErrorKind
    {
        None,
        State,
        Rate,
        Acceleration
    }

    public enum AxiomFeedbackType
    {
        StateChanged,
        TrajectoryChanged,
        ErrorDetected,
        ErrorCleared,
        ControlIntervention,
        ControlOpportunityOpened,
        ControlOpportunityRefreshed,
        ControlOpportunityExpired,
        ControlInterventionSucceeded,
        ControlInterventionFailed,
        // Labels only: Pass 2 does not implement either system.
        PhaseDebuffActive,
        ResonanceBreak
    }

    /// <summary>Centralized qualitative thresholds for all future Axiom channels.</summary>
    public struct AxiomTrajectoryThresholds
    {
        public float StableRateThreshold;
        public float FastRateThreshold;
        public float StableAccelerationThreshold;

        public AxiomTrajectoryThresholds(
            float stableRateThreshold,
            float fastRateThreshold,
            float stableAccelerationThreshold)
        {
            StableRateThreshold = Abs(stableRateThreshold);
            FastRateThreshold = Max(StableRateThreshold, Abs(fastRateThreshold));
            StableAccelerationThreshold = Abs(stableAccelerationThreshold);
        }

        public static AxiomTrajectoryThresholds Default =>
            new AxiomTrajectoryThresholds(0.05f, 1f, 0.05f);

        private static float Abs(float value) => value < 0f ? -value : value;
        private static float Max(float left, float right) => left > right ? left : right;
    }

    public struct AxiomTrajectoryState
    {
        public AxiomTrajectoryState(
            AxiomKind kind,
            float currentValue,
            float shortRate,
            float longRate,
            float shortAcceleration,
            bool hasShortRate,
            bool hasLongRate,
            bool hasShortAcceleration,
            AxiomTrajectoryDirection direction,
            AxiomRateIntensity rateIntensity,
            AxiomCurvature curvature,
            float timestamp)
        {
            Kind = kind;
            CurrentValue = currentValue;
            ShortRate = shortRate;
            LongRate = longRate;
            ShortAcceleration = shortAcceleration;
            HasShortRate = hasShortRate;
            HasLongRate = hasLongRate;
            HasShortAcceleration = hasShortAcceleration;
            Direction = direction;
            RateIntensity = rateIntensity;
            Curvature = curvature;
            Timestamp = timestamp;
        }

        public AxiomKind Kind { get; }
        public float CurrentValue { get; }
        public float ShortRate { get; }
        public float LongRate { get; }
        public float ShortAcceleration { get; }
        public bool HasShortRate { get; }
        public bool HasLongRate { get; }
        public bool HasShortAcceleration { get; }
        public AxiomTrajectoryDirection Direction { get; }
        public AxiomRateIntensity RateIntensity { get; }
        public AxiomCurvature Curvature { get; }
        public float Timestamp { get; }
    }

    public static class AxiomTrajectoryClassifier
    {
        public static AxiomTrajectoryState Classify(
            AxiomKind kind,
            ScalarTrajectoryTracker tracker,
            AxiomTrajectoryThresholds thresholds)
        {
            bool hasShortRate = tracker != null && tracker.HasShortRate;
            bool hasLongRate = tracker != null && tracker.HasLongRate;
            bool hasAcceleration = tracker != null && tracker.HasShortAcceleration;
            float shortRate = hasShortRate ? tracker.ShortRate : 0f;
            float longRate = hasLongRate ? tracker.LongRate : 0f;
            float acceleration = hasAcceleration ? tracker.ShortAcceleration : 0f;
            AxiomTrajectoryDirection direction = ClassifyDirection(shortRate, hasShortRate, thresholds);
            AxiomRateIntensity intensity = ClassifyRateIntensity(shortRate, hasShortRate, thresholds);
            AxiomCurvature curvature = ClassifyCurvature(acceleration, hasAcceleration, thresholds);
            return new AxiomTrajectoryState(
                kind,
                tracker != null ? tracker.CurrentValue : 0f,
                shortRate,
                longRate,
                acceleration,
                hasShortRate,
                hasLongRate,
                hasAcceleration,
                direction,
                intensity,
                curvature,
                tracker != null ? tracker.LastSampleTime : 0f);
        }

        private static AxiomTrajectoryDirection ClassifyDirection(
            float rate,
            bool available,
            AxiomTrajectoryThresholds thresholds)
        {
            if (!available)
            {
                return AxiomTrajectoryDirection.Unavailable;
            }

            if (Abs(rate) <= thresholds.StableRateThreshold)
            {
                return AxiomTrajectoryDirection.Stable;
            }

            return rate > 0f
                ? AxiomTrajectoryDirection.Rising
                : AxiomTrajectoryDirection.Falling;
        }

        private static AxiomRateIntensity ClassifyRateIntensity(
            float rate,
            bool available,
            AxiomTrajectoryThresholds thresholds)
        {
            if (!available)
            {
                return AxiomRateIntensity.Unavailable;
            }

            float magnitude = Abs(rate);
            if (magnitude <= thresholds.StableRateThreshold)
            {
                return AxiomRateIntensity.Slow;
            }

            return magnitude >= thresholds.FastRateThreshold
                ? AxiomRateIntensity.Fast
                : AxiomRateIntensity.Moderate;
        }

        private static AxiomCurvature ClassifyCurvature(
            float acceleration,
            bool available,
            AxiomTrajectoryThresholds thresholds)
        {
            if (!available)
            {
                return AxiomCurvature.Unavailable;
            }

            if (Abs(acceleration) <= thresholds.StableAccelerationThreshold)
            {
                return AxiomCurvature.StableRate;
            }

            return acceleration > 0f
                ? AxiomCurvature.Accelerating
                : AxiomCurvature.Decelerating;
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }

    /// <summary>
    /// A consumer-supplied desired trajectory. A future Axiom chooses which terms
    /// matter; this shared layer does not impose a universal equilibrium.
    /// </summary>
    public struct AxiomTrajectoryReference
    {
        public bool EvaluateState;
        public float State;
        public float StateTolerance;
        public bool EvaluateRate;
        public float Rate;
        public float RateTolerance;
        public bool EvaluateAcceleration;
        public float Acceleration;
        public float AccelerationTolerance;
    }

    public struct AxiomErrorState
    {
        public AxiomErrorState(
            AxiomKind kind,
            AxiomErrorKind errorKind,
            float signedDifference,
            float timestamp)
        {
            Kind = kind;
            ErrorKind = errorKind;
            SignedDifference = signedDifference;
            Magnitude = signedDifference < 0f ? -signedDifference : signedDifference;
            Timestamp = timestamp;
        }

        public AxiomKind Kind { get; }
        public AxiomErrorKind ErrorKind { get; }
        public float SignedDifference { get; }
        public float Magnitude { get; }
        public float Timestamp { get; }
        public bool HasError => ErrorKind != AxiomErrorKind.None;
    }

    public static class AxiomErrorEvaluator
    {
        public static AxiomErrorState Evaluate(
            AxiomTrajectoryState observation,
            AxiomTrajectoryReference reference,
            float timestamp)
        {
            if (reference.EvaluateAcceleration && observation.HasShortAcceleration)
            {
                float difference = observation.ShortAcceleration - reference.Acceleration;
                if (Abs(difference) > Abs(reference.AccelerationTolerance))
                {
                    return new AxiomErrorState(observation.Kind, AxiomErrorKind.Acceleration, difference, timestamp);
                }
            }

            if (reference.EvaluateRate && observation.HasShortRate)
            {
                float difference = observation.ShortRate - reference.Rate;
                if (Abs(difference) > Abs(reference.RateTolerance))
                {
                    return new AxiomErrorState(observation.Kind, AxiomErrorKind.Rate, difference, timestamp);
                }
            }

            if (reference.EvaluateState)
            {
                float difference = observation.CurrentValue - reference.State;
                if (Abs(difference) > Abs(reference.StateTolerance))
                {
                    return new AxiomErrorState(observation.Kind, AxiomErrorKind.State, difference, timestamp);
                }
            }

            return new AxiomErrorState(observation.Kind, AxiomErrorKind.None, 0f, timestamp);
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
