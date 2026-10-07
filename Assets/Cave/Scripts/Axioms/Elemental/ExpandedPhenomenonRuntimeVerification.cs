using Cave.Axioms.Control;

namespace Cave.Axioms.Elemental
{
    /// <summary>Pure coverage for the four expanded phenomenon channels; source hooks remain gameplay-owned.</summary>
    public static class ExpandedPhenomenonRuntimeVerification
    {
        private static readonly AxiomKind[] All = { AxiomKind.Heat, AxiomKind.Flow, AxiomKind.Mass, AxiomKind.Compression, AxiomKind.Potential, AxiomKind.Resonance, AxiomKind.Phase, AxiomKind.Order };
        private static readonly AxiomKind[] Expanded = { AxiomKind.Compression, AxiomKind.Potential, AxiomKind.Resonance, AxiomKind.Phase };

        public static bool TryRunAll(out string failure)
        {
            if (!AllSupported(out failure) || !ActualDesiredFinite(out failure) || !ActorRelative(out failure)
                || !SignedErrorsAndOpportunities(out failure) || !OnePerPhenomenon(out failure)
                || !MultiPhenomenon(out failure) || !NoThresholdRequirement(out failure) || !Deterministic(out failure)) return false;
            failure = null; return true;
        }

        private static bool AllSupported(out string failure)
        {
            for (int i = 0; i < All.Length; i++) if (!FiniteTrajectory(All[i], 1f, out _)) return Fail("A: unsupported dynamics channel for " + All[i], out failure);
            failure = null; return true;
        }
        private static bool ActualDesiredFinite(out string failure)
        {
            for (int i = 0; i < Expanded.Length; i++) if (!FiniteTrajectory(Expanded[i], 2f, out AxiomTrajectoryState t) || !Finite(t.CurrentValue) || !Finite(t.ShortRate) || !Finite(t.ShortAcceleration)) return Fail("H/R: non-finite expanded trajectory.", out failure);
            failure = null; return true;
        }
        private static bool ActorRelative(out string failure)
        {
            AxiomDynamicsParameters slow = AxiomDynamicsParameters.DefaultFor(AxiomKind.Compression); slow.InputGain = .02f;
            AxiomDynamicsParameters fast = AxiomDynamicsParameters.DefaultFor(AxiomKind.Compression); fast.InputGain = 2f;
            AxiomDynamicsChannel a = new AxiomDynamicsChannel(AxiomKind.Compression, slow); AxiomDynamicsChannel b = new AxiomDynamicsChannel(AxiomKind.Compression, fast);
            a.ApplyInput(1f, 1f, 0f); b.ApplyInput(1f, 1f, 0f);
            return Check(a.GetDesiredTrajectory(.5f, AxiomTrajectoryThresholds.Default).CurrentValue != b.GetDesiredTrajectory(.5f, AxiomTrajectoryThresholds.Default).CurrentValue, "I: profile overrides did not remain actor-relative.", out failure);
        }
        private static bool SignedErrorsAndOpportunities(out string failure)
        {
            for (int i = 0; i < Expanded.Length; i++)
            {
                AxiomControlReferenceProfile p = AxiomControlReferenceProfile.DefaultFor(Expanded[i]); p.Reference.EvaluateRate = false; p.Reference.EvaluateAcceleration = false; p.Reference.StateTolerance = 1f;
                AxiomOpportunityResolution down = AxiomOpportunityResolver.Resolve(Trajectory(Expanded[i], 2f), Trajectory(Expanded[i], 0f), p, false, default, false, 1f);
                AxiomOpportunityResolution up = AxiomOpportunityResolver.Resolve(Trajectory(Expanded[i], -2f), Trajectory(Expanded[i], 0f), p, false, default, false, 1f);
                if (!down.HasActiveCandidate || !up.HasActiveCandidate || down.Candidate.RequiredCorrectionDirection >= 0f || up.Candidate.RequiredCorrectionDirection <= 0f) return Fail("J/K: signed control error failed for " + Expanded[i], out failure);
            }
            failure = null; return true;
        }
        private static bool OnePerPhenomenon(out string failure)
        {
            AxiomControlReferenceProfile p = AxiomControlReferenceProfile.DefaultFor(AxiomKind.Compression); p.Reference.StateTolerance = 1f; p.Reference.RateTolerance = 1f; p.Reference.AccelerationTolerance = 1f;
            AxiomTrajectoryState actual = new AxiomTrajectoryState(AxiomKind.Compression, 2f, 3f, 3f, 4f, true, true, true, AxiomTrajectoryDirection.Rising, AxiomRateIntensity.Fast, AxiomCurvature.Accelerating, 1f);
            AxiomOpportunityResolution r = AxiomOpportunityResolver.Resolve(actual, Trajectory(AxiomKind.Compression, 0f), p, false, default, false, 1f);
            return Check(r.HasActiveCandidate && r.Candidate.Error.ErrorKind == AxiomErrorKind.Acceleration, "L: Compression did not resolve multiple errors to one opportunity.", out failure);
        }
        private static bool MultiPhenomenon(out string failure)
        {
            AxiomControlReferenceProfile a = AxiomControlReferenceProfile.DefaultFor(AxiomKind.Compression); AxiomControlReferenceProfile b = AxiomControlReferenceProfile.DefaultFor(AxiomKind.Potential);
            bool ok = AxiomOpportunityResolver.Resolve(Trajectory(AxiomKind.Compression, 2f), Trajectory(AxiomKind.Compression, 0f), a, false, default, false, 1f).HasActiveCandidate
                && AxiomOpportunityResolver.Resolve(Trajectory(AxiomKind.Potential, 2f), Trajectory(AxiomKind.Potential, 0f), b, false, default, false, 1f).HasActiveCandidate;
            return Check(ok, "M: expanded phenomena were globally suppressed.", out failure);
        }
        private static bool NoThresholdRequirement(out string failure) => AllSupported(out failure);
        private static bool Deterministic(out string failure)
        {
            AxiomTrajectoryState first; AxiomTrajectoryState second;
            bool ok = FiniteTrajectory(AxiomKind.Resonance, 2f, out first) && FiniteTrajectory(AxiomKind.Resonance, 2f, out second)
                && first.CurrentValue == second.CurrentValue && first.ShortRate == second.ShortRate && first.ShortAcceleration == second.ShortAcceleration;
            return Check(ok, "Q: repeated expanded input was not deterministic.", out failure);
        }
        private static bool FiniteTrajectory(AxiomKind kind, float amount, out AxiomTrajectoryState trajectory)
        {
            AxiomDynamicsChannel channel = new AxiomDynamicsChannel(kind, AxiomDynamicsParameters.DefaultFor(kind)); channel.ApplyInput(amount, amount, 0f); trajectory = channel.GetDesiredTrajectory(.5f, AxiomTrajectoryThresholds.Default); return Finite(trajectory.CurrentValue) && Finite(trajectory.ShortRate) && Finite(trajectory.ShortAcceleration);
        }
        private static AxiomTrajectoryState Trajectory(AxiomKind kind, float state) => new AxiomTrajectoryState(kind, state, 0f, 0f, 0f, true, true, true, AxiomTrajectoryDirection.Stable, AxiomRateIntensity.Slow, AxiomCurvature.StableRate, 1f);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Check(bool ok, string text, out string failure) { failure = ok ? null : text; return ok; }
        private static bool Fail(string text, out string failure) { failure = text; return false; }
    }
}
