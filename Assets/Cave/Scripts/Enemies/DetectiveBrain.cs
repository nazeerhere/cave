using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController), typeof(Damageable))]
    public sealed class DetectiveBrain : MobBrainBase
    {
        [Header("Fallback Range Bands")]
        [SerializeField, Min(0f)] private float minimumRange = 4f;
        [SerializeField, Min(0f)] private float preferredRange = 6f;
        [SerializeField, Min(0f)] private float maximumRange = 9f;
        [SerializeField, Min(0f)] private float preferredRangeTolerance = 0.75f;
        [SerializeField, Min(0f)] private float repositionSpeed = 2.6f;

        [Header("Ranged Repositioning")]
        [SerializeField, Min(0f)] private float strafeDuration = 0.45f;
        [SerializeField, Min(0f)] private float repositionCooldown = 1.4f;
        [SerializeField] private LayerMask lineOfSightBlockingLayers;
        [SerializeField] private bool drawPreferredRange = true;

        [Header("Strategist Decisions")]
        [SerializeField, Min(0f)] private float aggressiveClosingSpeed = 2f;
        [SerializeField, Min(0.1f)] private float stationaryPositionTolerance = 0.3f;
        [SerializeField, Min(0f)] private float stationaryTimeForSuppression = 2.25f;
        [SerializeField, Min(1)] private int crowdedFightEnemyCount = 2;
        [SerializeField, Min(0.1f)] private float pressureCheckRadius = 3.25f;
        [SerializeField, Min(0.1f)] private float supportProtectionRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float contextRefreshInterval = 0.5f;

        [Header("Coordinated Intent Movement")]
        [SerializeField, Min(0f)] private float builderMoveSpeed = 2f;
        [SerializeField, Min(0.1f)] private float builderThreatAbortRange = 2.2f;
        [SerializeField, Min(0f)] private float researcherMoveSpeed = 2.25f;
        [SerializeField, Min(1f)] private float researcherSpeedMultiplier = 1.22f;
        [SerializeField, Min(0.1f)] private float researcherAbortRange = 2.1f;
        [SerializeField, Min(0f)] private float researcherRangeHysteresis = 0.35f;
        [SerializeField, Min(0f)] private float sacrificialApproachSpeed = 2.8f;
        [SerializeField, Min(0f)] private float baitCommitmentDuration = 2f;
        [SerializeField] private DetectiveIntent currentIntent = DetectiveIntent.Fighter;

        [Header("Last Detective Recovery")]
        [SerializeField, Min(0.1f)] private float lastDetectiveSafeDistance = 7f;
        [SerializeField, Min(0f)] private float lastDetectiveRetreatSpeed = 3.15f;
        [SerializeField, Min(0f)] private float lastDetectiveSafeTolerance = 0.55f;

        private EnemyPoisonShooter poisonShooter;
        private EnemyKeepDistance keepDistance;
        private DetectiveStunGrenadeAbility stunGrenade;
        private DetectiveSuppressionZoneAbility suppressionZone;
        private DetectiveProjectileRedirector projectileRedirector;
        private DetectiveIdentity identity;
        private DetectiveEncounterCoordinator encounterCoordinator;
        private DetectiveCloneAbility cloneAbility;
        private DetectiveSacrificialAttack sacrificialAttack;
        private EnemyAllyCollisionPhasing allyPhasing;
        private float strafeUntil;
        private float nextStrafeTime;
        private float strafeDirection;
        private Vector2 lastObservedPlayerPosition;
        private float playerStationarySince;
        private float nextContextRefreshTime;
        private int nearbyPressureCount;
        private bool supportThreatened;
        private bool playerCanRecoverResources;
        private bool hasObservedPlayerPosition;
        private bool researcherHoldingObservationBand;
        private bool lastDetectiveRecoverySatisfied;
        private int previousRealDetectiveCount;
        private int previousActiveCloneCount;

        private float MinimumRange => keepDistance != null ? keepDistance.MinimumRange : minimumRange;
        private float PreferredRange => keepDistance != null ? keepDistance.PreferredRange : preferredRange;
        private float MaximumRange => keepDistance != null ? keepDistance.MaximumRange : maximumRange;
        private float EffectiveResearcherMoveSpeed => researcherMoveSpeed
            * researcherSpeedMultiplier;

        protected override void ConfigureCapabilities()
        {
            poisonShooter = GetComponentInChildren<EnemyPoisonShooter>(true);
            if (poisonShooter == null)
            {
                poisonShooter = gameObject.AddComponent<EnemyPoisonShooter>();
            }

            keepDistance = GetComponent<EnemyKeepDistance>();
            if (keepDistance == null)
            {
                keepDistance = gameObject.AddComponent<EnemyKeepDistance>();
            }

            stunGrenade = GetComponent<DetectiveStunGrenadeAbility>();
            if (stunGrenade == null)
            {
                stunGrenade = gameObject.AddComponent<DetectiveStunGrenadeAbility>();
            }

            suppressionZone = GetComponent<DetectiveSuppressionZoneAbility>();
            if (suppressionZone == null)
            {
                suppressionZone = gameObject.AddComponent<DetectiveSuppressionZoneAbility>();
            }

            projectileRedirector = GetComponent<DetectiveProjectileRedirector>();
            if (projectileRedirector == null)
            {
                projectileRedirector = gameObject.AddComponent<DetectiveProjectileRedirector>();
            }

            identity = GetComponent<DetectiveIdentity>();
            if (identity == null)
            {
                identity = gameObject.AddComponent<DetectiveIdentity>();
            }

            encounterCoordinator = identity.Coordinator != null
                ? identity.Coordinator
                : DetectiveEncounterCoordinator.GetOrCreate();
            if (identity.Coordinator == null)
            {
                identity.ConfigureReal(null, encounterCoordinator);
            }

            cloneAbility = GetComponent<DetectiveCloneAbility>();
            if (cloneAbility == null)
            {
                cloneAbility = gameObject.AddComponent<DetectiveCloneAbility>();
            }

            sacrificialAttack = GetComponent<DetectiveSacrificialAttack>();
            if (sacrificialAttack == null)
            {
                sacrificialAttack = gameObject.AddComponent<DetectiveSacrificialAttack>();
            }

            allyPhasing = GetComponent<EnemyAllyCollisionPhasing>();
            if (allyPhasing == null)
            {
                allyPhasing = gameObject.AddComponent<EnemyAllyCollisionPhasing>();
            }

            EnemySkillEvolution evolution = GetComponent<EnemySkillEvolution>();
            if (evolution != null)
            {
                cloneAbility.ApplyEvolution(evolution.CurrentStage);
            }

            GetComponent<EnemyArchetypeProfile>()?.AddRuntimeArchetype(EnemyArchetype.Ranged);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            hasObservedPlayerPosition = false;
            playerStationarySince = Time.time;
            nextContextRefreshTime = 0f;
            nearbyPressureCount = 0;
            supportThreatened = false;
            playerCanRecoverResources = false;
            researcherHoldingObservationBand = false;
            previousRealDetectiveCount = -1;
            previousActiveCloneCount = 0;
            lastDetectiveRecoverySatisfied = false;
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            poisonShooter?.SetBrainControlled(controlled);
            keepDistance?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return (poisonShooter != null && poisonShooter.IsBusy)
                || (stunGrenade != null && stunGrenade.IsBusy)
                || (suppressionZone != null && suppressionZone.IsBusy)
                || (projectileRedirector != null && projectileRedirector.IsBusy)
                || (cloneAbility != null && cloneAbility.IsBusy)
                || (sacrificialAttack != null && sacrificialAttack.IsBusy);
        }

        protected override string BusyDecisionLabel => projectileRedirector != null
            && projectileRedirector.IsBusy
            ? "Redirect projectile"
            : sacrificialAttack != null && sacrificialAttack.IsBusy
            ? "Committed sacrificial detonation"
            : cloneAbility != null && cloneAbility.IsBusy
            ? "Create deceptive clone"
            : suppressionZone != null && suppressionZone.IsBusy
            ? "Cast suppression zone"
            : stunGrenade != null && stunGrenade.IsBusy
                ? "Throw stun grenade"
                : "Poison shot windup";

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            allyPhasing?.SetAllyPhasing(false);
            if (encounterCoordinator == null)
            {
                encounterCoordinator = identity != null
                    ? identity.Coordinator
                    : DetectiveEncounterCoordinator.GetOrCreate();
            }

            if (HandleLastDetectiveRecovery(player, toPlayer))
            {
                return;
            }

            currentIntent = encounterCoordinator != null && identity != null
                ? encounterCoordinator.GetIntent(identity)
                : DetectiveIntent.Fighter;
            if (currentIntent != DetectiveIntent.Researcher)
            {
                researcherHoldingObservationBand = false;
            }
            if (currentIntent == DetectiveIntent.Builder
                && HandleBuilderIntent(player, toPlayer))
            {
                return;
            }

            if (currentIntent == DetectiveIntent.Researcher
                && HandleResearcherIntent(player, toPlayer))
            {
                return;
            }

            if (currentIntent == DetectiveIntent.Sacrificial
                && HandleSacrificialIntent(player, toPlayer))
            {
                return;
            }

            if (currentIntent == DetectiveIntent.Bait
                && HandleBaitIntent(toPlayer))
            {
                return;
            }

            float distance = Mathf.Abs(toPlayer.x);
            float towardPlayer = Mathf.Sign(toPlayer.x);
            UpdateStrategicContext(player);
            if (projectileRedirector != null
                && projectileRedirector.TryUseBestThreat())
            {
                HoldPosition(MobBrainState.Defend, "Redirect tactical projectile threat");
                return;
            }

            if (distance < MinimumRange)
            {
                if (stunGrenade != null && stunGrenade.TryUse(player))
                {
                    HoldPosition(MobBrainState.Attack, "Stun grenade disengage");
                    return;
                }

                encounterCoordinator?.RequestTemporaryIntent(
                    identity,
                    DetectiveIntent.Bait,
                    baitCommitmentDuration);
                allyPhasing?.SetAllyPhasing(true);
                Move(-towardPlayer, repositionSpeed, MobBrainState.Reposition, "Retreat to minimum range");
                return;
            }

            if (distance > MaximumRange)
            {
                Move(towardPlayer, repositionSpeed, MobBrainState.Chase, "Approach firing range");
                return;
            }

            if (ShouldCreateSuppressionZone()
                && suppressionZone != null
                && suppressionZone.TryUse(player))
            {
                HoldPosition(MobBrainState.Attack, "Suppress favorable player position");
                return;
            }

            if (cloneAbility != null
                && cloneAbility.TryUse())
            {
                HoldPosition(MobBrainState.Attack, "Create deceptive Detective clone");
                return;
            }

            if (ShouldThrowCrowdGrenade(player, toPlayer)
                && stunGrenade != null
                && stunGrenade.TryUse(player))
            {
                HoldPosition(MobBrainState.Attack, "Disrupt crowded advance");
                return;
            }

            if (poisonShooter != null
                && HasClearLineOfFire(player)
                && poisonShooter.TryUse(player.transform))
            {
                HoldPosition(MobBrainState.Attack, "Fire poison projectile");
                return;
            }

            if (distance < PreferredRange - preferredRangeTolerance)
            {
                allyPhasing?.SetAllyPhasing(true);
                Move(-towardPlayer, repositionSpeed, MobBrainState.Reposition, "Open preferred range");
                return;
            }

            if (distance > PreferredRange + preferredRangeTolerance)
            {
                Move(towardPlayer, repositionSpeed, MobBrainState.Reposition, "Close preferred range");
                return;
            }

            if (Time.time >= nextStrafeTime)
            {
                float tacticalBias = encounterCoordinator != null
                    ? encounterCoordinator.TacticalHorizontalBias
                    : 0f;
                bool usesNetworkBias = currentIntent == DetectiveIntent.Cover
                    || currentIntent == DetectiveIntent.Interceptor;
                strafeDirection = usesNetworkBias && Mathf.Abs(tacticalBias) > 0.01f
                    ? Mathf.Sign(tacticalBias)
                    : Random.value < 0.5f ? -1f : 1f;
                strafeUntil = Time.time + strafeDuration;
                nextStrafeTime = strafeUntil + repositionCooldown;
            }

            if (Time.time < strafeUntil)
            {
                Move(strafeDirection, repositionSpeed, MobBrainState.Reposition, "Short ranged strafe");
            }
            else
            {
                HoldPosition(MobBrainState.Reposition, "Hold preferred range");
            }
        }

        private bool HandleLastDetectiveRecovery(PlayerHealth player, Vector2 toPlayer)
        {
            if (encounterCoordinator == null
                || identity == null
                || !identity.IsRealDetective
                || cloneAbility == null
                || !cloneAbility.IsUnlocked)
            {
                lastDetectiveRecoverySatisfied = false;
                previousRealDetectiveCount = encounterCoordinator != null
                    ? encounterCoordinator.RealDetectiveCount
                    : -1;
                previousActiveCloneCount = encounterCoordinator != null
                    ? encounterCoordinator.ActiveCloneCount
                    : 0;
                return false;
            }

            int realCount = encounterCoordinator.RealDetectiveCount;
            int cloneCount = encounterCoordinator.ActiveCloneCount;
            if (realCount != 1)
            {
                lastDetectiveRecoverySatisfied = false;
                previousRealDetectiveCount = realCount;
                previousActiveCloneCount = cloneCount;
                return false;
            }

            if (previousRealDetectiveCount != 1)
            {
                lastDetectiveRecoverySatisfied = cloneCount > 0;
            }
            else if (cloneCount < previousActiveCloneCount)
            {
                lastDetectiveRecoverySatisfied = false;
            }
            else if (!lastDetectiveRecoverySatisfied && cloneCount > previousActiveCloneCount)
            {
                lastDetectiveRecoverySatisfied = true;
            }

            previousRealDetectiveCount = realCount;
            previousActiveCloneCount = cloneCount;
            if (lastDetectiveRecoverySatisfied
                || cloneCount >= encounterCoordinator.MaximumActiveClones)
            {
                return false;
            }

            float distance = toPlayer.magnitude;
            float retreatDirection = Mathf.Abs(toPlayer.x) > 0.05f
                ? -Mathf.Sign(toPlayer.x)
                : GetInstanceID() % 2 == 0 ? -1f : 1f;
            if (distance < lastDetectiveSafeDistance - lastDetectiveSafeTolerance)
            {
                allyPhasing?.SetAllyPhasing(true);
                Move(
                    retreatDirection,
                    lastDetectiveRetreatSpeed,
                    MobBrainState.Reposition,
                    "Last Detective retreats to rebuild clone pressure");
                return true;
            }

            allyPhasing?.SetAllyPhasing(false);
            if (cloneAbility.TryUse())
            {
                HoldPosition(MobBrainState.Attack, "Last Detective creates recovery clone");
                return true;
            }

            if (poisonShooter != null
                && HasClearLineOfFire(player)
                && poisonShooter.TryUse(player.transform))
            {
                HoldPosition(MobBrainState.Attack, "Last Detective covers clone cooldown");
                return true;
            }

            if (distance < lastDetectiveSafeDistance + lastDetectiveSafeTolerance)
            {
                allyPhasing?.SetAllyPhasing(true);
                Move(
                    retreatDirection,
                    lastDetectiveRetreatSpeed * 0.8f,
                    MobBrainState.Reposition,
                    "Last Detective preserves safe clone distance");
            }
            else
            {
                HoldPosition(MobBrainState.Recover, "Last Detective waits for clone recovery");
            }

            return true;
        }

        private bool HandleBuilderIntent(PlayerHealth player, Vector2 toPlayer)
        {
            if (encounterCoordinator == null
                || identity == null
                || !encounterCoordinator.TryGetConstructionPosition(identity, out Vector2 site))
            {
                return false;
            }

            float playerDistance = toPlayer.magnitude;
            if (playerDistance <= builderThreatAbortRange)
            {
                encounterCoordinator.AbandonCurrentRole(
                    identity,
                    encounterCoordinator.FailedBuilderRecovery);
                allyPhasing?.SetAllyPhasing(true);
                Move(
                    -Mathf.Sign(toPlayer.x),
                    repositionSpeed,
                    MobBrainState.Reposition,
                    "Builder interrupted; evade during recovery");
                return true;
            }

            float sideOffset = identity.GetInstanceID() % 2 == 0 ? -0.42f : 0.42f;
            float targetX = site.x + sideOffset;
            float delta = targetX - transform.position.x;
            if (Mathf.Abs(delta) > 0.22f)
            {
                Move(
                    Mathf.Sign(delta),
                    builderMoveSpeed,
                    MobBrainState.Reposition,
                    "Move into tower construction formation");
                return true;
            }

            encounterCoordinator.ReportBuilderReady(identity);
            HoldPosition(MobBrainState.Attack, "Construct control tower");
            return true;
        }

        private bool HandleResearcherIntent(PlayerHealth player, Vector2 toPlayer)
        {
            if (encounterCoordinator == null || identity == null)
            {
                return false;
            }

            float distance = toPlayer.magnitude;
            if (distance <= researcherAbortRange)
            {
                encounterCoordinator.ReportResearchObservation(identity, false);
                encounterCoordinator.AbandonCurrentRole(identity, 0.75f);
                allyPhasing?.SetAllyPhasing(true);
                Move(
                    -Mathf.Sign(toPlayer.x),
                    EffectiveResearcherMoveSpeed,
                    MobBrainState.Reposition,
                    "Researcher compromised; abandon role and evade");
                return true;
            }

            float minimumObservation = encounterCoordinator.MinimumObservationRange
                - (researcherHoldingObservationBand ? researcherRangeHysteresis : 0f);
            float maximumObservation = encounterCoordinator.MaximumObservationRange
                + (researcherHoldingObservationBand ? researcherRangeHysteresis : 0f);
            if (distance < minimumObservation)
            {
                researcherHoldingObservationBand = false;
                encounterCoordinator.ReportResearchObservation(identity, false);
                allyPhasing?.SetAllyPhasing(true);
                Move(
                    -Mathf.Sign(toPlayer.x),
                    EffectiveResearcherMoveSpeed,
                    MobBrainState.Reposition,
                    "Researcher opens observation distance");
                return true;
            }

            if (distance > maximumObservation)
            {
                researcherHoldingObservationBand = false;
                encounterCoordinator.ReportResearchObservation(identity, false);
                Move(
                    Mathf.Sign(toPlayer.x),
                    EffectiveResearcherMoveSpeed,
                    MobBrainState.Reposition,
                    "Researcher approaches observation band");
                return true;
            }

            researcherHoldingObservationBand = true;
            encounterCoordinator.ReportResearchObservation(identity, true);
            HoldPosition(MobBrainState.Attack, "Observe player for tower research");
            return true;
        }

        private bool HandleBaitIntent(Vector2 toPlayer)
        {
            if (toPlayer.magnitude >= PreferredRange + preferredRangeTolerance)
            {
                encounterCoordinator?.AbandonCurrentRole(identity, 0.2f);
                return false;
            }

            allyPhasing?.SetAllyPhasing(true);
            Move(
                -Mathf.Sign(toPlayer.x),
                repositionSpeed,
                MobBrainState.Reposition,
                "Bait pursuit through allied formation");
            return true;
        }

        private bool HandleSacrificialIntent(PlayerHealth player, Vector2 toPlayer)
        {
            if (sacrificialAttack == null)
            {
                encounterCoordinator?.AbandonCurrentRole(identity, 0.5f);
                return false;
            }

            if (toPlayer.magnitude > sacrificialAttack.CommitmentRange)
            {
                Move(
                    Mathf.Sign(toPlayer.x),
                    sacrificialApproachSpeed,
                    MobBrainState.Chase,
                    "Close distance with concealed intent");
                return true;
            }

            if (sacrificialAttack.TryCommit(player))
            {
                HoldPosition(MobBrainState.Attack, "Irreversible sacrificial detonation");
                return true;
            }

            HoldPosition(MobBrainState.Recover, "Wait for sacrificial commitment");
            return true;
        }

        private void UpdateStrategicContext(PlayerHealth player)
        {
            Vector2 playerPosition = player.transform.position;
            SpinSwordAttack stamina = player.GetComponent<SpinSwordAttack>();
            PlayerMana mana = player.GetComponent<PlayerMana>();
            playerCanRecoverResources = player.CurrentHealth < player.MaxHealth
                || (stamina != null && stamina.CurrentStamina < stamina.MaximumStamina - 0.01f)
                || (mana != null && mana.CurrentMana < mana.MaximumMana - 0.01f);
            if (!hasObservedPlayerPosition)
            {
                hasObservedPlayerPosition = true;
                lastObservedPlayerPosition = playerPosition;
                playerStationarySince = Time.time;
            }
            else if (Vector2.Distance(playerPosition, lastObservedPlayerPosition)
                > stationaryPositionTolerance)
            {
                lastObservedPlayerPosition = playerPosition;
                playerStationarySince = Time.time;
            }

            if (Time.time < nextContextRefreshTime)
            {
                return;
            }

            nextContextRefreshTime = Time.time + contextRefreshInterval;
            nearbyPressureCount = 0;
            supportThreatened = false;
            foreach (Damageable enemy in FindObjectsOfType<Damageable>())
            {
                if (enemy == null
                    || enemy.CurrentHealth <= 0
                    || !enemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                DetectiveIdentity otherDetective = enemy.GetComponent<DetectiveIdentity>();
                if (otherDetective != null && otherDetective.IsClone)
                {
                    continue;
                }

                float distanceToPlayer = Vector2.Distance(
                    enemy.transform.position,
                    playerPosition);
                if (distanceToPlayer <= pressureCheckRadius)
                {
                    nearbyPressureCount++;
                }

                EnemyArchetypeProfile profile = enemy.GetComponent<EnemyArchetypeProfile>();
                if (profile != null
                    && profile.Includes(EnemyArchetype.Support)
                    && distanceToPlayer <= supportProtectionRadius)
                {
                    supportThreatened = true;
                }
            }
        }

        private bool ShouldCreateSuppressionZone()
        {
            if (suppressionZone == null || !suppressionZone.IsReady)
            {
                return false;
            }

            bool playerHoldingPosition = hasObservedPlayerPosition
                && Time.time - playerStationarySince >= stationaryTimeForSuppression
                && playerCanRecoverResources;
            return (playerHoldingPosition && nearbyPressureCount > 0)
                || nearbyPressureCount >= crowdedFightEnemyCount
                || supportThreatened;
        }

        private bool ShouldThrowCrowdGrenade(PlayerHealth player, Vector2 toPlayer)
        {
            if (stunGrenade == null || !stunGrenade.IsReady)
            {
                return false;
            }

            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Vector2 towardDetective = -toPlayer.normalized;
            bool closingAggressively = playerBody != null
                && Vector2.Dot(playerBody.velocity, towardDetective) >= aggressiveClosingSpeed;
            return closingAggressively
                || nearbyPressureCount > crowdedFightEnemyCount;
        }

        private bool HasClearLineOfFire(PlayerHealth player)
        {
            if (lineOfSightBlockingLayers.value == 0)
            {
                return true;
            }

            RaycastHit2D hit = Physics2D.Linecast(
                transform.position,
                player.transform.position,
                lineOfSightBlockingLayers);
            return hit.collider == null
                || hit.collider.GetComponentInParent<PlayerHealth>() == player;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!drawPreferredRange)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, PreferredRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, MinimumRange);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, MaximumRange);
        }

        protected override void OnDisable()
        {
            allyPhasing?.SetAllyPhasing(false);
            base.OnDisable();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            preferredRange = Mathf.Max(minimumRange, preferredRange);
            maximumRange = Mathf.Max(preferredRange, maximumRange);
        }
    }
}
