using Cave.Axioms.Mastery;
using Cave.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>
    /// Presentation controller for the Domain tab. It renders and submits only
    /// through the player-owned collection and live current-run evidence.
    /// </summary>
    public sealed class DomainPageView : MonoBehaviour
    {
        private static readonly LawPhenomenon[] Phenomena =
        {
            LawPhenomenon.Heat, LawPhenomenon.Flow, LawPhenomenon.Mass,
            LawPhenomenon.Compression, LawPhenomenon.Potential,
            LawPhenomenon.Resonance, LawPhenomenon.Phase, LawPhenomenon.Order
        };

        private static readonly LawTerritoryPrinciple[] TerritoryPrinciples =
        {
            LawTerritoryPrinciple.Accumulation, LawTerritoryPrinciple.Propagation,
            LawTerritoryPrinciple.Synchronization, LawTerritoryPrinciple.Catalysis,
            LawTerritoryPrinciple.Interference, LawTerritoryPrinciple.Reversal
        };

        [SerializeField] private Text seedStatusText;
        [SerializeField] private Text complexityStatusText;
        [SerializeField] private Text complexityTierText;
        [SerializeField] private Text complexityNextTierText;
        [SerializeField] private Image complexityFill;
        [SerializeField] private Text ownedLawsStatusText;
        [SerializeField] private Button projectileButton;
        [SerializeField] private Button frenzyButton;
        [SerializeField] private Button trapButton;
        [SerializeField] private Button[] phenomenonButtons;
        [SerializeField] private Button[] territoryButtons;
        [SerializeField] private Text selectionStatusText;
        [SerializeField] private Text[] masteryLabels;
        [SerializeField] private Text[] masteryPercentages;
        [SerializeField] private Image[] masteryFills;
        [SerializeField] private Text authoringStatusText;
        [SerializeField] private Button createLawButton;

        private LawExpression selectedExpression = LawExpression.Projectile;
        private LawPhenomenon selectedPhenomenon = LawPhenomenon.Heat;
        private LawTerritoryPrinciple selectedTerritory = LawTerritoryPrinciple.Accumulation;
        private AxiomMasteryState mastery;
        private PlayerMasteryEvidenceRuntime productionEvidence;
        private PlayerDomainLawCollection ownedLaws;

        public void Configure(
            Text seedStatus,
            Text complexityStatus,
            Text complexityTier,
            Text complexityNextTier,
            Image complexityProgressFill,
            Text ownedLawsStatus,
            Button projectile,
            Button frenzy,
            Button trap,
            Button[] phenomena,
            Button[] territories,
            Text selectionStatus,
            Text[] masteryRowLabels,
            Text[] masteryRowPercentages,
            Image[] masteryRowFills,
            Text authoringStatus,
            Button createLaw)
        {
            seedStatusText = seedStatus;
            complexityStatusText = complexityStatus;
            complexityTierText = complexityTier;
            complexityNextTierText = complexityNextTier;
            complexityFill = complexityProgressFill;
            ownedLawsStatusText = ownedLawsStatus;
            projectileButton = projectile;
            frenzyButton = frenzy;
            trapButton = trap;
            phenomenonButtons = phenomena;
            territoryButtons = territories;
            selectionStatusText = selectionStatus;
            masteryLabels = masteryRowLabels;
            masteryPercentages = masteryRowPercentages;
            masteryFills = masteryRowFills;
            authoringStatusText = authoringStatus;
            createLawButton = createLaw;

            if (projectileButton != null)
            {
                projectileButton.onClick.AddListener(() => SelectExpression(LawExpression.Projectile));
            }

            if (frenzyButton != null)
            {
                frenzyButton.onClick.AddListener(() => SelectExpression(LawExpression.Frenzy));
            }

            for (int index = 0; phenomenonButtons != null && index < phenomenonButtons.Length && index < Phenomena.Length; index++)
            {
                LawPhenomenon phenomenon = Phenomena[index];
                phenomenonButtons[index].onClick.AddListener(() => SelectPhenomenon(phenomenon));
            }

            for (int index = 0; territoryButtons != null && index < territoryButtons.Length && index < TerritoryPrinciples.Length; index++)
            {
                LawTerritoryPrinciple territory = TerritoryPrinciples[index];
                territoryButtons[index].onClick.AddListener(() => SelectTerritory(territory));
            }

            // Trap has no production LawExpression; it remains visibly reserved
            // and cannot imply that a Law was created.
            if (trapButton != null) trapButton.interactable = false;
            if (createLawButton != null) createLawButton.interactable = false;
            Refresh(null);
        }

        public void BindProduction(PlayerMasteryEvidenceRuntime evidence, PlayerDomainLawCollection laws)
        {
            if (productionEvidence != null) productionEvidence.Changed -= HandleProductionChanged;
            if (ownedLaws != null) ownedLaws.Changed -= HandleProductionChanged;
            productionEvidence = evidence;
            ownedLaws = laws;
            if (productionEvidence != null) productionEvidence.Changed += HandleProductionChanged;
            if (ownedLaws != null) ownedLaws.Changed += HandleProductionChanged;
            if (createLawButton != null)
            {
                createLawButton.onClick.RemoveListener(TryCreateLaw);
                createLawButton.onClick.AddListener(TryCreateLaw);
            }
            Refresh(mastery);
        }

        public void Refresh(AxiomMasteryState currentMastery)
        {
            mastery = currentMastery;
            bool hasSeed = DomainProgression.HasDomainSeed;
            DomainComposition currentComposition = ownedLaws != null ? ownedLaws.Composition : DomainComposition.Empty;
            DomainLaw candidate;
            LawValidationResult validation;
            bool candidateIsValid = DomainLaw.TryCreate(
                selectedExpression, selectedPhenomenon, selectedTerritory, out candidate, out validation);
            DomainAuthoringEligibilityResult eligibility = candidateIsValid
                ? DomainAuthoringEligibility.Evaluate(currentComposition, candidate,
                    productionEvidence != null
                        ? DomainProgression.CreateAuthoringContext(productionEvidence.Snapshot,
                            PlayerMasteryPolicy.Default, DomainComplexityPolicy.Default)
                        : null)
                : null;

            if (seedStatusText != null)
            {
                seedStatusText.text = hasSeed
                    ? "AWAKENED\nDomain foundation established."
                    : "LOCKED\nA Domain Seed is required.";
            }

            if (complexityStatusText != null)
            {
                DomainComplexityCapacityEvaluation capacity = DomainProgression.EvaluateComplexityCapacity(
                    PlayerMasteryEvidenceState.Empty,
                    PlayerMasteryPolicy.Default);
                float committedComplexity = DomainComplexityCalculator.Calculate(
                    currentComposition,
                    DomainComplexityPolicy.Default).TotalComplexity;
                complexityStatusText.text = hasSeed
                    ? committedComplexity.ToString("0.##") + " / " + capacity.Capacity.ToString("0.##")
                    : "LOCKED";

                if (complexityTierText != null)
                {
                    complexityTierText.text = hasSeed
                        ? DomainComplexityCapacityProgression.GetDisplayName(capacity.CurrentTier)
                        : "DOMAIN SEED REQUIRED";
                }

                if (complexityNextTierText != null)
                {
                    complexityNextTierText.text = BuildNextTierText(capacity);
                }

                if (complexityFill != null)
                {
                    float progress = capacity.Capacity <= 0f ? 0f : committedComplexity / capacity.Capacity;
                    complexityFill.rectTransform.sizeDelta = new Vector2(260f * Mathf.Clamp01(progress), 7f);
                }
            }

            if (ownedLawsStatusText != null)
            {
                ownedLawsStatusText.text = hasSeed && ownedLaws != null && ownedLaws.Laws.Count > 0
                    ? BuildOwnedLawText(ownedLaws)
                    : hasSeed
                    ? "NO LAWS AUTHORED"
                    : "Awaken a Domain Seed to begin preparation.";
            }

            SetOptionState(projectileButton, hasSeed, selectedExpression == LawExpression.Projectile);
            SetOptionState(frenzyButton, hasSeed, selectedExpression == LawExpression.Frenzy);
            SetOptionState(trapButton, false, false);
            ApplyReservedState(trapButton);
            SetOptionStates(phenomenonButtons, Phenomena, selectedPhenomenon, hasSeed);
            SetOptionStates(territoryButtons, TerritoryPrinciples, selectedTerritory, hasSeed);

            if (selectionStatusText != null)
            {
                selectionStatusText.text = hasSeed
                    ? BuildSelectionStatus()
                    : "DOMAIN SEED REQUIRED";
            }

            RefreshMasteryRows();

            if (authoringStatusText != null)
            {
                authoringStatusText.text = BuildAuthoringStatus(eligibility);
            }

            if (createLawButton != null)
            {
                bool authoringReady = eligibility != null && eligibility.IsEligible && ownedLaws != null;
                createLawButton.interactable = authoringReady;
                ApplyCreateLawPresentation(createLawButton, BuildCreateLawLabel(eligibility, authoringReady));
            }
        }

        private void SelectExpression(LawExpression expression)
        {
            if (!DomainProgression.HasDomainSeed) return;
            selectedExpression = expression;
            Refresh(mastery);
        }

        private void SelectPhenomenon(LawPhenomenon phenomenon)
        {
            if (!DomainProgression.HasDomainSeed) return;
            selectedPhenomenon = phenomenon;
            Refresh(mastery);
        }

        private void SelectTerritory(LawTerritoryPrinciple territory)
        {
            if (!DomainProgression.HasDomainSeed) return;
            selectedTerritory = territory;
            Refresh(mastery);
        }

        private void RefreshMasteryRows()
        {
            for (int index = 0; index < DomainMasteryQuery.PhenomenonCount; index++)
            {
                MasteryDomain phenomenon = DomainMasteryQuery.GetPhenomenon(index);
                float value = DomainMasteryQuery.GetMastery(mastery, phenomenon);
                bool eligible = DomainMasteryQuery.IsEligible(mastery, phenomenon);

                if (masteryLabels != null && index < masteryLabels.Length && masteryLabels[index] != null)
                {
                    masteryLabels[index].text = DomainMasteryQuery.GetDisplayName(phenomenon);
                    masteryLabels[index].color = eligible ? CaveUiTheme.PrimaryText : CaveUiTheme.SecondaryText;
                }

                if (masteryPercentages != null && index < masteryPercentages.Length && masteryPercentages[index] != null)
                {
                    masteryPercentages[index].text = Mathf.RoundToInt(value * 100f) + "%";
                    masteryPercentages[index].color = eligible ? CaveUiTheme.Gold : CaveUiTheme.SecondaryText;
                }

                if (masteryFills != null && index < masteryFills.Length && masteryFills[index] != null)
                {
                    masteryFills[index].rectTransform.sizeDelta = new Vector2(260f * Mathf.Clamp01(value), 8f);
                    masteryFills[index].color = eligible ? CaveUiTheme.Gold : CaveUiTheme.BorderBright;
                }
            }
        }

        private static string BuildAuthoringStatus(DomainAuthoringEligibilityResult eligibility)
        {
            if (eligibility == null)
            {
                return "AUTHORING CONTEXT REQUIRED\nPlayer authoring state is not bound.";
            }

            if (eligibility.IsEligible)
            {
                return "AUTHORING READY\nCreate this Law with the current Domain composition.";
            }

            switch (eligibility.RejectionReason)
            {
                case DomainAuthoringRejectionReason.InvalidContext:
                    return "AUTHORING CONTEXT REQUIRED\nMastery and complexity capacity are not available.";
                case DomainAuthoringRejectionReason.DomainSeedRequired:
                    return "DOMAIN SEED REQUIRED\nAwaken the Domain Seed before authoring Laws.";
                case DomainAuthoringRejectionReason.PhenomenonMasteryIncomplete:
                    return "PHENOMENON MASTERY REQUIRED\nMaster the selected phenomenon before authoring this Law.";
                case DomainAuthoringRejectionReason.ExpressionMasteryIncomplete:
                    return "EXPRESSION MASTERY REQUIRED\nMaster the selected expression before authoring this Law.";
                case DomainAuthoringRejectionReason.ComplexityCapacityExceeded:
                    return "COMPLEXITY CAPACITY EXCEEDED\nIncrease Domain capacity or reduce the authored composition.";
                case DomainAuthoringRejectionReason.DuplicateLaw:
                    return "LAW ALREADY AUTHORED\nThis exact Expression, Phenomenon, and Territory combination already exists.";
                case DomainAuthoringRejectionReason.InvalidComplexityCapacity:
                    return "AUTHORING UNAVAILABLE\nThe current complexity capacity is invalid.";
                default:
                    return "AUTHORING UNAVAILABLE\nThe selected Law is structurally invalid.";
            }
        }

        private string BuildSelectionStatus()
        {
            string status = "PREVIEWED LAW  •  " + selectedExpression.ToString().ToUpperInvariant()
                + " / " + selectedPhenomenon.ToString().ToUpperInvariant()
                + " / " + selectedTerritory.ToString().ToUpperInvariant();
            if (selectedExpression == LawExpression.Frenzy && productionEvidence != null)
            {
                status += "\nFRENZY EVIDENCE  •  "
                    + productionEvidence.Snapshot.FrenzyEvidence.ToString("0.##")
                    + " / " + PlayerMasteryPolicy.Default.FrenzyThreshold.ToString("0.##");
            }

            return status;
        }

        private static string BuildCreateLawLabel(
            DomainAuthoringEligibilityResult eligibility,
            bool authoringReady)
        {
            if (authoringReady) return "CREATE LAW\nREADY";
            if (eligibility == null || eligibility.RejectionReason == DomainAuthoringRejectionReason.InvalidContext)
            {
                return "CREATE LAW\nAUTHORING CONTEXT REQUIRED";
            }

            if (eligibility.RejectionReason == DomainAuthoringRejectionReason.DomainSeedRequired)
            {
                return "CREATE LAW\nDOMAIN SEED REQUIRED";
            }

            if (eligibility.RejectionReason == DomainAuthoringRejectionReason.PhenomenonMasteryIncomplete
                || eligibility.RejectionReason == DomainAuthoringRejectionReason.ExpressionMasteryIncomplete)
            {
                return "CREATE LAW\nMASTERY REQUIRED";
            }

            if (eligibility.RejectionReason == DomainAuthoringRejectionReason.ComplexityCapacityExceeded)
            {
                return "CREATE LAW\nCAPACITY EXCEEDED";
            }

            if (eligibility.RejectionReason == DomainAuthoringRejectionReason.DuplicateLaw)
            {
                return "CREATE LAW\nALREADY AUTHORED";
            }

            return "CREATE LAW\nUNAVAILABLE";
        }

        private void HandleProductionChanged(PlayerMasteryEvidenceState _) { Refresh(mastery); }
        private void HandleProductionChanged() { Refresh(mastery); }

        private void TryCreateLaw()
        {
            if (ownedLaws == null || productionEvidence == null) return;
            DomainLaw candidate; LawValidationResult validation;
            if (!DomainLaw.TryCreate(selectedExpression, selectedPhenomenon, selectedTerritory, out candidate, out validation)) return;
            ownedLaws.TryAuthor(candidate, DomainProgression.CreateAuthoringContext(
                productionEvidence.Snapshot, PlayerMasteryPolicy.Default, DomainComplexityPolicy.Default));
            Refresh(mastery);
        }

        private static string BuildOwnedLawText(PlayerDomainLawCollection laws)
        {
            string text = string.Empty;
            for (int index = 0; index < laws.Laws.Count; index++)
            {
                DomainLaw law = laws.Laws[index].Law;
                if (index > 0) text += "\n";
                text += law.Phenomenon.ToString().ToUpperInvariant() + " • "
                    + law.Expression.ToString().ToUpperInvariant() + " • "
                    + law.TerritoryPrinciple.ToString().ToUpperInvariant();
            }
            return text;
        }

        private static string BuildNextTierText(DomainComplexityCapacityEvaluation capacity)
        {
            if (!capacity.HasDomainSeed)
            {
                return "Awaken a Domain Seed to establish capacity.";
            }

            if (capacity.NextTier == DomainComplexityCapacityTier.None)
            {
                return "Maximum capacity tier unlocked.";
            }

            if (capacity.UnsatisfiedRequirements.Count == 0)
            {
                return "NEXT TIER READY: "
                    + DomainComplexityCapacityProgression.GetDisplayName(capacity.NextTier);
            }

            string text = "NEXT TIER: ";
            for (int index = 0; index < capacity.UnsatisfiedRequirements.Count; index++)
            {
                if (index > 0) text += "  •  ";
                text += capacity.UnsatisfiedRequirements[index].Description;
            }

            return text;
        }

        private static void SetOptionStates<T>(Button[] buttons, T[] values, T selected, bool interactable)
        {
            if (buttons == null) return;
            for (int index = 0; index < buttons.Length && index < values.Length; index++)
            {
                SetOptionState(buttons[index], interactable, values[index].Equals(selected));
            }
        }

        private static void SetOptionState(Button button, bool interactable, bool selected)
        {
            if (button == null) return;
            button.interactable = interactable;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected && interactable ? new Color(0.17f, 0.115f, 0.045f, 1f)
                    : interactable ? CaveUiTheme.SurfaceRaised : CaveUiTheme.IronDark;
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selected && interactable ? CaveUiTheme.Gold : CaveUiTheme.IronLight;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = selected && interactable ? CaveUiTheme.Gold
                    : interactable ? CaveUiTheme.PrimaryText : CaveUiTheme.SecondaryText;
            }
        }

        private static void ApplyReservedState(Button button)
        {
            if (button == null) return;

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.075f, 0.052f, 0.028f, 1f);
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = CaveUiTheme.BronzeLight;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = CaveUiTheme.BronzeLight;
                label.fontStyle = FontStyle.Bold;
            }

            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        private static void ApplyCreateLawPresentation(Button button, string labelText)
        {
            if (button == null) return;

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.085f, 0.055f, 0.024f, 1f);
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = CaveUiTheme.BronzeLight;
                outline.effectDistance = new Vector2(3f, -3f);
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = labelText;
                label.color = CaveUiTheme.PrimaryText;
                label.fontStyle = FontStyle.Bold;
            }

            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }
    }
}
