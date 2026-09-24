using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Interactions;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Explicit encounter form; presentation never determines this state.</summary>
    public enum FalseGodForm
    {
        Prophet,
        Monk,
        TrueBodyFinalPending
    }

    public enum FalseGodTitheOutcome
    {
        Heal,
        TemporaryShield
    }

    public enum ClaimCrystalConfigurationMode
    {
        Relay,
        Defensive,
        Offensive
    }

    /// <summary>
    /// Completes each non-final defeat as a controlled form transition. The
    /// final True Body flow remains responsible for permanent completion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(FalseGodRuntimeFoundation))]
    public sealed class FalseGodFormController : MonoBehaviour
    {
        [SerializeField] private bool completeMonkAscensionAutomatically = true;
        [SerializeField, Min(1)] private int monkMaximumHealth = 18;

        private Damageable damageable;
        private FalseGodProphetPhaseBoundary phaseBoundary;
        private FalseGodCombatController combat;
        private bool subscribed;
        private bool monkTransitionPending;
        private bool prophetDefeatHandled;

        public FalseGodForm CurrentForm { get; private set; } = FalseGodForm.Prophet;
        public bool IsMonkTransitionPending => monkTransitionPending;
        public event Action<FalseGodForm> FormChanged;
        public event Action MonkAscensionRequested;
        public event Action TrueBodyDimensionTransitionRequested;

        private void Awake()
        {
            InitializeRuntimeDependencies();
        }

        private void OnEnable()
        {
            InitializeRuntimeDependencies();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void InitializeRuntimeDependencies()
        {
            Damageable resolvedDamageable = GetComponent<Damageable>();
            if (damageable != resolvedDamageable)
            {
                Unsubscribe();
                damageable = resolvedDamageable;
            }

            phaseBoundary = GetComponent<FalseGodProphetPhaseBoundary>();
            phaseBoundary?.InitializeRuntimeDependencies();
            combat = GetComponent<FalseGodCombatController>();
            Subscribe();
        }

        /// <summary>Animation may call this later when automatic completion is disabled.</summary>
        public bool CompleteMonkAscension()
        {
            if (!monkTransitionPending || CurrentForm != FalseGodForm.Prophet)
            {
                return false;
            }

            monkTransitionPending = false;
            CurrentForm = FalseGodForm.Monk;
            damageable?.SetRuntimeMaximumHealth(monkMaximumHealth, true);
            combat?.ActivateMonkForm();
            FormChanged?.Invoke(CurrentForm);
            return true;
        }

        public void RequestMonkAscension()
        {
            if (CurrentForm != FalseGodForm.Prophet || monkTransitionPending)
            {
                return;
            }

            monkTransitionPending = true;
            MonkAscensionRequested?.Invoke();
            if (completeMonkAscensionAutomatically)
            {
                CompleteMonkAscension();
            }
        }

        private void HandleDied()
        {
            // PhaseBoundary is an earlier Damageable subscriber. It may complete
            // the requested ascension before this callback observes the same
            // death, so the one-shot flag records the actual defeated form.
            if (!prophetDefeatHandled && (CurrentForm == FalseGodForm.Prophet
                || (phaseBoundary != null && phaseBoundary.HasRequestedAscension)))
            {
                // Damageable evaluates this after its Died event. This preserves
                // normal death behavior for every other owner.
                damageable?.SuppressNextDeactivation();
                RequestMonkAscension();
                prophetDefeatHandled = true;
                return;
            }

            if (CurrentForm == FalseGodForm.Monk)
            {
                damageable?.SuppressNextDeactivation();
                CurrentForm = FalseGodForm.TrueBodyFinalPending;
                combat?.EnterFinalAscensionPending();
                FormChanged?.Invoke(CurrentForm);
                TrueBodyDimensionTransitionRequested?.Invoke();
            }
        }

        private void Subscribe()
        {
            if (subscribed || damageable == null)
            {
                return;
            }

            subscribed = true;
            damageable.Died += HandleDied;
            if (phaseBoundary != null)
            {
                phaseBoundary.AscensionRequested -= RequestMonkAscension;
                phaseBoundary.AscensionRequested += RequestMonkAscension;
            }
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
            if (damageable != null)
            {
                damageable.Died -= HandleDied;
            }

            if (phaseBoundary != null)
            {
                phaseBoundary.AscensionRequested -= RequestMonkAscension;
            }
        }
    }

    /// <summary>Small typed functional state attached only to a reconfigured crystal.</summary>
    [DisallowMultipleComponent]
    public sealed class ClaimCrystalConfiguration : MonoBehaviour
    {
        [SerializeField] private ClaimCrystalConfigurationMode mode = ClaimCrystalConfigurationMode.Relay;
        [SerializeField, Min(0.05f)] private float offensivePulseCooldown = 1.2f;

        private ClaimCrystal crystal;
        private float nextPulseTime;

        public ClaimCrystalConfigurationMode Mode => mode;
        public event Action<ClaimCrystalConfigurationMode> ModeChanged;

        public void InitializeRuntime(ClaimCrystal owner)
        {
            crystal = owner != null ? owner : GetComponent<ClaimCrystal>();
        }

        public bool TrySetMode(ClaimCrystalConfigurationMode requestedMode)
        {
            if (crystal == null)
            {
                InitializeRuntime(null);
            }

            if (crystal == null || !crystal.IsActive)
            {
                return false;
            }

            if (mode == requestedMode)
            {
                return true;
            }

            mode = requestedMode;
            ModeChanged?.Invoke(mode);
            FalseGodCombatPresentation.CreateCrystalImpact(transform.position);
            return true;
        }

        /// <summary>Bounded offensive support; it never changes authority or scans for targets.</summary>
        public bool TryEmitOffensivePulse(PlayerHealth target)
        {
            if (mode != ClaimCrystalConfigurationMode.Offensive
                || crystal == null || !crystal.IsActive || target == null
                || Time.time < nextPulseTime)
            {
                return false;
            }

            nextPulseTime = Time.time + offensivePulseCooldown;
            target.TryTakeDamage(1, new DamageContext(gameObject, DamageTrait.AreaOfEffect));
            FalseGodCombatPresentation.CreateImpactTelegraph(transform.position, 0.55f, 0.25f);
            return true;
        }
    }

    /// <summary>
    /// Version 2 authority conversion. It acts only on supplied/network-local
    /// Claim relationships and holds no global ownership database.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodRuntimeFoundation), typeof(FalseGodFormController))]
    public sealed class FalseGodMonkCombat : MonoBehaviour
    {
        private const int MaximumRepossessionCandidates = 8;

        [SerializeField, Min(1)] private int titheHealAmount = 4;
        [SerializeField, Range(0f, 1f)] private float temporaryShieldBlockBonus = 0.18f;
        [SerializeField, Min(0.1f)] private float temporaryShieldDuration = 3f;

        private readonly List<InteractionIdentity> repossessionCandidates = new List<InteractionIdentity>(MaximumRepossessionCandidates);
        private readonly List<ClaimAnchor> anchorScratch = new List<ClaimAnchor>(8);
        private readonly List<ClaimCrystal> configuredCrystals = new List<ClaimCrystal>(8);
        private FalseGodRuntimeFoundation foundation;
        private FalseGodFormController form;
        private Damageable damageable;
        private EnemyDefenseController defense;
        private float shieldExpiresAt;
        private bool shieldApplied;
        private float nextReconfigurationTime;

        public int ReconfiguredCrystalCount => configuredCrystals.Count;
        public bool HasTemporaryShield => shieldApplied;
        public bool CanSelectReconfiguration => Time.time >= nextReconfigurationTime;

        private void Awake()
        {
            InitializeRuntimeDependencies();
        }

        private void Update()
        {
            if (shieldApplied && Time.time >= shieldExpiresAt)
            {
                ClearTemporaryShield();
            }
        }

        private void OnDisable()
        {
            ClearRuntimeState();
        }

        public void InitializeRuntimeDependencies()
        {
            foundation = GetComponent<FalseGodRuntimeFoundation>();
            foundation?.InitializeRuntime();
            form = GetComponent<FalseGodFormController>();
            damageable = GetComponent<Damageable>();
            defense = GetComponent<EnemyDefenseController>();
            defense?.InitializeRuntimeDependencies();
        }

        public void RegisterRepossessionCandidate(InteractionIdentity candidate)
        {
            if (candidate == null || repossessionCandidates.Contains(candidate))
            {
                return;
            }

            if (repossessionCandidates.Count >= MaximumRepossessionCandidates)
            {
                repossessionCandidates.RemoveAt(0);
            }

            repossessionCandidates.Add(candidate);
        }

        public bool TryGetRepossessionCandidate(out InteractionIdentity candidate)
        {
            candidate = null;
            if (!CanUseMonkAuthority())
            {
                return false;
            }

            GameObject source = foundation.AuthorityNetwork.AuthoritySource;
            for (int index = repossessionCandidates.Count - 1; index >= 0; index--)
            {
                InteractionIdentity entry = repossessionCandidates[index];
                if (entry == null)
                {
                    repossessionCandidates.RemoveAt(index);
                    continue;
                }

                if (entry.Ownership != InteractionOwnership.Claim && entry.HasAuthorityHistory(source))
                {
                    candidate = entry;
                    return true;
                }
            }

            return false;
        }

        public bool TryRepossess(InteractionIdentity target, out ClaimResult result)
        {
            result = default;
            if (!CanUseMonkAuthority() || target == null || !target.IsClaimable)
            {
                return false;
            }

            RegisterRepossessionCandidate(target);
            if (target.Ownership == InteractionOwnership.Claim
                || !target.HasAuthorityHistory(foundation.AuthorityNetwork.AuthoritySource))
            {
                return false;
            }

            FalseGodCombatPresentation.CreateCaptureCue(target.transform.position);
            result = ClaimResolver.Resolve(new ClaimAttempt(
                foundation.AuthorityIdentity,
                target,
                ClaimProvenance.Repossession,
                foundation.AuthorityIdentity.AuthorityStrength));
            if (result.Succeeded)
            {
                foundation.AuthorityNetwork.RegisterClaimedObject(target);
            }

            return result.Succeeded;
        }

        public bool TryGetTitheSource(out ClaimCrystal source)
        {
            source = null;
            if (!CanUseMonkAuthority())
            {
                return false;
            }

            foundation.AuthorityNetwork.CopyActiveAnchors(anchorScratch);
            int bestId = int.MaxValue;
            for (int index = 0; index < anchorScratch.Count; index++)
            {
                ClaimCrystal candidate = anchorScratch[index] != null
                    ? anchorScratch[index].GetComponent<ClaimCrystal>()
                    : null;
                if (IsValidTitheSource(candidate) && candidate.GetInstanceID() < bestId)
                {
                    source = candidate;
                    bestId = candidate.GetInstanceID();
                }
            }

            return source != null;
        }

        public bool TryTithe(ClaimCrystal source, FalseGodTitheOutcome outcome, out int benefit)
        {
            benefit = 0;
            if (!CanUseMonkAuthority() || !IsValidTitheSource(source))
            {
                return false;
            }

            Vector2 sourcePosition = source.transform.position;
            source.RetireAuthority();
            if (source.IsActive)
            {
                return false;
            }

            if (outcome == FalseGodTitheOutcome.Heal)
            {
                benefit = damageable != null ? damageable.RestoreHealthResolved(titheHealAmount) : 0;
            }
            else
            {
                ApplyTemporaryShield();
                benefit = shieldApplied ? 1 : 0;
            }

            if (benefit <= 0)
            {
                return false;
            }

            FalseGodCombatPresentation.CreateImpactTelegraph(sourcePosition, 0.48f, 0.3f);
            return true;
        }

        public bool TryGetReconfigurationTarget(out ClaimCrystal crystal)
        {
            return TryGetTitheSource(out crystal);
        }

        public bool TryReconfigure(
            ClaimCrystal crystal,
            ClaimCrystalConfigurationMode mode,
            PlayerHealth offensiveTarget = null)
        {
            if (!CanUseMonkAuthority() || crystal == null || !crystal.IsActive
                || crystal.Anchor == null || crystal.Anchor.AuthorityNetwork != foundation.AuthorityNetwork)
            {
                return false;
            }

            ClaimCrystalConfiguration configuration = crystal.GetComponent<ClaimCrystalConfiguration>();
            if (configuration == null)
            {
                configuration = crystal.gameObject.AddComponent<ClaimCrystalConfiguration>();
            }

            configuration.InitializeRuntime(crystal);
            if (!configuration.TrySetMode(mode))
            {
                return false;
            }

            if (!configuredCrystals.Contains(crystal))
            {
                configuredCrystals.Add(crystal);
            }

            nextReconfigurationTime = Time.time + 2f;

            if (mode == ClaimCrystalConfigurationMode.Defensive)
            {
                ApplyTemporaryShield();
            }
            else if (mode == ClaimCrystalConfigurationMode.Offensive)
            {
                configuration.TryEmitOffensivePulse(offensiveTarget);
            }

            return true;
        }

        public void ClearRuntimeState()
        {
            repossessionCandidates.Clear();
            configuredCrystals.Clear();
            ClearTemporaryShield();
        }

        private bool CanUseMonkAuthority()
        {
            InitializeRuntimeDependencies();
            return form != null && form.CurrentForm == FalseGodForm.Monk
                && foundation != null && foundation.AuthorityIdentity != null
                && foundation.AuthorityNetwork != null && foundation.AuthorityNetwork.CanMaintainAuthority;
        }

        private bool IsValidTitheSource(ClaimCrystal source)
        {
            return source != null && source.IsActive && source.Anchor != null
                && source.Anchor.AuthorityNetwork == foundation.AuthorityNetwork;
        }

        private void ApplyTemporaryShield()
        {
            if (defense == null)
            {
                defense = GetComponent<EnemyDefenseController>();
                defense?.InitializeRuntimeDependencies();
            }

            if (defense == null)
            {
                return;
            }

            defense.SetRuntimeBlockChanceBonus(temporaryShieldBlockBonus);
            shieldApplied = true;
            shieldExpiresAt = Time.time + temporaryShieldDuration;
        }

        private void ClearTemporaryShield()
        {
            if (shieldApplied && defense != null)
            {
                defense.SetRuntimeBlockChanceBonus(0f);
            }

            shieldApplied = false;
            shieldExpiresAt = 0f;
            nextReconfigurationTime = 0f;
        }
    }
}
