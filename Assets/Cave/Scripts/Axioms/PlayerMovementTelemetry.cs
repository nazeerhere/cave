using UnityEngine;

namespace Cave.Axioms
{
    public struct PlayerMovementObservation
    {
        public PlayerMovementObservation(
            Vector2 position,
            Vector2 velocity,
            float speed,
            Vector2 deltaVelocity,
            Vector2 acceleration,
            float totalDistance,
            bool hasShortWindow,
            Vector2 shortDisplacement,
            float shortDistance,
            bool hasLongWindow,
            Vector2 longDisplacement,
            float longDistance,
            bool hasMeaningfulDirection,
            Vector2 direction,
            bool hasDirectionAlignment,
            float directionAlignment,
            bool isReversing)
        {
            Position = position;
            Velocity = velocity;
            Speed = speed;
            DeltaVelocity = deltaVelocity;
            Acceleration = acceleration;
            TotalDistance = totalDistance;
            HasShortWindow = hasShortWindow;
            ShortDisplacement = shortDisplacement;
            ShortDistance = shortDistance;
            HasLongWindow = hasLongWindow;
            LongDisplacement = longDisplacement;
            LongDistance = longDistance;
            HasMeaningfulDirection = hasMeaningfulDirection;
            Direction = direction;
            HasDirectionAlignment = hasDirectionAlignment;
            DirectionAlignment = directionAlignment;
            IsReversing = isReversing;
        }

        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public float Speed { get; }
        public Vector2 DeltaVelocity { get; }
        public Vector2 Acceleration { get; }
        public float TotalDistance { get; }
        public bool HasShortWindow { get; }
        public Vector2 ShortDisplacement { get; }
        public float ShortDistance { get; }
        public bool HasLongWindow { get; }
        public Vector2 LongDisplacement { get; }
        public float LongDistance { get; }
        public bool HasMeaningfulDirection { get; }
        public Vector2 Direction { get; }
        public bool HasDirectionAlignment { get; }
        public float DirectionAlignment { get; }
        public bool IsReversing { get; }
    }

    /// <summary>
    /// A passive, fixed-step observation component for the player Rigidbody2D.
    /// It reads motion only; it never writes velocity, position, physics settings,
    /// or PlayerController state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovementTelemetry : MonoBehaviour
    {
        private struct MovementSample
        {
            public float Time;
            public Vector2 Position;
            public float DistanceFromPrevious;
        }

        [Header("Observation Windows")]
        [SerializeField, Min(8)] private int historyCapacity = 64;
        [SerializeField, Min(0.02f)] private float shortWindowSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float longWindowSeconds = 1f;

        [Header("Direction Noise Guard")]
        [SerializeField, Min(0f)] private float minimumMeaningfulSpeed = 0.05f;
        [SerializeField, Range(-1f, 0f)] private float reversalAlignmentThreshold = -0.5f;

        private Rigidbody2D body;
        private MovementSample[] samples;
        private int nextSampleIndex;
        private int sampleCount;
        private bool hasPreviousVelocity;
        private Vector2 previousVelocity;
        private bool hasPreviousMeaningfulDirection;
        private Vector2 previousMeaningfulDirection;

        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float Speed { get; private set; }
        public bool HasVelocityChange { get; private set; }
        public Vector2 DeltaVelocity { get; private set; }
        public Vector2 Acceleration { get; private set; }
        public float TotalDistance { get; private set; }
        public bool HasShortWindow { get; private set; }
        public Vector2 ShortDisplacement { get; private set; }
        public float ShortDistance { get; private set; }
        public bool HasLongWindow { get; private set; }
        public Vector2 LongDisplacement { get; private set; }
        public float LongDistance { get; private set; }
        public bool HasMeaningfulDirection { get; private set; }
        public Vector2 Direction { get; private set; }
        public bool HasDirectionAlignment { get; private set; }
        public float DirectionAlignment { get; private set; }
        public bool IsReversing { get; private set; }

        public PlayerMovementObservation Observation => new PlayerMovementObservation(
            Position,
            Velocity,
            Speed,
            DeltaVelocity,
            Acceleration,
            TotalDistance,
            HasShortWindow,
            ShortDisplacement,
            ShortDistance,
            HasLongWindow,
            LongDisplacement,
            LongDistance,
            HasMeaningfulDirection,
            Direction,
            HasDirectionAlignment,
            DirectionAlignment,
            IsReversing);

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            BuildHistory();
            ResetObservations();
        }

        private void OnEnable()
        {
            if (body != null)
            {
                ResetObservations();
            }
        }

        private void FixedUpdate()
        {
            float now = Time.fixedTime;
            Position = body.position;
            Velocity = body.velocity;
            Speed = Velocity.magnitude;

            UpdateVelocityObservations();
            RecordPosition(now, Position);
            UpdateWindowObservations(now);
            UpdateDirectionObservations();
        }

