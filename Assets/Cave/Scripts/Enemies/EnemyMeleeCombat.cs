using System.Collections;
using System.Collections.Generic;
using Cave.Audio;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum EnemyMeleePreset
    {
        Custom,
        Skeleton,
        Brute,
        Troll
    }

    public enum EnemyMeleeDecision
    {
        Basic,
        Charged,
        Feint,
        GuardBreak
    }

    public enum EnemyMeleeUseRejection
    {
        None,
        MissingTarget,
        TargetInactive,
        Busy,
        Cooldown,
        Staggered,
        Defending,
        OutOfRange,
        GuardRequired,
        ChanceRejected
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyMeleeCombat : MonoBehaviour,
        IEnemyInterruptible,
        IEnemyInterruptPolicy,
        IEnemySkillEvolutionReceiver,
        ICounterableGuardBreak
    {
        [Header("Targeting")]
        [SerializeField] private PlayerHealth target;
        [SerializeField, Min(0.1f)] private float awarenessRange = 8f;
        [SerializeField] private EnemyMeleePreset probabilityPreset = EnemyMeleePreset.Custom;

        [Header("Basic Melee Attack")]
        [SerializeField, Min(1)] private int basicDamage = 1;
        [SerializeField, Min(0.1f)] private float basicRange = 1.25f;
        [SerializeField, Min(0f)] private float basicWindup = 0.2f;
        [SerializeField, Min(0.01f)] private float basicActiveDuration = 0.1f;
        [SerializeField, Min(0f)] private float basicRecovery = 0.35f;
        [SerializeField, Min(0f)] private float basicCooldown = 0.7f;

        [Header("Charged Melee Attack")]
        [SerializeField, Range(0f, 1f)] private float chargedAttackChance = 0.2f;
        [SerializeField, Min(1)] private int chargedDamage = 3;
        [SerializeField, Min(0.1f)] private float chargedRange = 1.55f;
        [SerializeField, Min(0f)] private float chargedWindup = 0.75f;
        [SerializeField, Min(0.01f)] private float chargedActiveDuration = 0.14f;
        [SerializeField, Min(0f)] private float chargedRecovery = 0.7f;
        [SerializeField, Min(0f)] private float chargedCooldown = 1.35f;
        [SerializeField] private bool chargedAttackIsPiercing;

        [Header("Feint")]
        [SerializeField, Range(0f, 1f)] private float feintChance = 0.15f;
        [SerializeField, Min(0f)] private float feintDelay = 0.3f;

        [Header("Guard Break")]
        [SerializeField, Range(0f, 1f)] private float guardBreakChance = 0.15f;
        [SerializeField, Min(1)] private int guardBreakDamage = 2;
        [SerializeField, Min(0.1f)] private float guardBreakRange = 1.35f;
        [SerializeField, Min(0f)] private float guardBreakWindup = 0.65f;
        [SerializeField, Min(0.01f)] private float guardBreakActiveDuration = 0.12f;
        [SerializeField, Min(0f)] private float guardBreakRecovery = 0.65f;
        [SerializeField, Min(0f)] private float guardBreakCooldown = 1.25f;
        [SerializeField, Min(0.05f)] private float guardBreakCounterWindow = 0.22f;
        [SerializeField, Min(0f)] private float counteredRecovery = 0.45f;
        [SerializeField, Range(0f, 1f)] private float guardBreakFollowUpChance = 0.4f;

        [Header("Troll Heavy Basic Overrides")]
        [SerializeField] private bool useTrollHeavyOverrides = true;
        [SerializeField, Min(1)] private int trollBasicDamage = 3;
        [SerializeField, Min(0.1f)] private float trollBasicRange = 1.45f;
        [SerializeField, Min(0f)] private float trollBasicWindup = 0.5f;
        [SerializeField, Min(0.01f)] private float trollBasicActiveDuration = 0.12f;
        [SerializeField, Min(0f)] private float trollBasicRecovery = 0.5f;
        [SerializeField, Min(0f)] private float trollBasicCooldown = 0.9f;
        [SerializeField, Range(0f, 1f)] private float trollBasicFeintChance = 0.23f;
        [SerializeField, Range(0.1f, 0.9f)] private float trollBasicCommitPoint = 0.6f;
        [SerializeField, Min(0f)] private float trollBasicKnockback = 6f;

        [Header("Troll Charged Heavy Overrides")]
        [SerializeField, Min(1)] private int trollChargedDamage = 6;
        [SerializeField, Min(0.1f)] private float trollChargedRange = 1.8f;
        [SerializeField, Min(0.1f)] private float trollGuardBreakRange = 1.2f;
        [SerializeField, Min(0f)] private float trollChargedWindup = 1.1f;
        [SerializeField, Min(0.01f)] private float trollChargedActiveDuration = 0.16f;
        [SerializeField, Min(0f)] private float trollChargedRecovery = 0.85f;
        [SerializeField, Min(0f)] private float trollChargedCooldown = 1.45f;
        [SerializeField, Range(0f, 1f)] private float trollChargedFeintChance = 0.33f;
        [SerializeField, Range(0f, 1f)] private float trollHeavyToGuardBreakSoftFeintChance = 0.25f;
        [SerializeField, Range(0.1f, 0.9f)] private float trollChargedCommitPoint = 0.7f;
        [SerializeField, Min(0f)] private float trollChargedKnockback = 12f;
        [SerializeField, Min(0f)] private float trollKnockbackControlLock = 0.2f;

        [Header("Troll Feint Follow-ups")]
        [SerializeField, Min(0f)] private float followUpBasicWeight = 1f;
        [SerializeField, Min(0f)] private float followUpChargedWeight = 0.75f;
        [SerializeField, Min(0f)] private float followUpGuardBreakWeight = 0.9f;

        [Header("Optional Weapon Presentation")]
        [SerializeField] private Transform weaponPresentation;
        [SerializeField] private Transform attackOrigin;
        [SerializeField, Min(0f)] private float maximumAttackOriginForwardOffset = 0.85f;
        [SerializeField] private float basicDrawbackAngle = 32f;
        [SerializeField] private float basicStrikeAngle = -28f;
        [SerializeField] private float chargedDrawbackAngle = 62f;
        [SerializeField] private float chargedStrikeAngle = -48f;
        [SerializeField, Min(1f)] private float chargedWeaponScale = 1.15f;
        [SerializeField] private float trollChargedVerticalOffset = -0.18f;

        [Header("Telegraph Hooks")]
        [SerializeField] private Color basicTelegraphColor = new Color(0.35f, 0.72f, 1f, 1f);
        [SerializeField] private Color chargedTelegraphColor = new Color(1f, 0.52f, 0.12f, 1f);
        [SerializeField] private Color feintTelegraphColor = new Color(0.68f, 0.68f, 0.74f, 1f);
        [SerializeField] private Color guardBreakTelegraphColor = new Color(1f, 0.15f, 0.08f, 1f);

        [Header("Current Brain Request (Read Only)")]
        [SerializeField] private EnemyMeleeDecision lastRequestedDecision;
        [SerializeField] private EnemyMeleeUseRejection lastUseRejection;
        [SerializeField] private bool attackCommitted;
        [SerializeField] private EnemyGuardBreakState guardBreakState = EnemyGuardBreakState.Ready;
        [SerializeField] private EnemyMeleeDecision currentSequenceDecision;
        [SerializeField] private bool currentlyInterruptible = true;

        [Header("Charged Attack Evolution")]
        [SerializeField, Min(1f)] private float evolutionOneChargedDamageMultiplier = 1.15f;
        [SerializeField, Min(1f)] private float evolutionTwoChargedDamageMultiplier = 1.25f;
        [SerializeField, Range(0.65f, 1f)] private float evolutionOneWindupMultiplier = 0.9f;
        [SerializeField, Range(0.65f, 1f)] private float evolutionTwoWindupMultiplier = 0.82f;
        [SerializeField, Min(0.1f)] private float minimumEvolvedChargedWindup = 0.45f;
        [SerializeField] private bool evolutionTwoCreatesShockwave = true;
        [SerializeField, Min(0.1f)] private float shockwaveRadius = 2.25f;
        [SerializeField, Range(0f, 1f)] private float shockwaveDamageMultiplier = 0.65f;
        [SerializeField] private Color shockwaveColor = new Color(1f, 0.36f, 0.08f, 0.9f);

        private EnemyController movement;
        private EnemyDefenseController defense;
        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private SpriteRenderer[] renderers;
        private Color[] restingColors;
        private Coroutine attackRoutine;
        private float nextActionTime;
        private Vector3 restingScale;
        private int runtimeBasicDamage;
        private int runtimeChargedDamage;
        private int runtimeGuardBreakDamage;
        private int runtimeTrollBasicDamage;
        private int runtimeTrollChargedDamage;
        private float runtimeDamageScale = 1f;
        private float runtimeInheritanceAttackSpeedScale = 1f;
        private bool skeletonGuardBreakEnabled;
        private bool skeletonGeneralRank;
        private float skeletonGeneralBasicFollowUpChance;
        private EnemyEvolutionStage evolutionStage;
        private bool brainControlled;
        private bool trollHeavyModeRequested;
        private float nextBasicTime;
        private float nextChargedTime;
        private float nextGuardBreakTime;
        private float brainPostAttackFollowUpChance = 0.25f;
        private float brainGuardHeldFeintChanceBonus = 0.1f;
        private int brainMaximumChainDepth = 1;
        private bool attackStepFeinted;
        private float sequenceFinalCooldown;
        private Quaternion restingWeaponRotation;
        private Vector3 restingWeaponScale;
        private float currentSequenceRecovery;
        private float currentSequenceCooldown;
        private float reactiveFeintChanceBonus;
        private bool guardBreakCountered;
        private bool guardBreakStepSucceeded;
        private Transform[] mirroredPresentations;
        private Vector3[] authoredPresentationPositions;
        private Quaternion[] authoredPresentationRotations;
        private Vector3[] authoredPresentationScales;
        private float authoredPresentationFacing = 1f;
        private float combatFacingDirection = 1f;
        private bool animatePersistentWeaponOnBasic;

        public bool IsAttacking => attackRoutine != null;
        public bool IsBrutePreset => probabilityPreset == EnemyMeleePreset.Brute;
        public bool IsTrollPreset => probabilityPreset == EnemyMeleePreset.Troll;
        public float BasicAttackRange => ResolveAttackRange(EnemyMeleeDecision.Basic);
        public float BasicEngagementRange
        {
            get
            {
                Vector2 origin = ResolveCombatOriginPosition();
                float originForwardOffset =
                    (origin.x - transform.position.x) * combatFacingDirection;
                return ResolveAttackRange(EnemyMeleeDecision.Basic)
                    + Mathf.Max(0f, originForwardOffset);
            }
        }
        public float ChargedAttackRange => ResolveAttackRange(EnemyMeleeDecision.Charged);
        public float GuardBreakAttackRange => ResolveAttackRange(EnemyMeleeDecision.GuardBreak);
        public float MaximumCombatRange => MaximumAttackRange();
        public EnemyMeleeUseRejection LastUseRejection => lastUseRejection;
        public bool IsAttackCommitted => attackCommitted;
        public bool IsGuardBreakInProgress => guardBreakState == EnemyGuardBreakState.Windup
            || guardBreakState == EnemyGuardBreakState.CounterWindow
            || guardBreakState == EnemyGuardBreakState.Committed;
        public bool IsGuardBreakCounterWindowActive => guardBreakState
            == EnemyGuardBreakState.CounterWindow;
        public bool IsAvailableForMajorAbility => attackRoutine == null
            && Time.time >= nextActionTime
            && (stagger == null || stagger.CanAct)
            && (defense == null || defense.CanStartAttack);

        private void Awake()
        {
            RefreshRuntimeDependencies();
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = gameObject.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(EnemyArchetype.Melee);
            restingScale = transform.localScale;
            RefreshRuntimeDamage();
            if (weaponPresentation == null)
            {
                foreach (EnemyContactDamage contactDamage in
                    GetComponentsInChildren<EnemyContactDamage>(true))
                {
                    if (contactDamage.transform != transform
                        && contactDamage.GetComponent<SpriteRenderer>() != null)
                    {
                        weaponPresentation = contactDamage.transform;
                        break;
                    }
                }
            }

            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            restingColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                restingColors[index] = renderers[index].color;
            }

            if (weaponPresentation != null)
            {
                restingWeaponRotation = weaponPresentation.localRotation;
                restingWeaponScale = weaponPresentation.localScale;
            }

            CacheMirroredPresentations();
        }

        private void Start()
        {
            RefreshRuntimeDependencies();
        }

        private void RefreshRuntimeDependencies()
        {
            movement = GetComponent<EnemyController>();
            defense = GetComponent<EnemyDefenseController>();
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
        }

        private void Update()
        {
            currentlyInterruptible = CanBeInterruptedBy(StaggerStrength.Normal);
            EnsureTarget();
            if (brainControlled)
            {
                return;
            }

            if (target == null || attackRoutine != null || Time.time < nextActionTime)
            {
                return;
            }

            Vector2 toTarget = target.transform.position - transform.position;
            if (toTarget.sqrMagnitude > awarenessRange * awarenessRange)
            {
                return;
            }

            defense?.FaceDirection(toTarget.x);
            if ((stagger != null && !stagger.CanAct)
                || (defense != null && !defense.CanStartAttack)
                || Mathf.Abs(toTarget.x) > MaximumAttackRange())
            {
                return;
            }

            attackRoutine = StartCoroutine(PerformChosenAttack());
        }

        public bool TryUse(EnemyMeleeDecision decision, PlayerHealth requestedTarget)
        {
            return TryUse(decision, requestedTarget, out _);
        }

        public bool TryUse(
            EnemyMeleeDecision decision,
            PlayerHealth requestedTarget,
            out EnemyMeleeUseRejection rejection)
        {
            lastRequestedDecision = decision;
            rejection = GetUseRejection(decision, requestedTarget);
            if (rejection != EnemyMeleeUseRejection.None)
            {
                lastUseRejection = rejection;
                return false;
            }

            target = requestedTarget;
            SidewaysParryAttack guard = target.GetComponent<SidewaysParryAttack>();
            bool guardHeld = guard != null && guard.IsGuardHeld;
            if (!UseTrollHeavyOverrides && !PassesBrainAttemptChance(decision, guardHeld))
            {
                rejection = EnemyMeleeUseRejection.ChanceRejected;
                lastUseRejection = rejection;
                return false;
            }

            lastUseRejection = EnemyMeleeUseRejection.None;
            if (UseTrollHeavyAttacks)
            {
                attackRoutine = StartCoroutine(PerformTrollAttackSequence(decision));
            }
            else if (decision == EnemyMeleeDecision.GuardBreak)
            {
                attackRoutine = StartCoroutine(PerformStandaloneGuardBreak());
            }
            else
            {
                attackRoutine = StartCoroutine(PerformRequestedAttack(decision));
            }

            return true;
        }

        public bool CanUse(
            EnemyMeleeDecision decision,
            PlayerHealth requestedTarget,
            out EnemyMeleeUseRejection rejection)
        {
            rejection = GetUseRejection(decision, requestedTarget);
            return rejection == EnemyMeleeUseRejection.None;
        }

        public bool IsInRange(EnemyMeleeDecision decision, PlayerHealth requestedTarget)
        {
            if (requestedTarget == null || !requestedTarget.gameObject.activeInHierarchy)
            {
                return false;
            }

            target = requestedTarget;
            return IsTargetWithinRange(ResolveAttackRange(decision));
        }

        public void ConfigureBrainMixups(
            float postAttackFollowUpChance,
            int maximumChainDepth,
            float guardHeldFeintChanceBonus)
        {
            trollHeavyModeRequested = true;
            brainPostAttackFollowUpChance = Mathf.Clamp01(postAttackFollowUpChance);
            brainMaximumChainDepth = Mathf.Clamp(maximumChainDepth, 0, 1);
            brainGuardHeldFeintChanceBonus = Mathf.Clamp01(guardHeldFeintChanceBonus);
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        public void SetCombatFacing(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) <= 0.01f || IsAttacking)
            {
                return;
            }

            combatFacingDirection = Mathf.Sign(horizontalDirection);
            ApplyPresentationFacing();
        }

        public void SetDefensiveWeaponPose(float angle)
        {
            if (weaponPresentation == null || IsAttacking)
            {
                return;
            }

            ApplyPresentationFacing();
            weaponPresentation.localRotation = restingWeaponRotation
                * Quaternion.Euler(0f, 0f, angle * combatFacingDirection);
        }

        public void RestoreCombatPresentation()
        {
            if (!IsAttacking)
            {
                ApplyPresentationFacing();
            }
        }

        public bool CanBeInterruptedBy(StaggerStrength strength)
        {
            return !(UseTrollHeavyAttacks
                && currentSequenceDecision == EnemyMeleeDecision.Charged
                && attackCommitted);
        }

        public void SetReactiveFeintChanceBonus(float bonus)
        {
            reactiveFeintChanceBonus = Mathf.Clamp01(bonus);
        }

        private EnemyMeleeUseRejection GetUseRejection(
            EnemyMeleeDecision decision,
            PlayerHealth requestedTarget)
        {
            if (requestedTarget == null)
            {
                return EnemyMeleeUseRejection.MissingTarget;
            }

            if (!requestedTarget.gameObject.activeInHierarchy)
            {
                return EnemyMeleeUseRejection.TargetInactive;
            }

            if (attackRoutine != null)
            {
                return EnemyMeleeUseRejection.Busy;
            }

            if (Time.time < nextActionTime || Time.time < GetAbilityReadyTime(decision))
            {
                return EnemyMeleeUseRejection.Cooldown;
            }

            if (stagger != null && !stagger.CanAct)
            {
                return EnemyMeleeUseRejection.Staggered;
            }

            if (defense != null && !defense.CanStartAttack)
            {
                return EnemyMeleeUseRejection.Defending;
            }

            target = requestedTarget;
            if (!IsTargetWithinRange(ResolveAttackRange(decision)))
            {
                return EnemyMeleeUseRejection.OutOfRange;
            }

            if (decision == EnemyMeleeDecision.Feint)
            {
                SidewaysParryAttack guard = target.GetComponent<SidewaysParryAttack>();
                if (guard == null || !guard.IsGuardHeld)
                {
                    return EnemyMeleeUseRejection.GuardRequired;
                }
            }

            return EnemyMeleeUseRejection.None;
        }

        private bool PassesBrainAttemptChance(EnemyMeleeDecision decision, bool guardHeld)
        {
            if (probabilityPreset == EnemyMeleePreset.Skeleton
                && decision == EnemyMeleeDecision.GuardBreak)
            {
                return skeletonGuardBreakEnabled;
            }

            float chance;
            switch (decision)
            {
                case EnemyMeleeDecision.Charged:
                    PlayerAttackState playerAttack = target.GetComponent<PlayerAttackState>();
                    chance = playerAttack != null && playerAttack.IsActivelyAttacking
                        ? chargedAttackChance
                        : chargedAttackChance * 0.35f;
                    break;
                case EnemyMeleeDecision.Feint:
                    chance = guardHeld ? ResolveFeintChance() : 0f;
                    break;
                case EnemyMeleeDecision.GuardBreak:
                    chance = guardHeld
                        ? ResolveGuardBreakChance()
                        : ResolveGuardBreakChance() * 0.35f;
                    break;
                default:
                    return true;
            }

            return chance > 0f && Random.value < Mathf.Clamp01(chance);
        }

        private IEnumerator PerformTrollAttackSequence(EnemyMeleeDecision openingDecision)
        {
            sequenceFinalCooldown = 0f;
            attackCommitted = false;
            guardBreakStepSucceeded = false;
            yield return PerformTrollAttackStep(openingDecision, true);

            if (attackStepFeinted)
            {
                if (brainMaximumChainDepth > 0
                    && TryChooseHeavyGuardBreakSoftFeint(openingDecision, out EnemyMeleeDecision softFeint))
                {
                    yield return PerformTrollAttackStep(softFeint, false);
                }
                else if (brainMaximumChainDepth > 0
                    && TryChooseTrollFollowUp(openingDecision, true, out EnemyMeleeDecision followUp))
                {
                    yield return PerformTrollAttackStep(followUp, false);
                }
                else
                {
                    AttackProfile opening = ResolveAttackProfile(openingDecision);
                    yield return new WaitForSeconds(opening.Recovery);
                    sequenceFinalCooldown = opening.Cooldown;
                }
            }
            else if (brainMaximumChainDepth > 0
                && (openingDecision == EnemyMeleeDecision.Basic
                    || openingDecision == EnemyMeleeDecision.Charged
                    || (openingDecision == EnemyMeleeDecision.GuardBreak
                        && guardBreakStepSucceeded))
                && Random.value < (openingDecision == EnemyMeleeDecision.GuardBreak
                    ? guardBreakFollowUpChance
                    : brainPostAttackFollowUpChance)
                && TryChooseTrollFollowUp(
                    openingDecision,
                    false,
                    out EnemyMeleeDecision postAttackFollowUp))
            {
                yield return PerformTrollAttackStep(postAttackFollowUp, false);
            }

            RestoreFeedback();
            RestoreWeaponPose();
            attackCommitted = false;
            nextActionTime = Time.time + Mathf.Max(0f, sequenceFinalCooldown);
            attackRoutine = null;
        }

        private IEnumerator PerformTrollAttackStep(
            EnemyMeleeDecision decision,
            bool allowFeint)
        {
            AttackProfile attack = ResolveAttackProfile(decision);
            currentSequenceDecision = decision;
            currentSequenceRecovery = attack.Recovery;
            currentSequenceCooldown = attack.Cooldown;
            attackStepFeinted = false;
            if (decision == EnemyMeleeDecision.GuardBreak)
            {
                yield return PerformGuardBreakStep(attack);
                yield break;
            }

            bool isFeintable = allowFeint
                && (decision == EnemyMeleeDecision.Basic
                    || decision == EnemyMeleeDecision.Charged);
            float commitDelay = isFeintable
                ? attack.Windup * attack.CommitPoint
                : attack.Windup;

            movement?.SuspendMovement(attack.Windup + attack.ActiveDuration);
            defense?.SuspendForMajorAbility(
                attack.Windup + attack.ActiveDuration + attack.Recovery);
            ShowTelegraph(attack.TelegraphColor, attack.TelegraphScale);
            SetWeaponPose(
                attack.DrawbackAngle,
                attack.WeaponScale,
                attack.WeaponVerticalOffset);
            float facing = ResolveTargetFacing();
            CombatShapeEffect.Create(
                transform.position,
                decision == EnemyMeleeDecision.Charged
                    ? CombatShape.Wedge
                    : CombatShape.Arc,
                attack.Range,
                attack.TelegraphColor,
                Mathf.Max(0.15f, attack.Windup),
                facing < 0f ? 180f : 0f);

            yield return new WaitForSeconds(commitDelay);
            // The choice remains open until the configured commit point. This lets
            // the Troll respond to a newly raised guard without reading future input.
            bool shouldFeint = isFeintable
                && Random.value < ResolveTrollFeintChance(decision);
            if (shouldFeint)
            {
                attackStepFeinted = true;
                attackCommitted = false;
                ShowTelegraph(feintTelegraphColor, 0.96f);
                RestoreWeaponPose();
                CaveSfx.Play(CaveSfxCue.Whoosh, 0.35f);
                CombatShapeEffect.Create(
                    transform.position,
                    CombatShape.Chevron,
                    Mathf.Max(0.5f, attack.Range * 0.55f),
                    feintTelegraphColor,
                    Mathf.Max(0.12f, feintDelay),
                    facing < 0f ? 180f : 0f);
                yield return new WaitForSeconds(feintDelay);
                RestoreFeedback();
                yield break;
            }

            float remainingWindup = Mathf.Max(0f, attack.Windup - commitDelay);
            if (remainingWindup > 0f)
            {
                attackCommitted = true;
                yield return new WaitForSeconds(remainingWindup);
            }
            else
            {
                attackCommitted = true;
            }

            SetWeaponPose(
                attack.StrikeAngle,
                attack.WeaponScale,
                attack.WeaponVerticalOffset);
            ResolveTrollAttackHit(attack);
            yield return new WaitForSeconds(attack.ActiveDuration);
            RestoreFeedback();
            RestoreWeaponPose();
            yield return new WaitForSeconds(attack.Recovery);

            SetAbilityReadyTime(decision, Time.time + attack.Cooldown);
            sequenceFinalCooldown = attack.Cooldown;
            attackCommitted = false;
        }

        private void ResolveTrollAttackHit(AttackProfile attack)
        {
            if (target == null || !IsTargetWithinRange(attack.Range))
            {
                return;
            }

            int resolvedDamage = ResolveModifiedDamage(attack.Damage);
            int healthBeforeHit = target.CurrentHealth;
            target.TryTakeDamage(
                resolvedDamage,
                new DamageContext(gameObject, DamageTrait.Melee | attack.ExtraTraits));
            CaveSfx.Play(CaveSfxCue.Whoosh, attack.IsCharged ? 0.9f : 0.7f);
            if (target.CurrentHealth >= healthBeforeHit || attack.Knockback <= 0f)
            {
                return;
            }

            Vector2 direction = target.transform.position - transform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction = new Vector2(Mathf.Sign(direction.x), 0.3f).normalized;
            PlayerController playerController = target.GetComponent<PlayerController>();
            playerController?.ApplyExternalKnockback(
                direction * attack.Knockback,
                trollKnockbackControlLock);
        }

        private IEnumerator PerformStandaloneGuardBreak()
        {
            sequenceFinalCooldown = 0f;
            attackCommitted = false;
            yield return PerformGuardBreakStep(ResolveAttackProfile(EnemyMeleeDecision.GuardBreak));
            RestoreFeedback();
            RestoreWeaponPose();
            attackCommitted = false;
            nextActionTime = Time.time + Mathf.Max(0f, sequenceFinalCooldown);
            attackRoutine = null;
        }

        private IEnumerator PerformGuardBreakStep(AttackProfile attack)
        {
            guardBreakCountered = false;
            guardBreakStepSucceeded = false;
            guardBreakState = EnemyGuardBreakState.Windup;
            attackCommitted = false;
            movement?.SuspendMovement(attack.Windup + attack.ActiveDuration + attack.Recovery);
            defense?.SuspendForMajorAbility(
                attack.Windup + attack.ActiveDuration + attack.Recovery);
            ShowTelegraph(guardBreakTelegraphColor, attack.TelegraphScale);
            SetWeaponPose(
                attack.DrawbackAngle,
                attack.WeaponScale,
                attack.WeaponVerticalOffset);
            PlayerGuardBreak playerGuardBreak = target != null
                ? target.GetComponent<PlayerGuardBreak>()
                : null;
            playerGuardBreak?.ObserveIncomingGuardBreak(this);

            float facing = ResolveTargetFacing();
            Vector2 cuePosition = (Vector2)transform.position
                + Vector2.right * facing * attack.Range * 0.42f;
            CombatShapeEffect.Create(
                cuePosition,
                CombatShape.Arrow,
                attack.Range * 0.62f,
                guardBreakTelegraphColor,
                Mathf.Max(0.15f, attack.Windup),
                facing < 0f ? 180f : 0f);

            float openDuration = Mathf.Min(
                Mathf.Max(0.05f, guardBreakCounterWindow),
                Mathf.Max(0.05f, attack.Windup));
            float preCounterWindup = Mathf.Max(0f, attack.Windup - openDuration);
            if (preCounterWindup > 0f)
            {
                yield return new WaitForSeconds(preCounterWindup);
            }

            guardBreakState = EnemyGuardBreakState.CounterWindow;
            playerGuardBreak?.OpenCounterWindow(this, openDuration);
            float counterClosesAt = Time.time + openDuration;
            while (Time.time < counterClosesAt && !guardBreakCountered)
            {
                yield return null;
            }

            if (guardBreakCountered)
            {
                guardBreakState = EnemyGuardBreakState.Countered;
                RestoreFeedback();
                RestoreWeaponPose();
                yield return new WaitForSeconds(counteredRecovery);
                SetAbilityReadyTime(
                    EnemyMeleeDecision.GuardBreak,
                    Time.time + attack.Cooldown);
                sequenceFinalCooldown = attack.Cooldown;
                guardBreakState = EnemyGuardBreakState.Ready;
                yield break;
            }

            guardBreakState = EnemyGuardBreakState.Committed;
            attackCommitted = true;
            SetWeaponPose(
                attack.StrikeAngle,
                attack.WeaponScale,
                attack.WeaponVerticalOffset);
            guardBreakStepSucceeded = ResolveGuardBreakHit(attack);
            yield return new WaitForSeconds(attack.ActiveDuration);
            RestoreFeedback();
            RestoreWeaponPose();
            guardBreakState = EnemyGuardBreakState.Recovering;
            yield return new WaitForSeconds(attack.Recovery);

            SetAbilityReadyTime(
                EnemyMeleeDecision.GuardBreak,
                Time.time + attack.Cooldown);
            sequenceFinalCooldown = attack.Cooldown;
            attackCommitted = false;
            guardBreakState = EnemyGuardBreakState.Ready;
        }

        private bool ResolveGuardBreakHit(AttackProfile attack)
        {
            if (target == null || !IsTargetWithinRange(attack.Range))
            {
                return false;
            }

            int resolvedDamage = ResolveModifiedDamage(attack.Damage);
            bool connected = target.TryTakeDamage(
                resolvedDamage,
                new DamageContext(gameObject, DamageTrait.Melee | DamageTrait.GuardBreak));
            if (!connected)
            {
                return false;
            }

            Vector2 away = target.transform.position - transform.position;
            target.GetComponent<PlayerGuardBreak>()?.ApplyEnemyGuardBreak(away);
            CombatShapeEffect.Create(
                target.transform.position,
                CombatShape.Slash,
                0.82f,
                guardBreakTelegraphColor,
                0.22f,
                -25f * ResolveTargetFacing());
            CaveSfx.Play(CaveSfxCue.Hit, 0.85f);
            return true;
        }

        public bool TryCounterGuardBreak(GameObject counteringPlayer)
        {
            if (!IsGuardBreakCounterWindowActive
                || guardBreakCountered
                || target == null
                || counteringPlayer != target.gameObject)
            {
                return false;
            }

            guardBreakCountered = true;
            attackCommitted = false;
            Vector2 away = transform.position - counteringPlayer.transform.position;
            KnockbackReceiver receiver = GetComponent<KnockbackReceiver>();
            receiver?.ApplyKnockback(
                new Vector2(Mathf.Sign(away.x), 0.18f).normalized * 3f);
            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Diamond,
                0.76f,
                new Color(0.3f, 0.95f, 1f, 1f),
                0.26f);
            return true;
        }

        private float ResolveTargetFacing()
        {
            return combatFacingDirection;
        }

        private bool TryChooseTrollFollowUp(
            EnemyMeleeDecision openingDecision,
            bool followsFeint,
            out EnemyMeleeDecision decision)
        {
            bool guardHeld = IsTargetGuarding();
            float basic = 0f;
            float charged = 0f;
            float guardBreak = 0f;

            if (followsFeint)
            {
                basic = CanUseSequenceFollowUp(EnemyMeleeDecision.Basic)
                    ? followUpBasicWeight
                    : 0f;
                charged = openingDecision == EnemyMeleeDecision.Basic
                    && CanUseSequenceFollowUp(EnemyMeleeDecision.Charged)
                        ? followUpChargedWeight
                        : 0f;
            }
            else if (openingDecision == EnemyMeleeDecision.Basic)
            {
                basic = CanUseSequenceFollowUp(
                    EnemyMeleeDecision.Basic,
                    true)
                        ? followUpBasicWeight
                        : 0f;
                charged = CanUseSequenceFollowUp(EnemyMeleeDecision.Charged)
                    ? followUpChargedWeight
                    : 0f;
            }
            else if (openingDecision == EnemyMeleeDecision.Charged)
            {
                basic = CanUseSequenceFollowUp(EnemyMeleeDecision.Basic)
                    ? followUpBasicWeight
                    : 0f;
            }
            else if (openingDecision == EnemyMeleeDecision.GuardBreak)
            {
                basic = CanUseSequenceFollowUp(EnemyMeleeDecision.Basic)
                    ? followUpBasicWeight
                    : 0f;
                charged = CanUseSequenceFollowUp(EnemyMeleeDecision.Charged)
                    ? followUpChargedWeight * 0.45f
                    : 0f;
            }

            guardBreak = openingDecision != EnemyMeleeDecision.GuardBreak
                && guardHeld
                && CanUseSequenceFollowUp(EnemyMeleeDecision.GuardBreak)
                ? followUpGuardBreakWeight
                : 0f;
            float total = basic + charged + guardBreak;
            if (total <= 0f)
            {
                decision = EnemyMeleeDecision.Basic;
                return false;
            }

            float roll = Random.value * total;
            if ((roll -= basic) < 0f)
            {
                decision = EnemyMeleeDecision.Basic;
            }
            else if ((roll -= charged) < 0f)
            {
                decision = EnemyMeleeDecision.Charged;
            }
            else
            {
                decision = EnemyMeleeDecision.GuardBreak;
            }

            return true;
        }

        private bool TryChooseHeavyGuardBreakSoftFeint(
            EnemyMeleeDecision openingDecision,
            out EnemyMeleeDecision decision)
        {
            decision = EnemyMeleeDecision.GuardBreak;
            return openingDecision == EnemyMeleeDecision.Charged
                && IsTargetGuarding()
                && Random.value < trollHeavyToGuardBreakSoftFeintChance
                && CanUseSequenceFollowUp(EnemyMeleeDecision.GuardBreak);
        }

        private bool CanUseSequenceFollowUp(
            EnemyMeleeDecision decision,
            bool ignoreAbilityCooldown = false)
        {
            return target != null
                && target.gameObject.activeInHierarchy
                && (stagger == null || stagger.CanAct)
                && (ignoreAbilityCooldown || Time.time >= GetAbilityReadyTime(decision))
                && IsTargetWithinRange(ResolveAttackRange(decision));
        }

        private float ResolveTrollFeintChance(EnemyMeleeDecision decision)
        {
            float chance = decision == EnemyMeleeDecision.Charged
                ? trollChargedFeintChance
                : trollBasicFeintChance;
            if (IsTargetGuarding())
            {
                chance += brainGuardHeldFeintChanceBonus;
            }

            PlayerAttackState playerAttack = target != null
                ? target.GetComponent<PlayerAttackState>()
                : null;
            if (playerAttack != null && playerAttack.IsActivelyAttacking)
            {
                chance += reactiveFeintChanceBonus;
            }

            return Mathf.Clamp01(chance);
        }

        private bool IsTargetGuarding()
        {
            if (target == null)
            {
                return false;
            }

            SidewaysParryAttack guard = target.GetComponent<SidewaysParryAttack>();
            return guard != null && guard.IsGuardHeld;
        }

        private IEnumerator PerformRequestedAttack(EnemyMeleeDecision decision)
        {
            switch (decision)
            {
                case EnemyMeleeDecision.Charged:
                    DamageTrait chargedTraits = chargedAttackIsPiercing
                        ? DamageTrait.Piercing
                        : DamageTrait.Direct;
                    yield return PerformAttack(
                        runtimeChargedDamage,
                        chargedRange,
                        ResolveChargedWindup(),
                        chargedActiveDuration,
                        chargedRecovery,
                        chargedCooldown,
                        chargedTraits,
                        chargedTelegraphColor,
                        1.15f,
                        true);
                    break;
                case EnemyMeleeDecision.Feint:
                    ShowTelegraph(feintTelegraphColor, 0.94f);
                    CaveSfx.Play(CaveSfxCue.Whoosh, 0.35f);
                    yield return new WaitForSeconds(Mathf.Max(0.01f, basicWindup * 0.65f));
                    RestoreFeedback();
                    yield return new WaitForSeconds(feintDelay);
                    yield return PerformAttack(
                        runtimeBasicDamage,
                        basicRange,
                        basicWindup,
                        basicActiveDuration,
                        basicRecovery,
                        basicCooldown,
                        DamageTrait.Direct,
                        basicTelegraphColor,
                        1.07f);
                    break;
                case EnemyMeleeDecision.GuardBreak:
                    yield return PerformStandaloneGuardBreak();
                    break;
                default:
                {
                    bool useGeneralFollowUp = skeletonGeneralRank
                        && skeletonGeneralBasicFollowUpChance > 0f
                        && Random.value < skeletonGeneralBasicFollowUpChance;
                    yield return PerformAttack(
                        runtimeBasicDamage,
                        basicRange,
                        basicWindup,
                        basicActiveDuration,
                        basicRecovery,
                        basicCooldown,
                        DamageTrait.Direct,
                        basicTelegraphColor,
                        1.07f);
                    if (useGeneralFollowUp
                        && target != null
                        && target.gameObject.activeInHierarchy
                        && target.CurrentHealth > 0
                        && IsTargetWithinRange(basicRange))
                    {
                        if (skeletonGuardBreakEnabled && IsTargetGuarding())
                        {
                            sequenceFinalCooldown = 0f;
                            yield return PerformGuardBreakStep(
                                ResolveAttackProfile(EnemyMeleeDecision.GuardBreak));
                            float attackSpeed = Mathf.Max(
                                0.01f,
                                runtimeInheritanceAttackSpeedScale);
                            nextActionTime = Mathf.Max(
                                nextActionTime,
                                Time.time + guardBreakCooldown / attackSpeed);
                        }
                        else
                        {
                            yield return PerformAttack(
                                runtimeBasicDamage,
                                basicRange,
                                basicWindup,
                                basicActiveDuration,
                                basicRecovery,
                                basicCooldown,
                                DamageTrait.Direct,
                                basicTelegraphColor,
                                1.07f);
                        }
                    }

                    break;
                }
            }

            attackRoutine = null;
        }

        private IEnumerator PerformChosenAttack()
        {
            SidewaysParryAttack playerGuard = target.GetComponent<SidewaysParryAttack>();
            bool guardHeld = playerGuard != null && playerGuard.IsGuardHeld;
            float resolvedGuardBreakChance = ResolveGuardBreakChance();
            if (guardHeld
                && resolvedGuardBreakChance > 0f
                && Random.value < resolvedGuardBreakChance)
            {
                yield return PerformStandaloneGuardBreak();
                yield break;
            }

            float resolvedFeintChance = ResolveFeintChance();
            if (guardHeld && resolvedFeintChance > 0f && Random.value < resolvedFeintChance)
            {
                ShowTelegraph(feintTelegraphColor, 0.94f);
                CaveSfx.Play(CaveSfxCue.Whoosh, 0.35f);
                yield return new WaitForSeconds(Mathf.Max(0.01f, basicWindup * 0.65f));
                RestoreFeedback();
                yield return new WaitForSeconds(feintDelay);
                yield return PerformAttack(
                    runtimeBasicDamage,
                    basicRange,
                    basicWindup,
                    basicActiveDuration,
                    basicRecovery,
                    basicCooldown,
                    DamageTrait.Direct,
                    basicTelegraphColor,
                    1.07f);
                attackRoutine = null;
                yield break;
            }

            PlayerAttackState playerAttackState = target.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttackState != null && playerAttackState.IsActivelyAttacking;
            float contextualChargedChance = playerCommitted
                ? chargedAttackChance
                : chargedAttackChance * 0.35f;
            if (!guardHeld
                && contextualChargedChance > 0f
                && Random.value < contextualChargedChance)
            {
                DamageTrait chargedTraits = chargedAttackIsPiercing
                    ? DamageTrait.Piercing
                    : DamageTrait.Direct;
                yield return PerformAttack(
                    runtimeChargedDamage,
                    chargedRange,
                    ResolveChargedWindup(),
                    chargedActiveDuration,
                    chargedRecovery,
                    chargedCooldown,
                    chargedTraits,
                    chargedTelegraphColor,
                    1.15f,
                    true);
                attackRoutine = null;
                yield break;
            }

            yield return PerformAttack(
                runtimeBasicDamage,
                basicRange,
                basicWindup,
                basicActiveDuration,
                basicRecovery,
                basicCooldown,
                DamageTrait.Direct,
                basicTelegraphColor,
                1.07f);
            attackRoutine = null;
        }

        private IEnumerator PerformAttack(
            int damage,
            float range,
            float windup,
            float activeDuration,
            float recovery,
            float cooldown,
            DamageTrait extraTraits,
            Color telegraphColor,
            float telegraphScale,
            bool isChargedAttack = false)
        {
            float attackSpeed = Mathf.Max(0.01f, runtimeInheritanceAttackSpeedScale);
            windup /= attackSpeed;
            activeDuration /= attackSpeed;
            recovery /= attackSpeed;
            cooldown /= attackSpeed;
            movement?.SuspendMovement(windup + activeDuration);
            ShowTelegraph(telegraphColor, telegraphScale);
            if (animatePersistentWeaponOnBasic && !isChargedAttack)
            {
                SetWeaponPose(basicDrawbackAngle, 1f);
            }

            bool createsShockwave = probabilityPreset != EnemyMeleePreset.Troll
                && isChargedAttack
                && ChargedEvolutionStage == EnemyEvolutionStage.EvolutionTwo
                && evolutionTwoCreatesShockwave;
            float telegraphRadius = createsShockwave ? Mathf.Max(range, shockwaveRadius) : range;
            Cave.Combat.AreaPulseEffect.Create(
                transform.position,
                telegraphRadius,
                createsShockwave ? shockwaveColor : telegraphColor,
                Mathf.Max(0.15f, windup));
            yield return new WaitForSeconds(windup);

            if (animatePersistentWeaponOnBasic && !isChargedAttack)
            {
                SetWeaponPose(basicStrikeAngle, 1f);
            }

            bool inMeleeRange = target != null && IsTargetWithinRange(range);
            bool inShockwaveRange = createsShockwave
                && target != null
                && IsTargetWithinRange(shockwaveRadius);
            if (target != null && (inMeleeRange || inShockwaveRange))
            {
                int attackDamage = inMeleeRange
                    ? damage
                    : Mathf.Max(1, Mathf.RoundToInt(damage * shockwaveDamageMultiplier));
                int resolvedDamage = ResolveModifiedDamage(attackDamage);
                DamageTrait traits = DamageTrait.Melee | extraTraits;
                if (!inMeleeRange)
                {
                    traits |= DamageTrait.AreaOfEffect;
                }
                target.TryTakeDamage(resolvedDamage, new DamageContext(gameObject, traits));
                CaveSfx.Play(CaveSfxCue.Whoosh, 0.65f);
                if (createsShockwave)
                {
                    Cave.Combat.AreaPulseEffect.Create(
                        transform.position,
                        shockwaveRadius,
                        shockwaveColor,
                        0.24f);
                }
            }

            yield return new WaitForSeconds(activeDuration);
            RestoreFeedback();
            if (animatePersistentWeaponOnBasic && !isChargedAttack)
            {
                RestoreWeaponPose();
            }

            yield return new WaitForSeconds(recovery);
            nextActionTime = Time.time + cooldown;
        }

        private int ResolveModifiedDamage(int baseDamage)
        {
            if (damageModifiers == null)
            {
                damageModifiers = GetComponent<EnemyDamageModifiers>();
            }

            return damageModifiers != null
                ? damageModifiers.ResolveDamage(baseDamage)
                : baseDamage;
        }

        private bool IsTargetWithinRange(float range)
        {
            Vector2 origin = ResolveCombatOriginPosition();
            Vector2 delta = (Vector2)target.transform.position - origin;
            float forwardDistance = delta.x * combatFacingDirection;
            return forwardDistance >= -0.05f
                && forwardDistance <= range
                && Mathf.Abs(delta.y) <= range * 1.15f;
        }

        private Vector2 ResolveCombatOriginPosition()
        {
            Vector2 root = transform.position;
            if (!UseTrollHeavyOverrides)
            {
                return root;
            }

            Transform originReference = attackOrigin != null
                ? attackOrigin
                : weaponPresentation;
            if (originReference == null)
            {
                return root;
            }

            Vector2 referenced = originReference.position;
            float referencedForwardOffset = (referenced.x - root.x) * combatFacingDirection;
            float forwardOffset = Mathf.Clamp(
                referencedForwardOffset,
                0f,
                maximumAttackOriginForwardOffset);
            return new Vector2(
                root.x + combatFacingDirection * forwardOffset,
                referenced.y);
        }

        private float ResolveAttackRange(EnemyMeleeDecision decision)
        {
            switch (decision)
            {
                case EnemyMeleeDecision.Charged:
                    return UseTrollHeavyOverrides ? trollChargedRange : chargedRange;
                case EnemyMeleeDecision.GuardBreak:
                    return UseTrollHeavyOverrides ? trollGuardBreakRange : guardBreakRange;
                default:
                    return UseTrollHeavyOverrides ? trollBasicRange : basicRange;
            }
        }

        private float GetAbilityReadyTime(EnemyMeleeDecision decision)
        {
            switch (decision)
            {
                case EnemyMeleeDecision.Charged:
                    return nextChargedTime;
                case EnemyMeleeDecision.GuardBreak:
                    return nextGuardBreakTime;
                default:
                    return nextBasicTime;
            }
        }

        private void SetAbilityReadyTime(EnemyMeleeDecision decision, float readyTime)
        {
            switch (decision)
            {
                case EnemyMeleeDecision.Charged:
                    nextChargedTime = Mathf.Max(nextChargedTime, readyTime);
                    break;
                case EnemyMeleeDecision.GuardBreak:
                    nextGuardBreakTime = Mathf.Max(nextGuardBreakTime, readyTime);
                    break;
                default:
                    nextBasicTime = Mathf.Max(nextBasicTime, readyTime);
                    break;
            }
        }

        private float MaximumAttackRange()
        {
            float evolvedChargedRange = ChargedEvolutionStage == EnemyEvolutionStage.EvolutionTwo
                && evolutionTwoCreatesShockwave
                && !UseTrollHeavyOverrides
                ? Mathf.Max(ResolveAttackRange(EnemyMeleeDecision.Charged), shockwaveRadius)
                : ResolveAttackRange(EnemyMeleeDecision.Charged);
            return Mathf.Max(
                ResolveAttackRange(EnemyMeleeDecision.Basic),
                evolvedChargedRange,
                ResolveAttackRange(EnemyMeleeDecision.GuardBreak));
        }

        private AttackProfile ResolveAttackProfile(EnemyMeleeDecision decision)
        {
            if (decision == EnemyMeleeDecision.Charged)
            {
                DamageTrait traits = chargedAttackIsPiercing
                    ? DamageTrait.Piercing
                    : DamageTrait.Direct;
                return new AttackProfile
                {
                    Damage = runtimeTrollChargedDamage,
                    Range = trollChargedRange,
                    Windup = ResolveChargedWindup(trollChargedWindup),
                    ActiveDuration = trollChargedActiveDuration,
                    Recovery = trollChargedRecovery,
                    Cooldown = trollChargedCooldown,
                    CommitPoint = trollChargedCommitPoint,
                    Knockback = trollChargedKnockback,
                    ExtraTraits = traits,
                    TelegraphColor = chargedTelegraphColor,
                    TelegraphScale = 1.18f,
                    DrawbackAngle = chargedDrawbackAngle,
                    StrikeAngle = chargedStrikeAngle,
                    WeaponScale = chargedWeaponScale,
                    WeaponVerticalOffset = trollChargedVerticalOffset,
                    IsCharged = true
                };
            }

            if (decision == EnemyMeleeDecision.GuardBreak)
            {
                return new AttackProfile
                {
                    Damage = runtimeGuardBreakDamage,
                    Range = UseTrollHeavyOverrides ? trollGuardBreakRange : guardBreakRange,
                    Windup = ScaleInheritedAttackTime(guardBreakWindup),
                    ActiveDuration = ScaleInheritedAttackTime(guardBreakActiveDuration),
                    Recovery = ScaleInheritedAttackTime(guardBreakRecovery),
                    Cooldown = ScaleInheritedAttackTime(guardBreakCooldown),
                    CommitPoint = 1f,
                    ExtraTraits = DamageTrait.GuardBreak,
                    TelegraphColor = guardBreakTelegraphColor,
                    TelegraphScale = 1.18f,
                    DrawbackAngle = chargedDrawbackAngle * 0.7f,
                    StrikeAngle = chargedStrikeAngle * 0.7f,
                    WeaponScale = 1f
                };
            }

            return new AttackProfile
            {
                Damage = runtimeTrollBasicDamage,
                Range = trollBasicRange,
                Windup = trollBasicWindup,
                ActiveDuration = trollBasicActiveDuration,
                Recovery = trollBasicRecovery,
                Cooldown = trollBasicCooldown,
                CommitPoint = trollBasicCommitPoint,
                Knockback = trollBasicKnockback,
                ExtraTraits = DamageTrait.Direct,
                TelegraphColor = basicTelegraphColor,
                TelegraphScale = 1.08f,
                DrawbackAngle = basicDrawbackAngle,
                StrikeAngle = basicStrikeAngle,
                WeaponScale = 1f
            };
        }

        private float ResolveFeintChance()
        {
            switch (probabilityPreset)
            {
                case EnemyMeleePreset.Skeleton:
                    return 0f;
                case EnemyMeleePreset.Brute:
                    return 0.15f;
                case EnemyMeleePreset.Troll:
                    return 0.2f;
                default:
                    return feintChance;
            }
        }

        private float ScaleInheritedAttackTime(float duration)
        {
            return duration / Mathf.Max(0.01f, runtimeInheritanceAttackSpeedScale);
        }

        private float ResolveGuardBreakChance()
        {
            switch (probabilityPreset)
            {
                case EnemyMeleePreset.Skeleton:
                    return 0f;
                case EnemyMeleePreset.Brute:
                    return 0.15f;
                case EnemyMeleePreset.Troll:
                    return 0.25f;
                default:
                    return guardBreakChance;
            }
        }

        private void EnsureTarget()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                target = FindObjectOfType<PlayerHealth>();
            }
        }

        public void SetRuntimeDamageScale(float multiplier)
        {
            runtimeDamageScale = Mathf.Max(0f, multiplier);
            RefreshRuntimeDamage();
        }

        public void SetRuntimeInheritanceAttackSpeedMultiplier(float multiplier)
        {
            runtimeInheritanceAttackSpeedScale = Mathf.Max(0.01f, multiplier);
        }

        public void ConfigureSkeletonGuardBreak(bool enabled)
        {
            RefreshRuntimeDependencies();
            probabilityPreset = EnemyMeleePreset.Skeleton;
            chargedAttackChance = 0f;
            feintChance = 0f;
            skeletonGuardBreakEnabled = enabled;
            animatePersistentWeaponOnBasic = weaponPresentation != null;
        }

        public void ConfigureSkeletonRank(SkeletonRank rank, float generalFollowUpChance)
        {
            skeletonGeneralRank = rank == SkeletonRank.General;
            skeletonGeneralBasicFollowUpChance = Mathf.Clamp01(generalFollowUpChance);
        }

        public void SuspendForMajorAbility(float duration)
        {
            nextActionTime = Mathf.Max(nextActionTime, Time.time + Mathf.Max(0f, duration));
            movement?.SuspendMovement(duration);
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
            RefreshRuntimeDamage();
        }

        private void RefreshRuntimeDamage()
        {
            runtimeBasicDamage = Mathf.Max(1, Mathf.RoundToInt(basicDamage * runtimeDamageScale));
            float chargedEvolutionMultiplier = ChargedEvolutionStage == EnemyEvolutionStage.EvolutionTwo
                ? evolutionTwoChargedDamageMultiplier
                : ChargedEvolutionStage == EnemyEvolutionStage.EvolutionOne
                    ? evolutionOneChargedDamageMultiplier
                    : 1f;
            runtimeChargedDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(chargedDamage * runtimeDamageScale * chargedEvolutionMultiplier));
            runtimeGuardBreakDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(guardBreakDamage * runtimeDamageScale));
            runtimeTrollBasicDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(trollBasicDamage * runtimeDamageScale));
            runtimeTrollChargedDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    trollChargedDamage
                    * runtimeDamageScale
                    * chargedEvolutionMultiplier));
        }

        private float ResolveChargedWindup()
        {
            return ResolveChargedWindup(chargedWindup);
        }

        private float ResolveChargedWindup(float configuredWindup)
        {
            float multiplier = ChargedEvolutionStage == EnemyEvolutionStage.EvolutionTwo
                ? evolutionTwoWindupMultiplier
                : ChargedEvolutionStage == EnemyEvolutionStage.EvolutionOne
                    ? evolutionOneWindupMultiplier
                    : 1f;
            return Mathf.Max(minimumEvolvedChargedWindup, configuredWindup * multiplier);
        }

        private EnemyEvolutionStage ChargedEvolutionStage => probabilityPreset == EnemyMeleePreset.Skeleton
            ? EnemyEvolutionStage.Base
            : evolutionStage;

        private bool UseTrollHeavyOverrides => useTrollHeavyOverrides
            && (IsTrollPreset || trollHeavyModeRequested);
        private bool UseTrollHeavyAttacks => brainControlled && UseTrollHeavyOverrides;

        private void ShowTelegraph(Color color, float scale)
        {
            transform.localScale = new Vector3(
                restingScale.x * scale,
                restingScale.y * scale,
                restingScale.z);
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = color;
                }
            }
        }

        private void RestoreFeedback()
        {
            transform.localScale = restingScale;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].color = restingColors[index];
                }
            }
        }

        private void SetWeaponPose(float angle, float scale, float verticalOffset = 0f)
        {
            if (weaponPresentation == null)
            {
                return;
            }

            ApplyPresentationFacing();
            ApplyPresentationVerticalOffset(verticalOffset);
            weaponPresentation.localRotation = restingWeaponRotation
                * Quaternion.Euler(0f, 0f, angle * combatFacingDirection);
            weaponPresentation.localScale = ResolveMirroredScale(restingWeaponScale)
                * Mathf.Max(0.1f, scale);
        }

        private void RestoreWeaponPose()
        {
            ApplyPresentationFacing();
        }

        private void CacheMirroredPresentations()
        {
            List<Transform> presentations = new List<Transform>();
            if (weaponPresentation != null)
            {
                presentations.Add(weaponPresentation);
                if (Mathf.Abs(weaponPresentation.localPosition.x) > 0.01f)
                {
                    authoredPresentationFacing = Mathf.Sign(
                        weaponPresentation.localPosition.x);
                }
            }

            foreach (ChargedAttackHitbox hitbox in
                GetComponentsInChildren<ChargedAttackHitbox>(true))
            {
                if (hitbox.transform != transform
                    && !presentations.Contains(hitbox.transform))
                {
                    presentations.Add(hitbox.transform);
                }
            }

            mirroredPresentations = presentations.ToArray();
            authoredPresentationPositions = new Vector3[presentations.Count];
            authoredPresentationRotations = new Quaternion[presentations.Count];
            authoredPresentationScales = new Vector3[presentations.Count];
            for (int index = 0; index < presentations.Count; index++)
            {
                authoredPresentationPositions[index] = presentations[index].localPosition;
                authoredPresentationRotations[index] = presentations[index].localRotation;
                authoredPresentationScales[index] = presentations[index].localScale;
            }

            combatFacingDirection = authoredPresentationFacing;
            ApplyPresentationFacing();
        }

        private void ApplyPresentationFacing()
        {
            if (mirroredPresentations == null)
            {
                return;
            }

            float mirror = combatFacingDirection * authoredPresentationFacing;
            for (int index = 0; index < mirroredPresentations.Length; index++)
            {
                Transform presentation = mirroredPresentations[index];
                if (presentation == null)
                {
                    continue;
                }

                Vector3 position = authoredPresentationPositions[index];
                position.x *= mirror;
                presentation.localPosition = position;
                presentation.localRotation = authoredPresentationRotations[index];
                Vector3 scale = authoredPresentationScales[index];
                scale.x *= mirror;
                presentation.localScale = scale;
            }
        }

        private void ApplyPresentationVerticalOffset(float verticalOffset)
        {
            if (Mathf.Approximately(verticalOffset, 0f)
                || mirroredPresentations == null)
            {
                return;
            }

            for (int index = 0; index < mirroredPresentations.Length; index++)
            {
                Transform presentation = mirroredPresentations[index];
                if (presentation == null)
                {
                    continue;
                }

                Vector3 position = presentation.localPosition;
                position.y += verticalOffset;
                presentation.localPosition = position;
            }
        }

        private Vector3 ResolveMirroredScale(Vector3 authoredScale)
        {
            authoredScale.x *= combatFacingDirection * authoredPresentationFacing;
            return authoredScale;
        }

        public void Interrupt()
        {
            bool wasCommitted = attackCommitted;
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            if (UseTrollHeavyAttacks && wasCommitted)
            {
                SetAbilityReadyTime(
                    currentSequenceDecision,
                    Time.time + currentSequenceCooldown);
            }

            attackCommitted = false;
            attackStepFeinted = false;
            guardBreakCountered = false;
            guardBreakStepSucceeded = false;
            guardBreakState = EnemyGuardBreakState.Ready;
            RestoreFeedback();
            RestoreWeaponPose();
            float recovery = UseTrollHeavyAttacks
                ? currentSequenceRecovery
                : basicRecovery;
            nextActionTime = Mathf.Max(nextActionTime, Time.time + recovery);
        }

        private void OnDisable()
        {
            Interrupt();
            nextActionTime = 0f;
            nextBasicTime = 0f;
            nextChargedTime = 0f;
            nextGuardBreakTime = 0f;
        }

        private struct AttackProfile
        {
            public int Damage;
            public float Range;
            public float Windup;
            public float ActiveDuration;
            public float Recovery;
            public float Cooldown;
            public float CommitPoint;
            public float Knockback;
            public DamageTrait ExtraTraits;
            public Color TelegraphColor;
            public float TelegraphScale;
            public float DrawbackAngle;
            public float StrikeAngle;
            public float WeaponScale;
            public float WeaponVerticalOffset;
            public bool IsCharged;
        }
    }
}
