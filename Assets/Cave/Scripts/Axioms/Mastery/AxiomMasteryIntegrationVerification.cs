using System;
using Cave.Axioms.Control;
using Cave.Axioms.Elemental;
using Cave.Domain;
using UnityEngine;

namespace Cave.Axioms.Mastery
{
    /// <summary>Focused deterministic coverage for Domain-control evidence attribution.</summary>
    public static class AxiomMasteryIntegrationVerification
    {
        private static readonly AxiomKind[] All =
        {
            AxiomKind.Heat, AxiomKind.Flow, AxiomKind.Mass, AxiomKind.Compression,
            AxiomKind.Potential, AxiomKind.Resonance, AxiomKind.Phase, AxiomKind.Order
        };

        public static bool TryRunAll(out string failure)
        {
            if (!SourceActionIsNotMastery(out failure) || !DimensionOnly(AxiomErrorKind.State, out failure)
                || !DimensionOnly(AxiomErrorKind.Rate, out failure) || !DimensionOnly(AxiomErrorKind.Acceleration, out failure)
                || !FailedAndNaturalPathsDoNotSubmit(out failure) || !DuplicateProtection(out failure)
                || !AllEight(out failure) || !PartialAndCompleteMastery(out failure)
                || !LegacyPercentageCannotBypass(out failure) || !ComplexityUsesControlMastery(out failure)
                || !ExpressionRegression(out failure) || !ReadOnlyQuery(out failure) || !BoundsAndDeterminism(out failure)) return false;
            failure = null;
            return true;
        }

        private static bool SourceActionIsNotMastery(out string failure)
        {
            GameObject source = new GameObject("MasteryEvidenceSource");
            GameObject actor = new GameObject("MasteryEvidenceActor");
            try
            {
                PlayerMasteryEvidenceRuntime evidence = source.AddComponent<PlayerMasteryEvidenceRuntime>();
                // Charged Heavy and charged projectile both feed Compression;
                // Brace-exit and parry feed Potential and Resonance respectively.
                bool applied = AxiomPhenomenonApplicationBridge.Apply(actor, AxiomKind.Compression, 1f, source, actor, 1f)
                    && AxiomPhenomenonApplicationBridge.Apply(actor, AxiomKind.Compression, 1f, source, actor, 2f)
                    && AxiomPhenomenonApplicationBridge.Apply(actor, AxiomKind.Potential, 1f, source, actor, 3f)
                    && AxiomPhenomenonApplicationBridge.Apply(actor, AxiomKind.Resonance, 1f, source, actor, 4f);
                PlayerMasteryEvidenceState state = evidence.Snapshot;
                bool untouched = !DomainControlMasteryQuery.GetPhenomenon(state, LawPhenomenon.Compression, PlayerMasteryPolicy.Default).IsDomainControlMastered
                    && !DomainControlMasteryQuery.GetPhenomenon(state, LawPhenomenon.Potential, PlayerMasteryPolicy.Default).IsDomainControlMastered
                    && !DomainControlMasteryQuery.GetPhenomenon(state, LawPhenomenon.Resonance, PlayerMasteryPolicy.Default).IsDomainControlMastered;
                return Require(applied && untouched, "A: a source action submitted Domain-control mastery evidence.", out failure);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(actor); }
        }

        private static bool DimensionOnly(AxiomErrorKind error, out string failure)
        {
            PhenomenonMasteryEvidenceSubmission submission;
            bool built = AxiomControlMasteryEvidenceBridge.TryCreateSubmission(
                AxiomKind.Heat, Error(AxiomKind.Heat, error, .5f), 1f, null, out submission);
            PlayerMasteryEvidenceState state = built
                ? PlayerMasteryEvidenceState.Empty.Submit(submission, PlayerMasteryPolicy.Default)
                : PlayerMasteryEvidenceState.Empty;
            MasteryChannelReport report = state.GetPhenomenonReport(LawPhenomenon.Heat, PlayerMasteryPolicy.Default);
            bool exact = error == AxiomErrorKind.State
                ? report.StateEvidence > 0f && report.RateEvidence == 0f && report.AccelerationEvidence == 0f
                : error == AxiomErrorKind.Rate
                    ? report.StateEvidence == 0f && report.RateEvidence > 0f && report.AccelerationEvidence == 0f
                    : report.StateEvidence == 0f && report.RateEvidence == 0f && report.AccelerationEvidence > 0f;
            return Require(built && exact, "B-D: successful " + error + " control did not submit exactly its dimension.", out failure);
        }