        public void ResetObservations()
        {
            nextSampleIndex = 0;
            sampleCount = 0;
            hasPreviousVelocity = false;
            previousVelocity = Vector2.zero;
            hasPreviousMeaningfulDirection = false;
            previousMeaningfulDirection = Vector2.zero;
            Position = body != null ? body.position : Vector2.zero;
            Velocity = body != null ? body.velocity : Vector2.zero;
            Speed = Velocity.magnitude;
            HasVelocityChange = false;
            DeltaVelocity = Vector2.zero;
            Acceleration = Vector2.zero;
            TotalDistance = 0f;
            HasShortWindow = false;
            ShortDisplacement = Vector2.zero;
            ShortDistance = 0f;
            HasLongWindow = false;
            LongDisplacement = Vector2.zero;
            LongDistance = 0f;
            HasMeaningfulDirection = false;
            Direction = Vector2.zero;
            HasDirectionAlignment = false;
            DirectionAlignment = 0f;
            IsReversing = false;
        }

        private void BuildHistory()
        {
            int capacity = Mathf.Max(8, historyCapacity);
            samples = new MovementSample[capacity];
        }

        private void UpdateVelocityObservations()
        {
            if (!hasPreviousVelocity)
            {
                HasVelocityChange = false;
                DeltaVelocity = Vector2.zero;
                Acceleration = Vector2.zero;
                previousVelocity = Velocity;
                hasPreviousVelocity = true;
                return;
            }

            DeltaVelocity = Velocity - previousVelocity;
            Acceleration = DeltaVelocity / Time.fixedDeltaTime;
            HasVelocityChange = true;
            previousVelocity = Velocity;
        }

        private void RecordPosition(float timestamp, Vector2 position)
        {
            float distanceFromPrevious = 0f;
            if (sampleCount > 0)
            {
                distanceFromPrevious = Vector2.Distance(GetSample(sampleCount - 1).Position, position);
                TotalDistance += distanceFromPrevious;
            }

            samples[nextSampleIndex] = new MovementSample
            {
                Time = timestamp,
                Position = position,
                DistanceFromPrevious = distanceFromPrevious
            };
            nextSampleIndex = (nextSampleIndex + 1) % samples.Length;
            if (sampleCount < samples.Length)
            {
                sampleCount++;
            }
        }

        private void UpdateWindowObservations(float timestamp)
        {
            HasShortWindow = TryCalculateWindow(
                timestamp - shortWindowSeconds,
                out Vector2 shortDisplacement,
                out float shortDistance);
            ShortDisplacement = HasShortWindow ? shortDisplacement : Vector2.zero;
            ShortDistance = HasShortWindow ? shortDistance : 0f;

            HasLongWindow = TryCalculateWindow(
                timestamp - longWindowSeconds,
                out Vector2 longDisplacement,
                out float longDistance);
            LongDisplacement = HasLongWindow ? longDisplacement : Vector2.zero;
            LongDistance = HasLongWindow ? longDistance : 0f;
        }

        private bool TryCalculateWindow(float startTime, out Vector2 displacement, out float distance)
        {
            displacement = Vector2.zero;
            distance = 0f;
            if (sampleCount < 2 || !TryGetSampleAtOrBefore(startTime, out int startIndex))
            {
                return false;
            }

            MovementSample start = GetSample(startIndex);
            MovementSample current = GetSample(sampleCount - 1);
            displacement = current.Position - start.Position;
            for (int index = startIndex + 1; index < sampleCount; index++)
            {
                distance += GetSample(index).DistanceFromPrevious;
            }

            return true;
        }

        private void UpdateDirectionObservations()
        {
            HasMeaningfulDirection = Speed >= minimumMeaningfulSpeed;
            if (!HasMeaningfulDirection)
            {
                Direction = Vector2.zero;
                HasDirectionAlignment = false;
                DirectionAlignment = 0f;
                IsReversing = false;
                hasPreviousMeaningfulDirection = false;
                return;
            }

            Direction = Velocity / Speed;
            HasDirectionAlignment = hasPreviousMeaningfulDirection;
            DirectionAlignment = HasDirectionAlignment
                ? Vector2.Dot(previousMeaningfulDirection, Direction)
                : 0f;
            IsReversing = HasDirectionAlignment && DirectionAlignment <= reversalAlignmentThreshold;
            previousMeaningfulDirection = Direction;
            hasPreviousMeaningfulDirection = true;
        }

        private bool TryGetSampleAtOrBefore(float timestamp, out int chronologicalIndex)
        {
            chronologicalIndex = -1;
            for (int index = 0; index < sampleCount; index++)
            {
                if (GetSample(index).Time > timestamp)
                {
                    break;
                }

                chronologicalIndex = index;
            }

            return chronologicalIndex >= 0;
        }

        private MovementSample GetSample(int chronologicalIndex)
        {
            int oldestIndex = (nextSampleIndex - sampleCount + samples.Length) % samples.Length;
            return samples[(oldestIndex + chronologicalIndex) % samples.Length];
        }
    }
}
