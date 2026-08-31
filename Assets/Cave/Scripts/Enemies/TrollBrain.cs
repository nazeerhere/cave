using Cave.Combat;
using Cave.Player;
using Cave.UI;
using UnityEngine;

namespace Cave.Enemies
{
    public enum TrollCombatTendency
    {
        Aggressive,
        OffensiveInitiative,
        Defensive
    }

    [DisallowMultipleComponent]
    public sealed class TrollBrain : MobBrainBase
    {
        [Header("Deliberate Frontline")]
        [SerializeField, Min(0f)] private float pursuitSpeed = 1.85f;
        [SerializeField, Min(0f)] private float basicApproachSlowBand = 0.45f;
        [SerializeField, Range(0.1f, 1f)] private float nearBasicApproachSpeedMultiplier = 0.45f;

        [Header("Melee Weights")]
        [SerializeField, Min(0f)] private float basicWeight = 1f;
        [SerializeField, Min(0f)] private float chargedWeight = 0.85f;
        [SerializeField, Min(0f)] private float committedPlayerChargeBonus = 0.15f;
        [SerializeField, Range(0f, 1f)] private float guardedBasicWeightMultiplier = 0.8f;
        [SerializeField, Min(0f)] private float guardedChargedWeightBonus = 0.2f;
        [SerializeField, Min(0f)] private float guardedBreakWeight = 0.8f;
        [SerializeField, Min(0f)] private float neutralGuardBreakWeight = 0.3f;

        [Header("Heavy Mixup Overrides")]
        [SerializeField] private bool useHeavyBrainOverrides = true;
        [SerializeField, Min(0f)] private float heavyPursuitSpeed = 1.85f;
        [SerializeField, Min(0f)] private float heavyBasicWeight = 3.4f;
        [SerializeField, Min(0f)] private float minimumHeavyBasicWeight = 3.4f;
        [SerializeField, Min(0f)] private float heavyChargedWeight = 0.85f;
        [SerializeField, Min(0f)] private float heavyPlayerCommitBonus = 0.15f;
        [SerializeField, Range(0f, 1f)] private float heavyGuardedBasicMultiplier = 0.8f;
        [SerializeField, Min(0f)] private float heavyGuardedChargedBonus = 0.2f;
        [SerializeField, Min(0f)] private float heavyGuardBreakWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float heavyJumpStompChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float heavyGuardedJumpStompChance = 0.5f;

        [Header("Bounded Mixups")]
        [SerializeField, Range(0f, 1f)] private float minimumBasicOpeningPriority = 0.58f;
        [SerializeField, Range(0f, 1f)] private float maximumBasicOpeningPriority = 0.68f;
        [SerializeField, Range(0f, 1f)] private float initiativeBasicPriority = 0.85f;
        [SerializeField, Range(0f, 1f)] private float guardHeldFeintChanceBonus = 0.1f;
        [SerializeField, Range(0f, 1f)] private float postAttackFollowUpChance = 0.4f;
        [SerializeField, Range(0f, 1f)] private float minimumPostAttackFollowUpChance = 0.4f;
        [SerializeField, Range(0, 1)] private int maximumChainDepth = 1;

        [Header("Defense To Offense")]
        [SerializeField, Range(0.5f, 0.9f)] private float offensiveInitiativeDuration = 0.75f;

