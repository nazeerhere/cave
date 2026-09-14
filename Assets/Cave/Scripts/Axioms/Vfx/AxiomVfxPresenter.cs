using System.Collections.Generic;
using Cave.Axioms.Control;
using Cave.Axioms.Frequency;
using Cave.Axioms.Phase;
using Cave.Player;
using UnityEngine;

namespace Cave.Axioms.Vfx
{
    /// <summary>
    /// Actor-local, event-driven presentation bridge for Axiom combat feedback.
    /// It never samples global game state and it has no Update loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomVfxPresenter : MonoBehaviour
    {
        private const string CatalogResourcePath = "Axioms/AxiomVfxCatalog";
        private const string TemporalCatalogResourcePath = "Axioms/AxiomTemporalVfxCatalog";
        private const string ImaginaryCatalogResourcePath = "Axioms/AxiomImaginaryVfxCatalog";
        private static AxiomVfxCatalog cachedCatalog;
        private static AxiomTemporalVfxCatalog cachedTemporalCatalog;
        private static AxiomImaginaryVfxCatalog cachedImaginaryCatalog;
        private static bool catalogLoadAttempted;
        private static bool temporalCatalogLoadAttempted;
        private static bool imaginaryCatalogLoadAttempted;

        private readonly Dictionary<GameObject, float> lastSpawnByPrefab = new Dictionary<GameObject, float>();
        private AxiomRuntimeState runtime;
        private GuardResonanceState resonance;
        private PhaseCombatState phase;
        private AxiomControlState control;
        private PlayerController playerController;
        private PlayerAimDirection playerAimDirection;
        private AxiomVfxInstance activePhaseVulnerability;
        private GameObject activePhaseVulnerabilityPrefab;
        private AxiomVfxInstance activeTemporalPresentation;
        private AxiomVfxInstance activeImaginaryShadow;

        private void Awake()
        {
            runtime = GetComponent<AxiomRuntimeState>();
            resonance = GetComponent<GuardResonanceState>();
            phase = GetComponent<PhaseCombatState>();
            control = GetComponent<AxiomControlState>();
            playerController = GetComponent<PlayerController>();
            playerAimDirection = GetComponent<PlayerAimDirection>();
            ResolveCatalog();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (activePhaseVulnerability != null)
            {
                activePhaseVulnerability.Stop();
            }
            if (activeTemporalPresentation != null)
            {
                activeTemporalPresentation.Stop();
            }
            StopImaginaryShadow();
        }

        /// <summary>Called by actor-local Axiom components added after this presenter.</summary>
        public void RefreshBindings()
        {
            Unsubscribe();
            resonance = GetComponent<GuardResonanceState>();
            phase = GetComponent<PhaseCombatState>();
            control = GetComponent<AxiomControlState>();
            runtime = GetComponent<AxiomRuntimeState>();
            playerController = GetComponent<PlayerController>();
            playerAimDirection = GetComponent<PlayerAimDirection>();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Subscribe()
        {
            if (runtime == null)
            {
                runtime = GetComponent<AxiomRuntimeState>();
            }
            if (resonance == null)
            {
                resonance = GetComponent<GuardResonanceState>();
            }
            if (phase == null)
            {
                phase = GetComponent<PhaseCombatState>();
            }
            if (control == null)
            {
                control = GetComponent<AxiomControlState>();
            }

            if (runtime != null)
            {
                runtime.FeedbackRaised -= HandleRuntimeFeedback;
                runtime.FeedbackRaised += HandleRuntimeFeedback;
            }
            if (resonance != null)
            {
                resonance.ContactResolved -= HandleResonanceContact;
                resonance.ResonanceBroken -= HandleResonanceBreak;
                resonance.DestabilizedStarted -= HandleDestabilizedStarted;
                resonance.Counterphased -= HandleCounterphase;
                resonance.FrequencyFiltered -= HandleFrequencyFiltered;
                resonance.ContactResolved += HandleResonanceContact;
                resonance.ResonanceBroken += HandleResonanceBreak;
                resonance.DestabilizedStarted += HandleDestabilizedStarted;
                resonance.Counterphased += HandleCounterphase;
                resonance.FrequencyFiltered += HandleFrequencyFiltered;
            }
            if (phase != null)
            {
                phase.LatentStackApplied -= HandlePhaseStackApplied;
                phase.Collapsed -= HandlePhaseCollapsed;
                phase.OpeningArmed -= HandleImaginaryOpeningArmed;
                phase.OpeningEnded -= StopImaginaryShadow;
                phase.LatentStackApplied += HandlePhaseStackApplied;
                phase.Collapsed += HandlePhaseCollapsed;
                phase.OpeningArmed += HandleImaginaryOpeningArmed;
                phase.OpeningEnded += StopImaginaryShadow;
            }
            if (control != null)
            {
                control.OpportunityOpened -= HandleOpportunityOpened;
                control.TemporalActivityStarted -= HandleTemporalActivityStarted;
                control.TemporalStageChanged -= HandleTemporalStageChanged;
                control.OpportunityOpened += HandleOpportunityOpened;
                control.TemporalActivityStarted += HandleTemporalActivityStarted;
                control.TemporalStageChanged += HandleTemporalStageChanged;
            }

            // Covers a presenter added after an already-armed actor-local state
            // without introducing a polling loop.
            if (playerController != null && phase != null && phase.HasOpening)
            {
                HandleImaginaryOpeningArmed(phase.RemainingOpeningSeconds);
            }
        }

        private void Unsubscribe()
        {
            if (runtime != null) runtime.FeedbackRaised -= HandleRuntimeFeedback;
            if (resonance != null)
            {
                resonance.ContactResolved -= HandleResonanceContact;
                resonance.ResonanceBroken -= HandleResonanceBreak;
                resonance.DestabilizedStarted -= HandleDestabilizedStarted;
                resonance.Counterphased -= HandleCounterphase;
                resonance.FrequencyFiltered -= HandleFrequencyFiltered;
            }
            if (phase != null)
            {
                phase.LatentStackApplied -= HandlePhaseStackApplied;
                phase.Collapsed -= HandlePhaseCollapsed;
                phase.OpeningArmed -= HandleImaginaryOpeningArmed;
                phase.OpeningEnded -= StopImaginaryShadow;
            }
            if (control != null)
            {
                control.OpportunityOpened -= HandleOpportunityOpened;
                control.TemporalActivityStarted -= HandleTemporalActivityStarted;
                control.TemporalStageChanged -= HandleTemporalStageChanged;
            }
        }

        private void HandleRuntimeFeedback(AxiomFeedbackEvent feedback)
        {
            switch (feedback.FeedbackType)
            {
                case AxiomFeedbackType.StateChanged:
                    PlayElementalApplication(feedback.Kind);
                    break;
                case AxiomFeedbackType.ControlInterventionSucceeded:
                    PlayTransient(ResolveCatalog()?.ControlSuccess);
                    break;
                case AxiomFeedbackType.ControlInterventionFailed:
                    PlayTransient(ResolveCatalog()?.ControlFailure);
                    break;
                case AxiomFeedbackType.ConvergenceRecognized:
                    // Reuse the compact coherent-success cue; Convergence has no
                    // separately approved art or mechanical payoff in this pass.
                    PlayTransient(ResolveCatalog()?.ControlSuccess);
                    break;
            }
        }

        private void HandleResonanceContact(GameObject attacker, GuardResonanceContactResult result)
        {
            if (result.Outcome != GuardResonanceContactOutcome.Advanced)
            {
                return;
            }

            AxiomVfxDefinition definition = ResolveCatalog()?.ResonanceProgress ?? default(AxiomVfxDefinition);
            float scaled = definition.Scale * (1f + Mathf.Clamp(result.Progress, 1, 3) * .08f);
            PlayTransient(definition, scaled);
        }

        private void HandleResonanceBreak(GameObject attacker)
        {
            PlayTransient(ResolveCatalog()?.ResonanceBreak);
        }

        private void HandleDestabilizedStarted()
        {
            PlayTransient(ResolveCatalog()?.Destabilized);
        }

        private void HandleCounterphase(GameObject attacker)
        {
            PlayTransient(ResolveCatalog()?.Counterphase);
        }

        private void HandleFrequencyFiltered(GameObject attacker)
        {
            // A rate-interference glyph communicates adaptation without making a
            // correct cadence look like a normal Resonance gain or a player miss.
            AxiomVfxDefinition definition = ResolveCatalog()?.ErrorRate ?? default(AxiomVfxDefinition);
            PlayTransient(definition, definition.Scale * .75f);
        }

        private void HandlePhaseStackApplied(GameObject source, int stackCount)
        {
            PlayTransient(ResolveCatalog()?.PhaseApply);
        }

        /// <summary>
        /// Plays the approved rupture only after the caller has confirmed a
        /// player melee hit. The attack code owns hit validity and damage.
        /// </summary>
        public void PlayImaginaryImpact(Vector3 impactPosition)
        {
            if (playerController == null)
            {
                return;
            }

            AxiomImaginaryVfxCatalog catalog = ResolveImaginaryCatalog();
            if (catalog == null || catalog.ImpactPrefab == null || !catalog.ImpactRupture.IsValid)
            {
                return;
            }

            GameObject effect = Instantiate(catalog.ImpactPrefab, impactPosition, Quaternion.identity);
            AxiomVfxInstance instance = effect.GetComponent<AxiomVfxInstance>();
            if (instance == null)
            {
                Destroy(effect);
                return;
            }

            float lifetime = catalog.ImpactRupture.Frames.Length / catalog.ImpactRupture.FramesPerSecond;
            instance.Play(null, Vector3.zero, lifetime, catalog.ImpactRupture.Scale, false);
            instance.SetAnimation(catalog.ImpactRupture.Frames, catalog.ImpactRupture.FramesPerSecond);
        }

        private void HandlePhaseCollapsed(GameObject source, int stackCount, PhaseExposureDefinition exposure)
        {
            AxiomVfxCatalog catalog = ResolveCatalog();
            if (catalog == null)
            {
                return;
            }

            if (!exposure.IsActive)
            {
                PlayTransient(catalog.PhaseCollapseEven);
                StopPhaseVulnerability();
                return;
            }

            PlayTransient(exposure.EffectClass == PhaseEffectClass.Strong
                ? catalog.PhaseCollapseStrong
                : catalog.PhaseCollapseWeak);
            float remaining = phase != null ? phase.RemainingExposureSeconds : exposure.Duration;
            PlayPhaseVulnerability(catalog.PhaseVulnerability, remaining);
        }

        private void HandleOpportunityOpened(AxiomKind kind, AxiomErrorState error, bool refreshed)
        {
            if (refreshed)
            {
                return;
            }

            AxiomVfxCatalog catalog = ResolveCatalog();
            if (catalog == null)
            {
                return;
            }

            switch (error.ErrorKind)
            {
                case AxiomErrorKind.State: PlayTransient(catalog.ErrorState); break;
                case AxiomErrorKind.Rate: PlayTransient(catalog.ErrorRate); break;
                case AxiomErrorKind.Acceleration: PlayTransient(catalog.ErrorAcceleration); break;
            }
        }

        private void HandleTemporalActivityStarted(AxiomKind kind)
        {
            SetTemporalPresentation(ResolveTemporalCatalog()?.PulseRing);
        }

        private void HandleTemporalStageChanged(AxiomKind kind, AxiomTemporalLockStage current, AxiomTemporalLockStage previous)
        {
            AxiomTemporalVfxCatalog catalog = ResolveTemporalCatalog();
            if (catalog == null) return;
            if (current > previous)
            {
                PlayTemporalBurst(catalog.LockFlash);
            }
            else if (current < previous)
            {
                PlayTemporalBurst(current == AxiomTemporalLockStage.Uncontrolled ? catalog.FractureCollapse : catalog.UnstableBreak);
            }

            switch (current)
            {
                case AxiomTemporalLockStage.State: SetTemporalPresentation(catalog.LockStar); break;
                case AxiomTemporalLockStage.Rate: SetTemporalPresentation(catalog.OrbitalLock); break;
                case AxiomTemporalLockStage.Acceleration: SetTemporalPresentation(catalog.CoherenceRing); break;
                default: SetTemporalPresentation(catalog.PulseRing); break;
            }
        }

        private void PlayElementalApplication(AxiomKind kind)
        {
            AxiomVfxCatalog catalog = ResolveCatalog();
            if (catalog == null)
            {
                return;
            }

            switch (kind)
            {
                case AxiomKind.Heat: PlayTransient(catalog.Heat); break;
                case AxiomKind.Order: PlayTransient(catalog.Order); break;
                case AxiomKind.Flow: PlayTransient(catalog.Flow); break;
                case AxiomKind.Mass: PlayTransient(catalog.Mass); break;
            }
        }

        private void PlayTransient(AxiomVfxDefinition? nullableDefinition, float scaleMultiplier = 1f)
        {
            if (!nullableDefinition.HasValue)
            {
                return;
            }
            PlayTransient(nullableDefinition.Value, scaleMultiplier);
        }

        private void PlayTransient(AxiomVfxDefinition definition, float scaleMultiplier = 1f)
        {
            if (!definition.IsValid || !CanPlay(definition))
            {
                return;
            }

            GameObject effect = Instantiate(definition.Prefab);
            AxiomVfxInstance instance = effect.GetComponent<AxiomVfxInstance>();
            if (instance == null)
            {
                Destroy(effect);
                return;
            }

            instance.Play(transform, definition.LocalOffset, definition.Lifetime, definition.Scale * scaleMultiplier, false);
        }

        private void PlayPhaseVulnerability(AxiomVfxDefinition definition, float duration)
        {
            if (!definition.IsValid)
            {
                return;
            }

            if (activePhaseVulnerability != null && activePhaseVulnerabilityPrefab == definition.Prefab)
            {
                activePhaseVulnerability.Refresh(duration);
                return;
            }

            StopPhaseVulnerability();
            GameObject effect = Instantiate(definition.Prefab);
            activePhaseVulnerability = effect.GetComponent<AxiomVfxInstance>();
            activePhaseVulnerabilityPrefab = definition.Prefab;
            if (activePhaseVulnerability == null)
            {
                Destroy(effect);
                return;
            }

            activePhaseVulnerability.Play(transform, definition.LocalOffset, duration, definition.Scale, true);
        }

        private void StopPhaseVulnerability()
        {
            if (activePhaseVulnerability != null)
            {
                activePhaseVulnerability.Stop();
            }
            activePhaseVulnerability = null;
            activePhaseVulnerabilityPrefab = null;
        }

        private void HandleImaginaryOpeningArmed(float duration)
        {
            if (playerController == null)
            {
                return;
            }

            AxiomImaginaryVfxCatalog catalog = ResolveImaginaryCatalog();
            if (catalog == null || catalog.ShadowPrefab == null || !catalog.ShadowSmear.IsValid)
            {
                return;
            }

            if (activeImaginaryShadow != null)
            {
                activeImaginaryShadow.Refresh(duration);
                return;
            }

            GameObject effect = Instantiate(catalog.ShadowPrefab);
            activeImaginaryShadow = effect.GetComponent<AxiomVfxInstance>();
            if (activeImaginaryShadow == null)
            {
                Destroy(effect);
                return;
            }

            float horizontalOffset = playerAimDirection != null && playerAimDirection.FacingDirection.x < 0f
                ? .08f
                : -.08f;
            activeImaginaryShadow.Play(transform, new Vector3(horizontalOffset, .03f, 0f), duration, catalog.ShadowSmear.Scale, true);
            activeImaginaryShadow.SetAnimation(catalog.ShadowSmear.Frames, catalog.ShadowSmear.FramesPerSecond);
        }

        private void StopImaginaryShadow()
        {
            if (activeImaginaryShadow != null)
            {
                activeImaginaryShadow.Stop();
            }
            activeImaginaryShadow = null;
        }

        private void SetTemporalPresentation(AxiomSpriteAnimation? animation)
        {
            if (!animation.HasValue || !animation.Value.IsValid) return;
            AxiomTemporalVfxCatalog catalog = ResolveTemporalCatalog();
            if (catalog == null || catalog.PersistentPrefab == null) return;
            if (activeTemporalPresentation == null)
            {
                GameObject effect = Instantiate(catalog.PersistentPrefab);
                activeTemporalPresentation = effect.GetComponent<AxiomVfxInstance>();
                if (activeTemporalPresentation == null) { Destroy(effect); return; }
                activeTemporalPresentation.Play(transform, new Vector3(0f, .42f, 0f), 3600f, animation.Value.Scale, true);
            }
            activeTemporalPresentation.SetAnimation(animation.Value.Frames, animation.Value.FramesPerSecond);
        }

        private void PlayTemporalBurst(AxiomSpriteAnimation animation)
        {
            if (!animation.IsValid) return;
            AxiomTemporalVfxCatalog catalog = ResolveTemporalCatalog();
            if (catalog == null || catalog.PersistentPrefab == null) return;
            GameObject effect = Instantiate(catalog.PersistentPrefab);
            AxiomVfxInstance instance = effect.GetComponent<AxiomVfxInstance>();
            if (instance == null) { Destroy(effect); return; }
            instance.Play(transform, new Vector3(0f, .42f, 0f), .42f, animation.Scale, false);
            instance.SetAnimation(animation.Frames, animation.FramesPerSecond);
        }

        private bool CanPlay(AxiomVfxDefinition definition)
        {
            if (definition.Prefab == null)
            {
                return false;
            }

            float previous;
            if (lastSpawnByPrefab.TryGetValue(definition.Prefab, out previous)
                && Time.time - previous < definition.Cooldown)
            {
                return false;
            }

            lastSpawnByPrefab[definition.Prefab] = Time.time;
            return true;
        }

        private static AxiomVfxCatalog ResolveCatalog()
        {
            if (!catalogLoadAttempted)
            {
                cachedCatalog = Resources.Load<AxiomVfxCatalog>(CatalogResourcePath);
                catalogLoadAttempted = true;
            }
            return cachedCatalog;
        }

        private static AxiomTemporalVfxCatalog ResolveTemporalCatalog()
        {
            if (!temporalCatalogLoadAttempted)
            {
                cachedTemporalCatalog = Resources.Load<AxiomTemporalVfxCatalog>(TemporalCatalogResourcePath);
                temporalCatalogLoadAttempted = true;
            }
            return cachedTemporalCatalog;
        }

        private static AxiomImaginaryVfxCatalog ResolveImaginaryCatalog()
        {
            if (!imaginaryCatalogLoadAttempted)
            {
                cachedImaginaryCatalog = Resources.Load<AxiomImaginaryVfxCatalog>(ImaginaryCatalogResourcePath);
                imaginaryCatalogLoadAttempted = true;
            }
            return cachedImaginaryCatalog;
        }
    }
}
