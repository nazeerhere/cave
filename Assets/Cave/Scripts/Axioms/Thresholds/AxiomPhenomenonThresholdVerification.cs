using Cave.Axioms.Control;

namespace Cave.Axioms.Thresholds
{
    /// <summary>Pure deterministic coverage for threshold landmarks and their resolver seam.</summary>
    public static class AxiomPhenomenonThresholdVerification
    {
        private const float Time = 5f;

        public static bool TryRunAll(out string failure)
        {
            if (!NoConfiguration(out failure) || !ProximityApproaching(out failure) || !NearMovingAway(out failure)
                || !CrossingOnce(out failure) || !RemainBeyond(out failure) || !ReverseCrossing(out failure)
                || !Deadband(out failure) || !DirectionAuthority(out failure) || !ZeroError(out failure)
                || !SourceCompetition(out failure) || !NaturalCrossing(out failure) || !ManifestationSeam(out failure)
                || !ActorRelativity(out failure) || !FiniteRepeatable(out failure)) return false;
            failure = null; return true;
        }

        private static bool NoConfiguration(out string failure)
        {
            AxiomPhenomenonThresholdDefinition absent = default(AxiomPhenomenonThresholdDefinition);
            AxiomPhenomenonThresholdEvaluator evaluator = new AxiomPhenomenonThresholdEvaluator(absent);
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            AxiomThresholdSnapshot snapshot = evaluator.Evaluate(Trajectory(2f, 1f), Time, out context, out request);
            return Check(snapshot.Region == AxiomThresholdRegion.Unconfigured && !context.IsActive && request.Manifestation == AxiomThresholdManifestation.None, "A: an unconfigured threshold produced state.", out failure);
        }

        private static bool ProximityApproaching(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            evaluator.Evaluate(Trajectory(4.8f, 1f), Time, out context, out request);
            AxiomOpportunityResolution result = Resolve(Trajectory(2f, 2f), Trajectory(0f, 0f), context);
            return Check(context.Source == AxiomOpportunityCandidateSource.ThresholdProximity
                    && result.HasActiveCandidate && result.Candidate.RequiredCorrectionDirection < 0f,
                "B: approaching proximity did not produce a real threshold candidate with authoritative direction.", out failure);
        }

