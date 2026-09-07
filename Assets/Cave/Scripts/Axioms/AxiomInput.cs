using UnityEngine;

namespace Cave.Axioms
{
    public struct AxiomInput
    {
        public AxiomInput(
            AxiomKind kind,
            float amount,
            GameObject source,
            GameObject target,
            float timestamp)
        {
            Kind = kind;
            Amount = amount;
            Source = source;
            Target = target;
            Timestamp = timestamp;
        }

        public AxiomKind Kind { get; }
        public float Amount { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
        public float Timestamp { get; }
    }

    public struct AxiomFeedbackEvent
    {
        public AxiomFeedbackEvent(
            AxiomKind kind,
            AxiomFeedbackType feedbackType,
            float magnitude,
            GameObject source,
            GameObject target,
            float timestamp)
        {
            Kind = kind;
            FeedbackType = feedbackType;
            Magnitude = magnitude;
            Source = source;
            Target = target;
            Timestamp = timestamp;
        }

        public AxiomKind Kind { get; }
        public AxiomFeedbackType FeedbackType { get; }
        public float Magnitude { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
        public float Timestamp { get; }
    }
}
