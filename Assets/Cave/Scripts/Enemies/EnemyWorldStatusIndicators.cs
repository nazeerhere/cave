using System;
using System.Collections.Generic;
using Cave.Axioms;
using Cave.Axioms.Control;
using Cave.Axioms.Phase;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyWorldStatusIndicators : MonoBehaviour
    {
        private const string RegistryResourceName = "MobStatusIconRegistry";
        private const string OverlayRegistryResourceName = "AxiomControlOverlayRegistry";
        private const int BodyRendererRecoveryFrameInterval = 120;
        private const int OptionalDependencyRefreshFrameInterval = 120;
        /// <summary>World-space status-icon footprint, independent of actor art scale.</summary>
        public const float UniversalIconSize = 0.20f;

        [Header("Icon Source")]
        [SerializeField] private MobStatusIconRegistry iconRegistry;
        [SerializeField] private AxiomControlOverlayRegistry overlayRegistry;

        [Header("Feet Placement")]
        // Retained only for existing scene serialization. World status cells now
        // deliberately use UniversalIconSize for every actor.
        [SerializeField, Min(0.01f)] private float iconSize = UniversalIconSize;
        [SerializeField, Min(0.01f)] private float iconSpacing = 0.05f;
        [SerializeField, Min(0f)] private float feetPadding = 0.08f;
        [SerializeField] private int sortingOrderOffset = 18;

        [Header("Fixed Status Cell")]
        // The existing icon-size calculation is intentionally retained as the
        // square outer-cell dimension so Slow/Ice keeps its established size.
        [SerializeField, Min(0f)] private float artworkPadding = 0f;
        [SerializeField] private Vector2 stackCountAnchor = new Vector2(0.32f, -0.28f);
        [SerializeField, Min(0f)] private float stackCountScale = 0.15f;
        [SerializeField, Min(1)] private int stackCountFontSize = 24;
        [SerializeField] private FontStyle stackCountFontStyle = FontStyle.Bold;
        [SerializeField] private Color stackCountColor = new Color(0.94f, 0.98f, 1f, 1f);

        private readonly List<MobStatusPresentationEntry> activeEntries = new List<MobStatusPresentationEntry>();
        private readonly List<MobStatusIconCell> iconCells = new List<MobStatusIconCell>();
        private Damageable damageable;
        private SpriteRenderer bodyRenderer;
        private GameObject root;
        private EnemyStatusEffects statusEffects;
        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private EnemyElementalEmpowerment elementalEmpowerment;
        private EnemyCorruptionLifecycle corruption;
        private EyeBrain eye;
        private EyePossessedHost possessedHost;
        private PhaseCombatState phase;
        private EnemyTeamBuffState teamBuffState;
        private AxiomRuntimeState axiomRuntime;
        private AxiomControlState axiomControl;
        private readonly AxiomReactiveStatusPresentation[] axiomPresentations =
            new AxiomReactiveStatusPresentation[Enum.GetValues(typeof(AxiomKind)).Length];
        private int appliedStatusMask = int.MinValue;
        private int appliedImaginaryStacks = -1;
        private int appliedAxiomPresentationHash = int.MinValue;
        private int nextBodyRendererRecoveryFrame;
        private int nextOptionalDependencyRefreshFrame;
        private int nextAxiomDependencyRefreshFrame;
        private bool rootVisible;
        private bool subscribedToInterventionResults;
        private bool axiomPresentationDirty;

        private static readonly AxiomKind[] PresentedAxioms =
        {
            AxiomKind.Heat,
            AxiomKind.Flow,
            AxiomKind.Mass,
            AxiomKind.Compression,
            AxiomKind.Potential,
            AxiomKind.Resonance,
            AxiomKind.Phase,
            AxiomKind.Order
        };

        public static void EnsureOn(GameObject owner)
        {
            if (owner != null && owner.GetComponent<EnemyWorldStatusIndicators>() == null)
            {
                owner.AddComponent<EnemyWorldStatusIndicators>();
            }
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            statusEffects = GetComponent<EnemyStatusEffects>();
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            elementalEmpowerment = GetComponent<EnemyElementalEmpowerment>();
            corruption = GetComponent<EnemyCorruptionLifecycle>();
            eye = GetComponent<EyeBrain>();
            possessedHost = GetComponent<EyePossessedHost>();
            phase = GetComponent<PhaseCombatState>();
            teamBuffState = GetComponent<EnemyTeamBuffState>();
            axiomRuntime = GetComponent<AxiomRuntimeState>();
            axiomControl = GetComponent<AxiomControlState>();
            if (iconRegistry == null)
            {
                iconRegistry = Resources.Load<MobStatusIconRegistry>(RegistryResourceName);
            }
            if (overlayRegistry == null)
            {
                overlayRegistry = Resources.Load<AxiomControlOverlayRegistry>(OverlayRegistryResourceName);
            }

            CreateRoot();
            ResolveAndCacheBodyRenderer();
        }

        private void OnEnable()
        {
            appliedStatusMask = int.MinValue;
            appliedImaginaryStacks = -1;
            appliedAxiomPresentationHash = int.MinValue;
            axiomPresentationDirty = true;
            SubscribeToInterventionResults();
            rootVisible = false;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            RefreshOptionalDependenciesIfDue();
            RefreshAxiomDependenciesIfDue();
            int statusMask = BuildStatusMask(out int imaginaryStacks);
            int axiomPresentationHash = BuildAxiomPresentationHash(Time.time);
            if (axiomPresentationDirty
                || statusMask != appliedStatusMask
                || imaginaryStacks != appliedImaginaryStacks
                || axiomPresentationHash != appliedAxiomPresentationHash)
            {
                appliedStatusMask = statusMask;
                appliedImaginaryStacks = imaginaryStacks;
                appliedAxiomPresentationHash = axiomPresentationHash;
                axiomPresentationDirty = false;
                RebuildIconRow(statusMask, imaginaryStacks, Time.time);
            }

            PositionAtFeet();
        }

        private int BuildStatusMask(out int imaginaryStacks)
        {
            imaginaryStacks = 0;
            if (damageable == null || damageable.CurrentHealth <= 0)
            {
                return 0;
            }

            int mask = 0;
            if (statusEffects != null)
            {
                if (statusEffects.IsBurning) AddStatus(ref mask, MobStatusIconKind.Burn);
                if (statusEffects.IsImmobilized) AddStatus(ref mask, MobStatusIconKind.PinRoot);
                else if (statusEffects.IsSlowed) AddStatus(ref mask, MobStatusIconKind.Slow);
            }

            if (stagger != null && stagger.IsStaggered) AddStatus(ref mask, MobStatusIconKind.Stagger);
            if (damageModifiers != null
                && (damageModifiers.HasModifier(EnemyDamageModifierType.NecromancerBuff)
                    || damageModifiers.HasModifier(EnemyDamageModifierType.WizardBuff)))
            {
                AddStatus(ref mask, MobStatusIconKind.StrengthBuff);
            }

            if (elementalEmpowerment != null && elementalEmpowerment.IsEmpowered)
            {
                AddStatus(ref mask, MobStatusIconKind.ElementallyBuffed);
            }

            if (corruption != null)
            {
                if (corruption.IsRegenerating) AddStatus(ref mask, MobStatusIconKind.Regeneration);
                if (corruption.IsFrenzied) AddStatus(ref mask, MobStatusIconKind.Frenzied);
            }

            if (eye != null)
            {
                if (eye.IsGazeActive) AddStatus(ref mask, MobStatusIconKind.GazeLock);
                if (eye.IsFrenzied) AddStatus(ref mask, MobStatusIconKind.Frenzied);
            }

            if (possessedHost != null && possessedHost.IsPossessed)
            {
                AddStatus(ref mask, MobStatusIconKind.Possessed);
            }

            if (phase != null && phase.LatentStacks > 0)
            {
                imaginaryStacks = phase.LatentStacks;
            }


            if (teamBuffState != null)
            {
                AddTeamBuffIcon(ref mask, MobStatusIconKind.Stagger);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.Frenzied);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.StrengthBuff);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.ElementallyBuffed);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.Regeneration);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.GazeLock);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.TowerSuppression);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.PinRoot);
            }

            return mask;
        }

        private void AddTeamBuffIcon(ref int mask, MobStatusIconKind kind)
        {
            if (teamBuffState.HasIcon(kind)) AddStatus(ref mask, kind);
        }

        private void RebuildIconRow(int statusMask, int imaginaryStacks, float timestamp)
        {
            RebuildActiveKinds(statusMask, imaginaryStacks);
            int visibleCount = 0;
            float cellSize = ResolveBoundedCellSize();
            int sortingLayerId = bodyRenderer != null ? bodyRenderer.sortingLayerID : 0;
            int sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + sortingOrderOffset : 0;
            for (int index = 0; index < activeEntries.Count; index++)
            {
                MobStatusPresentationEntry entry = activeEntries[index];
                Sprite icon = iconRegistry != null ? iconRegistry.GetIcon(entry.Kind) : null;
                if (icon == null)
                {
                    continue;
                }

                MobStatusIconCell cell = GetOrCreateCell(visibleCount++);
                cell.Configure(
                    icon,
                    entry.StackCount,
                    cellSize,
                    cellSize,
                    artworkPadding,
                    stackCountAnchor,
                    stackCountScale,
                    stackCountFontSize,
                    stackCountFontStyle,
                    stackCountColor,
                    sortingLayerId,
                    sortingOrder);
                AxiomReactiveStatusVisual reactiveVisual;
                if (TryGetReactiveVisual(entry.Kind, timestamp, out reactiveVisual))
                {
                    cell.ConfigureReactive(reactiveVisual, overlayRegistry, sortingLayerId, sortingOrder);
                }
                cell.gameObject.SetActive(true);
            }

            for (int index = visibleCount; index < iconCells.Count; index++)
            {
                iconCells[index].gameObject.SetActive(false);
            }

            rootVisible = visibleCount > 0;
            if (root != null) root.SetActive(rootVisible);

            for (int index = 0; index < visibleCount; index++)
            {
                iconCells[index].transform.localPosition = new Vector3(
                    MobStatusPresentationLayout.CellCenterX(index, visibleCount, cellSize, iconSpacing),
                    0f,
                    0f);
            }
        }

        private MobStatusIconCell GetOrCreateCell(int index)
        {
            while (iconCells.Count <= index)
            {
                GameObject cellObject = new GameObject("Status Cell") { hideFlags = HideFlags.DontSave };
                cellObject.transform.SetParent(root.transform, false);
                iconCells.Add(cellObject.AddComponent<MobStatusIconCell>());
            }

            return iconCells[index];
        }

        private void CreateRoot()
        {
            root = new GameObject(name + " Status Indicators") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            root.SetActive(false);
        }

        private void PositionAtFeet()
        {
            if (root == null || !rootVisible)
            {
                return;
            }

            RecoverBodyRendererIfNeeded();
            if (bodyRenderer == null)
            {
                root.transform.position = transform.position + Vector3.down * feetPadding;
            }
            else
            {
                root.transform.position = new Vector3(
                    bodyRenderer.bounds.center.x,
                    bodyRenderer.bounds.min.y - feetPadding,
                    transform.position.z);
            }

            Vector3 inheritedScale = transform.lossyScale;
            root.transform.localScale = new Vector3(
                ReciprocalAbs(inheritedScale.x),
                ReciprocalAbs(inheritedScale.y),
                ReciprocalAbs(inheritedScale.z));
        }

        private float ResolveBoundedCellSize()
        {
            return UniversalIconSize;
        }

        private void RebuildActiveKinds(int statusMask, int imaginaryStacks)
        {
            int presentationMask = statusMask;
            int heatStacks;
            bool hasAxiomHeat = TryGetAxiomStackCount(AxiomKind.Heat, out heatStacks);
            // Heat is the authoritative phenomenon cell. Legacy Burn is the
            // same applied fire condition for world-status presentation, so it
            // is suppressed only while the target's Heat trajectory is active.
            // Other legacy effects retain their independent cells.
            if (MobStatusPresentationLayout.ShouldSuppressLegacyBurn(hasAxiomHeat))
            {
                presentationMask &= ~(1 << (int)MobStatusIconKind.Burn);
            }

            MobStatusPresentationLayout.PopulateLegacyEntries(presentationMask, imaginaryStacks, activeEntries);
            for (int index = 0; index < PresentedAxioms.Length; index++)
            {
                AxiomKind kind = PresentedAxioms[index];
                int stackCount;
                if (TryGetAxiomStackCount(kind, out stackCount))
                {
                    activeEntries.Add(new MobStatusPresentationEntry(ToIconKind(kind), stackCount));
                }
            }

            MobStatusPresentationLayout.Sort(activeEntries);
        }

        private int BuildAxiomPresentationHash(float timestamp)
        {
            int hash = 17;
            for (int index = 0; index < PresentedAxioms.Length; index++)
            {
                AxiomKind kind = PresentedAxioms[index];
                int stackCount;
                if (!TryGetAxiomStackCount(kind, out stackCount))
                {
                    continue;
                }

                hash = CombineHash(hash, (int)kind);
                hash = CombineHash(hash, stackCount);
                AxiomControlOpportunity opportunity = default(AxiomControlOpportunity);
                bool hasOpportunity = axiomControl != null
                    && axiomControl.TryGetActiveOpportunity(kind, timestamp, out opportunity);
                if (hasOpportunity)
                {
                    hash = CombineHash(hash, (int)opportunity.Error.ErrorKind);
                    hash = CombineHash(hash, opportunity.RequiredCorrectionDirection >= 0f ? 1 : -1);
                }

                AxiomReactiveStatusPresentation presentation = GetPresentation(kind);
                hash = CombineHash(hash, presentation.IsFeedbackActive(timestamp) ? 1 : 0);
            }

            return hash;
        }

        private bool TryGetReactiveVisual(
            MobStatusIconKind iconKind,
            float timestamp,
            out AxiomReactiveStatusVisual visual)
        {
            visual = default(AxiomReactiveStatusVisual);
            if (!MobStatusPresentationLayout.IsAxiomPhenomenon(iconKind))
            {
                return false;
            }

            AxiomKind kind;
            if (!TryGetAxiomKind(iconKind, out kind))
            {
                return false;
            }

            AxiomControlOpportunity opportunity = default(AxiomControlOpportunity);
            bool hasOpportunity = axiomControl != null
                && axiomControl.TryGetActiveOpportunity(kind, timestamp, out opportunity);
            visual = GetPresentation(kind).Resolve(hasOpportunity, opportunity, timestamp);
            return visual.HasOverlay;
        }

        private bool TryGetAxiomStackCount(AxiomKind kind, out int stackCount)
        {
            stackCount = 0;
            AxiomTrajectoryState trajectory;
            if (axiomRuntime == null || !axiomRuntime.TryGetTrajectory(kind, out trajectory))
            {
                return false;
            }

            float magnitude = trajectory.CurrentValue < 0f
                ? -trajectory.CurrentValue
                : trajectory.CurrentValue;
            if (magnitude <= 0.0001f)
            {
                return false;
            }

            stackCount = Mathf.Max(1, Mathf.CeilToInt(magnitude));
            return true;
        }

        private AxiomReactiveStatusPresentation GetPresentation(AxiomKind kind)
        {
            int index = (int)kind;
            AxiomReactiveStatusPresentation presentation = axiomPresentations[index];
            if (presentation == null)
            {
                presentation = new AxiomReactiveStatusPresentation();
                axiomPresentations[index] = presentation;
            }

            return presentation;
        }

        private static MobStatusIconKind ToIconKind(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat: return MobStatusIconKind.AxiomHeat;
                case AxiomKind.Flow: return MobStatusIconKind.AxiomFlow;
                case AxiomKind.Mass: return MobStatusIconKind.AxiomMass;
                case AxiomKind.Compression: return MobStatusIconKind.AxiomCompression;
                case AxiomKind.Potential: return MobStatusIconKind.AxiomPotential;
                case AxiomKind.Resonance: return MobStatusIconKind.AxiomResonance;
                case AxiomKind.Phase: return MobStatusIconKind.AxiomPhase;
                case AxiomKind.Order: return MobStatusIconKind.AxiomOrder;
                default: return MobStatusIconKind.AxiomHeat;
            }
        }

        private static bool TryGetAxiomKind(MobStatusIconKind iconKind, out AxiomKind kind)
        {
            switch (iconKind)
            {
                case MobStatusIconKind.AxiomHeat: kind = AxiomKind.Heat; return true;
                case MobStatusIconKind.AxiomFlow: kind = AxiomKind.Flow; return true;
                case MobStatusIconKind.AxiomMass: kind = AxiomKind.Mass; return true;
                case MobStatusIconKind.AxiomCompression: kind = AxiomKind.Compression; return true;
                case MobStatusIconKind.AxiomPotential: kind = AxiomKind.Potential; return true;
                case MobStatusIconKind.AxiomResonance: kind = AxiomKind.Resonance; return true;
                case MobStatusIconKind.AxiomPhase: kind = AxiomKind.Phase; return true;
                case MobStatusIconKind.AxiomOrder: kind = AxiomKind.Order; return true;
                default: kind = default(AxiomKind); return false;
            }
        }

        private static int CombineHash(int value, int input)
        {
            unchecked
            {
                return value * 31 + input;
            }
        }

        private void RefreshOptionalDependenciesIfDue()
        {
            if (Time.frameCount < nextOptionalDependencyRefreshFrame
                || !HasMissingDependencies())
            {
                return;
            }

            nextOptionalDependencyRefreshFrame = Time.frameCount
                + OptionalDependencyRefreshFrameInterval;
            if (damageable == null) damageable = GetComponent<Damageable>();
            if (statusEffects == null) statusEffects = GetComponent<EnemyStatusEffects>();
            if (stagger == null) stagger = GetComponent<EnemyStagger>();
            if (damageModifiers == null) damageModifiers = GetComponent<EnemyDamageModifiers>();
            if (elementalEmpowerment == null) elementalEmpowerment = GetComponent<EnemyElementalEmpowerment>();
            if (corruption == null) corruption = GetComponent<EnemyCorruptionLifecycle>();
            if (eye == null) eye = GetComponent<EyeBrain>();
            if (possessedHost == null) possessedHost = GetComponent<EyePossessedHost>();
            if (teamBuffState == null) teamBuffState = GetComponent<EnemyTeamBuffState>();
        }

        private void RefreshAxiomDependenciesIfDue()
        {
            if (Time.frameCount < nextAxiomDependencyRefreshFrame)
            {
                return;
            }

            nextAxiomDependencyRefreshFrame = Time.frameCount + OptionalDependencyRefreshFrameInterval;
            if (phase == null) phase = GetComponent<PhaseCombatState>();
            if (axiomRuntime == null) axiomRuntime = GetComponent<AxiomRuntimeState>();
            if (axiomControl == null) axiomControl = GetComponent<AxiomControlState>();
            if (overlayRegistry == null)
            {
                overlayRegistry = Resources.Load<AxiomControlOverlayRegistry>(OverlayRegistryResourceName);
            }

            SubscribeToInterventionResults();
        }

        private void SubscribeToInterventionResults()
        {
            if (subscribedToInterventionResults || axiomControl == null)
            {
                return;
            }

            axiomControl.InterventionResolved += HandleInterventionResolved;
            subscribedToInterventionResults = true;
        }

        private void UnsubscribeFromInterventionResults()
        {
            if (!subscribedToInterventionResults)
            {
                return;
            }

            if (axiomControl != null)
            {
                axiomControl.InterventionResolved -= HandleInterventionResolved;
            }

            subscribedToInterventionResults = false;
        }

        private void HandleInterventionResolved(AxiomControlInterventionOutcome outcome)
        {
            if (outcome.ControlOwner != gameObject || !IsPresentedAxiom(outcome.Kind))
            {
                return;
            }

            if (GetPresentation(outcome.Kind).Observe(
                    outcome,
                    AxiomReactiveStatusPresentation.DefaultFeedbackDuration))
            {
                axiomPresentationDirty = true;
            }
        }

        private static bool IsPresentedAxiom(AxiomKind kind)
        {
            for (int index = 0; index < PresentedAxioms.Length; index++)
            {
                if (PresentedAxioms[index] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasMissingDependencies()
        {
            return damageable == null
                || statusEffects == null
                || stagger == null
                || damageModifiers == null
                || elementalEmpowerment == null
                || corruption == null
                || eye == null
                || possessedHost == null
                || teamBuffState == null;
        }

        private void RecoverBodyRendererIfNeeded()
        {
            // A destroyed visual can be recovered, but never by scanning a healthy
            // enemy hierarchy from its recurring presentation path.
            if (bodyRenderer != null || Time.frameCount < nextBodyRendererRecoveryFrame)
            {
                return;
            }

            ResolveAndCacheBodyRenderer();
        }

        private void ResolveAndCacheBodyRenderer()
        {
            nextBodyRendererRecoveryFrame = Time.frameCount + BodyRendererRecoveryFrameInterval;
            SpriteRenderer primary = null;
            float primaryScore = float.NegativeInfinity;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate == null
                    || candidate.sprite == null
                    || candidate.transform.IsChildOf(root != null ? root.transform : transform)
                    || IsAuxiliaryRenderer(candidate))
                {
                    continue;
                }

                float sizeScore = candidate.bounds.size.x * candidate.bounds.size.y;
                if (candidate.transform == transform)
                {
                    sizeScore += 1000f;
                }

                if (sizeScore > primaryScore)
                {
                    primary = candidate;
                    primaryScore = sizeScore;
                }
            }

            bodyRenderer = primary;
        }

        private static bool IsAuxiliaryRenderer(SpriteRenderer candidate)
        {
            string rendererName = candidate.gameObject.name;
            return Contains(rendererName, "weapon")
                || Contains(rendererName, "sword")
                || Contains(rendererName, "axe")
                || Contains(rendererName, "pickaxe")
                || Contains(rendererName, "projectile")
                || Contains(rendererName, "hitbox")
                || Contains(rendererName, "helper")
                || Contains(rendererName, "effect")
                || Contains(rendererName, "vfx")
                || Contains(rendererName, "particle")
                || Contains(rendererName, "status")
                || Contains(rendererName, "icon");
        }

        private static bool Contains(string value, string fragment)
        {
            return value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float ReciprocalAbs(float value)
        {
            float magnitude = value < 0f ? -value : value;
            return magnitude > 0.0001f ? 1f / magnitude : 1f;
        }

        private static void AddStatus(ref int mask, MobStatusIconKind kind)
        {
            mask |= 1 << (int)kind;
        }

        private void OnDisable()
        {
            UnsubscribeFromInterventionResults();
            rootVisible = false;
            appliedStatusMask = int.MinValue;
            appliedImaginaryStacks = -1;
            appliedAxiomPresentationHash = int.MinValue;
            axiomPresentationDirty = true;
            for (int index = 0; index < axiomPresentations.Length; index++)
            {
                if (axiomPresentations[index] != null)
                {
                    axiomPresentations[index].Reset();
                }
            }
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromInterventionResults();
            if (root != null) Destroy(root);
        }
    }
}
