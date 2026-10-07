using System;
using Cave.Axioms.Mastery;
using Cave.Domain;

namespace Cave.UI
{
    /// <summary>Deterministic coverage for contextual Domain presentation data.</summary>
    public static class DomainContextualUiVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!FamiliarityCannotAuthorize(out failure) || !PartialAndFullPhenomenon(out failure)
                || !ExpressionContexts(out failure) || !TerritoryAndComposition(out failure)
                || !CapacityAndMultipleBlockers(out failure) || !AuthorableAndLocked(out failure)
                || !ContextSwitchAndDeterminism(out failure)) return false;
            failure = null;
            return true;
        }

        private static bool FamiliarityCannotAuthorize(out string failure)
        {
            DomainContextualUiModel model = Build(DomainContextFocus.Phenomenon,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation,
                PlayerMasteryEvidenceState.Empty, true, 10f);
            return Require(!model.CanAuthor && Has(model, DomainAuthoringBlockerKind.PhenomenonStateIncomplete)
                    && !model.ContextBody.Contains("Familiarity"),
                "A: contextual UI treated or displayed familiarity as Domain mastery.", out failure);
        }

        private static bool PartialAndFullPhenomenon(out string failure)
        {
            PlayerMasteryEvidenceState partial = Phenomenon(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Heat, false, null);
            DomainContextualUiModel partialModel = Build(DomainContextFocus.Phenomenon,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, partial, true, 10f);
            PlayerMasteryEvidenceState complete = Phenomenon(PlayerMasteryEvidenceState.Empty, LawPhenomenon.Heat, true, LawExpression.Projectile);
            DomainContextualUiModel completeModel = Build(DomainContextFocus.Phenomenon,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, complete, true, 10f);
            return Require(Has(partialModel, DomainAuthoringBlockerKind.PhenomenonAccelerationIncomplete)
                    && partialModel.ContextBody.Contains("ACCELERATION  INCOMPLETE")
                    && !Has(completeModel, DomainAuthoringBlockerKind.PhenomenonAccelerationIncomplete)
                    && completeModel.ContextBody.Contains("Domain Mastery: MASTERED"),
                "B-C: phenomenon S/R/A presentation was not authoritative.", out failure);
        }

        private static bool ExpressionContexts(out string failure)
        {
            DomainContextualUiModel projectile = Build(DomainContextFocus.Expression,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation,
                PlayerMasteryEvidenceState.Empty, true, 10f);
            PlayerMasteryEvidenceState frenzyEvidence = PlayerMasteryEvidenceState.Empty
                .SubmitFrenzy(new FrenzyMasterySample(0f, 0, 1), PlayerMasteryPolicy.Default);
            DomainContextualUiModel frenzy = Build(DomainContextFocus.Expression,
                LawExpression.Frenzy, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation,
                frenzyEvidence, true, 10f);
            return Require(projectile.ContextBody.Contains("PROJECTILE CONTROL")
                    && Has(projectile, DomainAuthoringBlockerKind.ProjectileStateIncomplete)
                    && frenzy.ContextBody.Contains("5 / 10")
                    && Has(frenzy, DomainAuthoringBlockerKind.FrenzyMasteryIncomplete),
                "D-E: expression mastery context was incorrect.", out failure);
        }

        private static bool TerritoryAndComposition(out string failure)
        {
            DomainContextualUiModel model = Build(DomainContextFocus.Territory,
                LawExpression.Frenzy, LawPhenomenon.Compression, LawTerritoryPrinciple.Propagation,
                PlayerMasteryEvidenceState.Empty, true, 10f);
            return Require(model.ContextTitle == "PROPAGATION PRINCIPLE"
                    && model.ContextBody.Contains("no separate mastery track")
                    && !model.ContextBody.Contains("CONTROL MASTERY")
                    && model.LawPreview.Contains("FRENZY") && model.LawPreview.Contains("COMPRESSION")
                    && model.LawPreview.Contains("PROPAGATION"),
                "F-G: territory context or Law preview was incorrect.", out failure);
        }

        private static bool CapacityAndMultipleBlockers(out string failure)
        {
            DomainContextualUiModel exceeded = Build(DomainContextFocus.Phenomenon,
                LawExpression.Frenzy, LawPhenomenon.Compression, LawTerritoryPrinciple.Propagation,
                PlayerMasteryEvidenceState.Empty, true, 0f);
            return Require(!exceeded.CanAuthor && Has(exceeded, DomainAuthoringBlockerKind.ComplexityExceeded)
                    && Has(exceeded, DomainAuthoringBlockerKind.PhenomenonAccelerationIncomplete)
                    && Has(exceeded, DomainAuthoringBlockerKind.FrenzyMasteryIncomplete)
                    && exceeded.Blockers[0].Kind == DomainAuthoringBlockerKind.PhenomenonStateIncomplete,
                "H-J: capacity or stable multiple blockers were incorrect.", out failure);
        }

        private static bool AuthorableAndLocked(out string failure)
        {
            PlayerMasteryEvidenceState evidence = Phenomenon(PlayerMasteryEvidenceState.Empty,
                LawPhenomenon.Heat, true, LawExpression.Projectile);
            DomainContextualUiModel authorable = Build(DomainContextFocus.Phenomenon,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation,
                evidence, true, 10f);
            DomainContextualUiModel locked = Build(DomainContextFocus.Phenomenon,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation,
                evidence, false, 10f);
            return Require(authorable.CanAuthor && authorable.Blockers.Count == 0
                    && !locked.CanAuthor && Has(locked, DomainAuthoringBlockerKind.SeedLocked),
                "K-L: authorable or Seed-locked behavior diverged from eligibility.", out failure);
        }

        private static bool ContextSwitchAndDeterminism(out string failure)
        {
            PlayerMasteryEvidenceState evidence = PlayerMasteryEvidenceState.Empty;
            DomainContextualUiModel expression = Build(DomainContextFocus.Expression,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, evidence, true, 1f);
            DomainContextualUiModel phenomenon = Build(DomainContextFocus.Phenomenon,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, evidence, true, 1f);
            DomainContextualUiModel territory = Build(DomainContextFocus.Territory,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, evidence, true, 1f);
            DomainContextualUiModel repeated = Build(DomainContextFocus.Territory,
                LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation, evidence, true, 1f);
            return Require(expression.Focus == DomainContextFocus.Expression && phenomenon.Focus == DomainContextFocus.Phenomenon
                    && territory.Focus == DomainContextFocus.Territory && territory.ContextBody == repeated.ContextBody
                    && territory.Blockers.Count == repeated.Blockers.Count,
                "N-Q: context switching or repeated read-only presentation was nondeterministic.", out failure);
        }

        private static DomainContextualUiModel Build(DomainContextFocus focus, LawExpression expression,
            LawPhenomenon phenomenon, LawTerritoryPrinciple territory, PlayerMasteryEvidenceState evidence,
            bool seed, float capacity)
        {
            return DomainContextualUiPresenter.Build(focus, expression, phenomenon, territory,
                DomainComposition.Empty, evidence, PlayerMasteryPolicy.Default,
                DomainComplexityPolicy.Default, seed, capacity);
        }

        private static PlayerMasteryEvidenceState Phenomenon(PlayerMasteryEvidenceState state,
            LawPhenomenon phenomenon, bool acceleration, LawExpression? expression)
        {
            state = state.Submit(new PhenomenonMasteryEvidenceSubmission(phenomenon,
                MasteryEvidenceDimension.State, 1f, true, expression), PlayerMasteryPolicy.Default);
            state = state.Submit(new PhenomenonMasteryEvidenceSubmission(phenomenon,
                MasteryEvidenceDimension.Rate, 1f, true, expression), PlayerMasteryPolicy.Default);
            return acceleration ? state.Submit(new PhenomenonMasteryEvidenceSubmission(phenomenon,
                MasteryEvidenceDimension.Acceleration, 1f, true, expression), PlayerMasteryPolicy.Default) : state;
        }

        private static bool Has(DomainContextualUiModel model, DomainAuthoringBlockerKind kind)
        {
            for (int index = 0; index < model.Blockers.Count; index++)
                if (model.Blockers[index].Kind == kind) return true;
            return false;
        }

        private static bool Require(bool condition, string message, out string failure)
        { failure = condition ? null : message; return condition; }
    }
}