        private static bool NearMovingAway(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4.8f, -1f), Time, out context, out request);
            return Check(!context.IsActive, "C: near-but-receding trajectory was classified as approaching.", out failure);
        }

        private static bool CrossingOnce(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            AxiomThresholdSnapshot snapshot = evaluator.Evaluate(Trajectory(5.3f, 1f), Time, out context, out request);
            return Check(snapshot.LatestCrossing == AxiomThresholdCrossingDirection.Rising
                    && context.Source == AxiomOpportunityCandidateSource.ThresholdCrossing,
                "D: rising crossing was not emitted exactly as a threshold crossing.", out failure);
        }

        private static bool RemainBeyond(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            evaluator.Evaluate(Trajectory(5.3f, 1f), 1f, out context, out request);
            AxiomThresholdSnapshot snapshot = evaluator.Evaluate(Trajectory(5.6f, .2f), 2f, out context, out request);
            return Check(snapshot.LatestCrossing == AxiomThresholdCrossingDirection.None && context.Source != AxiomOpportunityCandidateSource.ThresholdCrossing,
                "E: remaining beyond a boundary emitted a duplicate crossing.", out failure);
        }

        private static bool ReverseCrossing(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            evaluator.Evaluate(Trajectory(5.3f, 1f), 1f, out context, out request);
            AxiomThresholdSnapshot snapshot = evaluator.Evaluate(Trajectory(4.7f, -1f), 2f, out context, out request);
            return Check(snapshot.LatestCrossing == AxiomThresholdCrossingDirection.Falling && context.Source == AxiomOpportunityCandidateSource.ThresholdCrossing,
                "F: reverse crossing was not emitted once with falling direction.", out failure);
        }

        private static bool Deadband(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 0f), 0f, out context, out request);
            evaluator.Evaluate(Trajectory(4.95f, .1f), 1f, out context, out request);
            evaluator.Evaluate(Trajectory(5.05f, -.1f), 2f, out context, out request);
            AxiomThresholdSnapshot snapshot = evaluator.Evaluate(Trajectory(4.98f, .1f), 3f, out context, out request);
            return Check(snapshot.Region == AxiomThresholdRegion.Below && snapshot.LatestCrossing == AxiomThresholdCrossingDirection.None,
                "G: deadband oscillation caused threshold-region chatter.", out failure);
        }

        private static bool DirectionAuthority(out string failure)
        {
            AxiomThresholdOpportunityContext threshold = new AxiomThresholdOpportunityContext(AxiomOpportunityCandidateSource.ThresholdCrossing, 4f);
            AxiomOpportunityResolution down = Resolve(Trajectory(2f, 0f), Trajectory(0f, 0f), threshold);
            AxiomOpportunityResolution up = Resolve(Trajectory(-2f, 0f), Trajectory(0f, 0f), threshold);
            return Check(down.Candidate.RequiredCorrectionDirection < 0f && up.Candidate.RequiredCorrectionDirection > 0f,
                "H: threshold policy changed the authoritative correction direction.", out failure);
        }

        private static bool ZeroError(out string failure)
        {
            AxiomThresholdOpportunityContext threshold = new AxiomThresholdOpportunityContext(AxiomOpportunityCandidateSource.ThresholdCrossing, 4f);
            AxiomOpportunityResolution result = Resolve(Trajectory(0f, 0f), Trajectory(0f, 0f), threshold);
            return Check(!result.HasActiveCandidate, "I: threshold crossing manufactured an opportunity without control error.", out failure);
        }

        private static bool SourceCompetition(out string failure)
        {
            AxiomThresholdOpportunityContext threshold = new AxiomThresholdOpportunityContext(AxiomOpportunityCandidateSource.ThresholdProximity, 2f);
            AxiomOpportunityResolution result = AxiomOpportunityResolver.Resolve(Trajectory(2f, 3f), Trajectory(0f, 0f), Profile(), false, default(AxiomControlOpportunity), true, threshold, Time);
            return Check(result.HasActiveCandidate && result.Candidate.Source == AxiomOpportunityCandidateSource.ThresholdProximity,
                "J: source competition did not retain one deterministic Heat candidate.", out failure);
        }

        private static bool NaturalCrossing(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            AxiomThresholdSnapshot snapshot = evaluator.Evaluate(Trajectory(5.3f, 1f), Time, out context, out request);
            return Check(snapshot.LatestCrossing == AxiomThresholdCrossingDirection.Rising && context.IsActive,
                "K: natural trajectory crossing was not observed independently of intervention/mastery.", out failure);
        }

        private static bool ManifestationSeam(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator(AxiomThresholdManifestation.Burn);
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            evaluator.Evaluate(Trajectory(5.3f, 1f), 1f, out context, out request);
            bool activated = request.Manifestation == AxiomThresholdManifestation.Burn && request.Active;
            evaluator.Evaluate(Trajectory(5.5f, .2f), 2f, out context, out request);
            bool noSpam = request.Manifestation == AxiomThresholdManifestation.None;
            evaluator.Evaluate(Trajectory(4.7f, -1f), 3f, out context, out request);
            return Check(activated && noSpam && request.Manifestation == AxiomThresholdManifestation.Burn && !request.Active,
                "L: manifestation seam did not emit one activation and one deactivation request.", out failure);
        }

        private static bool ActorRelativity(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator first = NewEvaluator(); AxiomPhenomenonThresholdEvaluator second = NewEvaluator();
            AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            first.Evaluate(Trajectory(4f, 1f), 0f, out context, out request); second.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            AxiomThresholdSnapshot left = first.Evaluate(Trajectory(4.8f, 1f), 1f, out context, out request);
            AxiomThresholdSnapshot right = second.Evaluate(Trajectory(5.3f, 1f), 1f, out context, out request);
            return Check(left.LatestCrossing == AxiomThresholdCrossingDirection.None && right.LatestCrossing == AxiomThresholdCrossingDirection.Rising,
                "M: actor-local trajectories were collapsed into a global crossing outcome.", out failure);
        }

        private static bool FiniteRepeatable(out string failure)
        {
            AxiomPhenomenonThresholdEvaluator evaluator = NewEvaluator(); AxiomThresholdOpportunityContext context; AxiomThresholdManifestationRequest request;
            evaluator.Evaluate(Trajectory(4f, 1f), 0f, out context, out request);
            for (int index = 0; index < 64; index++)
            {
                AxiomThresholdSnapshot snapshot = evaluator.Evaluate(Trajectory(4.8f, 1f), index + 1f, out context, out request);
                if (float.IsNaN(snapshot.ObservedAt) || float.IsInfinity(snapshot.ObservedAt) || snapshot.LatestCrossing != AxiomThresholdCrossingDirection.None)
                    return Check(false, "N: threshold observation became non-finite or emitted unbounded repeat crossings.", out failure);
            }
            failure = null; return true;
        }

        private static AxiomPhenomenonThresholdEvaluator NewEvaluator(AxiomThresholdManifestation manifestation = AxiomThresholdManifestation.None)
        {
            return new AxiomPhenomenonThresholdEvaluator(new AxiomPhenomenonThresholdDefinition { Enabled = true, Kind = AxiomKind.Heat, Identity = AxiomThresholdIdentity.Primary, Boundary = 5f, ProximityDistance = .5f, MinimumApproachRate = .1f, Hysteresis = .2f, SignificanceMultiplier = 2f, AboveManifestation = manifestation });
        }
        private static AxiomOpportunityResolution Resolve(AxiomTrajectoryState actual, AxiomTrajectoryState desired, AxiomThresholdOpportunityContext context)
        {
            return AxiomOpportunityResolver.Resolve(actual, desired, Profile(), false, default(AxiomControlOpportunity), false, context, Time);
        }
        private static AxiomControlReferenceProfile Profile()
        {
            AxiomControlReferenceProfile profile = AxiomControlReferenceProfile.DefaultFor(AxiomKind.Heat);
            profile.Reference.StateTolerance = 1f; profile.Reference.RateTolerance = 1f; profile.Reference.AccelerationTolerance = 1f;
            return profile;
        }
        private static AxiomTrajectoryState Trajectory(float state, float rate)
        {
            return new AxiomTrajectoryState(AxiomKind.Heat, state, rate, rate, 0f, true, true, false,
                AxiomTrajectoryDirection.Stable, AxiomRateIntensity.Slow, AxiomCurvature.StableRate, Time);
        }
        private static bool Check(bool condition, string message, out string failure) { failure = condition ? null : message; return condition; }
    }
}
