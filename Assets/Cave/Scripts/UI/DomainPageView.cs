using Cave.Axioms.Mastery;
using Cave.Domain;
using Cave.Enemies;
using Cave.InputSystem;
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
        [SerializeField] private Text reserveStatusText;
        [SerializeField] private Text reserveDetailText;
        [SerializeField] private Image reserveFill;
        [SerializeField] private Text ownedLawsStatusText;
        [SerializeField] private DomainOwnedLawList ownedLawList;
        [SerializeField] private Button projectileButton;
        [SerializeField] private Button frenzyButton;
        [SerializeField] private Button trapButton;
        [SerializeField] private Button[] phenomenonButtons;
        [SerializeField] private Button[] territoryButtons;
        [SerializeField] private Text selectionStatusText;
        [SerializeField] private Text contextTitleText;
        [SerializeField] private Text contextBodyText;
        [SerializeField] private Text authoringStatusText;
        [SerializeField] private Button createLawButton;

        private LawExpression selectedExpression = LawExpression.Projectile;
        private LawPhenomenon selectedPhenomenon = LawPhenomenon.Heat;
        private LawTerritoryPrinciple selectedTerritory = LawTerritoryPrinciple.Accumulation;
        private AxiomMasteryState mastery;
        private PlayerMasteryEvidenceRuntime productionEvidence;
        private PlayerDomainLawCollection ownedLaws;
        private PlayerDomainReserve domainReserve;
        private PlayerDomainManifestation manifestation;
        private bool reserveSubscribed;
        private bool manifestationSubscribed;
        private DomainContextFocus contextFocus = DomainContextFocus.Phenomenon;

        public void Configure(
            Text seedStatus,
            Text complexityStatus,
            Text complexityTier,
            Text complexityNextTier,
            Image complexityProgressFill,
            Text reserveStatus,
            Text reserveDetail,
            Image reserveProgressFill,
            Text ownedLawsStatus,
            DomainOwnedLawList ownedLawsList,
            Button projectile,
            Button frenzy,
            Button trap,
            Button[] phenomena,
            Button[] territories,
            Text selectionStatus,
            Text contextTitle,
            Text contextBody,
            Text authoringStatus,
            Button createLaw)
        {
            seedStatusText = seedStatus;
            complexityStatusText = complexityStatus;
            complexityTierText = complexityTier;
            complexityNextTierText = complexityNextTier;
            complexityFill = complexityProgressFill;
            reserveStatusText = reserveStatus;
            reserveDetailText = reserveDetail;
            reserveFill = reserveProgressFill;
            ownedLawsStatusText = ownedLawsStatus;
            ownedLawList = ownedLawsList;
            projectileButton = projectile;
            frenzyButton = frenzy;
            trapButton = trap;
            phenomenonButtons = phenomena;
            territoryButtons = territories;
            selectionStatusText = selectionStatus;
            contextTitleText = contextTitle;
            contextBodyText = contextBody;
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

            if (trapButton != null)
            {
                trapButton.onClick.AddListener(() => SelectExpression(LawExpression.Trap));
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

            ApplyCanonicalPhenomenonIcons();

            Refresh(null);
        }

        /// <summary>Receives the established player-owned Reserve authority.
        /// The view never creates it and only observes its existing ledger.</summary>
        public void BindReserve(PlayerDomainReserve reserve)
        {
            UnsubscribeReserve();
            UnsubscribeManifestation();
            domainReserve = reserve;
            manifestation = domainReserve != null
                ? domainReserve.GetComponent<PlayerDomainManifestation>()
                : null;
            SubscribeReserve();
            SubscribeManifestation();
            Refresh(mastery);
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
            bool testOverride = DomainTestOverride.Enabled;
            bool hasSeed = DomainTestOverride.HasDomainAccess;
            DomainComposition currentComposition = ownedLaws != null ? ownedLaws.Composition : DomainComposition.Empty;
            PlayerMasteryEvidenceState realEvidence = productionEvidence != null
                ? productionEvidence.Snapshot : PlayerMasteryEvidenceState.Empty;
            PlayerMasteryEvidenceState evidence = DomainTestOverride.ResolveEvidence(
                realEvidence, PlayerMasteryPolicy.Default);
            float capacityValue = DomainTestOverride.ResolveCapacity(DomainComplexityPolicy.Default);
            DomainContextualUiModel model = DomainContextualUiPresenter.Build(
                contextFocus, selectedExpression, selectedPhenomenon, selectedTerritory,
                currentComposition, evidence, PlayerMasteryPolicy.Default,
                DomainComplexityPolicy.Default, hasSeed, capacityValue);

            if (seedStatusText != null)
            {
                seedStatusText.text = testOverride
                    ? "TEST OVERRIDE\nProgression gates bypassed for this session."
                    : hasSeed
                    ? "AWAKENED\nDomain foundation established."
                    : "LOCKED\nA Domain Seed is required.";
            }

            if (complexityStatusText != null)
            {
                DomainComplexityCapacityEvaluation capacity = DomainProgression.EvaluateComplexityCapacity(
                    evidence,
                    PlayerMasteryPolicy.Default);
                float previewComplexity = model.Complexity;
                complexityStatusText.text = testOverride
                    ? "TEST OVERRIDE\nUNBOUNDED TEST CAPACITY"
                    : hasSeed
                    ? model.HasComplexity
                        ? previewComplexity.ToString("0.##") + " / " + capacity.Capacity.ToString("0.##")
                        : "— / " + capacity.Capacity.ToString("0.##")
                    : "LOCKED";

                if (complexityTierText != null)
                {
                    complexityTierText.text = testOverride
                        ? "TEST OVERRIDE"
                        : hasSeed
                        ? DomainComplexityCapacityProgression.GetDisplayName(capacity.CurrentTier)
                        : "DOMAIN SEED REQUIRED";
                }

                if (complexityNextTierText != null)
                {
                    complexityNextTierText.text = testOverride
                        ? "Session-only: real Seed, mastery, and capacity are unchanged."
                        : BuildNextTierText(capacity);
                }

                if (complexityFill != null)
                {
                    float progress = !model.HasComplexity || capacity.Capacity <= 0f ? 0f : previewComplexity / capacity.Capacity;
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
            if (ownedLawList != null) ownedLawList.Refresh(ownedLaws, hasSeed);

            RefreshReserve();

            SetOptionState(projectileButton, hasSeed, selectedExpression == LawExpression.Projectile);
            SetOptionState(frenzyButton, hasSeed, selectedExpression == LawExpression.Frenzy);
            SetOptionState(trapButton, hasSeed, selectedExpression == LawExpression.Trap);
            SetOptionStates(phenomenonButtons, Phenomena, selectedPhenomenon, hasSeed);
            SetOptionStates(territoryButtons, TerritoryPrinciples, selectedTerritory, hasSeed);

            if (selectionStatusText != null)
            {
                selectionStatusText.text = hasSeed
                    ? "LAW PREVIEW  •  " + model.LawPreview
                    : "DOMAIN SEED REQUIRED";
            }

            if (contextTitleText != null) contextTitleText.text = model.ContextTitle;
            if (contextBodyText != null) contextBodyText.text = hasSeed
                ? model.ContextBody
                : "A Domain Seed is required before Laws can be authored.";

            if (authoringStatusText != null)
            {
                authoringStatusText.text = (testOverride ? "TEST OVERRIDE ACTIVE\n" : string.Empty)
                    + BuildAuthoringStatus(model);
            }

            if (createLawButton != null)
            {
                bool authoringReady = model.CanAuthor && ownedLaws != null;
                createLawButton.interactable = authoringReady;
                ApplyCreateLawPresentation(createLawButton, BuildCreateLawLabel(model.Eligibility, authoringReady));
            }
        }

        private void SelectExpression(LawExpression expression)
        {
            if (!DomainTestOverride.HasDomainAccess) return;
            selectedExpression = expression;
            contextFocus = DomainContextFocus.Expression;
            Refresh(mastery);
        }

        private void ApplyCanonicalPhenomenonIcons()
        {
            MobStatusIconRegistry registry = Resources.Load<MobStatusIconRegistry>("MobStatusIconRegistry");
            if (registry == null || phenomenonButtons == null) return;
            for (int index = 0; index < phenomenonButtons.Length && index < Phenomena.Length; index++)
            {
                Button button = phenomenonButtons[index];
                if (button == null) continue;
                Sprite icon = registry.GetIcon(IconFor(Phenomena[index]));
                if (icon == null) continue;
                Transform existing = button.transform.Find("Phenomenon Icon");
                Image image = existing != null ? existing.GetComponent<Image>() : null;
                if (image == null)
                {
                    GameObject child = new GameObject("Phenomenon Icon", typeof(RectTransform), typeof(Image));
                    child.transform.SetParent(button.transform, false);
                    image = child.GetComponent<Image>();
                    RectTransform rect = image.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(-42f, 0f);
                    rect.sizeDelta = new Vector2(24f, 24f);
                    image.raycastTarget = false;
                    Text label = button.GetComponentInChildren<Text>();
                    if (label != null) label.rectTransform.anchoredPosition = new Vector2(12f, 0f);
                }
                image.sprite = icon;
                image.preserveAspect = true;
                image.color = Color.white;
            }
        }

        private static MobStatusIconKind IconFor(LawPhenomenon value)
        {
            if (value == LawPhenomenon.Heat) return MobStatusIconKind.AxiomHeat;
            if (value == LawPhenomenon.Flow) return MobStatusIconKind.AxiomFlow;
            if (value == LawPhenomenon.Mass) return MobStatusIconKind.AxiomMass;
            if (value == LawPhenomenon.Compression) return MobStatusIconKind.AxiomCompression;
            if (value == LawPhenomenon.Potential) return MobStatusIconKind.AxiomPotential;
            if (value == LawPhenomenon.Resonance) return MobStatusIconKind.AxiomResonance;
            if (value == LawPhenomenon.Phase) return MobStatusIconKind.AxiomPhase;
            return MobStatusIconKind.AxiomOrder;
        }

        private void SelectPhenomenon(LawPhenomenon phenomenon)
        {
            if (!DomainTestOverride.HasDomainAccess) return;
            selectedPhenomenon = phenomenon;
            contextFocus = DomainContextFocus.Phenomenon;
            Refresh(mastery);
        }

        private void SelectTerritory(LawTerritoryPrinciple territory)
        {
            if (!DomainTestOverride.HasDomainAccess) return;
            selectedTerritory = territory;
            contextFocus = DomainContextFocus.Territory;
            Refresh(mastery);
        }

        private static string BuildAuthoringStatus(DomainContextualUiModel model)
        {
            if (model != null && model.CanAuthor)
            {
                return "AUTHORING READY\nCreate this Law with the current Domain composition.";
            }
            if (model == null || model.Blockers == null || model.Blockers.Count == 0)
            {
                return "AUTHORING UNAVAILABLE";
            }
            string text = "CANNOT AUTHOR:";
            for (int index = 0; index < model.Blockers.Count; index++)
            {
                text += "\n• " + model.Blockers[index].Detail;
            }
            return text;
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
        private void HandleReserveChanged() { Refresh(mastery); }

        private void TryCreateLaw()
        {
            if (ownedLaws == null) return;
            DomainLaw candidate; LawValidationResult validation;
            if (!DomainLaw.TryCreate(selectedExpression, selectedPhenomenon, selectedTerritory, out candidate, out validation)) return;
            ownedLaws.TryAuthor(candidate, DomainTestOverride.CreateAuthoringContext(
                productionEvidence != null ? productionEvidence.Snapshot : PlayerMasteryEvidenceState.Empty,
                PlayerMasteryPolicy.Default,
                DomainComplexityPolicy.Default));
            Refresh(mastery);
        }

        private void OnEnable()
        {
            DomainTestOverride.Changed += HandleDomainTestOverrideChanged;
            SubscribeReserve();
            SubscribeManifestation();
        }

        private void OnDisable()
        {
            DomainTestOverride.Changed -= HandleDomainTestOverrideChanged;
            UnsubscribeReserve();
            UnsubscribeManifestation();
        }

        private void SubscribeReserve()
        {
            if (reserveSubscribed || domainReserve == null || !isActiveAndEnabled) return;
            domainReserve.ReserveChanged += HandleReserveChanged;
            reserveSubscribed = true;
        }

        private void UnsubscribeReserve()
        {
            if (!reserveSubscribed || domainReserve == null) return;
            domainReserve.ReserveChanged -= HandleReserveChanged;
            reserveSubscribed = false;
        }

        private void SubscribeManifestation()
        {
            if (manifestationSubscribed || manifestation == null || !isActiveAndEnabled) return;
            manifestation.StateChanged += HandleManifestationStateChanged;
            manifestationSubscribed = true;
        }

        private void UnsubscribeManifestation()
        {
            if (!manifestationSubscribed || manifestation == null) return;
            manifestation.StateChanged -= HandleManifestationStateChanged;
            manifestationSubscribed = false;
        }

        private void HandleManifestationStateChanged(DomainManifestationState _) { Refresh(mastery); }
        private void HandleDomainTestOverrideChanged() { Refresh(mastery); }

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

        private void RefreshReserve()
        {
            if (domainReserve == null)
            {
                if (reserveStatusText != null) reserveStatusText.text = "RESERVE UNAVAILABLE";
                if (reserveDetailText != null) reserveDetailText.text = "No player Domain Reserve authority is installed.";
                if (reserveFill != null) reserveFill.rectTransform.sizeDelta = new Vector2(0f, reserveFill.rectTransform.sizeDelta.y);
                return;
            }

            float maximum = domainReserve.MaximumAvailableCharge;
            float current = domainReserve.CurrentCharge;
            float committed = domainReserve.CommittedReserve;
            if (reserveStatusText != null) reserveStatusText.text = current.ToString("0.##") + " / " + maximum.ToString("0.##");
            if (reserveDetailText != null)
            {
                bool canManifest = manifestation != null
                    && (manifestation.IsActive || current + .0001f >= manifestation.MinimumActivationCharge)
                    && DomainTestOverride.HasDomainAccess;
                string hint = canManifest
                    ? "HOLD " + SettingsMenuController.FormatBinding(GameAction.SummonCurseAltar).ToUpperInvariant()
                        + " — " + (manifestation.IsActive ? "DISMISS" : "MANIFEST") + "\n"
                    : string.Empty;
                reserveDetailText.text = hint
                    + "UNCHARGED CAPACITY • " + domainReserve.AvailableReserve.ToString("0.##")
                    + "\nALLOCATED RESERVE • " + committed.ToString("0.##") + " / " + domainReserve.BaseMaximumEnergy.ToString("0.##");
            }
            if (reserveFill != null)
            {
                float ratio = maximum <= 0f ? 0f : current / maximum;
                reserveFill.rectTransform.sizeDelta = new Vector2(260f * Mathf.Clamp01(ratio), reserveFill.rectTransform.sizeDelta.y);
            }
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

            DomainUiSkin.ApplyOption(button, selected, interactable);

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
            DomainUiSkin.ApplyOption(button, button.interactable, button.interactable, true);

            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }
    }
}