        private static bool FailedAndNaturalPathsDoNotSubmit(out string failure)
        {
            PhenomenonMasteryEvidenceSubmission ignored;
            bool wrong = !AxiomControlMasteryEvidenceBridge.TryCreateSubmission(
                AxiomKind.Heat, Error(AxiomKind.Heat, AxiomErrorKind.None, 1f), 1f, null, out ignored);
            bool natural = !AxiomControlMasteryEvidenceBridge.TryCreateSubmission(
                AxiomKind.Heat, Error(AxiomKind.Heat, AxiomErrorKind.State, 0f), 1f, null, out ignored);
            bool threshold = !AxiomControlMasteryEvidenceBridge.TryCreateSubmission(
                AxiomKind.Heat, Error(AxiomKind.Heat, AxiomErrorKind.State, 1f), 0f, null, out ignored);
            return Require(wrong && natural && threshold, "E-G: a non-success control path created evidence.", out failure);
        }

        private static bool DuplicateProtection(out string failure)
        {
            GameObject source = new GameObject("MasteryControlSource");
            GameObject actor = new GameObject("MasteryControlActor");
            try
            {
                PlayerMasteryEvidenceRuntime evidence = source.AddComponent<PlayerMasteryEvidenceRuntime>();
                AxiomRuntimeState runtime = actor.AddComponent<AxiomRuntimeState>();
                AxiomDynamicsState dynamics = AxiomDynamicsState.EnsureOn(actor);
                AxiomControlState control = AxiomControlState.EnsureOn(actor);
                // Seed State just outside the inclusive default State band.
                // The control impulse must actually enter the band before this
                // fixture can receive its one evidence submission.
                AxiomDynamicResponse ignored;
                dynamics.ApplyControlCorrection(AxiomKind.Heat, AxiomErrorKind.State,
                    1f, 1.05f, 1f, out ignored);
                control.EvaluateAndRefresh(runtime, dynamics, AxiomKind.Heat,
                    AxiomOpportunityCandidateSource.TrajectoryDeviation, 1f, source, actor);
                AxiomControlOpportunity opportunity;
                if (!control.TryGetActiveOpportunity(AxiomKind.Heat, 1f, out opportunity))
                    return Require(false, "H: fixture did not create an opportunity.", out failure);
                bool first = control.TryIntervene(runtime, dynamics, AxiomKind.Heat,
                    opportunity.RequiredCorrectionDirection, 1f, 1f, source, actor);
                float afterFirst = evidence.Snapshot.GetPhenomenonReport(LawPhenomenon.Heat, PlayerMasteryPolicy.Default).StateEvidence;
                bool second = control.TryIntervene(runtime, dynamics, AxiomKind.Heat,
                    opportunity.RequiredCorrectionDirection, 1f, 1f, source, actor);
                float afterSecond = evidence.Snapshot.GetPhenomenonReport(LawPhenomenon.Heat, PlayerMasteryPolicy.Default).StateEvidence;
                return Require(first && !second && afterFirst > 0f && afterFirst == afterSecond,
                    "H: one opportunity awarded more than one evidence unit.", out failure);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(actor); }
        }

        private static bool AllEight(out string failure)
        {
            for (int index = 0; index < All.Length; index++)
            {
                PhenomenonMasteryEvidenceSubmission submission;
                AxiomKind kind = All[index];
                if (!AxiomControlMasteryEvidenceBridge.TryCreateSubmission(kind,
                    Error(kind, AxiomErrorKind.Acceleration, .5f), 1f, null, out submission)
                    || submission.Dimension != MasteryEvidenceDimension.Acceleration) return Require(false,
                        "I: " + kind + " did not map to typed acceleration evidence.", out failure);
            }
            failure = null;
            return true;
        }

        private static bool PartialAndCompleteMastery(out string failure)
        {
            PlayerMasteryEvidenceState partial = SubmitDimensions(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Heat, false, null);
            PlayerMasteryEvidenceState complete = SubmitDimensions(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Heat, true, LawExpression.Projectile);
            bool expected = !DomainControlMasteryQuery.GetPhenomenon(partial, LawPhenomenon.Heat, PlayerMasteryPolicy.Default).IsDomainControlMastered
                && !DomainControlMasteryQuery.GetPhenomenon(partial, LawPhenomenon.Heat, PlayerMasteryPolicy.Default).AccelerationMastered
                && DomainControlMasteryQuery.GetPhenomenon(complete, LawPhenomenon.Heat, PlayerMasteryPolicy.Default).IsDomainControlMastered;
            return Require(expected, "J-K: partial/full Domain-control mastery was incorrect.", out failure);
        }

        private static bool LegacyPercentageCannotBypass(out string failure)
        {
            GameObject legacyOwner = new GameObject("LegacyFamiliarity");
            try
            {
                AxiomMasteryState familiarity = legacyOwner.AddComponent<AxiomMasteryState>();
                familiarity.Record(new MasteryEvidence(MasteryDomain.Heat, MasteryEvidenceKind.OrdinaryUse, 1f, 100f, 1f));
                DomainLaw law; LawValidationResult validation;
                DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, out law, out validation);
                bool blocked = !DomainMasteryEligibility.Evaluate(law, PlayerMasteryEvidenceState.Empty, PlayerMasteryPolicy.Default).IsEligible;
                PlayerMasteryEvidenceState complete = SubmitDimensions(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Heat, true, LawExpression.Projectile);
                bool allowed = DomainMasteryEligibility.Evaluate(law, complete, PlayerMasteryPolicy.Default).IsEligible;
                return Require(familiarity.Get(MasteryDomain.Heat) == 1f && blocked && allowed,
                    "L-M: legacy familiarity bypassed or blocked evidence-authoritative authoring.", out failure);
            }
            finally { UnityEngine.Object.DestroyImmediate(legacyOwner); }
        }

