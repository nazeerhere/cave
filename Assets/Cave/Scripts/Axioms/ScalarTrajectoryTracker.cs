using System;

namespace Cave.Axioms
{
    /// <summary>
    /// Bounded finite-difference observations for a scalar value sampled over time.
    /// This class is intentionally independent of Unity so gameplay systems can feed
    /// application events without owning a MonoBehaviour or a global service.
    /// </summary>
    public sealed class ScalarTrajectoryTracker
    {
        private struct ScalarSample
        {
            public float Time;
            public float Value;
        }

        private struct RateSample
        {
            public float Time;
            public float Rate;
        }

        private readonly ScalarSample[] samples;
        private readonly RateSample[] shortRateSamples;
        private readonly float shortWindowSeconds;
        private readonly float longWindowSeconds;
        private readonly float minimumDeltaTime;

        private int nextSampleIndex;
        private int sampleCount;
        private int nextShortRateIndex;
        private int shortRateCount;

        public ScalarTrajectoryTracker(
            int historyCapacity = 64,
            float shortWindowSeconds = 0.25f,
            float longWindowSeconds = 1f,
            float minimumDeltaTime = 0.0001f)
        {
            if (historyCapacity < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(historyCapacity), "At least four samples are required.");
            }

            if (shortWindowSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(shortWindowSeconds));
            }