        [Header("Adaptive Pressure")]
        [SerializeField, Min(0.1f)] private float maximumPressure = 4f;
        [SerializeField, Min(0f)] private float pressureMemoryWindow = 1.6f;
        [SerializeField, Min(0f)] private float pressurePerMeleeAttempt = 1f;
        [SerializeField, Min(0f)] private float pressurePerStagger = 1.4f;
        [SerializeField, Min(0f)] private float pressureDecayPerSecond = 0.75f;
        [SerializeField, Min(0f)] private float defensivePressureThreshold = 2f;
        [SerializeField, Range(0f, 1f)] private float defensivePostureDecisionChance = 0.85f;
        [SerializeField, Min(0.05f)] private float defensivePostureDuration = 0.58f;
        [SerializeField, Min(0f)] private float defensiveDecisionCooldown = 1.25f;
        [SerializeField, Range(0f, 0.2f)] private float maximumPressureBlockBonus = 0.1f;
        [SerializeField, Range(0f, 1f)] private float postBlockCounterChance = 0.4f;
        [SerializeField, Range(0f, 0.25f)] private float maximumReactiveFeintBonus = 0.12f;

        [Header("Jump Stomp Selection")]
        [SerializeField, Range(0f, 1f)] private float jumpStompUseChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float guardedJumpStompUseChance = 0.5f;
        [SerializeField, Min(0.1f)] private float jumpStompDecisionCooldown = 0.9f;

        [Header("Visual Facing")]
        [SerializeField] private SpriteRenderer trollVisual;
        [SerializeField] private Transform authoredVisualChildRoot;
        [SerializeField] private Transform weaponVisualRoot;
        [SerializeField, Min(0f)] private float facingVelocityThreshold = 0.05f;
        [SerializeField] private bool sourceSpriteFacesRight = true;

        [Header("Current Offense Decision (Read Only)")]
        [SerializeField] private EnemyMeleeDecision chosenDecision;
        [SerializeField] private EnemyMeleeUseRejection rejectionReason;
        [SerializeField] private EnemyMeleeUseRejection basicAvailability;
        [SerializeField] private EnemyMeleeUseRejection chargedAvailability;
        [SerializeField] private EnemyMeleeUseRejection guardBreakAvailability;
        [SerializeField] private string fallbackDecision = "None";
        [SerializeField] private TrollCombatTendency currentTendency;
        [SerializeField, Min(0f)] private float currentPressure;
        [SerializeField] private bool postBlockInitiativePending;
        [SerializeField, Min(0f)] private float offensiveInitiativeRemaining;
        [SerializeField] private string currentMixupContext = "Neutral";
        [SerializeField] private bool defensiveResponseEligible;
        [SerializeField] private bool closingToBasicRange;

        private EnemyMeleeCombat melee;
        private TrollJumpStomp jumpStomp;
        private Rigidbody2D body;
        private float nextJumpStompDecisionTime;
        private bool committedFacingLocked;
        private Damageable observedDamageable;
        private EnemyDefenseController observedDefense;
        private EnemyStagger observedStagger;
        private float nextDefensiveDecisionTime;
        private float lastPressureEventTime;
        private bool defenseInitiativeQueued;
        private float offensiveInitiativeUntil;
        private EnemyVisualChildFacing authoredChildFacing;

        protected override void ConfigureCapabilities()
        {
            melee = GetComponentInChildren<EnemyMeleeCombat>(true);
            jumpStomp = GetComponent<TrollJumpStomp>();
            body = GetComponent<Rigidbody2D>();
            if (trollVisual == null)
            {
                trollVisual = GetComponent<SpriteRenderer>();
            }

            if (authoredVisualChildRoot == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child != transform && child.name.ToLowerInvariant().Contains("corruption"))
                    {
                        authoredVisualChildRoot = child;
                        break;
                    }
                }
            }

