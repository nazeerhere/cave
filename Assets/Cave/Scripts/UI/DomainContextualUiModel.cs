using System;
using System.Collections.Generic;
using Cave.Axioms.Mastery;
using Cave.Domain;

namespace Cave.UI
{
    public enum DomainContextFocus { Expression, Phenomenon, Territory }
    public enum DomainAuthoringBlockerKind
    {
        AuthoringContextRequired, SeedLocked, ExpressionUnavailable,
        PhenomenonStateIncomplete, PhenomenonRateIncomplete, PhenomenonAccelerationIncomplete,
        ProjectileStateIncomplete, ProjectileRateIncomplete, ProjectileAccelerationIncomplete,
        FrenzyMasteryIncomplete, ComplexityExceeded, DuplicateLaw, InvalidLaw
    }

    public sealed class DomainAuthoringBlocker
    {
        internal DomainAuthoringBlocker(DomainAuthoringBlockerKind kind, string detail)
        { Kind = kind; Detail = detail; }
        public DomainAuthoringBlockerKind Kind { get; }
        public string Detail { get; }
    }

    /// <summary>Immutable, read-only rendering input composed from existing authorities.</summary>
    public sealed class DomainContextualUiModel
    {
        internal DomainContextualUiModel(DomainContextFocus focus, string title, string body,
            string lawPreview, float complexity, float capacity, bool hasComplexity,
            DomainAuthoringEligibilityResult eligibility, IReadOnlyList<DomainAuthoringBlocker> blockers)
        {
            Focus = focus; ContextTitle = title; ContextBody = body; LawPreview = lawPreview;
            Complexity = complexity; Capacity = capacity; HasComplexity = hasComplexity;
            Eligibility = eligibility; Blockers = blockers;
        }
        public DomainContextFocus Focus { get; }
        public string ContextTitle { get; }
        public string ContextBody { get; }
        public string LawPreview { get; }
        public float Complexity { get; }
        public float Capacity { get; }
        public bool HasComplexity { get; }
        public DomainAuthoringEligibilityResult Eligibility { get; }
        public IReadOnlyList<DomainAuthoringBlocker> Blockers { get; }
        public bool CanAuthor => Eligibility != null && Eligibility.IsEligible;
    }

    /// <summary>
    /// Presentation-only adapter for the Domain page. It reads existing mastery,
    /// composition, capacity, and eligibility authorities; it never mutates them.
    /// </summary>
    public static class DomainContextualUiPresenter
    {
        public static DomainContextualUiModel Build(DomainContextFocus focus, LawExpression expression,
            LawPhenomenon phenomenon, LawTerritoryPrinciple territory, DomainComposition composition,
            PlayerMasteryEvidenceState evidence, PlayerMasteryPolicy masteryPolicy,
            DomainComplexityPolicy complexityPolicy, bool hasSeed, float capacity)
        {
            PlayerMasteryPolicy activeMasteryPolicy = masteryPolicy ?? PlayerMasteryPolicy.Default;
            DomainComplexityPolicy activeComplexityPolicy = complexityPolicy ?? DomainComplexityPolicy.Default;
            PlayerMasteryEvidenceState activeEvidence = evidence ?? PlayerMasteryEvidenceState.Empty;
            DomainComposition current = composition ?? DomainComposition.Empty;
            DomainLaw candidate;
            LawValidationResult validation;
            bool validLaw = DomainLaw.TryCreate(expression, phenomenon, territory, out candidate, out validation);
            DomainAuthoringContext context = new DomainAuthoringContext(hasSeed, activeEvidence, capacity,
                activeMasteryPolicy, activeComplexityPolicy);
            DomainAuthoringEligibilityResult eligibility = validLaw
                ? DomainAuthoringEligibility.Evaluate(current, candidate, context)
                : DomainAuthoringEligibility.Evaluate(current, null, context);
            List<DomainAuthoringBlocker> blockers = BuildBlockers(validLaw, candidate, current, context,
                eligibility, activeEvidence, activeMasteryPolicy, activeComplexityPolicy);

            float complexity = 0f;
            bool hasComplexity = false;
            if (validLaw && !current.Contains(candidate))
            {
                DomainComposition proposed = DomainCompositionEditor.Add(current, candidate).Resulting;
                complexity = DomainComplexityCalculator.Calculate(proposed, activeComplexityPolicy).TotalComplexity;
                hasComplexity = true;
            }

            string title;
            string body;
            BuildContext(focus, expression, phenomenon, territory, activeEvidence, activeMasteryPolicy,
                out title, out body);
            return new DomainContextualUiModel(focus, title, body,
                expression.ToString().ToUpperInvariant() + "  +  " + phenomenon.ToString().ToUpperInvariant()
                    + "  +  " + territory.ToString().ToUpperInvariant(),
                complexity, capacity, hasComplexity, eligibility, blockers.AsReadOnly());
        }

