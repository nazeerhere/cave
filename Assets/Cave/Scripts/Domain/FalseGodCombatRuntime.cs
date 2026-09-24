using System;
using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Interactions;
using Cave.Player;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Inspectable, bounded first-pass False God combat states.</summary>
    public enum FalseGodAbilityKind
    {
        None,
        CrystalRain,
        CausalBeam,
        Appropriation,
        EchoProjectile,
        SlowBolt,
        DashShadow,
        CrystalChainHook,
        TriuneAnchors,
        Block,
        Repossession,
        Tithe,
        Reconfiguration,
        ClaimBurst,
        ClaimTeleport
    }

    /// <summary>
    /// Small coordinator for the first Claim-based False God moves. It owns no
    /// phase logic and never discovers targets; a future brain supplies the
    /// target explicitly through SetCombatTarget.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodRuntimeFoundation))]
    public sealed class FalseGodCombatController : MonoBehaviour
    {
        [SerializeField] private Transform combatTarget;

        private FalseGodRuntimeFoundation foundation;
        private Damageable damageable;
        private CrystalRainAbility crystalRain;
        private CausalBeamAbility causalBeam;
        private ClaimedProjectileHold projectileHold;
        private FalseGodProphetBasicCast prophetBasicCast;
        private FalseGodDashShadow prophetDashShadow;
        private FalseGodCrystalChainHook prophetChainHook;
        private FalseGodTriuneAnchors triuneAnchors;
        private FalseGodProphetBlock prophetBlock;
        private FalseGodProphetPhaseBoundary phaseBoundary;
        private FalseGodFormController formController;
        private FalseGodMonkCombat monkCombat;
        private FalseGodClaimBurst claimBurst;
        private FalseGodClaimTeleport claimTeleport;
        private bool stopped;
        private bool deathSubscribed;
        private readonly List<FalseGodAbilityKind> actionScratch = new List<FalseGodAbilityKind>(8);
        private FalseGodAbilityKind previousAction;

        public FalseGodAbilityKind CurrentAbility { get; private set; }
        public FalseGodProphetPresentationState PresentationState { get; private set; } = FalseGodProphetPresentationState.Idle;
        public ClaimLoadSnapshot ClaimLoad => foundation != null ? foundation.ClaimLoad : default;
        public FalseGodForm CurrentForm => formController != null ? formController.CurrentForm : FalseGodForm.Prophet;
        public bool IsCasting => CurrentAbility != FalseGodAbilityKind.None;
        public event Action<FalseGodProphetPresentationState> PresentationStateChanged;

        private void Awake()
        {
            InitializeRuntime();
        }

        /// <summary>Explicit factory/verification setup without invoking Unity lifecycle methods manually.</summary>
        public void InitializeRuntime()
        {
            foundation = GetComponent<FalseGodRuntimeFoundation>();
            damageable = GetComponent<Damageable>();
            crystalRain = GetComponent<CrystalRainAbility>();
            causalBeam = GetComponent<CausalBeamAbility>();
            projectileHold = GetComponent<ClaimedProjectileHold>();
            prophetBasicCast = GetComponent<FalseGodProphetBasicCast>();
            prophetDashShadow = GetComponent<FalseGodDashShadow>();
            prophetChainHook = GetComponent<FalseGodCrystalChainHook>();
            triuneAnchors = GetComponent<FalseGodTriuneAnchors>();
            prophetBlock = GetComponent<FalseGodProphetBlock>();
            phaseBoundary = GetComponent<FalseGodProphetPhaseBoundary>();
            formController = GetComponent<FalseGodFormController>();
            monkCombat = GetComponent<FalseGodMonkCombat>();
            claimBurst = GetComponent<FalseGodClaimBurst>();
            claimTeleport = GetComponent<FalseGodClaimTeleport>();
            projectileHold?.InitializeRuntime(foundation != null ? foundation.AuthorityNetwork : null);
            triuneAnchors?.InitializeRuntimeDependencies();
            if (phaseBoundary != null)
            {
                phaseBoundary.InitializeRuntimeDependencies();
                phaseBoundary.AscensionRequested -= HandleAscensionRequested;
                phaseBoundary.AscensionRequested += HandleAscensionRequested;
            }
            formController?.InitializeRuntimeDependencies();
            monkCombat?.InitializeRuntimeDependencies();
            SubscribeDeath();
        }

        private void OnEnable()
        {
            stopped = false;
            SubscribeDeath();
        }

        private void OnDisable()
        {
            UnsubscribeDeath();

            StopCombat();
        }

        private void Update()
        {
            if (stopped || combatTarget == null)
            {
                return;
            }

            RefreshCastState();
            if (CurrentAbility != FalseGodAbilityKind.None)
            {
                return;
            }

            if (CurrentForm == FalseGodForm.TrueBodyFinalPending)
            {
                return;
            }

            if (CurrentForm == FalseGodForm.Monk)
            {
                UpdateMonkCombat();
                return;
            }

            // Establish the first authority source before selecting from the
            // contextual action pool. Every other action keeps its own legality.
            if (foundation != null
                && foundation.ActiveClaimAnchors == 0
                && crystalRain != null
                && crystalRain.TryBegin(combatTarget.position))
            {
                CurrentAbility = FalseGodAbilityKind.CrystalRain;
                SetPresentationState(FalseGodProphetPresentationMap.ForAbility(CurrentAbility));
                return;
            }

            PlayerHealth targetHealth = combatTarget.GetComponentInParent<PlayerHealth>();
            float basicDamageMultiplier = triuneAnchors != null
                ? triuneAnchors.ResolveBasicCastDamageMultiplier()
                : 1f;
            float distance = Vector2.Distance(transform.position, combatTarget.position);
            if (distance < 1.5f && UnityEngine.Random.value < 0.02f && claimTeleport != null && claimTeleport.TryTeleport())
            {
                previousAction = FalseGodAbilityKind.ClaimTeleport;
                return;
            }
            TrySelectProphetAction(targetHealth, distance, basicDamageMultiplier);
        }

        public void SetCombatTarget(Transform target)
        {
            combatTarget = target;
        }

        /// <summary>
        /// Explicit ability entry point for a projectile that has already been
        /// registered by trigger/event-driven gameplay. It intentionally does
        /// not search the scene for projectiles.
        /// </summary>
        public bool TryAppropriate(PlayerProjectile projectile, out ClaimResult result)
        {
            result = default;
            if (stopped || CurrentAbility != FalseGodAbilityKind.None || projectileHold == null)
            {
                return false;
            }

            CurrentAbility = FalseGodAbilityKind.Appropriation;
            SetPresentationState(FalseGodProphetPresentationMap.ForAbility(CurrentAbility));
            bool captured = projectileHold.TryCapture(projectile, out result);
            CurrentAbility = FalseGodAbilityKind.None;
            SetPresentationState(FalseGodProphetPresentationState.Idle);
            return captured;
        }

        public bool TryRepossess(InteractionIdentity target, out ClaimResult result)
        {
            result = default;
            if (stopped || CurrentForm != FalseGodForm.Monk || monkCombat == null)
            {
                return false;
            }

            CurrentAbility = FalseGodAbilityKind.Repossession;
            SetPresentationState(FalseGodProphetPresentationMap.ForMonkAbility(CurrentAbility));
            bool succeeded = monkCombat.TryRepossess(target, out result);
            CurrentAbility = FalseGodAbilityKind.None;
            SetPresentationState(FalseGodProphetPresentationState.Idle);
            return succeeded;
        }

        public bool TryTithe(ClaimCrystal source, FalseGodTitheOutcome outcome, out int benefit)
        {
            benefit = 0;
            if (stopped || CurrentForm != FalseGodForm.Monk || monkCombat == null)
            {
                return false;
            }

            CurrentAbility = FalseGodAbilityKind.Tithe;
            SetPresentationState(FalseGodProphetPresentationMap.ForMonkAbility(CurrentAbility));
            bool succeeded = monkCombat.TryTithe(source, outcome, out benefit);
            CurrentAbility = FalseGodAbilityKind.None;
            SetPresentationState(FalseGodProphetPresentationState.Idle);
            return succeeded;
        }

        public bool TryReconfigure(ClaimCrystal crystal, ClaimCrystalConfigurationMode mode, PlayerHealth offensiveTarget = null)
        {
            if (stopped || CurrentForm != FalseGodForm.Monk || monkCombat == null)
            {
                return false;
            }

            CurrentAbility = FalseGodAbilityKind.Reconfiguration;
            SetPresentationState(FalseGodProphetPresentationMap.ForMonkAbility(CurrentAbility));
            bool succeeded = monkCombat.TryReconfigure(crystal, mode, offensiveTarget);
            CurrentAbility = FalseGodAbilityKind.None;
            SetPresentationState(FalseGodProphetPresentationState.Idle);
            return succeeded;
        }

        /// <summary>Reactive entry point; actual damage resolution remains in EnemyDefenseController.</summary>
        public bool TryBlock()
        {
            if (stopped || CurrentAbility == FalseGodAbilityKind.CrystalRain
                || CurrentAbility == FalseGodAbilityKind.CausalBeam || prophetBlock == null)
            {
                return false;
            }

            if (!prophetBlock.TryBeginBlock())
            {
                return false;
            }

            CurrentAbility = FalseGodAbilityKind.Block;
            SetPresentationState(FalseGodProphetPresentationMap.ForAbility(CurrentAbility));
            return true;
        }

        public void SetPresentationState(FalseGodProphetPresentationState state)
        {
            if (PresentationState == state)
            {
                return;
            }

            PresentationState = state;
            PresentationStateChanged?.Invoke(state);
        }

        public void ReleasePresentationHold()
        {
            GetComponent<FalseGodProphetPresentation>()?.ReleaseGameplayHold();
        }

        private void TrySelectProphetAction(PlayerHealth targetHealth, float distance, float damageMultiplier)
        {
            actionScratch.Clear();
            triuneAnchors?.SetSuppressionTarget(combatTarget);
            if (triuneAnchors != null && triuneAnchors.ActiveAnchorCount < 3) actionScratch.Add(FalseGodAbilityKind.TriuneAnchors);
            if (targetHealth != null && foundation != null && foundation.ActiveClaimAnchors > 0 && prophetChainHook != null) actionScratch.Add(FalseGodAbilityKind.CrystalChainHook);
            if (prophetBasicCast != null) actionScratch.Add(FalseGodAbilityKind.EchoProjectile);
            if (prophetBasicCast != null && distance > 3.5f) actionScratch.Add(FalseGodAbilityKind.SlowBolt);
            if (prophetDashShadow != null && distance > 1.5f && distance < 8f) actionScratch.Add(FalseGodAbilityKind.DashShadow);
            if (causalBeam != null && distance > 2.5f) actionScratch.Add(FalseGodAbilityKind.CausalBeam);
            PlayerAttackState playerPressure = targetHealth != null ? targetHealth.GetComponent<PlayerAttackState>() : null;
            bool closePressure = distance < 2.25f && playerPressure != null
                && (playerPressure.IsDefending || playerPressure.IsActivelyAttacking);
            if (closePressure) actionScratch.Add(FalseGodAbilityKind.Block);
            if (distance < 2.2f && playerPressure != null && playerPressure.IsDefending && claimBurst != null) actionScratch.Add(FalseGodAbilityKind.ClaimBurst);

            for (int attempts = actionScratch.Count; attempts > 0; attempts--)
            {
                int pick = UnityEngine.Random.Range(0, actionScratch.Count);
                if (actionScratch.Count > 1 && actionScratch[pick] == previousAction)
                {
                    pick = (pick + 1) % actionScratch.Count;
                }

                FalseGodAbilityKind choice = actionScratch[pick];
                actionScratch.RemoveAt(pick);
                bool started = choice == FalseGodAbilityKind.EchoProjectile ? prophetBasicCast.TryCastEcho(combatTarget, damageMultiplier)
                    : choice == FalseGodAbilityKind.SlowBolt ? prophetBasicCast.TryCastSlowBolt(combatTarget, damageMultiplier)
                    : choice == FalseGodAbilityKind.DashShadow ? prophetDashShadow.TryCast(combatTarget, damageMultiplier)
                    : choice == FalseGodAbilityKind.CausalBeam ? causalBeam.TryBegin(combatTarget.position, targetHealth)
                    : choice == FalseGodAbilityKind.CrystalChainHook ? prophetChainHook.TryCast(targetHealth)
                    : choice == FalseGodAbilityKind.ClaimBurst ? claimBurst.TryCast(targetHealth)
                    : choice == FalseGodAbilityKind.Block ? TryBlock()
                    : TryCreateNextTriune();
                if (started)
                {
                    if (choice != FalseGodAbilityKind.Block) CurrentAbility = choice;
                    previousAction = choice;
                    SetPresentationState(FalseGodProphetPresentationMap.ForAbility(choice));
                    return;
                }
            }
        }

        private bool TryCreateNextTriune()
        {
            if (triuneAnchors == null) return false;
            FalseGodTriuneAnchorKind next = !triuneAnchors.HasActive(FalseGodTriuneAnchorKind.Heal) ? FalseGodTriuneAnchorKind.Heal
                : !triuneAnchors.HasActive(FalseGodTriuneAnchorKind.Empower) ? FalseGodTriuneAnchorKind.Empower : FalseGodTriuneAnchorKind.Suppress;
            return triuneAnchors.TryCreate(next);
        }

        public void StopCombat()
        {
            if (stopped)
            {
                return;
            }

            stopped = true;
            crystalRain?.CancelCast();
            causalBeam?.CancelCast();
            prophetBasicCast?.CancelCast();
            prophetBasicCast?.RetireAllProjectiles();
            prophetDashShadow?.CancelCast();
            prophetDashShadow?.RetireLastShadow();
            prophetChainHook?.BreakTether();
            triuneAnchors?.RetireAll();
            projectileHold?.RetireAll();
            monkCombat?.ClearRuntimeState();
            CurrentAbility = FalseGodAbilityKind.None;
            SetPresentationState(FalseGodProphetPresentationState.Idle);
        }

        private void HandleDied()
        {
            // FormController may complete Prophet -> Monk from an earlier
            // Damageable subscription. Do not subsequently stop the newly
            // activated Monk controller for the same resolved defeat.
            if (formController != null && formController.CurrentForm == FalseGodForm.Monk)
            {
                stopped = false;
                SetPresentationState(FalseGodProphetPresentationState.Idle);
                return;
            }

            StopCombat();
            if (phaseBoundary != null && phaseBoundary.HasRequestedAscension)
            {
                SetPresentationState(FalseGodProphetPresentationState.Ascension);
            }
        }

        public void ActivateMonkForm()
        {
            StopCombat();
            stopped = false;
            prophetBlock?.ConfigureMonkBlock();
            monkCombat?.InitializeRuntimeDependencies();
            SetPresentationState(FalseGodProphetPresentationState.Idle);
        }

        public void EnterFinalAscensionPending()
        {
            StopCombat();
            SetPresentationState(FalseGodProphetPresentationState.FinalAscension);
        }

        private void UpdateMonkCombat()
        {
            if (monkCombat == null)
            {
                return;
            }

            PlayerHealth targetHealth = combatTarget.GetComponentInParent<PlayerHealth>();
            if (monkCombat.TryGetRepossessionCandidate(out InteractionIdentity repossessionTarget)
                && TryRepossess(repossessionTarget, out _))
            {
                return;
            }

            bool underPressure = damageable != null && damageable.CurrentHealth * 2 <= damageable.MaximumHealth;
            if ((ClaimLoad.TotalRelationships >= 2 || underPressure)
                && monkCombat.TryGetTitheSource(out ClaimCrystal titheSource)
                && TryTithe(titheSource, underPressure ? FalseGodTitheOutcome.Heal : FalseGodTitheOutcome.TemporaryShield, out _))
            {
                return;
            }

            if (monkCombat.CanSelectReconfiguration
                && monkCombat.TryGetReconfigurationTarget(out ClaimCrystal crystal))
            {
                ClaimCrystalConfiguration configuration = crystal.GetComponent<ClaimCrystalConfiguration>();
                ClaimCrystalConfigurationMode next = configuration == null
                    ? ClaimCrystalConfigurationMode.Defensive
                    : configuration.Mode == ClaimCrystalConfigurationMode.Defensive
                        ? ClaimCrystalConfigurationMode.Offensive
                        : ClaimCrystalConfigurationMode.Defensive;
                TryReconfigure(crystal, next, targetHealth);
                return;
            }

            float basicDamageMultiplier = triuneAnchors != null
                ? triuneAnchors.ResolveBasicCastDamageMultiplier()
                : 1f;
            if (causalBeam != null && causalBeam.TryBegin(combatTarget.position, targetHealth))
            {
                CurrentAbility = FalseGodAbilityKind.CausalBeam;
                SetPresentationState(FalseGodProphetPresentationMap.ForMonkAbility(CurrentAbility));
                return;
            }

            if (prophetBasicCast != null && prophetBasicCast.TryCastEcho(combatTarget, basicDamageMultiplier))
            {
                CurrentAbility = FalseGodAbilityKind.EchoProjectile;
                SetPresentationState(FalseGodProphetPresentationMap.ForMonkAbility(CurrentAbility));
            }
        }

        private void RefreshCastState()
        {
            if (CurrentAbility == FalseGodAbilityKind.CrystalRain
                && (crystalRain == null || !crystalRain.IsCasting))
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }
            else if (CurrentAbility == FalseGodAbilityKind.CausalBeam
                && (causalBeam == null || !causalBeam.IsCasting))
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }
            else if ((CurrentAbility == FalseGodAbilityKind.EchoProjectile || CurrentAbility == FalseGodAbilityKind.SlowBolt)
                && (prophetBasicCast == null || !prophetBasicCast.IsCasting))
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }
            else if (CurrentAbility == FalseGodAbilityKind.DashShadow
                && (prophetDashShadow == null || !prophetDashShadow.IsCasting))
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }
            else if (CurrentAbility == FalseGodAbilityKind.CrystalChainHook
                && (prophetChainHook == null || !prophetChainHook.IsActive))
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }
            else if (CurrentAbility == FalseGodAbilityKind.TriuneAnchors)
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }
            else if (CurrentAbility == FalseGodAbilityKind.Block
                && (prophetBlock == null || !prophetBlock.IsBlocking))
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }
            else if (CurrentAbility == FalseGodAbilityKind.ClaimBurst
                && (claimBurst == null || !claimBurst.IsCasting))
            {
                CurrentAbility = FalseGodAbilityKind.None;
            }

            if (CurrentAbility == FalseGodAbilityKind.None && PresentationState != FalseGodProphetPresentationState.Ascension)
            {
                SetPresentationState(FalseGodProphetPresentationState.Idle);
            }
        }

        private void SubscribeDeath()
        {
            if (deathSubscribed || damageable == null)
            {
                return;
            }

            deathSubscribed = true;
            damageable.Died += HandleDied;
        }

        private void UnsubscribeDeath()
        {
            if (!deathSubscribed || damageable == null)
            {
                return;
            }

            deathSubscribed = false;
            damageable.Died -= HandleDied;
        }

        private void HandleAscensionRequested()
        {
            SetPresentationState(FalseGodProphetPresentationState.Ascension);
        }
    }

    /// <summary>
    /// Bounded territory-establishing cast. A caller supplies the target point;
    /// placement performs only local clearance checks and network-local anchor
    /// spacing checks, never a world scan.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodRuntimeFoundation))]
    public sealed class CrystalRainAbility : MonoBehaviour
    {
        [SerializeField, Min(1)] private int crystalsPerCast = 3;
        [SerializeField, Min(0.1f)] private float spawnRadius = 2.75f;
        [SerializeField, Min(0.1f)] private float minimumCrystalSpacing = 1.1f;
        [SerializeField, Min(0f)] private float telegraphDuration = 0.7f;
        [SerializeField, Min(0f)] private float impactInterval = 0.12f;
        [SerializeField, Min(0.1f)] private float cooldown = 7f;
        [SerializeField, Min(1)] private int authorityStrength = 3;
        [SerializeField, Min(0.1f)] private float territoryRadius = 2f;
        [SerializeField, Min(0f)] private float crystalLifetime = 0f;
        [SerializeField, Min(0f)] private float clearanceRadius = 0.3f;
        [SerializeField] private LayerMask solidGeometryLayers;

        private readonly List<Vector2> impactScratch = new List<Vector2>(6);
        private readonly List<ClaimAnchor> anchorScratch = new List<ClaimAnchor>(8);
        private readonly List<GameObject> telegraphs = new List<GameObject>(6);
        private FalseGodRuntimeFoundation foundation;
        private Coroutine castRoutine;
        private float nextCastTime;
        private int castSequence;

        public bool IsCasting => castRoutine != null;
        public int MaximumCrystalsPerCast => crystalsPerCast;

        private void Awake()
        {
            EnsureFoundation();
        }

        public bool TryBegin(Vector2 targetPoint)
        {
            EnsureFoundation();
            if (IsCasting || Time.time < nextCastTime || foundation == null || foundation.AuthorityNetwork == null)
            {
                return false;
            }

            BuildImpactPoints(targetPoint, impactScratch);
            if (impactScratch.Count == 0)
            {
                return false;
            }

            nextCastTime = Time.time + cooldown;
            castRoutine = StartCoroutine(PerformCast());
            return true;
        }

        /// <summary>Deterministic editor verification path using the same impact builder and manifestation call as the coroutine.</summary>
        public int ExecuteImmediatelyForVerification(Vector2 targetPoint)
        {
            EnsureFoundation();
            if (foundation == null || foundation.AuthorityNetwork == null)
            {
                return 0;
            }

            BuildImpactPoints(targetPoint, impactScratch);
            return ManifestImpacts();
        }

        public void CancelCast()
        {
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
                castRoutine = null;
            }

            ClearTelegraphs();
        }

        private IEnumerator PerformCast()
        {
            for (int index = 0; index < impactScratch.Count; index++)
            {
                telegraphs.Add(FalseGodCombatPresentation.CreateImpactTelegraph(
                    impactScratch[index],
                    Mathf.Max(0.1f, territoryRadius * 0.3f),
                    telegraphDuration + impactInterval * impactScratch.Count));
            }

            if (telegraphDuration > 0f)
            {
                yield return new WaitForSeconds(telegraphDuration);
            }

            for (int index = 0; index < impactScratch.Count; index++)
            {
                GetComponent<FalseGodCombatController>()?.ReleasePresentationHold();
                ManifestImpact(impactScratch[index]);
                if (impactInterval > 0f && index < impactScratch.Count - 1)
                {
                    yield return new WaitForSeconds(impactInterval);
                }
            }

            ClearTelegraphs();
            castRoutine = null;
        }

        private int ManifestImpacts()
        {
            int manifested = 0;
            for (int index = 0; index < impactScratch.Count; index++)
            {
                if (ManifestImpact(impactScratch[index]))
                {
                    manifested++;
                }
            }

            return manifested;
        }

        private bool ManifestImpact(Vector2 impactPoint)
        {
            if (foundation == null || !foundation.isActiveAndEnabled)
            {
                return false;
            }

            ClaimCrystal crystal = foundation.ManifestCrystal(
                impactPoint,
                authorityStrength,
                territoryRadius,
                crystalLifetime);
            if (crystal == null)
            {
                return false;
            }

            FalseGodProphetVfxPlayback.PlayOneShot(FalseGodProphetVfxKind.CrystalRain, impactPoint, 1.15f);
            FalseGodProphetVfxPlayback.PlayOneShot(FalseGodProphetVfxKind.CrystalManifestation, crystal.transform.position, 0.9f);
            FalseGodCombatPresentation.CreateCrystalImpact(impactPoint);
            return true;
        }

        private void BuildImpactPoints(Vector2 targetPoint, List<Vector2> destination)
        {
            destination.Clear();
            if (foundation == null || foundation.AuthorityNetwork == null)
            {
                return;
            }

            foundation.AuthorityNetwork.CopyActiveAnchors(anchorScratch);
            int requested = Mathf.Max(1, crystalsPerCast);
            float startAngle = (castSequence++ % 8) * 45f;
            for (int index = 0; index < requested; index++)
            {
                float angle = startAngle + index * (360f / requested);
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)) * spawnRadius;
                Vector2 candidate = targetPoint + offset;
                if (IsValidImpact(candidate, destination))
                {
                    destination.Add(candidate);
                }
            }
        }

        private bool IsValidImpact(Vector2 candidate, List<Vector2> accepted)
        {
            if (solidGeometryLayers.value != 0
                && Physics2D.OverlapCircle(candidate, clearanceRadius, solidGeometryLayers) != null)
            {
                return false;
            }

            float minimumDistanceSquared = minimumCrystalSpacing * minimumCrystalSpacing;
            for (int index = 0; index < accepted.Count; index++)
            {
                if ((accepted[index] - candidate).sqrMagnitude < minimumDistanceSquared)
                {
                    return false;
                }
            }

            for (int index = 0; index < anchorScratch.Count; index++)
            {
                ClaimAnchor anchor = anchorScratch[index];
                if (anchor != null
                    && ((Vector2)anchor.transform.position - candidate).sqrMagnitude < minimumDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearTelegraphs()
        {
            for (int index = 0; index < telegraphs.Count; index++)
            {
                if (telegraphs[index] != null)
                {
                    Destroy(telegraphs[index]);
                }
            }

            telegraphs.Clear();
        }

        private void EnsureFoundation()
        {
            if (foundation == null)
            {
                foundation = GetComponent<FalseGodRuntimeFoundation>();
            }
        }
    }

    /// <summary>Compact readout of one bounded causal route.</summary>
    public readonly struct CausalBeamRoute
    {
        public CausalBeamRoute(int relayCount, bool usesOnlyAuthorityAnchors)
        {
            RelayCount = relayCount;
            UsesOnlyAuthorityAnchors = usesOnlyAuthorityAnchors;
        }

        public int RelayCount { get; }
        public bool UsesOnlyAuthorityAnchors { get; }
    }

    /// <summary>
    /// Claim-aware line attack. It builds a short deterministic route from the
    /// authority network's active anchors and has no scene-wide object search.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodRuntimeFoundation))]
    public sealed class CausalBeamAbility : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float windupDuration = 0.65f;
        [SerializeField, Min(0f)] private float activeDuration = 0.325f;
        [SerializeField, Min(0.1f)] private float cooldown = 5f;
        [SerializeField, Range(0, 2)] private int maximumRelayCount = 2;
        [SerializeField, Min(0.1f)] private float maximumRelayDistance = 7f;
        [SerializeField, Min(0f)] private float relayLineTolerance = 1.25f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField] private LayerMask lineOfSightLayers;

        private readonly List<ClaimAnchor> anchorScratch = new List<ClaimAnchor>(8);
        private readonly List<ClaimAnchor> routeRelays = new List<ClaimAnchor>(2);
        private readonly List<ClaimAnchor> routeCandidates = new List<ClaimAnchor>(8);
        private readonly List<Vector3> pathScratch = new List<Vector3>(4);
        private FalseGodRuntimeFoundation foundation;
        private Coroutine castRoutine;
        private float nextCastTime;
        private LineRenderer beamRenderer;
        private Material beamMaterial;

        public bool IsCasting => castRoutine != null;
        public int LastRelayCount { get; private set; }
        public int AvailableRouteCount { get; private set; }
        public ClaimAnchor LastSelectedRoute { get; private set; }

        private void Awake()
        {
            EnsureFoundation();
            CreateRenderer();
        }

        private void OnDestroy()
        {
            if (beamMaterial != null)
            {
                Destroy(beamMaterial);
            }
        }

        public bool TryBegin(Vector2 targetPoint, PlayerHealth target = null)
        {
            EnsureFoundation();
            if (IsCasting || Time.time < nextCastTime || foundation == null || foundation.AuthorityNetwork == null)
            {
                return false;
            }

            BuildRoute(targetPoint);
            nextCastTime = Time.time + cooldown;
            castRoutine = StartCoroutine(PerformCast(target));
            return true;
        }

        public CausalBeamRoute BuildRouteForVerification(Vector2 targetPoint)
        {
            EnsureFoundation();
            BuildRoute(targetPoint);
            return new CausalBeamRoute(LastRelayCount, RouteUsesAuthorityAnchors());
        }

        public void CancelCast()
        {
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
                castRoutine = null;
            }

            if (beamRenderer != null)
            {
                beamRenderer.enabled = false;
            }
        }

        private IEnumerator PerformCast(PlayerHealth target)
        {
            FalseGodProphetVfxPlayback.PlayOneShot(FalseGodProphetVfxKind.CausalBeam, transform.position, 1.25f);
            ShowBeam(0.09f, new Color(0.85f, 0.2f, 1f, 0.65f));
            if (windupDuration > 0f)
            {
                yield return new WaitForSeconds(windupDuration);
            }

            GetComponent<FalseGodCombatController>()?.ReleasePresentationHold();
            ShowBeam(0.18f, new Color(0.95f, 0.6f, 1f, 1f));
            if (target != null)
            {
                target.TryTakeDamage(damage, new DamageContext(
                    foundation != null ? foundation.gameObject : gameObject,
                    DamageTrait.Direct | DamageTrait.Projectile));
            }

            if (activeDuration > 0f)
            {
                yield return new WaitForSeconds(activeDuration);
            }

            if (beamRenderer != null)
            {
                beamRenderer.enabled = false;
            }

            castRoutine = null;
        }

        private void BuildRoute(Vector2 targetPoint)
        {
            routeRelays.Clear();
            pathScratch.Clear();
            pathScratch.Add(transform.position);
            if (foundation == null || foundation.AuthorityNetwork == null)
            {
                pathScratch.Add(targetPoint);
                LastRelayCount = 0;
                return;
            }

            foundation.AuthorityNetwork.CopyActiveAnchors(anchorScratch);
            Vector2 routeStart = transform.position;
            for (int depth = 0; depth < maximumRelayCount; depth++)
            {
                ClaimAnchor best = FindBestRelay(routeStart, targetPoint);
                if (best == null)
                {
                    break;
                }

                routeRelays.Add(best);
                pathScratch.Add(best.transform.position);
                routeStart = best.transform.position;
            }

            pathScratch.Add(targetPoint);
            LastRelayCount = routeRelays.Count;
        }

        private ClaimAnchor FindBestRelay(Vector2 routeStart, Vector2 targetPoint)
        {
            Vector2 targetDelta = targetPoint - routeStart;
            float targetDistance = targetDelta.magnitude;
            if (targetDistance < 0.001f)
            {
                return null;
            }

            Vector2 direction = targetDelta / targetDistance;
            routeCandidates.Clear();
            for (int index = 0; index < anchorScratch.Count; index++)
            {
                ClaimAnchor candidate = anchorScratch[index];
                if (candidate == null || routeRelays.Contains(candidate) || !candidate.IsActive)
                {
                    continue;
                }

                Vector2 candidateDelta = (Vector2)candidate.transform.position - routeStart;
                float along = Vector2.Dot(candidateDelta, direction);
                if (along <= 0f || along > maximumRelayDistance || along >= targetDistance)
                {
                    continue;
                }

                float lateral = Mathf.Abs(candidateDelta.x * direction.y - candidateDelta.y * direction.x);
                if (lateral > relayLineTolerance || !HasLineOfSight(routeStart, candidate.transform.position))
                {
                    continue;
                }

                routeCandidates.Add(candidate);
            }

            AvailableRouteCount = routeCandidates.Count;
            if (routeCandidates.Count == 0) return null;
            int pick = UnityEngine.Random.Range(0, routeCandidates.Count);
            if (routeCandidates.Count > 1 && routeCandidates[pick] == LastSelectedRoute)
            {
                pick = (pick + 1) % routeCandidates.Count;
            }
            LastSelectedRoute = routeCandidates[pick];
            return LastSelectedRoute;
        }

        private bool HasLineOfSight(Vector2 start, Vector2 end)
        {
            return lineOfSightLayers.value == 0
                || Physics2D.Linecast(start, end, lineOfSightLayers).collider == null;
        }

        private bool RouteUsesAuthorityAnchors()
        {
            if (foundation == null || foundation.AuthorityNetwork == null)
            {
                return routeRelays.Count == 0;
            }

            foundation.AuthorityNetwork.CopyActiveAnchors(anchorScratch);
            for (int index = 0; index < routeRelays.Count; index++)
            {
                if (!anchorScratch.Contains(routeRelays[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private void CreateRenderer()
        {
            if (beamRenderer != null)
            {
                return;
            }

            GameObject visual = new GameObject("Causal Beam Presentation");
            visual.transform.SetParent(transform, false);
            beamRenderer = visual.AddComponent<LineRenderer>();
            beamRenderer.useWorldSpace = true;
            beamRenderer.alignment = LineAlignment.View;
            beamRenderer.textureMode = LineTextureMode.Stretch;
            beamRenderer.startWidth = 0.05f;
            beamRenderer.endWidth = 0.05f;
            beamRenderer.sortingOrder = 30;
            beamRenderer.enabled = false;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                beamMaterial = new Material(shader);
                beamRenderer.sharedMaterial = beamMaterial;
            }
        }

        private void ShowBeam(float width, Color color)
        {
            CreateRenderer();
            if (beamRenderer == null)
            {
                return;
            }

            beamRenderer.enabled = true;
            beamRenderer.startWidth = width;
            beamRenderer.endWidth = width;
            beamRenderer.startColor = color;
            beamRenderer.endColor = color;
            beamRenderer.positionCount = pathScratch.Count;
            for (int index = 0; index < pathScratch.Count; index++)
            {
                beamRenderer.SetPosition(index, pathScratch[index]);
            }
        }

        private void EnsureFoundation()
        {
            if (foundation == null)
            {
                foundation = GetComponent<FalseGodRuntimeFoundation>();
            }
        }
    }

    /// <summary>
    /// Holds explicitly appropriated PlayerProjectiles. It is deliberately
    /// separate from ownership: ClaimResolver changes ownership, while this
    /// component only pauses and stores the captured projectile for a future
    /// Repossession/Reconfiguration move.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClaimedProjectileHold : MonoBehaviour, IInteractionResponder
    {
        private readonly List<PlayerProjectile> capturedProjectiles = new List<PlayerProjectile>(4);
        private ClaimAuthorityNetwork network;
        private Transform holdRoot;
        private bool subscribed;

        public int CapturedCount => capturedProjectiles.Count;

        private void Awake()
        {
            EnsureHoldRoot();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            RetireAll();
        }

        public void InitializeRuntime(ClaimAuthorityNetwork authorityNetwork)
        {
            network = authorityNetwork;
            Subscribe();
        }

        public bool TryCapture(PlayerProjectile projectile, out ClaimResult result)
        {
            result = default;
            EnsureHoldRoot();
            InteractionIdentity target = projectile != null ? projectile.InteractionIdentity : null;
            if (network == null
                || !network.CanMaintainAuthority
                || projectile == null
                || !projectile.CanBeClaimSuspended
                || target == null
                || !target.IsClaimable
                || target.Ownership == InteractionOwnership.Claim
                || (target.Traits & InteractionTraits.Projectile) == 0
                || !network.TryGetTerritorialAnchor(target, out ClaimAnchor anchor))
            {
                return false;
            }

            result = ClaimResolver.Resolve(new ClaimAttempt(
                anchor.Identity,
                target,
                ClaimProvenance.InsideClaimedTerritory,
                anchor.AuthorityStrength));
            if (!result.Succeeded)
            {
                return false;
            }

            FalseGodCombatPresentation.CreateCaptureCue(projectile.transform.position);
            if (!projectile.TrySuspendForClaim(holdRoot))
            {
                Debug.LogError("[Cave] Appropriation resolved ownership but the projectile could not enter its required hold state.", projectile);
                return false;
            }

            capturedProjectiles.Add(projectile);
            return true;
        }

        public void RetireAll()
        {
            for (int index = 0; index < capturedProjectiles.Count; index++)
            {
                if (capturedProjectiles[index] != null)
                {
                    capturedProjectiles[index].RetireClaimCapture();
                }
            }

            capturedProjectiles.Clear();
        }

        public void Respond(InteractionEvent interactionEvent)
        {
            if (interactionEvent.Kind != InteractionEventKind.Destroyed || interactionEvent.Source == null)
            {
                return;
            }

            for (int index = capturedProjectiles.Count - 1; index >= 0; index--)
            {
                PlayerProjectile projectile = capturedProjectiles[index];
                if (projectile == null || projectile.InteractionIdentity == interactionEvent.Source)
                {
                    capturedProjectiles.RemoveAt(index);
                }
            }
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;
            InteractionEventBus.Register(this);
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
            InteractionEventBus.Unregister(this);
        }

        private void EnsureHoldRoot()
        {
            if (holdRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("Claimed Projectile Hold");
            root.transform.SetParent(transform, false);
            holdRoot = root.transform;
        }
    }

    /// <summary>Development-only factory; it never serializes a production prefab or adds spawn-table references.</summary>
    public static class FalseGodDevelopmentShell
    {
        public static FalseGodCombatController Create(Vector2 position, bool includeMonkForm = false)
        {
            GameObject shell = new GameObject("False God Development Shell");
            shell.transform.position = position;
            // Buapah currently uses one predictable root-level body/hurtbox.
            // This is deliberately not an attack collider and remains a simple
            // solid shape for the development shell's existing world contacts.
            int damageableLayer = LayerMask.NameToLayer("Damageable");
            if (damageableLayer >= 0)
            {
                shell.layer = damageableLayer;
            }

            BoxCollider2D body = shell.AddComponent<BoxCollider2D>();
            body.size = new Vector2(1.2f, 1.8f);
            body.offset = new Vector2(0f, -0.2f);
            Damageable damageable = shell.AddComponent<Damageable>();
            damageable.InitializeRuntimeState();
            damageable.SetRuntimeMaximumHealth(12, true);
            FalseGodRuntimeFoundation foundation = shell.AddComponent<FalseGodRuntimeFoundation>();
            foundation.InitializeRuntime();
            shell.AddComponent<CrystalRainAbility>();
            shell.AddComponent<CausalBeamAbility>();
            shell.AddComponent<ClaimedProjectileHold>();
            shell.AddComponent<FalseGodProphetBasicCast>();
            shell.AddComponent<FalseGodDashShadow>();
            shell.AddComponent<FalseGodCrystalChainHook>();
            shell.AddComponent<FalseGodTriuneAnchors>();
            shell.AddComponent<FalseGodProphetBlock>();
            shell.AddComponent<FalseGodClaimBurst>();
            shell.AddComponent<FalseGodClaimTeleport>();
            shell.AddComponent<FalseGodWraithSpawner>();
            shell.AddComponent<FalseGodProphetPhaseBoundary>();
            if (includeMonkForm)
            {
                shell.AddComponent<FalseGodFormController>();
                shell.AddComponent<FalseGodMonkCombat>();
            }
            FalseGodCombatController controller = shell.AddComponent<FalseGodCombatController>();
            controller.InitializeRuntime();
            return controller;
        }
    }

    /// <summary>Temporary readable lines/rings isolated from boss combat logic.</summary>
    internal static class FalseGodCombatPresentation
    {
        public static GameObject CreateImpactTelegraph(Vector2 position, float radius, float lifetime)
        {
            return CreateRing("Crystal Rain Telegraph", position, radius, lifetime, new Color(0.85f, 0.25f, 1f, 0.9f));
        }

        public static void CreateCaptureCue(Vector2 position)
        {
            FalseGodProphetVfxPlayback.PlayOneShot(FalseGodProphetVfxKind.Appropriation, position, 0.8f);
            CreateRing("Appropriation Capture Cue", position, 0.42f, 0.4f, new Color(1f, 0.6f, 1f, 1f));
        }

        public static void CreateCrystalImpact(Vector2 position)
        {
            CreateRing("Crystal Rain Impact", position, 0.34f, 0.35f, new Color(0.7f, 0.4f, 1f, 1f));
        }

        private static GameObject CreateRing(string name, Vector2 position, float radius, float lifetime, Color color)
        {
            GameObject visual = new GameObject(name);
            visual.transform.position = position;
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 20;
            line.startWidth = 0.035f;
            line.endWidth = 0.035f;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 28;
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.sharedMaterial = new Material(shader);
            }

            FalseGodTemporaryVisual expiry = visual.AddComponent<FalseGodTemporaryVisual>();
            expiry.Initialize(lifetime);
            return visual;
        }
    }

    internal sealed class FalseGodTemporaryVisual : MonoBehaviour
    {
        private Material material;

        public void Initialize(float lifetime)
        {
            LineRenderer line = GetComponent<LineRenderer>();
            material = line != null ? line.sharedMaterial : null;
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }

            if (lifetime > 0f)
            {
                Destroy(gameObject, lifetime);
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
        }
    }
}
