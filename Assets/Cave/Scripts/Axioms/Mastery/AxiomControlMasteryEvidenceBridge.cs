using Cave.Domain;
using UnityEngine;

namespace Cave.Axioms.Mastery
{
    /// <summary>
    /// Converts a completed authoritative control intervention into the one
    /// corresponding Domain-control evidence unit. Source actions, opportunity
    /// discovery, thresholds, and natural convergence never call this seam.
    /// </summary>
    public static class AxiomControlMasteryEvidenceBridge
    {
        public static bool TrySubmitSuccessfulControl(GameObject controller, AxiomKind kind,
            AxiomErrorState error, float quality, LawExpression? expression)
        {
            PhenomenonMasteryEvidenceSubmission submission;
            if (!TryCreateSubmission(kind, error, quality, expression, out submission)) return false;

            // Only an already-established player evidence owner may receive
            // Domain progress. Enemy/local control never creates one as a side effect.
            PlayerMasteryEvidenceRuntime owner = controller != null
                ? controller.GetComponent<PlayerMasteryEvidenceRuntime>() : null;
            if (owner == null) return false;
            owner.Submit(submission, PlayerMasteryPolicy.Default);
            return true;
        }

        /// <summary>Pure mapping seam shared by the focused verifier.</summary>
        public static bool TryCreateSubmission(AxiomKind kind, AxiomErrorState error,
            float quality, LawExpression? expression, out PhenomenonMasteryEvidenceSubmission submission)
        {
            LawPhenomenon phenomenon;
            MasteryEvidenceDimension dimension;
            submission = null;
            if (!TryMapPhenomenon(kind, out phenomenon) || !TryMapDimension(error.ErrorKind, out dimension)
                || error.Kind != kind || !IsFinite(error.Magnitude) || error.Magnitude <= 0f
                || !IsFinite(quality) || quality <= 0f) return false;

            // Intervention quality is already gated by AxiomControlState. Keep
            // the established magnitude contribution, bounded per success.
            submission = new PhenomenonMasteryEvidenceSubmission(phenomenon, dimension,
                Mathf.Clamp(error.Magnitude, .1f, 1f), true, expression);
            return true;
        }

        private static bool TryMapPhenomenon(AxiomKind kind, out LawPhenomenon phenomenon)
        {
            switch (kind)
            {
                case AxiomKind.Heat: phenomenon = LawPhenomenon.Heat; return true;
                case AxiomKind.Flow: phenomenon = LawPhenomenon.Flow; return true;
                case AxiomKind.Mass: phenomenon = LawPhenomenon.Mass; return true;
                case AxiomKind.Compression: phenomenon = LawPhenomenon.Compression; return true;
                case AxiomKind.Potential: phenomenon = LawPhenomenon.Potential; return true;
                case AxiomKind.Resonance: phenomenon = LawPhenomenon.Resonance; return true;
                case AxiomKind.Phase: phenomenon = LawPhenomenon.Phase; return true;
                case AxiomKind.Order: phenomenon = LawPhenomenon.Order; return true;
                default: phenomenon = default(LawPhenomenon); return false;
            }
        }

        private static bool TryMapDimension(AxiomErrorKind error, out MasteryEvidenceDimension dimension)
        {
            switch (error)
            {
                case AxiomErrorKind.State: dimension = MasteryEvidenceDimension.State; return true;
                case AxiomErrorKind.Rate: dimension = MasteryEvidenceDimension.Rate; return true;
                case AxiomErrorKind.Acceleration: dimension = MasteryEvidenceDimension.Acceleration; return true;
                default: dimension = default(MasteryEvidenceDimension); return false;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
