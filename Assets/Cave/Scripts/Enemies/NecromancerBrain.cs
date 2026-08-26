using Cave.Player;
using Cave.Progression;
using Cave.Projectiles;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemySupport))]
    public sealed class NecromancerBrain : MobBrainBase
    {
        [Header("Support Spacing")]
        [SerializeField, Min(0.1f)] private float urgentRetreatRange = 2f;
        [SerializeField, Min(0.1f)] private float minimumSafeSummonRange = 3.25f;
        [SerializeField, Min(0.1f)] private float preferredMinimumRange = 4.5f;
        [SerializeField, Min(0.1f)] private float preferredMaximumRange = 6f;
        [SerializeField, Min(0f)] private float rangeHysteresis = 0.5f;
        [SerializeField, Min(0f)] private float repositionSpeed = 2.2f;
        [SerializeField, Min(0f)] private float urgentRetreatSpeed = 3f;

        [Header("Skeleton Summoning")]
        [SerializeField] private StrategicCombatSettings sharedCombatSettings;
        [SerializeField] private GameObject skeletonPrefab;
        [SerializeField] private GameObject generalSkeletonPrefab;
        [SerializeField, Min(1)] private int desiredActiveSkeletons = 7;
        [SerializeField, Min(0)] private int desiredGeneralSkeletons = 2;
        [SerializeField, Min(0)] private int desiredLesserSkeletons = 5;
        [SerializeField, Min(1)] private int maximumActiveSkeletons = 7;
        [SerializeField, Min(0f)] private float summonWindup = 0.75f;
        [SerializeField, Min(0f)] private float initialDeploymentInterval = 0.35f;
        [SerializeField, Min(0f)] private float replacementSummonWindup = 0.5f;
        [SerializeField, Min(0.1f)] private float summonCooldown = 2f;
        [SerializeField, Min(0f)] private float summonRadius = 1.5f;
        [SerializeField, Min(0f)] private float minimumSummonSeparation = 0.75f;

        [Header("Late-Round Detective Specialists")]
        [SerializeField] private GameObject detectivePrefab;
        [SerializeField, Min(0)] private int detectiveUnlockWorldTier = 8;
        [SerializeField, Min(1)] private int maximumActiveDetectives = 3;
        [SerializeField, Min(1)] private int detectivesPerSpecialistSummon = 1;
        [SerializeField, Min(0f)] private float detectiveSummonWindup = 0.75f;
        [SerializeField, Min(0.1f)] private float detectiveSummonCooldown = 12f;
        [SerializeField, Min(0f)] private float detectiveSpawnRadius = 2.5f;

        [Header("Support Damage Buff")]
        [SerializeField, Range(0f, 2f)] private float supportDamageBonus = 0.25f;
        [SerializeField, Min(0.1f)] private float supportBuffRadius = 6f;
        [SerializeField, Min(0.1f)] private float supportBuffDuration = 5f;
        [SerializeField, Min(0f)] private float supportBuffCooldown = 7f;
        [SerializeField, Min(0f)] private float supportBuffWindup = 0.65f;
        [SerializeField] private LayerMask supportBuffAllyLayers = ~0;

        [Header("Owned Skeleton Regeneration Heal")]
        [SerializeField, Min(0.1f)] private float healMinimumPlayerDistance = 7f;
        [SerializeField, Min(0.1f)] private float healRadius = 6f;
        [SerializeField, Range(0.01f, 1f)] private float healTotalMaximumHealthFraction = 0.25f;
        [SerializeField, Min(0.1f)] private float healDuration = 3f;
        [SerializeField, Min(0.05f)] private float healTickInterval = 0.25f;
        [SerializeField, Min(0f)] private float healCooldown = 8f;
        [SerializeField, Min(0f)] private float healWindup = 0.65f;

        [Header("Slow Bolt")]
        [SerializeField] private Transform slowBoltFirePoint;
        [SerializeField] private EnemySlowProjectile slowBoltPrefab;
        [SerializeField, Min(1)] private int slowBoltDamage = 1;
        [SerializeField, Range(0.1f, 1f)] private float slowMovementMultiplier = 0.7f;
        [SerializeField, Min(0.1f)] private float slowDuration = 2f;
        [SerializeField, Min(0.01f)] private float slowBoltSpeed = 8f;
        [SerializeField, Min(0.1f)] private float slowBoltLifetime = 6f;
        [SerializeField, Min(0f)] private float slowBoltCooldown = 1.75f;
        [SerializeField, Min(0f)] private float slowBoltWindup = 0.35f;
        [SerializeField, Min(0.1f)] private float slowBoltRange = 8f;
        [SerializeField] private LayerMask lineOfSightBlockingLayers;

        [Header("Evolved Regenerating Melee Shield")]
        [SerializeField] private EnemyEvolutionStage shieldUnlockStage =
            EnemyEvolutionStage.EvolutionOne;
        [SerializeField, Min(1f)] private float maximumShieldHealth = 4f;
        [SerializeField, Min(0f)] private float shieldRegenerationDelay = 1.75f;
        [SerializeField, Min(0f)] private float shieldRegenerationFractionPerSecond = 0.25f;
        [SerializeField, Min(0f)] private float shieldBreakLockout = 5f;
        [SerializeField, Min(0f)] private float guardBreakShieldDamage = 2f;

        [Header("Current Slow Bolt Decision (Read Only)")]
        [SerializeField] private int currentActiveSummons;
        [SerializeField] private bool skeletonLineMaintained;
        [SerializeField] private bool slowBoltAttemptedLastDecision;
        [SerializeField] private EnemySlowUseRejection lastSlowBoltRejection;

        [Header("Current Formation (Read Only)")]
        [SerializeField, Min(0)] private int currentActiveGenerals;
        [SerializeField, Min(0)] private int currentActiveLessers;
        [SerializeField] private bool initialDeploymentActive;
        [SerializeField, Min(0f)] private float nextReplacementCooldown;
        [SerializeField] private SkeletonInheritance currentStrongestGeneral;
        [SerializeField, Min(0)] private int strongestGeneralWitnessedDeaths;
        [SerializeField] private GameObject currentSupportTarget;
        [SerializeField] private GameObject currentHealTarget;
        [SerializeField, Min(0)] private int currentActiveDetectives;

        private EnemySupport supportRole;
        private SwarmCaller summon;
        private NecromancerDetectiveSummoner detectiveSummoner;
        private EnemyDamageBuffAbility supportBuff;
        private EnemyHealAbility skeletonHeal;
        private EnemySlowShooter slowBolt;
        private NecromancerMeleeShield meleeShield;
        private WorldDifficultyManager difficultyManager;

        public bool SlowBoltAttemptedLastDecision => slowBoltAttemptedLastDecision;

        protected override void ConfigureCapabilities()
        {
            NormalizeTuning();
            supportRole = GetComponent<EnemySupport>();
            if (supportRole == null)
            {
                supportRole = gameObject.AddComponent<EnemySupport>();
            }

            supportRole.ApplyRoleIdentity();
            meleeShield = GetComponent<NecromancerMeleeShield>();
            if (meleeShield == null)
            {
                meleeShield = gameObject.AddComponent<NecromancerMeleeShield>();
            }

            meleeShield.Configure(
                shieldUnlockStage,
                maximumShieldHealth,
                shieldRegenerationDelay,
                shieldRegenerationFractionPerSecond,
                shieldBreakLockout,
                guardBreakShieldDamage);
            EnemySkillEvolution existingEvolution = GetComponent<EnemySkillEvolution>();
            if (existingEvolution != null)
            {
                meleeShield.ApplyEvolution(existingEvolution.CurrentStage);
            }

            if (sharedCombatSettings == null)
            {
                sharedCombatSettings = Resources.Load<StrategicCombatSettings>(
                    "StrategicCombatSettings");
            }

            if (difficultyManager == null)
            {
                difficultyManager = FindObjectOfType<WorldDifficultyManager>();
            }

            summon = GetComponentInChildren<SwarmCaller>(true);
            if (summon == null)
            {
                summon = gameObject.AddComponent<SwarmCaller>();
            }

            summon.ConfigureIfMissing(sharedCombatSettings, difficultyManager);
            GameObject resolvedSkeletonPrefab = skeletonPrefab != null
                ? skeletonPrefab
                : sharedCombatSettings != null
                    ? sharedCombatSettings.GroundSwarmPrefab
                    : null;
            summon.ConfigureNecromancerFormation(
                resolvedSkeletonPrefab,
                generalSkeletonPrefab,
                maximumActiveSkeletons,
                desiredGeneralSkeletons,
                desiredLesserSkeletons,
                summonWindup,
                initialDeploymentInterval,
                replacementSummonWindup,
                summonCooldown,
                summonRadius,
                minimumSummonSeparation);

            detectiveSummoner = GetComponent<NecromancerDetectiveSummoner>();
            if (detectiveSummoner == null)
            {
                detectiveSummoner = gameObject.AddComponent<NecromancerDetectiveSummoner>();
            }

            detectiveSummoner.Configure(
                detectivePrefab,
                difficultyManager,
                detectiveUnlockWorldTier,
                maximumActiveDetectives,
                detectivesPerSpecialistSummon,
                detectiveSummonWindup,
                detectiveSummonCooldown,
                detectiveSpawnRadius);

            supportBuff = GetComponentInChildren<EnemyDamageBuffAbility>(true);
            if (supportBuff == null)
            {
                supportBuff = gameObject.AddComponent<EnemyDamageBuffAbility>();
            }

            supportBuff.ConfigureNecromancerSupport(
                supportDamageBonus,
                supportBuffRadius,
                supportBuffDuration,
                supportBuffCooldown,
                supportBuffWindup,
                supportBuffAllyLayers,
                gameObject);

            skeletonHeal = GetComponentInChildren<EnemyHealAbility>(true);
            if (skeletonHeal == null)
            {
                skeletonHeal = gameObject.AddComponent<EnemyHealAbility>();
            }

            skeletonHeal.ConfigureNecromancerRegeneration(
                summon,
                healTotalMaximumHealthFraction,
                healDuration,
                healTickInterval,
                healCooldown,
                healWindup,
                healRadius);

            slowBolt = GetComponentInChildren<EnemySlowShooter>(true);
            if (slowBolt == null)
            {
                slowBolt = gameObject.AddComponent<EnemySlowShooter>();
            }

            LayerMask resolvedLineOfSightLayers = lineOfSightBlockingLayers.value != 0
                ? lineOfSightBlockingLayers
                : LayerMask.GetMask("Ground");
            slowBolt.Configure(
                slowBoltFirePoint,
                slowBoltPrefab,
                slowBoltDamage,
                slowMovementMultiplier,
                slowDuration,
                slowBoltSpeed,
                slowBoltLifetime,
                slowBoltCooldown,
                slowBoltWindup,
                slowBoltRange,
                resolvedLineOfSightLayers);
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            summon?.SetBrainControlled(controlled);
            skeletonHeal?.SetBrainControlled(controlled);
            supportBuff?.SetBrainControlled(controlled);
            slowBolt?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            if (summon != null
                && summon.IsInitialDeploymentActive
                && Target != null
                && Mathf.Abs(Target.transform.position.x - transform.position.x)
                    <= urgentRetreatRange)
            {
                summon.InterruptInitialDeployment();
            }

            return (summon != null && summon.IsSummoning)
                || (detectiveSummoner != null && detectiveSummoner.IsBusy)
                || (skeletonHeal != null && skeletonHeal.IsCasting)
                || (supportBuff != null && supportBuff.IsCasting)
                || (slowBolt != null && slowBolt.IsBusy);
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            currentActiveSummons = summon != null ? summon.ActiveSummonCount : 0;
            skeletonLineMaintained = summon == null
                || currentActiveSummons >= summon.DesiredFormationTotal;
            slowBoltAttemptedLastDecision = false;

            float distance = Mathf.Abs(toPlayer.x);
            float towardPlayer = Mathf.Sign(toPlayer.x);
            if (Mathf.Approximately(towardPlayer, 0f))
            {
                towardPlayer = 1f;
            }

            if (distance <= urgentRetreatRange)
            {
                Move(
                    -towardPlayer,
                    urgentRetreatSpeed,
                    MobBrainState.Reposition,
                    "Urgent retreat from melee pressure");
                return;
            }

            bool needsSkeletons = summon != null
                && summon.ActiveSummonCount < summon.DesiredFormationTotal;
            if (needsSkeletons
                && distance >= minimumSafeSummonRange
                && summon.TrySummon())
            {
                HoldPosition(
                    MobBrainState.Attack,
                    "Build Skeleton formation "
                        + summon.ActiveSummonCount
                        + "/"
                        + summon.DesiredFormationTotal);
                return;
            }

            if (skeletonLineMaintained
                && detectiveSummoner != null
                && detectiveSummoner.TrySummon())
            {
                HoldPosition(MobBrainState.Attack, "Summon late-round Detective specialist");
                return;
            }

            if (toPlayer.magnitude >= healMinimumPlayerDistance
                && skeletonHeal != null
                && skeletonHeal.TryUse())
            {
                HoldPosition(MobBrainState.Attack, "Regenerate injured owned Skeleton");
                return;
            }

            if (supportBuff != null && supportBuff.TryUse())
            {
                HoldPosition(MobBrainState.Attack, "Cast damage buff");
                return;
            }

            if (skeletonLineMaintained
                && slowBolt != null)
            {
                slowBoltAttemptedLastDecision = true;
                if (slowBolt.TryUse(player.transform))
                {
                    lastSlowBoltRejection = EnemySlowUseRejection.None;
                    HoldPosition(MobBrainState.Attack, "Cast Slow Bolt");
                    return;
                }

                lastSlowBoltRejection = slowBolt.LastUseRejection;
            }

            float retreatThreshold = preferredMinimumRange - rangeHysteresis;
            if (distance < retreatThreshold)
            {
                Move(
                    -towardPlayer,
                    repositionSpeed,
                    MobBrainState.Reposition,
                    needsSkeletons
                        ? "Regain safe summon spacing"
                        : "Recover support distance");
                return;
            }

            float approachThreshold = preferredMaximumRange + rangeHysteresis;
            if (distance > approachThreshold)
            {
                Move(
                    towardPlayer,
                    repositionSpeed,
                    MobBrainState.Reposition,
                    needsSkeletons
                        ? "Move into safe summon range"
                        : "Move into support range");
                return;
            }

            HoldPosition(
                MobBrainState.Reposition,
                needsSkeletons
                    ? "Wait for summon cooldown"
                    : "Maintain support spacing");
        }

        protected override string BusyDecisionLabel
        {
            get
            {
                if (summon != null && summon.IsSummoning)
                {
                    return "Committed to Skeleton summon";
                }

                if (supportBuff != null && supportBuff.IsCasting)
                {
                    return "Committed to damage buff";
                }

                if (detectiveSummoner != null && detectiveSummoner.IsBusy)
                {
                    return "Committed to Detective specialist summon";
                }

                if (skeletonHeal != null && skeletonHeal.IsCasting)
                {
                    return "Committed to Skeleton regeneration";
                }

                return slowBolt != null && slowBolt.IsBusy
                    ? "Committed to Slow Bolt"
                    : base.BusyDecisionLabel;
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            NormalizeTuning();
        }

        private void NormalizeTuning()
        {
            minimumSafeSummonRange = Mathf.Max(urgentRetreatRange, minimumSafeSummonRange);
            preferredMinimumRange = Mathf.Max(minimumSafeSummonRange, preferredMinimumRange);
            preferredMaximumRange = Mathf.Max(preferredMinimumRange, preferredMaximumRange);
            healMinimumPlayerDistance = Mathf.Max(
                urgentRetreatRange,
                healMinimumPlayerDistance);
            healTickInterval = Mathf.Clamp(healTickInterval, 0.05f, healDuration);
            desiredActiveSkeletons = Mathf.Max(1, desiredActiveSkeletons);
            maximumActiveSkeletons = desiredActiveSkeletons;
            desiredGeneralSkeletons = Mathf.Clamp(
                desiredGeneralSkeletons,
                0,
                desiredActiveSkeletons);
            desiredLesserSkeletons = desiredActiveSkeletons - desiredGeneralSkeletons;
            minimumSummonSeparation = Mathf.Clamp(
                minimumSummonSeparation,
                0f,
                summonRadius);
        }

        private void LateUpdate()
        {
            currentActiveSummons = summon != null ? summon.ActiveSummonCount : 0;
            currentActiveGenerals = summon != null ? summon.ActiveGeneralCount : 0;
            currentActiveLessers = summon != null ? summon.ActiveLesserCount : 0;
            initialDeploymentActive = summon != null && summon.IsInitialDeploymentActive;
            nextReplacementCooldown = summon != null
                ? summon.NextSummonCooldownRemaining
                : 0f;
            currentStrongestGeneral = summon != null ? summon.StrongestGeneral : null;
            strongestGeneralWitnessedDeaths = currentStrongestGeneral != null
                ? currentStrongestGeneral.WitnessedDeaths
                : 0;
            currentSupportTarget = supportBuff != null && supportBuff.CurrentTarget != null
                ? supportBuff.CurrentTarget.gameObject
                : null;
            currentHealTarget = skeletonHeal != null && skeletonHeal.CurrentTarget != null
                ? skeletonHeal.CurrentTarget.gameObject
                : null;
            currentActiveDetectives = detectiveSummoner != null
                ? detectiveSummoner.ActiveRealDetectives
                : 0;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, urgentRetreatRange);
            Gizmos.color = new Color(0.55f, 0.35f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, preferredMinimumRange);
            Gizmos.DrawWireSphere(transform.position, preferredMaximumRange);
        }
    }
}
