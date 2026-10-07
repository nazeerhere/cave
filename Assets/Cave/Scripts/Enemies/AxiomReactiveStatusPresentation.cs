using Cave.Axioms;
using Cave.Axioms.Control;

namespace Cave.Enemies
{
    public enum AxiomReactiveMarker
    {
        None,
        Up,
        Down,
        Success,
        Failure
    }

    public struct AxiomReactiveStatusVisual
    {
        public AxiomReactiveStatusVisual(AxiomErrorKind dimension, AxiomReactiveMarker marker)
        {
            Dimension = dimension;
            Marker = marker;
        }

        public AxiomErrorKind Dimension { get; }
        public AxiomReactiveMarker Marker { get; }
        public bool HasOverlay => Dimension != AxiomErrorKind.None && Marker != AxiomReactiveMarker.None;
    }

    /// <summary>
    /// Presentation-only, bounded state for a single Axiom cell. It never owns
    /// opportunity selection or control outcome authority.
    /// </summary>
    public sealed class AxiomReactiveStatusPresentation
    {
        public const float DefaultFeedbackDuration = 0.4f;

        private long consumedResultSequence;
        private float feedbackExpiresAt;
        private AxiomErrorKind feedbackDimension;
        private AxiomReactiveMarker feedbackMarker;

        public bool Observe(AxiomControlInterventionOutcome outcome, float duration)
        {
            if (outcome.Sequence <= consumedResultSequence)
            {
                return false;
            }

            consumedResultSequence = outcome.Sequence;
            feedbackDimension = outcome.Error.ErrorKind;
            feedbackMarker = outcome.Succeeded
                ? AxiomReactiveMarker.Success
                : AxiomReactiveMarker.Failure;
            feedbackExpiresAt = outcome.Timestamp + duration;
            return true;
        }

        public AxiomReactiveStatusVisual Resolve(
            bool hasOpportunity,
            AxiomControlOpportunity opportunity,
            float timestamp)
        {
            if (timestamp < feedbackExpiresAt)
            {
                return new AxiomReactiveStatusVisual(feedbackDimension, feedbackMarker);
            }

            if (!hasOpportunity)
            {
                return new AxiomReactiveStatusVisual(AxiomErrorKind.None, AxiomReactiveMarker.None);
            }

            return new AxiomReactiveStatusVisual(
                opportunity.Error.ErrorKind,
                opportunity.RequiredCorrectionDirection >= 0f
                    ? AxiomReactiveMarker.Up
                    : AxiomReactiveMarker.Down);
        }

        public bool IsFeedbackActive(float timestamp)
        {
            return timestamp < feedbackExpiresAt;
        }

        public void Reset()
        {
            feedbackExpiresAt = 0f;
            feedbackDimension = AxiomErrorKind.None;
            feedbackMarker = AxiomReactiveMarker.None;
        }
    }
}
