using System.Collections;
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
        IEnemySkillEvolutionReceiver
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
        [SerializeField, Min(0f)] private float trollChargedWindup = 1.1f;
        [SerializeField, Min(0.01f)] private float trollChargedActiveDuration = 0.16f;
        [SerializeField, Min(0f)] private float trollChargedRecovery = 0.85f;
        [SerializeField, Min(0f)] private float trollChargedCooldown = 1.45f;
        [SerializeField, Range(0f, 1f)] private float trollChargedFeintChance = 0.33f;
        [SerializeField, Range(0.1f, 0.9f)] private float trollChargedCommitPoint = 0.7f;
        [SerializeField, Min(0f)] private float trollChargedKnockback = 12f;
        [SerializeField, Min(0f)] private float trollKnockbackControlLock = 0.2f;

        [Header("Troll Feint Follow-ups")]
        [SerializeField, Min(0f)] private float followUpBasicWeight = 1f;
        [SerializeField, Min(0f)] private float followUpChargedWeight = 0.75f;
        [SerializeField, Min(0f)] private float followUpGuardBreakWeight = 0.9f;

        [Header("Optional Weapon Presentation")]
        [SerializeField] private Transform weaponPresentation;
        [SerializeField] private float basicDrawbackAngle = 32f;
        [SerializeField] private float basicStrikeAngle = -28f;
        [SerializeField] private float chargedDrawbackAngle = 62f;
        [SerializeField] private float chargedStrikeAngle = -48f;
        [SerializeField, Min(1f)] private float chargedWeaponScale = 1.15f;

        [Header("Telegraph Hooks")]
        [SerializeField] private Color basicTelegraphColor = new Color(0.35f, 0.72f, 1f, 1f);
        [SerializeField] private Color chargedTelegraphColor = new Color(1f, 0.52f, 0.12f, 1f);
        [SerializeField] private Color feintTelegraphColor = new Color(0.68f, 0.68f, 0.74f, 1f);
        [SerializeField] private Color guardBreakTelegraphColor = new Color(1f, 0.15f, 0.08f, 1f);

        [Header("Current Brain Request (Read Only)")]
        [SerializeField] private EnemyMeleeDecision lastRequestedDecision;
        [SerializeField] private EnemyMeleeUseRejection lastUseRejection;
        [SerializeField] private bool attackCommitted;

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
        private EnemyMeleeDecision currentSequenceDecision;
        private float currentSequenceRecovery;
        private float currentSequenceCooldown;

        public bool IsAttacking => attackRoutine != null;
        public bool IsBrutePreset => probabilityPreset == EnemyMeleePreset.Brute;
        public bool IsTrollPreset => probabilityPreset == EnemyMeleePreset.Troll;
        public float BasicAttackRange => ResolveAttackRange(EnemyMeleeDecision.Basic);
        public float ChargedAttackRange => ResolveAttackRange(EnemyMeleeDecision.Charged);
        public float GuardBreakAttackRange => ResolveAttackRange(EnemyMeleeDecision.GuardBreak);
        public float MaximumCombatRange => MaximumAttackRange();
        public EnemyMeleeUseRejection LastUseRejection => lastUseRejection;
        public bool IsAttackCommitted => attackCommitted;
        public bool IsAvailableForMajorAbility => attackRoutine == null
            && Time.time >= nextActionTime
            && (stagger == null || stagger.CanAct)
            && (defense == null || defense.CanStartAttack);

        private void Awake()
        {
            movement = GetComponent<EnemyController>();
            defense = GetComponent<EnemyDefenseController>();
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
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
        }

        private void Update()
        {
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
            if (UseTrollHeavyAttacks
                && (decision == EnemyMeleeDecision.Basic
                    || decision == EnemyMeleeDecision.Charged))
            {
                attackRoutine = StartCoroutine(PerformTrollAttackSequence(decision));
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

            if (decision == EnemyMeleeDecision.Feint
                || decision == EnemyMeleeDecision.GuardBreak)
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
                    chance = guardHeld ? ResolveGuardBreakChance() : 0f;
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
            yield return PerformTrollAttackStep(openingDecision, true);

            if (attackStepFeinted)
            {
                if (brainMaximumChainDepth > 0
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
                    || openingDecision == EnemyMeleeDecision.Charged)
                && Random.value < brainPostAttackFollowUpChance
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
            bool isFeintable = allowFeint
                && (decision == EnemyMeleeDecision.Basic
                    || decision == EnemyMeleeDecision.Charged);
            bool shouldFeint = isFeintable
                && Random.value < ResolveTrollFeintChance(decision);
            float commitDelay = isFeintable
                ? attack.Windup * attack.CommitPoint
                : attack.Windup;

            movement?.SuspendMovement(attack.Windup + attack.ActiveDuration);
            defense?.SuspendForMajorAbility(
                attack.Windup + attack.ActiveDuration + attack.Recovery);
            ShowTelegraph(attack.TelegraphColor, attack.TelegraphScale);
            SetWeaponPose(attack.DrawbackAngle, attack.WeaponScale);
            Cave.Combat.AreaPulseEffect.Create(
                transform.position,
                attack.Range,
                attack.TelegraphColor,
                Mathf.Max(0.15f, attack.Windup));

            yield return new WaitForSeconds(commitDelay);
            if (shouldFeint)
            {
                attackStepFeinted = true;
                attackCommitted = false;
                ShowTelegraph(feintTelegraphColor, 0.96f);
                RestoreWeaponPose();
                CaveSfx.Play(CaveSfxCue.Whoosh, 0.35f);
                Cave.Combat.AreaPulseEffect.Create(
                    transform.position,
                    Mathf.Max(0.5f, attack.Range * 0.55f),
                    feintTelegraphColor,
                    Mathf.Max(0.12f, feintDelay));
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

            SetWeaponPose(attack.StrikeAngle, attack.WeaponScale);
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

            int resolvedDamage = damageModifiers != null
                ? damageModifiers.ResolveDamage(attack.Damage)
                : attack.Damage;
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

            guardBreak = guardHeld && CanUseSequenceFollowUp(EnemyMeleeDecision.GuardBreak)
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

        private bool CanUseSequenceFollowUp(EnemyMeleeDecision decision)
        {
            return target != null
                && target.gameObject.activeInHierarchy
                && (stagger == null || stagger.CanAct)
                && Time.time >= GetAbilityReadyTime(decision)
                && IsTargetWithinRange(ResolveAttackRange(decision))
                && (decision != EnemyMeleeDecision.GuardBreak || IsTargetGuarding());
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
                    yield return PerformAttack(
                        runtimeGuardBreakDamage,
                        guardBreakRange,
                        guardBreakWindup,
                        guardBreakActiveDuration,
                        guardBreakRecovery,
                        guardBreakCooldown,
                        DamageTrait.GuardBreak,
                        guardBreakTelegraphColor,
                        1.18f);
                    break;
                default:
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
            }
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
                yield return PerformAttack(
                    runtimeGuardBreakDamage,
                    guardBreakRange,
                    guardBreakWindup,
                    guardBreakActiveDuration,
                    guardBreakRecovery,
                    guardBreakCooldown,
                    DamageTrait.GuardBreak,
                    guardBreakTelegraphColor,
                    1.18f);
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
            movement?.SuspendMovement(windup + activeDuration);
            ShowTelegraph(telegraphColor, telegraphScale);
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

            bool inMeleeRange = target != null && IsTargetWithinRange(range);
            bool inShockwaveRange = createsShockwave
                && target != null
                && IsTargetWithinRange(shockwaveRadius);
            if (target != null && (inMeleeRange || inShockwaveRange))
            {
                int attackDamage = inMeleeRange
                    ? damage
                    : Mathf.Max(1, Mathf.RoundToInt(damage * shockwaveDamageMultiplier));
                int resolvedDamage = damageModifiers != null
                    ? damageModifiers.ResolveDamage(attackDamage)
                    : attackDamage;
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
            yield return new WaitForSeconds(recovery);
            nextActionTime = Time.time + cooldown;
            attackRoutine = null;
        }

        private bool IsTargetWithinRange(float range)
        {
            Vector2 delta = target.transform.position - transform.position;
            return Mathf.Abs(delta.x) <= range && Mathf.Abs(delta.y) <= range * 1.15f;
        }

        private float ResolveAttackRange(EnemyMeleeDecision decision)
        {
            switch (decision)
            {
                case EnemyMeleeDecision.Charged:
                    return UseTrollHeavyOverrides ? trollChargedRange : chargedRange;
                case EnemyMeleeDecision.GuardBreak:
                    return guardBreakRange;
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
                guardBreakRange);
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
                    IsCharged = true
                };
            }

            if (decision == EnemyMeleeDecision.GuardBreak)
            {
                return new AttackProfile
                {
                    Damage = runtimeGuardBreakDamage,
                    Range = guardBreakRange,
                    Windup = guardBreakWindup,
                    ActiveDuration = guardBreakActiveDuration,
                    Recovery = guardBreakRecovery,
                    Cooldown = guardBreakCooldown,
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

        private void SetWeaponPose(float angle, float scale)
        {
            if (weaponPresentation == null)
            {
                return;
            }

            float facing = target != null && target.transform.position.x < transform.position.x
                ? -1f
                : 1f;
            weaponPresentation.localRotation = restingWeaponRotation
                * Quaternion.Euler(0f, 0f, angle * facing);
            weaponPresentation.localScale = restingWeaponScale * Mathf.Max(0.1f, scale);
        }

        private void RestoreWeaponPose()
        {
            if (weaponPresentation == null)
            {
                return;
            }

            weaponPresentation.localRotation = restingWeaponRotation;
            weaponPresentation.localScale = restingWeaponScale;
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
            public bool IsCharged;
        }
    }
}
