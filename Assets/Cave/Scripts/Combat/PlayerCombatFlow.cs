using System;
using System.Collections.Generic;
using Cave.Axioms.Mastery;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Player;
using Cave.Progression;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Combat
{
    public enum FormalFollowUpSource
    {
        None,
        NormalParry,
        PerfectParry,
        GuardBreak
    }

    public enum FrenzyBreakInfusion
    {
        Physical,
        Fire,
        Ice,
        Wind,
        Strength
    }

    public enum FrenzyBreakRole
    {
        Opener,
        Finisher
    }

    public enum FrenzyBreakAttackKind
    {
        Spin,
        Charged,
        GroundSlam,
        Projectile
    }

    public sealed class FrenzyBreakActivation
    {
        private readonly PlayerCombatFlow owner;
        private readonly HashSet<Damageable> resolvedTargets = new HashSet<Damageable>();
        private bool windBurstApplied;
        private bool completed;

        internal FrenzyBreakActivation(
            PlayerCombatFlow flow,
            FrenzyBreakInfusion selectedInfusion,
            int infusionLevel,
            FrenzyBreakRole selectedRole,
            FrenzyBreakAttackKind attackKind,
            int skillTier,
            bool manaInfused)
        {
            owner = flow;
            Infusion = selectedInfusion;
            InfusionLevel = infusionLevel;
            Role = selectedRole;
            AttackKind = attackKind;
            SkillTier = skillTier;
            ManaInfused = manaInfused;
        }

        public FrenzyBreakInfusion Infusion { get; }
        public int InfusionLevel { get; }
        public FrenzyBreakRole Role { get; }
        public FrenzyBreakAttackKind AttackKind { get; }
        public int SkillTier { get; }
        public bool ManaInfused { get; }
        public bool UseFrostTierThreePin => AttackKind == FrenzyBreakAttackKind.Projectile
            && Infusion == FrenzyBreakInfusion.Ice
            && SkillTier >= 3;

        public bool TryResolveDamage(int baseDamage, Damageable target, out int resolvedDamage)
        {
            resolvedDamage = baseDamage;
            if (completed
                || owner == null
                || target == null
                || AttackKind != FrenzyBreakAttackKind.Charged
                || !owner.HasFrenzyCriticalOpportunity)
            {
                return false;
            }

            resolvedDamage = owner.ResolveCriticalDamage(
                baseDamage,
                target,
                Infusion,
                InfusionLevel);
            return true;
        }

        /// <summary>
        /// Completes an accepted hit. Criticals are deliberately consumed here,
        /// after authoritative damage resolution, rather than on an overlap.
        /// </summary>
        public bool ApplyImpact(
            Damageable target,
            Vector2 attackDirection,
            int appliedDamage,
            bool wasCriticalCandidate)
        {
            if (completed || appliedDamage <= 0 || owner == null)
            {
                return false;
            }

            // Collision callbacks can repeat while a spinning or area attack
            // overlaps the same body.  Each attack context bills and applies its
            // Frenzy layer once per accepted target.
            if (target == null || !resolvedTargets.Add(target))
            {
                return false;
            }

            bool criticalCommitted = wasCriticalCandidate
                && owner.TryConsumeFrenzyCriticalAfterAcceptedDamage();
            if (!ManaInfused && !criticalCommitted)
            {
                return false;
            }

            bool allowWindBurst = Infusion != FrenzyBreakInfusion.Wind || !windBurstApplied;
            owner.ApplyFrenzyImpact(
                target,
                attackDirection,
                Infusion,
                InfusionLevel,
                AttackKind,
                SkillTier,
                allowWindBurst,
                appliedDamage,
                ManaInfused,
                criticalCommitted);
            if (Infusion == FrenzyBreakInfusion.Wind)
            {
                windBurstApplied = true;
            }

            return criticalCommitted;
        }

        public void Complete()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            owner?.CompleteFrenzyAction(this);
        }

        public void KeepAlive()
        {
            if (!completed)
            {
                owner?.RefreshFrenzyActionSafety(this);
            }
        }

        internal void Invalidate()
        {
            completed = true;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMana), typeof(PlayerSpecialMode))]
    public sealed class PlayerCombatFlow : MonoBehaviour
    {
        [Header("Formal Charged Follow-ups")]
        [SerializeField, Min(0.05f)] private float followUpWindow = 0.7f;

        [Header("Spin to Bash Follow-up")]
        [SerializeField, Min(0f)] private float minimumMeaningfulSpinDuration = 0.08f;
        [SerializeField, Min(0.05f)] private float spinBashFollowUpWindow = 0.65f;

        [Header("Frenzy Timing")]
        [SerializeField, Min(0.05f)] private float manaInfusionHoldThreshold = 0.30f;
        [SerializeField, Min(0.05f)] private float levelTwoHoldThreshold = 0.65f;
        [SerializeField, Min(0.05f)] private float levelThreeHoldThreshold = 1f;
        [SerializeField, Min(0.1f)] private float baseFrenzyDuration = 5f;
        [SerializeField, Range(0.05f, 0.95f)] private float frenzyMasteryWindowFraction = 0.25f;
        [SerializeField, Min(0f)] private float frenzyMasteryDurationPerLevel = 0.25f;
        [SerializeField, Min(0f)] private float maximumFrenzyMasteryDurationBonus = 2f;
        [SerializeField, Min(0f)] private float baseInfusionCost = 1f;
        [SerializeField, Min(0f)] private float manaPerAcceptedDamage = 0.25f;
        [SerializeField, Min(0.05f)] private float combatContextRadius = 7f;

        [Header("Frenzy Damage")]
        [SerializeField, Min(0f)] private float baseCriticalBonus = 1.75f;
        [SerializeField, Min(0f)] private float normalLevelOneBonus = 0.80f;
        [SerializeField, Min(0f)] private float normalLevelTwoBonus = 1.20f;
        [SerializeField, Min(0f)] private float normalLevelThreeBonus = 1.60f;
        [SerializeField, Min(0f)] private float strengthLevelOneBonus = 2.25f;
        [SerializeField, Min(0f)] private float strengthLevelTwoBonus = 2.35f;
        [SerializeField, Min(0f)] private float strengthLevelThreeBonus = 2.50f;

        [Header("Infusion Effects")]
        [SerializeField, Min(1)] private int fireDamagePerTick = 1;
        [SerializeField, Min(0.05f)] private float fireTickInterval = 1f;
        [SerializeField, Min(0f)] private float fireDuration = 3f;
        [SerializeField, Range(0f, 1f)] private float iceMovementMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float iceDuration = 2f;
        [SerializeField, Min(0f)] private float iceImmobilizeDuration = 0.35f;
        [SerializeField, Min(0f)] private float windKnockback = 8f;
        [SerializeField, Min(1f)] private float windTierThreeBurstMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float strengthHeavyStaggerDuration = 0.65f;
        [SerializeField] private LayerMask frenzyDamageableLayers = ~0;

        [Header("Frenzy Aura Prefabs")]
        [SerializeField] private GameObject physicalAuraPrefab;
        [SerializeField] private GameObject fireAuraPrefab;
        [SerializeField] private GameObject frostAuraPrefab;
        [SerializeField] private GameObject windAuraPrefab;
        [SerializeField] private GameObject strengthAuraPrefab;

        [Header("Elemental Sword Swing Prefabs")]
        [SerializeField] private GameObject fireSwordSwingPrefab;
        [SerializeField] private GameObject frostSwordSwingPrefab;
        [SerializeField] private GameObject windSwordSwingPrefab;
        [SerializeField] private GameObject strengthSwordSwingPrefab;

        [Header("Impact Feedback")]
        [SerializeField] private GameObject criticalImpactVfxPrefab;
        [SerializeField] private Vector2 criticalImpactVfxOffset;
        [SerializeField, Min(0.01f)] private float criticalImpactVfxScale = 1f;
        [SerializeField, Min(0.05f)] private float frenzyVfxLifetime = 1.25f;
        [SerializeField] private Color criticalFallbackColor = new Color(1f, 0.55f, 0.12f, 1f);

        [Header("Current Flow (Read Only)")]
        [SerializeField] private FormalFollowUpSource followUpSource;
        [SerializeField, Range(0, 3)] private int chargedStartingTier;
        [SerializeField, Min(0f)] private float followUpRemaining;
        [SerializeField] private bool formalChainInProgress;
        [SerializeField] private bool spinBashAvailable;
        [SerializeField, Min(0f)] private float spinBashRemaining;
        [SerializeField] private bool isPreparingFrenzy;
        [SerializeField] private bool frenzyArmed;
        [SerializeField] private bool frenzyCriticalArmed;
        [SerializeField] private bool frenzyMasteryAwarded;
        [SerializeField, Min(0f)] private float currentFrenzyDuration;
        [SerializeField] private FrenzyBreakInfusion armedInfusion;
        [SerializeField, Range(0, 3)] private int armedInfusionLevel;
        [SerializeField] private FrenzyBreakRole armedRole;

        private SpinSwordAttack stamina;
        private PlayerMana mana;
        private PlayerHealth playerHealth;
        private PlayerSpecialMode specialMode;
        private PlayerResourceMastery resourceMastery;
        private ChargedAttack chargedAttack;
        private PlayerAttackState attackState;
        private PlayerGuardBreak guardBreak;
        private SidewaysParryAttack parry;
        private PlayerCurseAltarController altar;
        private PlayerSpecialModeUpgradeState upgradeState;
        private FrenzyMasteryState frenzyMastery;
        private float followUpExpiresAt;
        private float preparationStartedAt;
        private float frenzyExpiresAt;
        private float nextHostileRefreshTime;
        private bool hostileNearby;
        private float meaningfulSpinStartedAt = -1f;
        private float spinBashExpiresAt;
        private GameObject activeAura;

        public event Action StateChanged;
        public event Action<string> FeedbackRequested;

        public bool IsPreparingFrenzy => isPreparingFrenzy;
        public bool IsFrenzyActive => frenzyArmed && Time.time <= frenzyExpiresAt;
        // Retained as a compatibility surface for existing HUD and parry code.
        public bool IsFrenzyArmed => IsFrenzyActive;
        public bool IsFrenzyBound => false;
        public bool HasFrenzyCriticalOpportunity => IsFrenzyActive && frenzyCriticalArmed;
        public FrenzyBreakInfusion ArmedInfusion => armedInfusion;
        public int ArmedInfusionLevel => armedInfusionLevel;
        public FrenzyBreakRole ArmedRole => armedRole;
        public FrenzyBreakAttackKind BoundAttackKind => FrenzyBreakAttackKind.Spin;
        public float ArmedTimeRemaining => IsFrenzyActive
            ? Mathf.Max(0f, frenzyExpiresAt - Time.time)
            : 0f;
        public float CurrentFrenzyDuration => currentFrenzyDuration;
        public float BaseFrenzyDuration => baseFrenzyDuration;
        public int FrenzyMasteryLevel => frenzyMastery != null ? frenzyMastery.CurrentLevel : 0;
        public float FrenzyMasteryProgress => frenzyMastery != null ? frenzyMastery.CurrentProgress : 0f;
        public float CurrentTheoreticalMultiplier => 1f
            + baseCriticalBonus
            + ResolveInfusionBonus(armedInfusion, armedInfusionLevel);
        public FormalFollowUpSource FollowUpSource => HasFollowUpToken ? followUpSource : FormalFollowUpSource.None;
        public int ChargedStartingTier => HasFollowUpToken ? chargedStartingTier : 0;
        public bool CanChainSpinToBash => spinBashAvailable
            && Time.time <= spinBashExpiresAt;

        private bool HasFollowUpToken => followUpSource != FormalFollowUpSource.None
            && Time.time <= followUpExpiresAt;

        private void Awake()
        {
            stamina = GetComponent<SpinSwordAttack>();
            mana = GetComponent<PlayerMana>();
            playerHealth = GetComponent<PlayerHealth>();
            specialMode = GetComponent<PlayerSpecialMode>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            chargedAttack = GetComponent<ChargedAttack>();
            attackState = GetComponent<PlayerAttackState>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            parry = GetComponent<SidewaysParryAttack>();
            altar = GetComponent<PlayerCurseAltarController>();
            upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            EnsureFrenzyMastery();
            if (stamina == null)
            {
                enabled = false;
                Debug.LogError("PlayerCombatFlow requires the existing SpinSwordAttack stamina owner.", this);
            }
        }

        private void OnEnable()
        {
            SubscribeSignals();
        }

        private void Start()
        {
            SubscribeSignals();
        }

        private void Update()
        {
            RefreshTimers();
            RefreshSpinBashChain();
            if (!GameInput.GameplayInputEnabled)
            {
                isPreparingFrenzy = false;
                return;
            }

            bool altarAvailable = altar != null && altar.HasAvailableAltar();
            if (GameInput.InteractPressed
                && !IsFrenzyActive
                && ShouldPrioritizeFrenzyBreak(altarAvailable))
            {
                isPreparingFrenzy = true;
                preparationStartedAt = Time.time;
                StateChanged?.Invoke();
            }

            if (isPreparingFrenzy && GameInput.InteractReleased)
            {
                float heldDuration = Mathf.Max(0f, Time.time - preparationStartedAt);
                isPreparingFrenzy = false;
                ArmFrenzyBreak(heldDuration);
            }
        }

        public bool ShouldPrioritizeFrenzyBreak(bool interactableAvailable)
        {
            if (Time.time >= nextHostileRefreshTime)
            {
                hostileNearby = HostileMobQuery.CountRealHostiles(transform.position, combatContextRadius) > 0;
                nextHostileRefreshTime = Time.time + 0.15f;
            }

            bool combatContext = hostileNearby
                || formalChainInProgress
                || CanChainSpinToBash
                || HasFollowUpToken
                || (attackState != null && attackState.IsActivelyAttacking)
                || (chargedAttack != null && chargedAttack.IsCharging)
                || (guardBreak != null && guardBreak.IsGuardBreaking);
            return combatContext || !interactableAvailable;
        }

        public int ConsumeChargedStartingTier()
        {
            if (!HasFollowUpToken)
            {
                ClearFollowUpToken();
                return 0;
            }

            int tier = chargedStartingTier;
            formalChainInProgress = true;
            followUpSource = FormalFollowUpSource.None;
            followUpExpiresAt = 0f;
            followUpRemaining = 0f;
            StateChanged?.Invoke();
            return tier;
        }

        public void NotifyChargedCommitted()
        {
            formalChainInProgress = false;
            StateChanged?.Invoke();
        }

        public void NotifyChargedCancelled()
        {
            formalChainInProgress = false;
            StateChanged?.Invoke();
        }

        public bool TryConsumeSpinBash()
        {
            if (!CanChainSpinToBash)
            {
                ClearSpinBashToken();
                return false;
            }

            ClearSpinBashToken();
            formalChainInProgress = false;
            StateChanged?.Invoke();
            return true;
        }

        public void NotifyGroundSlamCommitted()
        {
            ClearSpinBashToken();
            ClearFollowUpToken();
            formalChainInProgress = false;
            StateChanged?.Invoke();
        }

        public bool TryCommitFrenzyBreak(
            FrenzyBreakAttackKind attackKind,
            out FrenzyBreakActivation activation)
        {
            activation = null;
            if (!IsFrenzyActive)
            {
                return false;
            }

            if (upgradeState == null)
            {
                upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            int currentSkillTier = upgradeState != null
                ? upgradeState.GetCurrentTier(specialMode.CurrentMode)
                : 1;
            activation = new FrenzyBreakActivation(
                this,
                armedInfusion,
                armedInfusionLevel,
                armedRole,
                attackKind,
                currentSkillTier,
                TryBeginManaInfusion());
            if (attackKind != FrenzyBreakAttackKind.Charged)
            {
                formalChainInProgress = false;
            }

            StateChanged?.Invoke();
            return true;
        }

        internal void CompleteFrenzyAction(FrenzyBreakActivation activation)
        {
            // Frenzy is a timed state, not a one-attack transaction.  Action
            // completion intentionally leaves the current activation running.
        }

        internal void RefreshFrenzyActionSafety(FrenzyBreakActivation activation)
        {
            // Compatibility no-op for long-lived action components.  Expiry is
            // owned solely by the Frenzy timer.
        }

        internal bool TryConsumeFrenzyCriticalAfterAcceptedDamage()
        {
            if (!HasFrenzyCriticalOpportunity)
            {
                return false;
            }

            frenzyCriticalArmed = false;
            StateChanged?.Invoke();
            return true;
        }

        internal int ResolveCriticalDamage(
            int baseDamage,
            Damageable target,
            FrenzyBreakInfusion infusion,
            int infusionLevel)
        {
            float resistance = 0f;
            EnemyCriticalResistance criticalResistance = target != null
                ? target.GetComponent<EnemyCriticalResistance>()
                : null;
            if (criticalResistance != null)
            {
                resistance = criticalResistance.CurrentResistance;
            }

            float bonus = baseCriticalBonus + ResolveInfusionBonus(infusion, infusionLevel);
            float multiplier = 1f + bonus * (1f - Mathf.Clamp01(resistance));
            return Mathf.Max(baseDamage, Mathf.RoundToInt(baseDamage * multiplier));
        }

        /// <summary>Uses the established critical-resistance calculation for altar-granted normal crits.</summary>
        public bool TryResolveAltarCriticalDamage(
            int baseDamage,
            Damageable target,
            float chance,
            out int resolvedDamage)
        {
            resolvedDamage = baseDamage;
            if (target == null || baseDamage <= 0 || UnityEngine.Random.value > Mathf.Clamp01(chance))
            {
                return false;
            }

            resolvedDamage = ResolveCriticalDamage(
                baseDamage,
                target,
                FrenzyBreakInfusion.Physical,
                0);
            return true;
        }

        internal void ApplyFrenzyImpact(
            Damageable target,
            Vector2 attackDirection,
            FrenzyBreakInfusion infusion,
            int infusionLevel,
            FrenzyBreakAttackKind attackKind,
            int skillTier,
            bool allowWindBurst,
            int appliedDamage,
            bool manaInfused,
            bool criticalCommitted)
        {
            if (target == null)
            {
                return;
            }

            if (manaInfused)
            {
                ConsumeDamageBasedInfusionMana(appliedDamage);
#if UNITY_EDITOR
                TraceFrenzyMasteryKillAttempt(target);
#endif
                TryAwardFrenzyMasteryForLateInfusedKill(target);
            }

            if (resourceMastery == null)
            {
                resourceMastery = GetComponent<PlayerResourceMastery>();
            }

            bool targetStillAlive = target.gameObject.activeInHierarchy;
            EnemyStatusEffects status = targetStillAlive
                ? target.GetComponent<EnemyStatusEffects>()
                : null;
            bool projectileOwnsElementalStatus = attackKind == FrenzyBreakAttackKind.Projectile
                && (infusion == FrenzyBreakInfusion.Fire || infusion == FrenzyBreakInfusion.Ice);
            if (infusion == FrenzyBreakInfusion.Fire
                && status != null
                && !projectileOwnsElementalStatus)
            {
                DamageContext context = resourceMastery != null
                    ? resourceMastery.CreateManaDamageContext().WithTraits(DamageTrait.AreaOfEffect)
                    : default;
                status.ApplyBurn(fireDamagePerTick, fireTickInterval, fireDuration, context);
            }
            else if (infusion == FrenzyBreakInfusion.Ice
                && status != null
                && !projectileOwnsElementalStatus)
            {
                status.ApplySlow(iceMovementMultiplier, iceDuration);
                float pinDuration = skillTier >= 3
                    && upgradeState != null
                    && upgradeState.Settings != null
                        ? upgradeState.Settings.FrostTier2PinDuration
                        : iceImmobilizeDuration;
                status.ApplyImmobilize(pinDuration);
                if (skillTier >= 3 && upgradeState != null && upgradeState.Settings != null)
                {
                    SpecialModeTier2Settings settings = upgradeState.Settings;
                    SlowFieldEffect.Create(
                        target.transform.position,
                        settings.SlowFieldRadius,
                        settings.SlowFieldDuration,
                        settings.SlowFieldMovementMultiplier,
                        frenzyDamageableLayers,
                        settings.FrostFieldOutlineColor,
                        settings.FrostFieldFillColor,
                        settings.FrostTier2PinDuration,
                        settings.SlowFieldMergeGrowth,
                        settings.SlowFieldMaximumRadius);
                }
            }
            else if (targetStillAlive
                && infusion == FrenzyBreakInfusion.Wind
                && allowWindBurst)
            {
                Vector2 direction = attackDirection.sqrMagnitude > 0.001f
                    ? attackDirection.normalized
                    : ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                float burst = skillTier >= 3 ? windTierThreeBurstMultiplier : 1f;
                target.GetComponent<KnockbackReceiver>()
                    ?.ApplyKnockback(direction * windKnockback * burst);
            }
            else if (targetStillAlive && infusion == FrenzyBreakInfusion.Strength)
            {
                target.GetComponent<EnemyStagger>()?.TryStagger(
                    StaggerStrength.Heavy,
                    strengthHeavyStaggerDuration);
            }

            if (criticalCommitted)
            {
                SpawnCriticalFeedback(target.transform.position, infusion, infusionLevel, attackKind);
                FrenzyCriticalPopup.Create(
                    target,
                    appliedDamage,
                    ResolveCriticalFeedbackColor(infusion));
            }
        }

        private void ArmFrenzyBreak(float heldDuration)
        {
            armedInfusionLevel = ResolveInfusionLevel(heldDuration);
            armedInfusion = armedInfusionLevel > 0
                ? ResolveInfusion(specialMode.CurrentMode)
                : FrenzyBreakInfusion.Physical;
            armedRole = formalChainInProgress
                || HasFollowUpToken
                || CanChainSpinToBash
                ? FrenzyBreakRole.Finisher
                : FrenzyBreakRole.Opener;
            frenzyArmed = true;
            frenzyCriticalArmed = true;
            frenzyMasteryAwarded = false;
            EnsureFrenzyMastery();
            currentFrenzyDuration = Mathf.Max(0.1f, baseFrenzyDuration)
                + (frenzyMastery != null ? frenzyMastery.DurationBonus : 0f);
            frenzyExpiresAt = Time.time + currentFrenzyDuration;
            SpawnFrenzyAura(armedInfusion);
            FeedbackRequested?.Invoke(
                "FRENZY ACTIVE — " + armedInfusion.ToString().ToUpperInvariant());
            StateChanged?.Invoke();
        }

        private void GrantChargedFollowUp(FormalFollowUpSource source, int tier)
        {
            followUpSource = source;
            chargedStartingTier = Mathf.Clamp(tier, 1, 3);
            followUpExpiresAt = Time.time + followUpWindow;
            followUpRemaining = followUpWindow;
            StateChanged?.Invoke();
        }

        private void HandleDefenseSucceeded(PlayerDefenseQuality quality)
        {
            if (quality == PlayerDefenseQuality.PerfectParry)
            {
                GrantChargedFollowUp(FormalFollowUpSource.PerfectParry,
                    HeavyFollowUpPolicy.StartingTier(FormalFollowUpSource.PerfectParry));
            }
            else if (quality == PlayerDefenseQuality.NormalParry)
            {
                GrantChargedFollowUp(FormalFollowUpSource.NormalParry,
                    HeavyFollowUpPolicy.StartingTier(FormalFollowUpSource.NormalParry));
            }
        }

        private void HandleOffensiveGuardBreakSucceeded()
        {
            GrantChargedFollowUp(FormalFollowUpSource.GuardBreak,
                HeavyFollowUpPolicy.StartingTier(FormalFollowUpSource.GuardBreak));
        }

        private void HandlePlayerDied()
        {
            isPreparingFrenzy = false;
            ClearFrenzy();
        }

        private void RefreshTimers()
        {
            followUpRemaining = HasFollowUpToken
                ? Mathf.Max(0f, followUpExpiresAt - Time.time)
                : 0f;
            if (followUpSource != FormalFollowUpSource.None && !HasFollowUpToken)
            {
                ClearFollowUpToken();
            }

            if (frenzyArmed && !IsFrenzyActive)
            {
                ClearFrenzy();
                FeedbackRequested?.Invoke("FRENZY EXPIRED");
            }
        }

        private void RefreshSpinBashChain()
        {
            if (stamina.IsAttacking)
            {
                if (meaningfulSpinStartedAt < 0f)
                {
                    meaningfulSpinStartedAt = Time.time;
                }

                if (Time.time - meaningfulSpinStartedAt >= minimumMeaningfulSpinDuration)
                {
                    spinBashAvailable = true;
                    spinBashExpiresAt = Time.time + spinBashFollowUpWindow;
                }
            }
            else
            {
                meaningfulSpinStartedAt = -1f;
            }

            spinBashRemaining = CanChainSpinToBash
                ? Mathf.Max(0f, spinBashExpiresAt - Time.time)
                : 0f;
            if (spinBashAvailable && !CanChainSpinToBash)
            {
                ClearSpinBashToken();
            }
        }

        private void ClearSpinBashToken()
        {
            spinBashAvailable = false;
            spinBashExpiresAt = 0f;
            spinBashRemaining = 0f;
            meaningfulSpinStartedAt = -1f;
        }

        private int ResolveInfusionLevel(float heldDuration)
        {
            if (heldDuration < manaInfusionHoldThreshold)
            {
                return 0;
            }

            if (heldDuration >= levelThreeHoldThreshold)
            {
                return 3;
            }

            return heldDuration >= levelTwoHoldThreshold ? 2 : 1;
        }

        private bool TryBeginManaInfusion()
        {
            if (armedInfusionLevel <= 0 || mana == null)
            {
                return false;
            }

            // The small commitment is paid once per compatible action.  The
            // damage-dependent remainder is collected only after accepted hits.
            return baseInfusionCost <= 0f || mana.TrySpendMana(baseInfusionCost);
        }

        private void ConsumeDamageBasedInfusionMana(int appliedDamage)
        {
            if (mana == null || appliedDamage <= 0 || manaPerAcceptedDamage <= 0f)
            {
                return;
            }

            // DrainMana clamps at zero.  This is intentional: damage is already
            // authoritative and accepted, so an underfunded final bill cannot
            // retroactively alter it.  A later action will not infuse until it
            // can pay its base commitment again.
            float requestedCost = mana.GetModifiedManaCost(appliedDamage * manaPerAcceptedDamage);
            mana.DrainMana(requestedCost);
        }

        private void TryAwardFrenzyMasteryForLateInfusedKill(Damageable target)
        {
            if (frenzyMasteryAwarded
                || !IsFrenzyActive
                || target == null
                || target.gameObject.activeInHierarchy
                || currentFrenzyDuration <= 0f)
            {
                return;
            }

            float remaining = Mathf.Max(0f, frenzyExpiresAt - Time.time);
            float masteryWindow = currentFrenzyDuration
                * Mathf.Clamp01(frenzyMasteryWindowFraction);
            if (remaining > masteryWindow)
            {
                return;
            }

            EnsureFrenzyMastery();

            // One successful mana-infused enemy kill is the sole default
            // qualification per activation, including for multi-target actions.
            frenzyMasteryAwarded = true;
            PlayerMasteryEvidenceRuntime.EnsureOn(gameObject)?.SubmitFrenzy(
                new FrenzyMasterySample(0f, 0, 1), PlayerMasteryPolicy.Default);
            if (frenzyMastery != null && frenzyMastery.TryGainQualifiedLevel())
            {
                FeedbackRequested?.Invoke("FRENZY MASTERY +1");
            }

            StateChanged?.Invoke();
        }

#if UNITY_EDITOR
        private void TraceFrenzyMasteryKillAttempt(Damageable target)
        {
            float remaining = Mathf.Max(0f, frenzyExpiresAt - Time.time);
            float masteryWindow = currentFrenzyDuration
                * Mathf.Clamp01(frenzyMasteryWindowFraction);
            Debug.Log(
                "[Cave][FrenzyMasteryTrace] mana-infused impact"
                + " target=" + (target != null ? target.gameObject.name : "<none>")
                + " targetAlive=" + (target != null && target.gameObject.activeInHierarchy)
                + " frenzyActive=" + IsFrenzyActive
                + " alreadyAwarded=" + frenzyMasteryAwarded
                + " duration=" + currentFrenzyDuration.ToString("0.##")
                + " remaining=" + remaining.ToString("0.##")
                + " masteryWindow=" + masteryWindow.ToString("0.##"),
                this);
        }
#endif

        private float ResolveInfusionBonus(FrenzyBreakInfusion infusion, int infusionLevel)
        {
            bool strength = infusion == FrenzyBreakInfusion.Strength;
            if (infusionLevel >= 3)
            {
                return strength ? strengthLevelThreeBonus : normalLevelThreeBonus;
            }

            if (infusionLevel == 2)
            {
                return strength ? strengthLevelTwoBonus : normalLevelTwoBonus;
            }

            if (infusionLevel == 1)
            {
                return strength ? strengthLevelOneBonus : normalLevelOneBonus;
            }

            return 0f;
        }

        private static FrenzyBreakInfusion ResolveInfusion(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return FrenzyBreakInfusion.Ice;
                case SpecialMode.BurnShot:
                    return FrenzyBreakInfusion.Fire;
                case SpecialMode.Flight:
                    return FrenzyBreakInfusion.Wind;
                case SpecialMode.DamageBoost:
                    return FrenzyBreakInfusion.Strength;
                default:
                    return FrenzyBreakInfusion.Physical;
            }
        }

        private Color ResolveCriticalFeedbackColor(FrenzyBreakInfusion infusion)
        {
            switch (infusion)
            {
                case FrenzyBreakInfusion.Fire:
                    return new Color(1f, 0.38f, 0.1f, 1f);
                case FrenzyBreakInfusion.Ice:
                    return new Color(0.3f, 0.9f, 1f, 1f);
                case FrenzyBreakInfusion.Wind:
                    return new Color(0.65f, 1f, 0.78f, 1f);
                case FrenzyBreakInfusion.Strength:
                    return new Color(0.95f, 0.45f, 1f, 1f);
                default:
                    return criticalFallbackColor;
            }
        }

        private void SpawnCriticalFeedback(
            Vector3 position,
            FrenzyBreakInfusion infusion,
            int infusionLevel,
            FrenzyBreakAttackKind attackKind)
        {
            GameObject swingPrefab = attackKind != FrenzyBreakAttackKind.Projectile
                ? ResolveSwordSwingPrefab(infusion)
                : null;
            if (swingPrefab != null)
            {
                GameObject swing = Instantiate(swingPrefab, position, Quaternion.identity);
                Destroy(swing, frenzyVfxLifetime);
            }

            if (criticalImpactVfxPrefab != null)
            {
                GameObject effect = Instantiate(
                    criticalImpactVfxPrefab,
                    position + (Vector3)criticalImpactVfxOffset,
                    Quaternion.identity);
                effect.transform.localScale *= criticalImpactVfxScale;
                Destroy(effect, frenzyVfxLifetime);
            }
            else
            {
                CombatShapeEffect.Create(
                    position,
                    CombatShape.Diamond,
                    0.72f + infusionLevel * 0.08f,
                    criticalFallbackColor,
                    0.24f);
            }
        }

        private void SpawnFrenzyAura(FrenzyBreakInfusion infusion)
        {
            if (activeAura != null)
            {
                Destroy(activeAura);
            }

            GameObject auraPrefab = ResolveAuraPrefab(infusion);
            if (auraPrefab != null)
            {
                activeAura = Instantiate(auraPrefab, transform);
                activeAura.transform.localPosition = Vector3.zero;
                activeAura.transform.localRotation = Quaternion.identity;
            }
            else
            {
                CombatShapeEffect.Create(
                    transform.position,
                    CombatShape.Ring,
                    0.9f,
                    criticalFallbackColor,
                    0.35f);
            }
        }

        private GameObject ResolveAuraPrefab(FrenzyBreakInfusion infusion)
        {
            switch (infusion)
            {
                case FrenzyBreakInfusion.Fire:
                    return fireAuraPrefab;
                case FrenzyBreakInfusion.Ice:
                    return frostAuraPrefab;
                case FrenzyBreakInfusion.Wind:
                    return windAuraPrefab;
                case FrenzyBreakInfusion.Strength:
                    return strengthAuraPrefab;
                default:
                    return physicalAuraPrefab;
            }
        }

        private GameObject ResolveSwordSwingPrefab(FrenzyBreakInfusion infusion)
        {
            switch (infusion)
            {
                case FrenzyBreakInfusion.Fire:
                    return fireSwordSwingPrefab;
                case FrenzyBreakInfusion.Ice:
                    return frostSwordSwingPrefab;
                case FrenzyBreakInfusion.Wind:
                    return windSwordSwingPrefab;
                case FrenzyBreakInfusion.Strength:
                    return strengthSwordSwingPrefab;
                default:
                    return null;
            }
        }

        private void ClearFollowUpToken()
        {
            followUpSource = FormalFollowUpSource.None;
            chargedStartingTier = 0;
            followUpExpiresAt = 0f;
            followUpRemaining = 0f;
            StateChanged?.Invoke();
        }

        private void ClearFrenzy()
        {
            if (activeAura != null)
            {
                Destroy(activeAura);
                activeAura = null;
            }
            frenzyArmed = false;
            frenzyCriticalArmed = false;
            frenzyMasteryAwarded = false;
            armedInfusionLevel = 0;
            armedInfusion = FrenzyBreakInfusion.Physical;
            frenzyExpiresAt = 0f;
            currentFrenzyDuration = 0f;
            StateChanged?.Invoke();
        }

        private void EnsureFrenzyMastery()
        {
            if (frenzyMastery == null)
            {
                frenzyMastery = FrenzyMasteryState.EnsureOn(gameObject);
            }

            frenzyMastery?.Configure(
                frenzyMasteryDurationPerLevel,
                maximumFrenzyMasteryDurationBonus);
        }

        private void SubscribeSignals()
        {
            if (parry == null)
            {
                parry = GetComponent<SidewaysParryAttack>();
            }

            if (guardBreak == null)
            {
                guardBreak = GetComponent<PlayerGuardBreak>();
            }

            if (parry != null)
            {
                parry.DefenseSucceeded -= HandleDefenseSucceeded;
                parry.DefenseSucceeded += HandleDefenseSucceeded;
            }

            if (guardBreak != null)
            {
                guardBreak.OffensiveGuardBreakSucceeded -= HandleOffensiveGuardBreakSucceeded;
                guardBreak.OffensiveGuardBreakSucceeded += HandleOffensiveGuardBreakSucceeded;
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
                playerHealth.Died += HandlePlayerDied;
            }
        }

        private void OnDisable()
        {
            if (parry != null)
            {
                parry.DefenseSucceeded -= HandleDefenseSucceeded;
            }

            if (guardBreak != null)
            {
                guardBreak.OffensiveGuardBreakSucceeded -= HandleOffensiveGuardBreakSucceeded;
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
            }

            isPreparingFrenzy = false;
            formalChainInProgress = false;
            ClearSpinBashToken();
            ClearFrenzy();
            ClearFollowUpToken();
        }

        private void OnValidate()
        {
            levelTwoHoldThreshold = Mathf.Max(manaInfusionHoldThreshold, levelTwoHoldThreshold);
            levelThreeHoldThreshold = Mathf.Max(levelTwoHoldThreshold, levelThreeHoldThreshold);
            baseFrenzyDuration = Mathf.Max(0.1f, baseFrenzyDuration);
            frenzyMasteryWindowFraction = Mathf.Clamp(frenzyMasteryWindowFraction, 0.05f, 0.95f);
            frenzyMasteryDurationPerLevel = Mathf.Max(0f, frenzyMasteryDurationPerLevel);
            maximumFrenzyMasteryDurationBonus = Mathf.Max(0f, maximumFrenzyMasteryDurationBonus);
            baseInfusionCost = Mathf.Max(0f, baseInfusionCost);
            manaPerAcceptedDamage = Mathf.Max(0f, manaPerAcceptedDamage);
        }
    }

    /// <summary>Authoritative one-shot entry tiers for the normal Heavy state machine.</summary>
    public static class HeavyFollowUpPolicy
    {
        public static int StartingTier(FormalFollowUpSource source)
        {
            switch (source)
            {
                case FormalFollowUpSource.NormalParry:
                case FormalFollowUpSource.GuardBreak:
                    return 2;
                case FormalFollowUpSource.PerfectParry:
                    return 3;
                default:
                    return 0;
            }
        }
    }
}
