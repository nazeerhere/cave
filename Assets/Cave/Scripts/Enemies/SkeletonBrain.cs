using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    [RequireComponent(typeof(EnemyMeleeCombat))]
    [RequireComponent(typeof(EnemyArchetypeProfile))]
    [RequireComponent(typeof(SkeletonInheritance))]
    public sealed class SkeletonBrain : MobBrainBase
    {
        [Header("Skeleton Melee")]
        [SerializeField, Min(0f)] private float pursuitSpeed = 3.25f;

        [Header("General Offensive Initiative")]
        [SerializeField, Min(1f)] private float generalPursuitSpeedMultiplier = 1.08f;
        [SerializeField, Range(0f, 1f)] private float generalBasicFollowUpChance = 0.48f;
        [SerializeField, Min(1f)] private float generalBlockDecisionMultiplier = 1.25f;
        [SerializeField, Min(1f)] private float generalGuardBreakDecisionMultiplier = 1.5f;
        [SerializeField, Range(0.25f, 1f)] private float generalDecisionCooldownMultiplier = 0.75f;

        [Header("Anchor General Formation")]
        [SerializeField, Min(0.1f)] private float anchorPreferredDistance = 2f;
        [SerializeField, Min(0.1f)] private float anchorHardLeash = 3.75f;
        [SerializeField, Min(0.1f)] private float anchorDefendedRadius = 4f;

        [Header("Assault General Formation")]
        [SerializeField, Min(0f)] private float assaultPreferredLead = 0.45f;
        [SerializeField, Min(0.1f)] private float assaultMaximumLead = 1.25f;
        [SerializeField, Range(0.1f, 1f)] private float assaultPacingSpeedMultiplier = 0.65f;

        [Header("Current Rank (Read Only)")]
        [SerializeField] private SkeletonRank currentRank = SkeletonRank.Lesser;
        [SerializeField] private SkeletonGeneralRole currentGeneralRole;
        [SerializeField, Min(0f)] private float currentAnchorDistanceToSummoner;
        [SerializeField, Min(0f)] private float currentAssaultDistanceToNearestLesser;
        [SerializeField] private float currentAssaultLead;

        [Header("Learned Block Decisions")]
        [SerializeField, Range(0f, 1f)] private float learnedBlockPostureChance = 0.12f;
        [SerializeField, Range(0f, 1f)] private float improvedBlockPostureChance = 0.22f;
        [SerializeField, Min(0.01f)] private float defensivePostureDuration = 0.18f;
        [SerializeField, Min(0f)] private float defensiveDecisionCooldown = 0.8f;

        [Header("Learned Guard Break Decisions")]
        [SerializeField, Range(0f, 1f)] private float learnedNeutralGuardBreakChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float improvedNeutralGuardBreakChance = 0.12f;
        [SerializeField, Range(0f, 1f)] private float learnedGuardedBreakChance = 0.22f;
        [SerializeField, Range(0f, 1f)] private float improvedGuardedBreakChance = 0.45f;
        [SerializeField, Min(0f)] private float guardBreakDecisionCooldown = 0.75f;

        private EnemyMeleeCombat meleeCombat;
        private EnemySwarm swarmIdentity;
        private SkeletonInheritance inheritance;
        private EnemyDefenseController learnedDefense;
        private float nextDefensiveDecisionTime;
        private float nextGuardBreakDecisionTime;

        protected override void ConfigureCapabilities()
        {
            meleeCombat = GetComponent<EnemyMeleeCombat>();
            swarmIdentity = GetComponent<EnemySwarm>();
            inheritance = GetComponent<SkeletonInheritance>();
            learnedDefense = GetComponent<EnemyDefenseController>();
            GetComponent<EnemyArchetypeProfile>()?.AddRuntimeArchetype(EnemyArchetype.Melee);
            meleeCombat?.ConfigureSkeletonGuardBreak(
                inheritance != null && inheritance.GuardBreakUnlocked);
            ApplyRankAndRole(
                inheritance != null ? inheritance.Rank : SkeletonRank.Lesser,
                inheritance != null ? inheritance.GeneralRole : SkeletonGeneralRole.None);
        }

        internal void ApplyRankAndRole(SkeletonRank rank, SkeletonGeneralRole role)
        {
            currentRank = rank;
            currentGeneralRole = currentRank == SkeletonRank.General
                ? role
                : SkeletonGeneralRole.None;
            if (meleeCombat == null)
            {
                meleeCombat = GetComponent<EnemyMeleeCombat>();
            }

            meleeCombat?.ConfigureSkeletonRank(
                currentRank,
                generalBasicFollowUpChance);
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            meleeCombat?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return (meleeCombat != null && meleeCombat.IsAttacking)
                || (learnedDefense != null
                    && (learnedDefense.CurrentState == EnemyDefenseState.Blocking
                        || learnedDefense.CurrentState == EnemyDefenseState.Recovering));
        }

        protected override string BusyDecisionLabel => "Basic melee recovery";

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            if (meleeCombat == null)
            {
                HoldPosition(MobBrainState.Recover, "Missing melee capability");
                return;
            }

            meleeCombat.SetCombatFacing(toPlayer.x);
            if (meleeCombat.IsInRange(EnemyMeleeDecision.Basic, player))
            {
                if (TryUseLearnedBlock(player))
                {
                    return;
                }

                if (TryUseLearnedGuardBreak(player))
                {
                    return;
                }

                if (meleeCombat.TryUse(EnemyMeleeDecision.Basic, player))
                {
                    HoldPosition(MobBrainState.Attack, "Basic melee attack");
                }
                else
                {
                    HoldPosition(MobBrainState.Recover, "Wait for basic attack");
                }

                return;
            }

            if (currentGeneralRole == SkeletonGeneralRole.Anchor
                && EvaluateAnchorMovement(toPlayer))
            {
                return;
            }

            if (currentGeneralRole == SkeletonGeneralRole.Assault
                && EvaluateAssaultMovement(toPlayer))
            {
                return;
            }

            float direction = Mathf.Sign(toPlayer.x);
            if (Mathf.Approximately(direction, 0f))
            {
                HoldPosition(MobBrainState.Chase, "Target vertically aligned");
                return;
            }

            Move(
                direction,
                ResolveBrainMoveSpeed(),
                MobBrainState.Chase,
                "Pursue player");
        }

        protected override bool TryOverrideIdleBehavior()
        {
            if (currentGeneralRole != SkeletonGeneralRole.Anchor)
            {
                return false;
            }

            Transform summoner = ResolveSummoner();
            if (summoner == null)
            {
                return false;
            }

            float delta = summoner.position.x - transform.position.x;
            currentAnchorDistanceToSummoner = Vector2.Distance(
                transform.position,
                summoner.position);
            if (Mathf.Abs(delta) > anchorPreferredDistance)
            {
                Move(
                    Mathf.Sign(delta),
                    ResolveBrainMoveSpeed(),
                    MobBrainState.ReturnToPatrol,
                    "Anchor return to Necromancer");
            }
            else
            {
                HoldPosition(MobBrainState.Patrol, "Anchor guard position");
            }

            return true;
        }

        private bool EvaluateAnchorMovement(Vector2 toPlayer)
        {
            Transform summoner = ResolveSummoner();
            if (summoner == null)
            {
                return false;
            }

            Vector2 toSummoner = summoner.position - transform.position;
            currentAnchorDistanceToSummoner = toSummoner.magnitude;
            float playerDistanceToSummoner = Target != null
                ? Vector2.Distance(Target.transform.position, summoner.position)
                : float.PositiveInfinity;
            if (currentAnchorDistanceToSummoner > anchorHardLeash)
            {
                Move(
                    Mathf.Sign(toSummoner.x),
                    ResolveBrainMoveSpeed(),
                    MobBrainState.ReturnToPatrol,
                    "Anchor hard-leash return");
                return true;
            }

            if (playerDistanceToSummoner > anchorDefendedRadius)
            {
                if (Mathf.Abs(toSummoner.x) > anchorPreferredDistance)
                {
                    Move(
                        Mathf.Sign(toSummoner.x),
                        ResolveBrainMoveSpeed(),
                        MobBrainState.ReturnToPatrol,
                        "Anchor disengage and return");
                }
                else
                {
                    HoldPosition(MobBrainState.Defend, "Anchor protects Necromancer");
                }

                return true;
            }

            float direction = Mathf.Sign(toPlayer.x);
            if (Mathf.Approximately(direction, 0f))
            {
                HoldPosition(MobBrainState.Chase, "Anchor target vertically aligned");
            }
            else
            {
                Move(
                    direction,
                    ResolveBrainMoveSpeed(),
                    MobBrainState.Chase,
                    "Anchor intercept player");
            }

            return true;
        }

        private bool EvaluateAssaultMovement(Vector2 toPlayer)
        {
            float direction = Mathf.Sign(toPlayer.x);
            if (Mathf.Approximately(direction, 0f))
            {
                HoldPosition(MobBrainState.Chase, "Assault target vertically aligned");
                return true;
            }

            EncounterGroup group = inheritance != null ? inheritance.EncounterGroup : null;
            if (group == null
                || !group.TryGetLesserFormationReference(
                    transform.position,
                    direction,
                    out float lesserFrontX,
                    out currentAssaultDistanceToNearestLesser))
            {
                currentAssaultLead = 0f;
                return false;
            }

            currentAssaultLead = direction * (transform.position.x - lesserFrontX);
            if (currentAssaultLead >= assaultMaximumLead)
            {
                HoldPosition(MobBrainState.Reposition, "Assault waits for Lesser front");
                return true;
            }

            float pace = currentAssaultLead > assaultPreferredLead
                ? assaultPacingSpeedMultiplier
                : 1f;
            Move(
                direction,
                ResolveBrainMoveSpeed() * pace,
                MobBrainState.Chase,
                pace < 1f ? "Assault paces with Lessers" : "Assault leads formation");
            return true;
        }

        private Transform ResolveSummoner()
        {
            return inheritance != null && inheritance.Summoner != null
                ? inheritance.Summoner.transform
                : null;
        }

        private bool TryUseLearnedBlock(PlayerHealth player)
        {
            if (inheritance == null
                || !inheritance.BlockUnlocked
                || learnedDefense == null
                || Time.time < nextDefensiveDecisionTime)
            {
                return false;
            }

            PlayerAttackState attackState = player.GetComponent<PlayerAttackState>();
            if (attackState == null || !attackState.IsActivelyAttacking)
            {
                return false;
            }

            float cooldownMultiplier = currentRank == SkeletonRank.General
                ? generalDecisionCooldownMultiplier
                : 1f;
            nextDefensiveDecisionTime = Time.time
                + defensiveDecisionCooldown * cooldownMultiplier;
            float chance = inheritance.ImprovedBlockUnlocked
                ? improvedBlockPostureChance
                : learnedBlockPostureChance;
            if (currentRank == SkeletonRank.General)
            {
                chance *= generalBlockDecisionMultiplier;
            }
            if (Random.value > chance
                || !learnedDefense.TryEnterDefensivePosture(defensivePostureDuration))
            {
                return false;
            }

            HoldPosition(MobBrainState.Defend, "Learned Block");
            return true;
        }

        private bool TryUseLearnedGuardBreak(PlayerHealth player)
        {
            if (inheritance == null
                || !inheritance.GuardBreakUnlocked
                || Time.time < nextGuardBreakDecisionTime
                || !meleeCombat.IsInRange(EnemyMeleeDecision.GuardBreak, player))
            {
                return false;
            }

            SidewaysParryAttack guard = player.GetComponent<SidewaysParryAttack>();
            bool guardHeld = guard != null && guard.IsGuardHeld;
            float chance;
            if (guardHeld)
            {
                chance = inheritance.ImprovedGuardBreakUnlocked
                    ? improvedGuardedBreakChance
                    : learnedGuardedBreakChance;
            }
            else
            {
                chance = inheritance.ImprovedGuardBreakUnlocked
                    ? improvedNeutralGuardBreakChance
                    : learnedNeutralGuardBreakChance;
            }

            if (currentRank == SkeletonRank.General)
            {
                chance *= generalGuardBreakDecisionMultiplier;
            }

            float cooldownMultiplier = currentRank == SkeletonRank.General
                ? generalDecisionCooldownMultiplier
                : 1f;
            nextGuardBreakDecisionTime = Time.time
                + guardBreakDecisionCooldown * cooldownMultiplier;
            if (Random.value > chance
                || !meleeCombat.TryUse(EnemyMeleeDecision.GuardBreak, player))
            {
                return false;
            }

            HoldPosition(
                MobBrainState.Attack,
                guardHeld ? "Learned Guard Break against Guard" : "Learned Guard Break");
            return true;
        }

        private float ResolveBrainMoveSpeed()
        {
            if (swarmIdentity == null
                || Movement == null
                || Movement.BaseMoveSpeed <= 0f
                || swarmIdentity.MoveSpeed <= 0f)
            {
                return pursuitSpeed * ResolveRankPursuitMultiplier();
            }

            // EnemySwarm supplies an archetype multiplier. Compensating here keeps
            // the Inspector's pursuit speed as the actual pre-difficulty speed.
            return pursuitSpeed
                * ResolveRankPursuitMultiplier()
                * Movement.BaseMoveSpeed
                / swarmIdentity.MoveSpeed;
        }

        private float ResolveRankPursuitMultiplier()
        {
            return currentRank == SkeletonRank.General
                ? generalPursuitSpeedMultiplier
                : 1f;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Transform summoner = Application.isPlaying ? ResolveSummoner() : null;
            if (summoner == null || currentGeneralRole != SkeletonGeneralRole.Anchor)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
            Gizmos.DrawWireSphere(summoner.position, anchorPreferredDistance);
            Gizmos.color = new Color(1f, 0.4f, 0.15f, 0.75f);
            Gizmos.DrawWireSphere(summoner.position, anchorHardLeash);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            anchorHardLeash = Mathf.Max(anchorPreferredDistance, anchorHardLeash);
            assaultMaximumLead = Mathf.Max(assaultPreferredLead + 0.1f, assaultMaximumLead);
        }
    }
}