        private static bool ComplexityUsesControlMastery(out string failure)
        {
            PlayerMasteryEvidenceState mastery = SubmitDimensions(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Heat, true, LawExpression.Projectile);
            mastery = SubmitDimensions(mastery, LawPhenomenon.Flow, true, LawExpression.Projectile);
            DomainComplexityCapacityEvaluation evaluation = DomainComplexityCapacityProgression.Evaluate(
                true, mastery, PlayerMasteryPolicy.Default, DomainComplexityCapacityTier.Seed);
            return Require(evaluation.QualifiedTier >= DomainComplexityCapacityTier.TierII,
                "N: complexity did not count fully controlled phenomena.", out failure);
        }

        private static bool ExpressionRegression(out string failure)
        {
            PlayerMasteryEvidenceState projectile = SubmitDimensions(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Heat, true, LawExpression.Projectile);
            PlayerMasteryEvidenceState frenzy = PlayerMasteryEvidenceState.Empty
                .SubmitFrenzy(new FrenzyMasterySample(0f, 0, 1), PlayerMasteryPolicy.Default)
                .SubmitFrenzy(new FrenzyMasterySample(0f, 0, 1), PlayerMasteryPolicy.Default);
            return Require(projectile.GetProjectileReport(PlayerMasteryPolicy.Default).IsMastered
                    && projectile.FrenzyEvidence == 0f && frenzy.IsFrenzyMastered(PlayerMasteryPolicy.Default),
                "O-P: Projectile or Frenzy evidence semantics changed.", out failure);
        }

        private static bool ReadOnlyQuery(out string failure)
        {
            PlayerMasteryEvidenceState state = SubmitDimensions(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Phase, false, null);
            DomainPhenomenonMasteryProgress progress = DomainControlMasteryQuery.GetPhenomenon(
                state, LawPhenomenon.Phase, PlayerMasteryPolicy.Default);
            return Require(progress.StateMastered && progress.RateMastered && !progress.AccelerationMastered
                    && !progress.IsDomainControlMastered,
                "S: read-only blocker progress was incomplete or ambiguous.", out failure);
        }

        private static bool BoundsAndDeterminism(out string failure)
        {
            PlayerMasteryEvidenceState first = PlayerMasteryEvidenceState.Empty;
            PlayerMasteryEvidenceState second = PlayerMasteryEvidenceState.Empty;
            for (int index = 0; index < 16; index++)
            {
                PhenomenonMasteryEvidenceSubmission submission;
                AxiomControlMasteryEvidenceBridge.TryCreateSubmission(AxiomKind.Order,
                    Error(AxiomKind.Order, AxiomErrorKind.State, 1000f), 1f, null, out submission);
                first = first.Submit(submission, PlayerMasteryPolicy.Default);
                second = second.Submit(submission, PlayerMasteryPolicy.Default);
            }
            float left = first.GetPhenomenonReport(LawPhenomenon.Order, PlayerMasteryPolicy.Default).StateEvidence;
            float right = second.GetPhenomenonReport(LawPhenomenon.Order, PlayerMasteryPolicy.Default).StateEvidence;
            return Require(left == PlayerMasteryPolicy.Default.StateThreshold && left == right
                    && !float.IsNaN(left) && !float.IsInfinity(left),
                "T: successful-control evidence was unbounded or nondeterministic.", out failure);
        }

        private static PlayerMasteryEvidenceState SubmitDimensions(PlayerMasteryEvidenceState state,
            LawPhenomenon phenomenon, bool acceleration, LawExpression? expression)
        {
            state = state.Submit(new PhenomenonMasteryEvidenceSubmission(phenomenon,
                MasteryEvidenceDimension.State, 1f, true, expression), PlayerMasteryPolicy.Default);
            state = state.Submit(new PhenomenonMasteryEvidenceSubmission(phenomenon,
                MasteryEvidenceDimension.Rate, 1f, true, expression), PlayerMasteryPolicy.Default);
            return acceleration ? state.Submit(new PhenomenonMasteryEvidenceSubmission(phenomenon,
                MasteryEvidenceDimension.Acceleration, 1f, true, expression), PlayerMasteryPolicy.Default) : state;
        }

        private static AxiomErrorState Error(AxiomKind kind, AxiomErrorKind dimension, float magnitude)
        {
            return new AxiomErrorState(kind, dimension, magnitude, 1f);
        }

        private static bool Require(bool condition, string text, out string failure)
        {
            failure = condition ? null : text;
            return condition;
        }
    }
}
