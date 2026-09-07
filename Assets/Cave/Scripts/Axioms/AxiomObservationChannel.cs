namespace Cave.Axioms
{
    /// <summary>Actor-local, allocation-free scalar channel for one Axiom family.</summary>
    public sealed class AxiomObservationChannel
    {
        private readonly ScalarTrajectoryTracker trajectory;

        public AxiomObservationChannel(
            AxiomKind kind,
            int historyCapacity = 64,
            float shortWindowSeconds = 0.25f,
            float longWindowSeconds = 1f,
            float minimumDeltaTime = 0.0001f)
        {
            Kind = kind;
            trajectory = new ScalarTrajectoryTracker(
                historyCapacity,
                shortWindowSeconds,
                longWindowSeconds,
                minimumDeltaTime);
        }

        public AxiomKind Kind { get; }
        public float CurrentValue => trajectory.CurrentValue;
        public float LatestInputMagnitude { get; private set; }
        public float LatestInputTime { get; private set; }
        public bool HasSamples => trajectory.HasSamples;
        public bool HasShortRate => trajectory.HasShortRate;
        public bool HasLongRate => trajectory.HasLongRate;
        public bool HasShortAcceleration => trajectory.HasShortAcceleration;
        public float ShortRate => trajectory.ShortRate;
        public float LongRate => trajectory.LongRate;
        public float ShortAcceleration => trajectory.ShortAcceleration;

        public void ApplyDelta(float amount, float timestamp)
        {
            LatestInputMagnitude = amount;
            LatestInputTime = timestamp;
            trajectory.RecordDelta(timestamp, amount);
        }

        public void RecordValue(float value, float timestamp)
        {
            LatestInputMagnitude = value - (trajectory.HasSamples ? trajectory.CurrentValue : 0f);
            LatestInputTime = timestamp;
            trajectory.RecordValue(timestamp, value);
        }

        public AxiomTrajectoryState GetTrajectory(AxiomTrajectoryThresholds thresholds)
        {
            return AxiomTrajectoryClassifier.Classify(Kind, trajectory, thresholds);
        }

        public void Reset(float initialValue = 0f)
        {
            trajectory.Reset(initialValue);
            LatestInputMagnitude = 0f;
            LatestInputTime = 0f;
        }
    }
}
