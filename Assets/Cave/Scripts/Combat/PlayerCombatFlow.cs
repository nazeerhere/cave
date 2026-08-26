using System;
using System.Collections.Generic;
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
        private readonly HashSet<Damageable> criticalTargets = new HashSet<Damageable>();
        private bool windBurstApplied;
        private bool completed;

        internal FrenzyBreakActivation(
            PlayerCombatFlow flow,
            FrenzyBreakInfusion selectedInfusion,
            int infusionLevel,
            FrenzyBreakRole selectedRole,
            FrenzyBreakAttackKind attackKind,
            int skillTier)
        {
            owner = flow;
            Infusion = selectedInfusion;
            InfusionLevel = infusionLevel;
            Role = selectedRole;
            AttackKind = attackKind;
            SkillTier = skillTier;
        }

        public FrenzyBreakInfusion Infusion { get; }
        public int InfusionLevel { get; }
        public FrenzyBreakRole Role { get; }
        public FrenzyBreakAttackKind AttackKind { get; }
        public int SkillTier { get; }
        public bool ManaInfused => InfusionLevel > 0;
        public bool UseFrostTierThreePin => AttackKind == FrenzyBreakAttackKind.Projectile
            && Infusion == FrenzyBreakInfusion.Ice
            && SkillTier >= 3;

        public bool TryResolveDamage(int baseDamage, Damageable target, out int resolvedDamage)
        {
            resolvedDamage = baseDamage;
            if (completed
                || owner == null
                || !owner.IsBoundActivation(this)
                || target == null
                || !criticalTargets.Add(target))
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

        public void ApplyImpact(Damageable target, Vector2 attackDirection, int appliedDamage)
        {
            if (completed || appliedDamage <= 0 || owner == null)
            {
                return;
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
                appliedDamage);
            if (Infusion == FrenzyBreakInfusion.Wind)
            {
                windBurstApplied = true;
            }
        }

        public void Complete()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            owner?.CompleteBoundFrenzy(this);
        }

        public void KeepAlive()
        {
            if (!completed)
            {
                owner?.RefreshBoundFrenzySafety(this);
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
        [SerializeField, Min(0.1f)] private float armedTimeout = 12f;
        [SerializeField, Min(0.1f)] private float boundAttackSafetyTimeout = 5f;
        [SerializeField, Min(0.05f)] private float combatContextRadius = 7f;

        [Header("Frenzy Break Costs — Current Maximum")]
        [SerializeField, Range(0f, 1f)] private float staminaCostFraction = 0.35f;
        [SerializeField, Range(0f, 1f)] private float levelOneManaCostFraction = 0.25f;
        [SerializeField, Range(0f, 1f)] private float levelTwoManaCostFraction = 0.40f;
        [SerializeField, Range(0f, 1f)] private float levelThreeManaCostFraction = 0.55f;

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
        [SerializeField, Range(0, 2)] private int chargedStartingTier;
        [SerializeField, Min(0f)] private float followUpRemaining;
        [SerializeField] private bool formalChainInProgress;
        [SerializeField] private bool spinBashAvailable;
        [SerializeField, Min(0f)] private float spinBashRemaining;
        [SerializeField] private bool isPreparingFrenzy;
        [SerializeField] private bool frenzyArmed;
        [SerializeField] private bool frenzyBound;
        [SerializeField] private FrenzyBreakInfusion armedInfusion;
        [SerializeField, Range(0, 3)] private int armedInfusionLevel;
        [SerializeField] private FrenzyBreakRole armedRole;
        [SerializeField, Min(0f)] private float armedRemaining;
        [SerializeField] private FrenzyBreakAttackKind boundAttackKind;
        [SerializeField, Min(0f)] private float boundSafetyRemaining;

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
        private float followUpExpiresAt;
        private float preparationStartedAt;
        private float frenzyExpiresAt;
        private float boundSafetyExpiresAt;
        private float nextHostileRefreshTime;
        private bool hostileNearby;
        private float meaningfulSpinStartedAt = -1f;
        private float spinBashExpiresAt;
        private GameObject activeAura;
        private FrenzyBreakActivation boundActivation;

        public event Action StateChanged;
        public event Action<string> FeedbackRequested;

        public bool IsPreparingFrenzy => isPreparingFrenzy;
        public bool IsFrenzyArmed => frenzyArmed && Time.time <= frenzyExpiresAt;
        public bool IsFrenzyBound => frenzyBound && boundActivation != null;
        public FrenzyBreakInfusion ArmedInfusion => armedInfusion;
        public int ArmedInfusionLevel => armedInfusionLevel;
        public FrenzyBreakRole ArmedRole => armedRole;
        public FrenzyBreakAttackKind BoundAttackKind => boundAttackKind;
        public float ArmedTimeRemaining => IsFrenzyArmed
            ? Mathf.Max(0f, frenzyExpiresAt - Time.time)
            : 0f;
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
                && !IsFrenzyArmed
                && !IsFrenzyBound
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
            if (!IsFrenzyArmed || IsFrenzyBound)
            {
                return false;
            }

            float staminaCost = stamina.MaximumStamina * staminaCostFraction;
            float manaCost = ResolveManaCost(armedInfusionLevel);
            if (!stamina.CanSpendStamina(staminaCost)
                || (manaCost > 0f && !mana.CanSpendMana(manaCost)))
            {
                string missing = !stamina.CanSpendStamina(staminaCost) ? "STAMINA" : "MANA";
                ClearFrenzy();
                FeedbackRequested?.Invoke("FRENZY FAILED — " + missing);
                return false;
            }

            bool staminaPaid = stamina.TrySpendStamina(staminaCost);
            bool manaPaid = manaCost <= 0f || mana.TrySpendMana(manaCost);
            if (!staminaPaid || !manaPaid)
            {
                ClearFrenzy();
                FeedbackRequested?.Invoke("FRENZY FAILED");
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
                currentSkillTier);
            boundActivation = activation;
            frenzyArmed = false;
            frenzyBound = true;
            boundAttackKind = attackKind;
            frenzyExpiresAt = 0f;
            armedRemaining = 0f;
            boundSafetyExpiresAt = Time.time + boundAttackSafetyTimeout;
            boundSafetyRemaining = boundAttackSafetyTimeout;
            string message = "FRENZY " + armedRole.ToString().ToUpperInvariant()
                + " BOUND — " + attackKind.ToString().ToUpperInvariant();
            FeedbackRequested?.Invoke(message);
            if (attackKind != FrenzyBreakAttackKind.Charged)
            {
                formalChainInProgress = false;
            }

            StateChanged?.Invoke();
            return true;
        }

        internal bool IsBoundActivation(FrenzyBreakActivation activation)
        {
            return frenzyBound && activation != null && activation == boundActivation;
        }

        internal void CompleteBoundFrenzy(FrenzyBreakActivation activation)
        {
            if (!IsBoundActivation(activation))
            {
                return;
            }

            ClearFrenzy();
        }

        internal void RefreshBoundFrenzySafety(FrenzyBreakActivation activation)
        {
            if (!IsBoundActivation(activation))
            {
                return;
            }

            boundSafetyExpiresAt = Time.time + boundAttackSafetyTimeout;
            boundSafetyRemaining = boundAttackSafetyTimeout;
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

        internal void ApplyFrenzyImpact(
            Damageable target,
            Vector2 attackDirection,
            FrenzyBreakInfusion infusion,
            int infusionLevel,
            FrenzyBreakAttackKind attackKind,
            int skillTier,
            bool allowWindBurst,
            int appliedDamage)
        {
            if (target == null)
            {
                return;
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
                        settings.FrostFieldFillColor);
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

            SpawnCriticalFeedback(target.transform.position, infusion, infusionLevel, attackKind);
            FrenzyCriticalPopup.Create(
                target,
                appliedDamage,
                ResolveCriticalFeedbackColor(infusion));
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
            frenzyExpiresAt = Time.time + armedTimeout;
            SpawnFrenzyAura(armedInfusion);
            FeedbackRequested?.Invoke(
                "FRENZY ARMED — " + armedInfusion.ToString().ToUpperInvariant());
            StateChanged?.Invoke();
        }

        private void GrantChargedFollowUp(FormalFollowUpSource source, int tier)
        {
            followUpSource = source;
            chargedStartingTier = Mathf.Clamp(tier, 1, 2);
            followUpExpiresAt = Time.time + followUpWindow;
            followUpRemaining = followUpWindow;
            StateChanged?.Invoke();
        }

        private void HandleDefenseSucceeded(PlayerDefenseQuality quality)
        {
            if (quality == PlayerDefenseQuality.PerfectParry)
            {
                GrantChargedFollowUp(FormalFollowUpSource.PerfectParry, 2);
            }
            else if (quality == PlayerDefenseQuality.NormalParry)
            {
                GrantChargedFollowUp(FormalFollowUpSource.NormalParry, 1);
            }
        }

        private void HandleOffensiveGuardBreakSucceeded()
        {
            GrantChargedFollowUp(FormalFollowUpSource.GuardBreak, 1);
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

            armedRemaining = IsFrenzyArmed ? Mathf.Max(0f, frenzyExpiresAt - Time.time) : 0f;
            if (frenzyArmed && !IsFrenzyArmed)
            {
                ClearFrenzy();
                FeedbackRequested?.Invoke("FRENZY EXPIRED");
            }

            boundSafetyRemaining = IsFrenzyBound
                ? Mathf.Max(0f, boundSafetyExpiresAt - Time.time)
                : 0f;
            if (IsFrenzyBound && Time.time > boundSafetyExpiresAt)
            {
                ClearFrenzy();
                FeedbackRequested?.Invoke("FRENZY BIND RESET");
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

        private float ResolveManaCost(int infusionLevel)
        {
            float fraction = infusionLevel >= 3
                ? levelThreeManaCostFraction
                : infusionLevel == 2
                    ? levelTwoManaCostFraction
                    : infusionLevel == 1
                        ? levelOneManaCostFraction
                        : 0f;
            return mana.MaximumMana * fraction;
        }

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
            frenzyBound = false;
            if (boundActivation != null)
            {
                boundActivation.Invalidate();
                boundActivation = null;
            }
            armedInfusionLevel = 0;
            armedInfusion = FrenzyBreakInfusion.Physical;
            frenzyExpiresAt = 0f;
            armedRemaining = 0f;
            boundAttackKind = FrenzyBreakAttackKind.Spin;
            boundSafetyExpiresAt = 0f;
            boundSafetyRemaining = 0f;
            StateChanged?.Invoke();
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
        }
    }
}