            if (weaponVisualRoot == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    string name = child.name.ToLowerInvariant();
                    if (child != transform
                        && (name.Contains("axe") || name.Contains("weapon")))
                    {
                        weaponVisualRoot = child;
                        break;
                    }
                }
            }

            if (trollVisual == null
                && authoredVisualChildRoot == null
                && weaponVisualRoot == null)
            {
                trollVisual = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (authoredVisualChildRoot != null || weaponVisualRoot != null)
            {
                authoredChildFacing = GetComponent<EnemyVisualChildFacing>();
                if (authoredChildFacing == null)
                {
                    authoredChildFacing = gameObject.AddComponent<EnemyVisualChildFacing>();
                }

                authoredChildFacing.Configure(
                    authoredVisualChildRoot,
                    weaponVisualRoot,
                    sourceSpriteFacesRight);
            }

            if (GetComponent<EnemyWorldHealthBar>() == null)
            {
                gameObject.AddComponent<EnemyWorldHealthBar>();
            }

            ApplyMixupConfiguration();
        }

        protected override void Start()
        {
            base.Start();
            SubscribeRuntimeSignals();
            RefreshAdaptiveModifiers();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            closingToBasicRange = false;
            SubscribeRuntimeSignals();
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            melee?.SetBrainControlled(controlled);
            jumpStomp?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return (melee != null && melee.IsAttacking)
                || (jumpStomp != null && jumpStomp.IsStomping);
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            FaceVisual(toPlayer.x);
            ActivateQueuedOffensiveInitiative();
            RefreshAdaptiveModifiers();
            if (TryChooseDefensivePosture())
            {
                currentMixupContext = "Pressure defense";
                HoldPosition(MobBrainState.Defend, "Adaptive defensive posture");
                return;
            }

            SidewaysParryAttack guard = player.GetComponent<SidewaysParryAttack>();
            bool guardHeld = guard != null && guard.IsGuardHeld;
            if (melee == null)
            {
                Move(Mathf.Sign(toPlayer.x), PursuitSpeed, MobBrainState.Chase, "Advance deliberately");
                return;
            }

            bool canBasic = melee.CanUse(
                EnemyMeleeDecision.Basic,
                player,
                out EnemyMeleeUseRejection basicRejection);
            bool canCharged = melee.CanUse(
                EnemyMeleeDecision.Charged,
                player,
                out EnemyMeleeUseRejection chargedRejection);
            EnemyMeleeUseRejection guardBreakRejection;
            bool canGuardBreak = melee.CanUse(
                EnemyMeleeDecision.GuardBreak,
                player,
                out guardBreakRejection);
            basicAvailability = basicRejection;
            chargedAvailability = chargedRejection;
            guardBreakAvailability = guardBreakRejection;
            bool basicInRange = melee.IsInRange(EnemyMeleeDecision.Basic, player);

            if (closingToBasicRange)
            {
                if (!basicInRange)
                {
                    AdvanceIntoBasicRange(toPlayer);
                    return;
                }

                closingToBasicRange = false;
                if (canBasic && TryStartMelee(
                    player,
                    EnemyMeleeDecision.Basic,
                    "Basic after stable range entry"))
                {
                    return;
                }
            }

            if (canBasic
                && ShouldPrioritizeBasic(player, guardHeld, canCharged, canGuardBreak)
                && TryStartMelee(player, EnemyMeleeDecision.Basic, "Basic-first pressure"))
            {
                return;
            }

            // Stomp remains a medium-range closer. It no longer consumes close-range
            // decisions while the bread-and-butter Basic is available.
            if (!canBasic
                && jumpStomp != null
                && Time.time >= nextJumpStompDecisionTime
                && jumpStomp.CanUse(player))
            {
                nextJumpStompDecisionTime = Time.time + jumpStompDecisionCooldown;
                float chance = guardHeld
                    ? GuardedJumpStompChance
                    : JumpStompChance;
                if (Random.value <= chance && jumpStomp.TryUse(player))
                {
                    ConsumeOffensiveInitiative();
                    currentMixupContext = guardHeld
                        ? "Guarded medium-range stomp"
                        : "Medium-range stomp";
                    fallbackDecision = "None";
                    HoldPosition(MobBrainState.Attack, "Jump Stomp");
                    return;
                }
            }

            if (!canBasic && !canCharged && !canGuardBreak)
            {
                if (!basicInRange)
                {
                    closingToBasicRange = true;
                    rejectionReason = EnemyMeleeUseRejection.OutOfRange;
                    fallbackDecision = "Close to Basic range";
                    AdvanceIntoBasicRange(toPlayer);
                    return;
                }

                bool anyAttackInRange = basicInRange
                    || melee.IsInRange(EnemyMeleeDecision.Charged, player)
                    || melee.IsInRange(EnemyMeleeDecision.GuardBreak, player);
                if (!anyAttackInRange)
                {
                    rejectionReason = EnemyMeleeUseRejection.OutOfRange;
                    fallbackDecision = "Advance";
                    Move(
                        Mathf.Sign(toPlayer.x),
                        PursuitSpeed,
                        MobBrainState.Chase,
                        "Advance: no attack in range");
                    return;
                }

                rejectionReason = ResolveMostUsefulRejection(
                    basicRejection,
                    chargedRejection,
                    guardBreakRejection);
                // Cooldowns and a rejected contextual option must not turn an
                // engaged Troll into a stationary target. Keep deliberate forward
                // pressure until a normal melee range is actionable again.
                fallbackDecision = "Maintain pressure while unavailable";
                Move(
                    Mathf.Sign(toPlayer.x),
                    PursuitSpeed * nearBasicApproachSpeedMultiplier,
                    MobBrainState.Chase,
                    $"Maintain pressure: {rejectionReason}");
                return;
            }

            HoldPosition(MobBrainState.Attack, "Choose range-valid melee action");
            chosenDecision = ChooseMixup(player, guardHeld, canBasic, canCharged, canGuardBreak);
            fallbackDecision = "None";
            if (melee.TryUse(chosenDecision, player, out rejectionReason))
            {
                ConsumeOffensiveInitiative();
                SetState(MobBrainState.Attack, chosenDecision.ToString());
                return;
            }

            if (chosenDecision != EnemyMeleeDecision.Basic
                && melee.CanUse(EnemyMeleeDecision.Basic, player, out _)
                && melee.TryUse(
                    EnemyMeleeDecision.Basic,
                    player,
                    out _))
            {
                ConsumeOffensiveInitiative();
                fallbackDecision = $"Basic after {chosenDecision} rejected: {rejectionReason}";
                SetState(MobBrainState.Attack, fallbackDecision);
                return;
            }

            fallbackDecision = $"None ({rejectionReason})";
            if (rejectionReason == EnemyMeleeUseRejection.OutOfRange)
            {
                AdvanceIntoBasicRange(toPlayer);
                return;
            }

            Move(
                Mathf.Sign(toPlayer.x),
                PursuitSpeed * nearBasicApproachSpeedMultiplier,
                MobBrainState.Chase,
                $"Maintain pressure after {chosenDecision} rejection");
        }

        private void AdvanceIntoBasicRange(Vector2 toPlayer)
        {
            float distance = Mathf.Abs(toPlayer.x);
            float speed = distance <= melee.BasicEngagementRange + basicApproachSlowBand
                ? PursuitSpeed * nearBasicApproachSpeedMultiplier
                : PursuitSpeed;
            Move(
                Mathf.Sign(toPlayer.x),
                speed,
                MobBrainState.Chase,
                "Advance through Basic range dead zone");
        }

        private EnemyMeleeDecision ChooseMixup(
            PlayerHealth player,
            bool guardHeld,
            bool canBasic,
            bool canCharged,
            bool canGuardBreak)
        {
            PlayerAttackState playerAttack = player.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttack != null && playerAttack.IsActivelyAttacking;
            float charged = canCharged
                ? ChargedWeight
                    + (playerCommitted ? PlayerCommitBonus : 0f)
                    + (guardHeld ? GuardedChargedBonus : 0f)
                : 0f;
            float guardBreak = canGuardBreak
                ? (guardHeld ? GuardBreakWeight : neutralGuardBreakWeight)
                : 0f;
            currentMixupContext = guardHeld
                    ? "Player guarding"
                    : playerCommitted
                        ? "Player committed"
                        : "Aggressive mixup";
            float total = charged + guardBreak;
            if (total <= 0f)
            {
                return canBasic
                    ? EnemyMeleeDecision.Basic
                    : canCharged
                        ? EnemyMeleeDecision.Charged
                        : EnemyMeleeDecision.GuardBreak;
            }

            float roll = Random.value * Mathf.Max(0.0001f, total);
            if ((roll -= charged) < 0f)
            {
                return EnemyMeleeDecision.Charged;
            }

            return EnemyMeleeDecision.GuardBreak;
        }

        private bool ShouldPrioritizeBasic(
            PlayerHealth player,
            bool guardHeld,
            bool canCharged,
            bool canGuardBreak)
        {
            if (!canCharged && !canGuardBreak)
            {
                return true;
            }

            if (IsOffensiveInitiativeActive)
            {
                currentMixupContext = "Defense-to-offense Basic initiative";
                return Random.value < initiativeBasicPriority;
            }

            PlayerAttackState playerAttack = player.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttack != null && playerAttack.IsActivelyAttacking;
            float basic = BasicWeight * (guardHeld ? GuardedBasicMultiplier : 1f);
            float charged = canCharged
                ? ChargedWeight
                    + (playerCommitted ? PlayerCommitBonus : 0f)
                    + (guardHeld ? GuardedChargedBonus : 0f)
                : 0f;
            float guardBreak = canGuardBreak
                ? (guardHeld ? GuardBreakWeight : neutralGuardBreakWeight)
                : 0f;
            float weightedShare = basic / Mathf.Max(0.0001f, basic + charged + guardBreak);
            float priority = Mathf.Clamp(
                weightedShare,
                minimumBasicOpeningPriority,
                maximumBasicOpeningPriority);
            currentMixupContext = guardHeld
                ? $"Guarded Basic priority ({priority:P0})"
                : $"Aggressive Basic priority ({priority:P0})";
            return Random.value < priority;
        }

        private bool TryStartMelee(
            PlayerHealth player,
            EnemyMeleeDecision decision,
            string decisionLabel)
        {
            chosenDecision = decision;
            fallbackDecision = "None";
            if (!melee.TryUse(decision, player, out rejectionReason))
            {
                return false;
            }

            ConsumeOffensiveInitiative();
            SetState(MobBrainState.Attack, decisionLabel);
            return true;
        }

        private void ApplyMixupConfiguration()
        {
            melee?.ConfigureBrainMixups(
                Mathf.Max(postAttackFollowUpChance, minimumPostAttackFollowUpChance),
                maximumChainDepth,
                guardHeldFeintChanceBonus);
        }

        private bool TryChooseDefensivePosture()
        {
            defensiveResponseEligible = Defense != null
                && !IsOffensiveInitiativeActive
                && currentPressure >= defensivePressureThreshold
                && Time.time >= nextDefensiveDecisionTime
                && Defense.CanEnterDefensivePosture;
            if (!defensiveResponseEligible
                || Defense == null
                || maximumPressure <= 0f)
            {
                return false;
            }

            float pressureAboveThreshold = Mathf.InverseLerp(
                defensivePressureThreshold,
                maximumPressure,
                currentPressure);
            float decisionChance = Mathf.Lerp(
                defensivePostureDecisionChance,
                1f,
                pressureAboveThreshold);
            if (Random.value > decisionChance)
            {
                nextDefensiveDecisionTime = Time.time + Mathf.Min(
                    0.25f,
                    defensiveDecisionCooldown);
                return false;
            }

            bool entered = Defense.TryEnterDefensivePosture(defensivePostureDuration);
            nextDefensiveDecisionTime = Time.time + (entered
                ? defensiveDecisionCooldown
                : Mathf.Min(0.25f, defensiveDecisionCooldown));
            return entered;
        }

        private void QueueOffensiveInitiative()
        {
            defenseInitiativeQueued = true;
            postBlockInitiativePending = true;
        }

        private void ActivateQueuedOffensiveInitiative()
        {
            if (!defenseInitiativeQueued)
            {
                offensiveInitiativeRemaining = Mathf.Max(
                    0f,
                    offensiveInitiativeUntil - Time.time);
                if (offensiveInitiativeRemaining <= 0f)
                {
                    postBlockInitiativePending = false;
                }

                return;
            }

            defenseInitiativeQueued = false;
            offensiveInitiativeUntil = Time.time + offensiveInitiativeDuration;
            offensiveInitiativeRemaining = offensiveInitiativeDuration;
            postBlockInitiativePending = true;
        }

        private void ConsumeOffensiveInitiative()
        {
            defenseInitiativeQueued = false;
            offensiveInitiativeUntil = 0f;
            offensiveInitiativeRemaining = 0f;
            postBlockInitiativePending = false;
        }

        private bool IsOffensiveInitiativeActive => postBlockInitiativePending
            && (defenseInitiativeQueued || Time.time < offensiveInitiativeUntil);

        private void SubscribeRuntimeSignals()
        {
            UnsubscribeRuntimeSignals();
            observedDamageable = GetComponent<Damageable>();
            observedDefense = GetComponent<EnemyDefenseController>();
            observedStagger = GetComponent<EnemyStagger>();
            if (observedDamageable != null)
            {
                observedDamageable.DamageResolved += HandleDamageResolved;
            }

            if (observedDefense != null)
            {
                observedDefense.DefenseResolved += HandleDefenseResolved;
                observedDefense.GuardBroken += HandleGuardBroken;
            }

            if (observedStagger != null)
            {
                observedStagger.Staggered += HandleStaggered;
            }
        }

        private void UnsubscribeRuntimeSignals()
        {
            if (observedDamageable != null)
            {
                observedDamageable.DamageResolved -= HandleDamageResolved;
            }

            if (observedDefense != null)
            {
                observedDefense.DefenseResolved -= HandleDefenseResolved;
                observedDefense.GuardBroken -= HandleGuardBroken;
            }

            if (observedStagger != null)
            {
                observedStagger.Staggered -= HandleStaggered;
            }

            observedDamageable = null;
            observedDefense = null;
            observedStagger = null;
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (!context.IsPlayerDamage || !context.HasTrait(DamageTrait.Melee))
            {
                return;
            }

            AddPressure(pressurePerMeleeAttempt + (appliedDamage > 0 ? 0.25f : 0f));
            if (blocked && Random.value < postBlockCounterChance)
            {
                QueueOffensiveInitiative();
            }
        }

        private void HandleDefenseResolved(EnemyDefenseState state, bool succeeded)
        {
            if (succeeded
                && (state == EnemyDefenseState.Blocking
                    || state == EnemyDefenseState.Parrying))
            {
                // The defense component owns its active/recovery timing. The queued
                // window starts on the first Brain evaluation after that timing ends.
                QueueOffensiveInitiative();
                return;
            }

            if (state == EnemyDefenseState.Blocking && !succeeded)
            {
                currentPressure = Mathf.Max(0f, currentPressure - 0.25f);
            }
        }

        private void HandleGuardBroken()
        {
            ConsumeOffensiveInitiative();
            AddPressure(pressurePerStagger);
        }

        private void HandleStaggered(StaggerStrength strength, float duration)
        {
            AddPressure(pressurePerStagger);
            ConsumeOffensiveInitiative();
        }

        private void AddPressure(float amount)
        {
            currentPressure = Mathf.Clamp(
                currentPressure + Mathf.Max(0f, amount),
                0f,
                maximumPressure);
            lastPressureEventTime = Time.time;
            RefreshAdaptiveModifiers();
        }

        private void RefreshAdaptiveModifiers()
        {
            float normalized = maximumPressure > 0f
                ? Mathf.Clamp01(currentPressure / maximumPressure)
                : 0f;
            Defense?.SetRuntimeBlockChanceBonus(normalized * maximumPressureBlockBonus);
            melee?.SetReactiveFeintChanceBonus(normalized * maximumReactiveFeintBonus);
            currentTendency = IsOffensiveInitiativeActive
                ? TrollCombatTendency.OffensiveInitiative
                : currentPressure >= defensivePressureThreshold
                    ? TrollCombatTendency.Defensive
                    : TrollCombatTendency.Aggressive;
        }

        private void LateUpdate()
        {
            if (currentPressure > 0f
                && Time.time - lastPressureEventTime >= pressureMemoryWindow)
            {
                currentPressure = Mathf.Max(
                    0f,
                    currentPressure - pressureDecayPerSecond * Time.deltaTime);
                RefreshAdaptiveModifiers();
            }

            bool committed = IsCapabilityBusy();
            if (committed)
            {
                if (!committedFacingLocked && Target != null)
                {
                    FaceVisual(Target.transform.position.x - transform.position.x);
                }

                committedFacingLocked = true;
                return;
            }

            committedFacingLocked = false;
            if (body != null && Mathf.Abs(body.velocity.x) >= facingVelocityThreshold)
            {
                FaceVisual(body.velocity.x);
            }
        }

        private void FaceVisual(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) < facingVelocityThreshold)
            {
                return;
            }

            if (trollVisual != null)
            {
                bool faceRight = horizontalDirection > 0f;
                trollVisual.flipX = sourceSpriteFacesRight ? !faceRight : faceRight;
            }

            authoredChildFacing?.Face(horizontalDirection);

            melee?.SetCombatFacing(horizontalDirection);
        }

        private float PursuitSpeed => useHeavyBrainOverrides
            ? heavyPursuitSpeed
            : pursuitSpeed;
        private float BasicWeight => useHeavyBrainOverrides
            ? Mathf.Max(heavyBasicWeight, minimumHeavyBasicWeight)
            : basicWeight;
        private float ChargedWeight => useHeavyBrainOverrides ? heavyChargedWeight : chargedWeight;
        private float PlayerCommitBonus => useHeavyBrainOverrides
            ? heavyPlayerCommitBonus
            : committedPlayerChargeBonus;
        private float GuardedBasicMultiplier => useHeavyBrainOverrides
            ? heavyGuardedBasicMultiplier
            : guardedBasicWeightMultiplier;
        private float GuardedChargedBonus => useHeavyBrainOverrides
            ? heavyGuardedChargedBonus
            : guardedChargedWeightBonus;
        private float GuardBreakWeight => useHeavyBrainOverrides
            ? heavyGuardBreakWeight
            : guardedBreakWeight;
        private float JumpStompChance => useHeavyBrainOverrides
            ? heavyJumpStompChance
            : jumpStompUseChance;
        private float GuardedJumpStompChance => useHeavyBrainOverrides
            ? heavyGuardedJumpStompChance
            : guardedJumpStompUseChance;

        private static EnemyMeleeUseRejection ResolveMostUsefulRejection(
            EnemyMeleeUseRejection basic,
            EnemyMeleeUseRejection charged,
            EnemyMeleeUseRejection guardBreak)
        {
            if (basic != EnemyMeleeUseRejection.OutOfRange)
            {
                return basic;
            }

            return charged != EnemyMeleeUseRejection.OutOfRange ? charged : guardBreak;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            maximumBasicOpeningPriority = Mathf.Max(
                minimumBasicOpeningPriority,
                maximumBasicOpeningPriority);
            if (Application.isPlaying)
            {
                ApplyMixupConfiguration();
            }
        }

        protected override void OnDisable()
        {
            UnsubscribeRuntimeSignals();
            ConsumeOffensiveInitiative();
            base.OnDisable();
        }
    }
}