        private static List<DomainAuthoringBlocker> BuildBlockers(bool validLaw, DomainLaw candidate,
            DomainComposition current, DomainAuthoringContext context,
            DomainAuthoringEligibilityResult eligibility, PlayerMasteryEvidenceState evidence,
            PlayerMasteryPolicy policy, DomainComplexityPolicy complexityPolicy)
        {
            List<DomainAuthoringBlocker> blockers = new List<DomainAuthoringBlocker>();
            if (context == null || context.MasteryPolicy == null || context.ComplexityPolicy == null)
            {
                blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.AuthoringContextRequired,
                    "Authoring context required."));
                return blockers;
            }
            if (!context.HasDomainSeed)
                blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.SeedLocked,
                    "A Domain Seed is required."));
            if (!validLaw)
            {
                blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.InvalidLaw,
                    "Selected Law is structurally invalid."));
                return blockers;
            }
            if (current.Contains(candidate))
                blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.DuplicateLaw,
                    "This Law is already authored."));

            AppendPhenomenonBlockers(blockers, candidate.Phenomenon,
                DomainControlMasteryQuery.GetPhenomenon(evidence, candidate.Phenomenon, policy));
            AppendExpressionBlockers(blockers, candidate.Expression, evidence, policy);

            if (!current.Contains(candidate))
            {
                DomainComposition proposed = DomainCompositionEditor.Add(current, candidate).Resulting;
                float cost = DomainComplexityCalculator.Calculate(proposed, complexityPolicy).TotalComplexity;
                if (cost > context.ComplexityCapacity)
                    blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.ComplexityExceeded,
                        "Complexity " + Format(cost) + " exceeds Capacity " + Format(context.ComplexityCapacity) + "."));
            }
            return blockers;
        }

        private static void AppendPhenomenonBlockers(List<DomainAuthoringBlocker> blockers,
            LawPhenomenon phenomenon, DomainPhenomenonMasteryProgress progress)
        {
            string name = phenomenon.ToString();
            if (!progress.StateMastered) blockers.Add(new DomainAuthoringBlocker(
                DomainAuthoringBlockerKind.PhenomenonStateIncomplete, name + " State control incomplete."));
            if (!progress.RateMastered) blockers.Add(new DomainAuthoringBlocker(
                DomainAuthoringBlockerKind.PhenomenonRateIncomplete, name + " Rate control incomplete."));
            if (!progress.AccelerationMastered) blockers.Add(new DomainAuthoringBlocker(
                DomainAuthoringBlockerKind.PhenomenonAccelerationIncomplete, name + " Acceleration control incomplete."));
        }

        private static void AppendExpressionBlockers(List<DomainAuthoringBlocker> blockers,
            LawExpression expression, PlayerMasteryEvidenceState evidence, PlayerMasteryPolicy policy)
        {
            if (expression == LawExpression.Projectile)
            {
                MasteryChannelReport report = evidence.GetProjectileReport(policy);
                if (!report.StateLock) blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.ProjectileStateIncomplete, "Projectile State control incomplete."));
                if (!report.RateLock) blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.ProjectileRateIncomplete, "Projectile Rate control incomplete."));
                if (!report.AccelerationLock) blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.ProjectileAccelerationIncomplete, "Projectile Acceleration control incomplete."));
                return;
            }
            if (expression == LawExpression.Frenzy && !evidence.IsFrenzyMastered(policy)) blockers.Add(new DomainAuthoringBlocker(
                DomainAuthoringBlockerKind.FrenzyMasteryIncomplete,
                "Frenzy mastery incomplete — " + Format(evidence.FrenzyEvidence) + " / " + Format(policy.FrenzyThreshold) + "."));
            if (expression == LawExpression.Trap)
            {
                MasteryChannelReport report=evidence.GetExpressionReport(LawExpression.Trap,policy);
                if(!report.StateLock) blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.ProjectileStateIncomplete,"Trap State control incomplete."));
                if(!report.RateLock) blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.ProjectileRateIncomplete,"Trap Rate control incomplete."));
                if(!report.AccelerationLock) blockers.Add(new DomainAuthoringBlocker(DomainAuthoringBlockerKind.ProjectileAccelerationIncomplete,"Trap Acceleration control incomplete."));
            }
        }

        private static void BuildContext(DomainContextFocus focus, LawExpression expression,
            LawPhenomenon phenomenon, LawTerritoryPrinciple territory, PlayerMasteryEvidenceState evidence,
            PlayerMasteryPolicy policy, out string title, out string body)
        {
            if (focus == DomainContextFocus.Expression)
            {
                title = expression.ToString().ToUpperInvariant() + " EXPRESSION";
                if (expression == LawExpression.Projectile)
                {
                    MasteryChannelReport report = evidence.GetProjectileReport(policy);
                    body = "Emits Domain authority through projectile expression.\n\nPROJECTILE CONTROL\n"
                        + Dimension("State", report.StateEvidence, policy.StateThreshold, report.StateLock)
                        + Dimension("Rate", report.RateEvidence, policy.RateThreshold, report.RateLock)
                        + Dimension("Acceleration", report.AccelerationEvidence, policy.AccelerationThreshold, report.AccelerationLock)
                        + "\nStatus: " + (report.IsMastered ? "MASTERED" : "INCOMPLETE");
                }
                else if (expression == LawExpression.Frenzy)
                {
                    body = "Embodies Domain authority through Frenzy.\n\nFRENZY MASTERY\n"
                        + Format(evidence.FrenzyEvidence) + " / " + Format(policy.FrenzyThreshold) + "\n"
                        + "Qualifying mana-infused kills in the final quarter of Frenzy.\n"
                        + "One qualifying award per activation.\n\nStatus: "
                        + (evidence.IsFrenzyMastered(policy) ? "MASTERED" : "INCOMPLETE");
                }
                else
                {
                    MasteryChannelReport report=evidence.GetExpressionReport(LawExpression.Trap,policy);
                    body="Establishes Domain authority through an enhanced Oblivion Disk.\n\nTRAP CONTROL\n"
                        +Dimension("State",report.StateEvidence,policy.StateThreshold,report.StateLock)
                        +Dimension("Rate",report.RateEvidence,policy.RateThreshold,report.RateLock)
                        +Dimension("Acceleration",report.AccelerationEvidence,policy.AccelerationThreshold,report.AccelerationLock)
                        +"\nStatus: "+(report.IsMastered?"MASTERED":"INCOMPLETE");
                }
                return;
            }
            if (focus == DomainContextFocus.Territory)
            {
                title = territory.ToString().ToUpperInvariant() + " PRINCIPLE";
                body = TerritoryDescription(territory) + "\n\nThis principle defines the compositional role of the Law.\nIt has no separate mastery track.";
                return;
            }

            DomainPhenomenonMasteryProgress progress = DomainControlMasteryQuery.GetPhenomenon(evidence, phenomenon, policy);
            title = phenomenon.ToString().ToUpperInvariant();
            body = PhenomenonDescription(phenomenon) + "\n\nNATURAL SOURCES\n" + PhenomenonSources(phenomenon)
                + "\n\nCONTROL MASTERY\n"
                + Dimension("State", progress.StateEvidence, progress.StateRequired, progress.StateMastered)
                + Dimension("Rate", progress.RateEvidence, progress.RateRequired, progress.RateMastered)
                + Dimension("Acceleration", progress.AccelerationEvidence, progress.AccelerationRequired, progress.AccelerationMastered)
                + "\nDomain Mastery: " + (progress.IsDomainControlMastered ? "MASTERED" : "INCOMPLETE")
                + "\nState is the current condition; Rate is how it changes; Acceleration is how that rate changes.";
        }

        private static string Dimension(string label, float current, float required, bool complete)
        { return label.ToUpperInvariant() + "  " + (complete ? "COMPLETE" : "INCOMPLETE") + "  " + Format(current) + " / " + Format(required) + "\n"; }
        private static string Format(float value) { return value.ToString("0.##"); }

        private static string PhenomenonDescription(LawPhenomenon phenomenon)
        {
            switch (phenomenon)
            {
                case LawPhenomenon.Compression: return "Compression concentrates force before release.";
                case LawPhenomenon.Potential: return "Potential records stored combat commitment before release.";
                case LawPhenomenon.Resonance: return "Resonance records a successful responsive combat exchange.";
                case LawPhenomenon.Phase: return "Phase is represented by the current Phase and Imaginary combat state.";
                default: return phenomenon + " is an authoritative Domain phenomenon.";
            }
        }

        private static string PhenomenonSources(LawPhenomenon phenomenon)
        {
            switch (phenomenon)
            {
                case LawPhenomenon.Compression: return "• Charged Heavy impact\n• Committed charged projectile release";
                case LawPhenomenon.Potential: return "• Successful Brace exit into Spin or Heavy";
                case LawPhenomenon.Resonance: return "• Successful regular or perfect parry";
                case LawPhenomenon.Phase: return "• Current Phase / Imaginary combat interactions";
                case LawPhenomenon.Heat: return "• Existing elemental Heat / Fire interactions";
                case LawPhenomenon.Flow: return "• Existing production Flow interactions";
                case LawPhenomenon.Mass: return "• Existing production Mass interactions";
                case LawPhenomenon.Order: return "• Existing production Order interactions";
                default: return "• No production source description available.";
            }
        }

        private static string TerritoryDescription(LawTerritoryPrinciple territory)
        {
            switch (territory)
            {
                case LawTerritoryPrinciple.Accumulation: return "Combines bounded same-phenomenon contributions at one focal recipient.";
                case LawTerritoryPrinciple.Propagation: return "Carries a resolved same-phenomenon operation to supplied recipients.";
                case LawTerritoryPrinciple.Synchronization: return "Establishes a persistent relationship group for the Law's phenomenon.";
                case LawTerritoryPrinciple.Catalysis: return "Observes a source region crossing and proposes one non-recursive follow-up.";
                case LawTerritoryPrinciple.Interference: return "Constructively combines compatible same-region contributions.";
                case LawTerritoryPrinciple.Reversal: return "Transforms the incoming resolution intent before later processing.";
                default: return "Defines the Law's spatial and compositional organization.";
            }
        }
    }
}