            if (longWindowSeconds < shortWindowSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(longWindowSeconds), "The long window must be at least as large as the short window.");
            }

            if (minimumDeltaTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumDeltaTime));
            }

            samples = new ScalarSample[historyCapacity];
            shortRateSamples = new RateSample[historyCapacity];
            this.shortWindowSeconds = shortWindowSeconds;
            this.longWindowSeconds = longWindowSeconds;
            this.minimumDeltaTime = minimumDeltaTime;
        }

        public float CurrentValue { get; private set; }
        public float LastSampleTime { get; private set; }
        public int SampleCount => sampleCount;
        public bool HasSamples => sampleCount > 0;

        public bool HasShortRate { get; private set; }
        public float ShortRate { get; private set; }
        public bool HasLongRate { get; private set; }
        public float LongRate { get; private set; }
        public bool HasShortAcceleration { get; private set; }
        public float ShortAcceleration { get; private set; }

        /// <summary>
        /// Records an absolute scalar observation. Samples that arrive at effectively
        /// the same timestamp replace the latest value rather than producing an
        /// unstable divide-by-near-zero derivative.
        /// </summary>
        public void RecordValue(float timestamp, float value)
        {
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp)
                || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(timestamp), "Trajectory samples must be finite.");
            }

            if (sampleCount > 0)
            {
                ScalarSample latest = GetSample(sampleCount - 1);
                if (timestamp < latest.Time - minimumDeltaTime)
                {
                    throw new ArgumentOutOfRangeException(nameof(timestamp), "Trajectory sample timestamps must be monotonic.");
                }

                if (timestamp - latest.Time < minimumDeltaTime)
                {
                    ReplaceLatestSample(MathfMax(timestamp, latest.Time), value);
                    CurrentValue = value;
                    LastSampleTime = MathfMax(timestamp, latest.Time);
                    RecalculateObservations();
                    return;
                }
            }

            samples[nextSampleIndex] = new ScalarSample { Time = timestamp, Value = value };
            nextSampleIndex = (nextSampleIndex + 1) % samples.Length;
            if (sampleCount < samples.Length)
            {
                sampleCount++;
            }

            CurrentValue = value;
            LastSampleTime = timestamp;
            RecalculateObservations();
        }

        /// <summary>
        /// Records an application delta against the latest tracked value. The first
        /// delta is interpreted as a change from the conventional scalar origin of 0.
        /// </summary>
        public void RecordDelta(float timestamp, float delta)
        {
            RecordValue(timestamp, (HasSamples ? CurrentValue : 0f) + delta);
        }

        public void Reset(float initialValue = 0f)
        {
            nextSampleIndex = 0;
            sampleCount = 0;
            nextShortRateIndex = 0;
            shortRateCount = 0;
            CurrentValue = initialValue;
            LastSampleTime = 0f;
            HasShortRate = false;
            ShortRate = 0f;
            HasLongRate = false;
            LongRate = 0f;
            HasShortAcceleration = false;
            ShortAcceleration = 0f;
        }

        private void RecalculateObservations()
        {
            HasShortRate = TryCalculateRate(shortWindowSeconds, out float shortRate);
            ShortRate = HasShortRate ? shortRate : 0f;

            HasLongRate = TryCalculateRate(longWindowSeconds, out float longRate);
            LongRate = HasLongRate ? longRate : 0f;

            if (HasShortRate)
            {
                AddShortRateSample(LastSampleTime, ShortRate);
            }

            HasShortAcceleration = TryCalculateShortAcceleration(out float shortAcceleration);
            ShortAcceleration = HasShortAcceleration ? shortAcceleration : 0f;
        }

        private bool TryCalculateRate(float windowSeconds, out float rate)
        {
            rate = 0f;
            if (sampleCount < 2)
            {
                return false;
            }

            ScalarSample current = GetSample(sampleCount - 1);
            if (!TryGetSampleAtOrBefore(current.Time - windowSeconds, out ScalarSample previous))
            {
                return false;
            }

            float deltaTime = current.Time - previous.Time;
            if (deltaTime < minimumDeltaTime)
            {
                return false;
            }

            rate = (current.Value - previous.Value) / deltaTime;
            return true;
        }

        private bool TryCalculateShortAcceleration(out float acceleration)
        {
            acceleration = 0f;
            if (shortRateCount < 2)
            {
                return false;
            }

            RateSample current = GetShortRateSample(shortRateCount - 1);
            if (!TryGetShortRateAtOrBefore(current.Time - shortWindowSeconds, out RateSample previous))
            {
                return false;
            }

            float deltaTime = current.Time - previous.Time;
            if (deltaTime < minimumDeltaTime)
            {
                return false;
            }

            acceleration = (current.Rate - previous.Rate) / deltaTime;
            return true;
        }

        private void AddShortRateSample(float timestamp, float rate)
        {
            if (shortRateCount > 0)
            {
                RateSample latest = GetShortRateSample(shortRateCount - 1);
                if (timestamp - latest.Time < minimumDeltaTime)
                {
                    int latestIndex = (nextShortRateIndex - 1 + shortRateSamples.Length) % shortRateSamples.Length;
                    shortRateSamples[latestIndex] = new RateSample { Time = MathfMax(timestamp, latest.Time), Rate = rate };
                    return;
                }
            }

            shortRateSamples[nextShortRateIndex] = new RateSample { Time = timestamp, Rate = rate };
            nextShortRateIndex = (nextShortRateIndex + 1) % shortRateSamples.Length;
            if (shortRateCount < shortRateSamples.Length)
            {
                shortRateCount++;
            }
        }

        private bool TryGetSampleAtOrBefore(float timestamp, out ScalarSample result)
        {
            result = default(ScalarSample);
            bool found = false;
            for (int index = 0; index < sampleCount; index++)
            {
                ScalarSample candidate = GetSample(index);
                if (candidate.Time > timestamp)
                {
                    break;
                }

                result = candidate;
                found = true;
            }

            return found;
        }

        private bool TryGetShortRateAtOrBefore(float timestamp, out RateSample result)
        {
            result = default(RateSample);
            bool found = false;
            for (int index = 0; index < shortRateCount; index++)
            {
                RateSample candidate = GetShortRateSample(index);
                if (candidate.Time > timestamp)
                {
                    break;
                }

                result = candidate;
                found = true;
            }

            return found;
        }

        private ScalarSample GetSample(int chronologicalIndex)
        {
            int oldestIndex = (nextSampleIndex - sampleCount + samples.Length) % samples.Length;
            return samples[(oldestIndex + chronologicalIndex) % samples.Length];
        }

        private RateSample GetShortRateSample(int chronologicalIndex)
        {
            int oldestIndex = (nextShortRateIndex - shortRateCount + shortRateSamples.Length) % shortRateSamples.Length;
            return shortRateSamples[(oldestIndex + chronologicalIndex) % shortRateSamples.Length];
        }

        private void ReplaceLatestSample(float timestamp, float value)
        {
            int latestIndex = (nextSampleIndex - 1 + samples.Length) % samples.Length;
            samples[latestIndex] = new ScalarSample { Time = timestamp, Value = value };
        }

        private static float MathfMax(float left, float right)
        {
            return left > right ? left : right;
        }
    }
}
