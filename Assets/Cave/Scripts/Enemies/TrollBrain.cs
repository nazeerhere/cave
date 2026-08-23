using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class TrollBrain : MobBrainBase
    {
        [Header("Deliberate Frontline")]
        [SerializeField, Min(0f)] private float pursuitSpeed = 1.85f;

        [Header("Melee Weights")]
        [SerializeField, Min(0f)] private float basicWeight = 1f;
        [SerializeField, Min(0f)] private float chargedWeight = 0.85f;
        [SerializeField, Min(0f)] private float committedPlayerChargeBonus = 0.15f;
        [SerializeField, Range(0f, 1f)] private float guardedBasicWeightMultiplier = 0.8f;
        [SerializeField, Min(0f)] private float guardedChargedWeightBonus = 0.2f;
        [SerializeField, Min(0f)] private float guardedBreakWeight = 0.8f;

        [Header("Heavy Mixup Overrides")]
        [SerializeField] private bool useHeavyBrainOverrides = true;
        [SerializeField, Min(0f)] private float heavyPursuitSpeed = 1.85f;
        [SerializeField, Min(0f)] private float heavyBasicWeight = 1f;
        [SerializeField, Min(0f)] private float heavyChargedWeight = 0.85f;
        [SerializeField, Min(0f)] private float heavyPlayerCommitBonus = 0.15f;
        [SerializeField, Range(0f, 1f)] private float heavyGuardedBasicMultiplier = 0.8f;
        [SerializeField, Min(0f)] private float heavyGuardedChargedBonus = 0.2f;
        [SerializeField, Min(0f)] private float heavyGuardBreakWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float heavyJumpStompChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float heavyGuardedJumpStompChance = 0.5f;

        [Header("Bounded Mixups")]
        [SerializeField, Range(0f, 1f)] private float guardHeldFeintChanceBonus = 0.1f;
        [SerializeField, Range(0f, 1f)] private float postAttackFollowUpChance = 0.25f;
        [SerializeField, Range(0, 1)] private int maximumChainDepth = 1;

        [Header("Jump Stomp Selection")]
        [SerializeField, Range(0f, 1f)] private float jumpStompUseChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float guardedJumpStompUseChance = 0.5f;
        [SerializeField, Min(0.1f)] private float jumpStompDecisionCooldown = 0.9f;

        [Header("Visual Facing")]
        [SerializeField] private SpriteRenderer trollVisual;
        [SerializeField, Min(0f)] private float facingVelocityThreshold = 0.05f;
        [SerializeField] private bool sourceSpriteFacesRight = true;

        [Header("Current Offense Decision (Read Only)")]
        [SerializeField] private EnemyMeleeDecision chosenDecision;
        [SerializeField] private EnemyMeleeUseRejection rejectionReason;
        [SerializeField] private EnemyMeleeUseRejection basicAvailability;
        [SerializeField] private EnemyMeleeUseRejection chargedAvailability;
        [SerializeField] private EnemyMeleeUseRejection guardBreakAvailability;
        [SerializeField] private string fallbackDecision = "None";

        private EnemyMeleeCombat melee;
        private TrollJumpStomp jumpStomp;
        private Rigidbody2D body;
        private float nextJumpStompDecisionTime;
        private bool committedFacingLocked;

        protected override void ConfigureCapabilities()
        {
            melee = GetComponentInChildren<EnemyMeleeCombat>(true);
            jumpStomp = GetComponent<TrollJumpStomp>();
            body = GetComponent<Rigidbody2D>();
            if (trollVisual == null)
            {
                trollVisual = GetComponent<SpriteRenderer>();
            }

            ApplyMixupConfiguration();
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
            SidewaysParryAttack guard = player.GetComponent<SidewaysParryAttack>();
            bool guardHeld = guard != null && guard.IsGuardHeld;
            if (jumpStomp != null
                && Time.time >= nextJumpStompDecisionTime
                && jumpStomp.CanUse(player))
            {
                nextJumpStompDecisionTime = Time.time + jumpStompDecisionCooldown;
                float chance = guardHeld
                    ? GuardedJumpStompChance
                    : JumpStompChance;
                if (Random.value <= chance && jumpStomp.TryUse(player))
                {
                    fallbackDecision = "None";
                    HoldPosition(MobBrainState.Attack, "Jump Stomp");
                    return;
                }
            }

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
            EnemyMeleeUseRejection guardBreakRejection =
                EnemyMeleeUseRejection.GuardRequired;
            bool canGuardBreak = guardHeld && melee.CanUse(
                EnemyMeleeDecision.GuardBreak,
                player,
                out guardBreakRejection);
            basicAvailability = basicRejection;
            chargedAvailability = chargedRejection;
            guardBreakAvailability = guardBreakRejection;

            if (!canBasic && !canCharged && !canGuardBreak)
            {
                bool anyAttackInRange = melee.IsInRange(EnemyMeleeDecision.Basic, player)
                    || melee.IsInRange(EnemyMeleeDecision.Charged, player)
                    || (guardHeld
                        && melee.IsInRange(EnemyMeleeDecision.GuardBreak, player));
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
                    guardHeld ? guardBreakRejection : EnemyMeleeUseRejection.GuardRequired);
                fallbackDecision = "Wait";
                HoldPosition(MobBrainState.Recover, $"Melee unavailable: {rejectionReason}");
                return;
            }

            HoldPosition(MobBrainState.Attack, "Choose range-valid melee action");
            chosenDecision = ChooseAttack(player, guardHeld, canBasic, canCharged, canGuardBreak);
            fallbackDecision = "None";
            if (melee.TryUse(chosenDecision, player, out rejectionReason))
            {
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
                fallbackDecision = $"Basic after {chosenDecision} rejected: {rejectionReason}";
                SetState(MobBrainState.Attack, fallbackDecision);
                return;
            }

            fallbackDecision = $"None ({rejectionReason})";
            SetState(MobBrainState.Recover, $"Rejected {chosenDecision}: {rejectionReason}");
        }

        private EnemyMeleeDecision ChooseAttack(
            PlayerHealth player,
            bool guardHeld,
            bool canBasic,
            bool canCharged,
            bool canGuardBreak)
        {
            PlayerAttackState playerAttack = player.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttack != null && playerAttack.IsActivelyAttacking;
            float basic = canBasic
                ? BasicWeight * (guardHeld ? GuardedBasicMultiplier : 1f)
                : 0f;
            float charged = canCharged
                ? ChargedWeight
                    + (playerCommitted ? PlayerCommitBonus : 0f)
                    + (guardHeld ? GuardedChargedBonus : 0f)
                : 0f;
            float guardBreak = canGuardBreak ? GuardBreakWeight : 0f;
            float total = basic + charged + guardBreak;
            if (total <= 0f)
            {
                return canBasic
                    ? EnemyMeleeDecision.Basic
                    : canCharged
                        ? EnemyMeleeDecision.Charged
                        : EnemyMeleeDecision.GuardBreak;
            }

            float roll = Random.value * Mathf.Max(0.0001f, total);
            if ((roll -= basic) < 0f)
            {
                return EnemyMeleeDecision.Basic;
            }

            if ((roll -= charged) < 0f)
            {
                return EnemyMeleeDecision.Charged;
            }

            return EnemyMeleeDecision.GuardBreak;
        }

        private void ApplyMixupConfiguration()
        {
            melee?.ConfigureBrainMixups(
                postAttackFollowUpChance,
                maximumChainDepth,
                guardHeldFeintChanceBonus);
        }

        private void LateUpdate()
        {
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
            if (trollVisual == null || Mathf.Abs(horizontalDirection) < facingVelocityThreshold)
            {
                return;
            }

            bool faceRight = horizontalDirection > 0f;
            trollVisual.flipX = sourceSpriteFacesRight ? !faceRight : faceRight;
        }

        private float PursuitSpeed => useHeavyBrainOverrides
            ? heavyPursuitSpeed
            : pursuitSpeed;
        private float BasicWeight => useHeavyBrainOverrides ? heavyBasicWeight : basicWeight;
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
            if (Application.isPlaying)
            {
                ApplyMixupConfiguration();
            }
        }
    }
}
