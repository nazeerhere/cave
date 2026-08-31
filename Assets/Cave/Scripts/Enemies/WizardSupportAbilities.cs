using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using Cave.Progression;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    public enum WizardCastAction
    {
        None,
        Heal,
        Dispel,
        Empower,
        SelfTeleport,
        AllyTeleport,
        ForcePlayerTeleport,
        SummonEyes
    }

    public enum WizardEmpowerGroup
    {
        None,
        Skeletons,
        Frontline
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class WizardSupportAbilities : MonoBehaviour,
        IEnemyInterruptible,
        IEnemySkillEvolutionReceiver
    {
        private static readonly Dictionary<Damageable, WizardSupportAbilities> HealClaims =
            new Dictionary<Damageable, WizardSupportAbilities>();

        [Header("Healing")]
        [SerializeField, Range(0f, 1f)] private float healTargetThreshold = 0.7f;
        [SerializeField, Min(0f)] private float healCastStartup = 0.6f;
        [SerializeField, Range(0f, 1f)] private float healPercentOfMaximumHealthPerTick = 0.05f;
        [SerializeField, Min(1)] private int minimumHealPerTick = 1;
        [SerializeField, Min(1)] private int maximumHealPerTick = 999;
        [SerializeField, Min(0.1f)] private float healTickInterval = 1f;
        [SerializeField, Min(0.1f)] private float maximumHealChannel = 4f;
        [SerializeField, Min(0.1f)] private float healRange = 6f;
        [SerializeField, Min(0f)] private float healCooldown = 2f;
        [SerializeField] private LayerMask allyLayers = ~0;

        [Header("Healing Corruption")]
        [SerializeField] private Vector4 corruptionStageThresholds = new Vector4(0.25f, 0.75f, 1.5f, 2.5f);
        [SerializeField] private Vector4 corruptionStageSlowPercentages = new Vector4(0.05f, 0.1f, 0.15f, 0.2f);
        [SerializeField, Range(0f, 0.8f)] private float maximumPermanentSlow = 0.2f;
        [SerializeField] private Color extremeCorruptionTint = new Color(0.48f, 0.28f, 0.58f, 1f);

        [Header("Empower")]
        [SerializeField, Min(0.1f)] private float empowerRange = 6f;
        [SerializeField, Min(0f)] private float empowerCastTime = 0.6f;
        [SerializeField, Min(0.1f)] private float empowerDuration = 6f;
        [SerializeField, Min(0f)] private float empowerCooldown = 10f;
        [SerializeField, Range(0f, 1f)] private float elementalDamageBonus = 0.25f;
        [SerializeField, Range(0.1f, 1f)] private float frostMovementMultiplier = 0.85f;
        [SerializeField, Min(0.1f)] private float frostSlowDuration = 1.5f;
        [SerializeField, Min(1)] private int normalSkeletonCapacity = 3;
        [SerializeField, Min(1)] private int evolvedSkeletonCapacity = 5;
        [SerializeField, Min(1)] private int normalFrontlineCapacity = 1;
        [SerializeField, Min(1)] private int evolvedFrontlineCapacity = 2;
        [SerializeField] private EnemyEvolutionStage empowerCapacityEvolutionStage =
            EnemyEvolutionStage.EvolutionOne;

        [Header("Dispel")]
        [SerializeField, Min(0f)] private float dispelCastTime = 0.75f;
        [SerializeField, Min(0f)] private float dispelCooldown = 12f;
        [SerializeField, Min(0.1f)] private float dispelRange = 6f;

        [Header("Teleport")]
        [SerializeField, Min(0.1f)] private float teleportDistance = 3f;
        [SerializeField, Min(0f)] private float teleportCastTime = 0.75f;
        [SerializeField, Min(0f)] private float sharedTeleportCooldown = 10f;
        [SerializeField, Range(0f, 1f)] private float criticalAllyThreshold = 0.3f;
        [SerializeField, Range(0f, 1f)] private float lowManaThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float playerRejectStaminaCostPercent = 0.5f;
        [SerializeField, Min(0.1f)] private float destinationClearanceRadius = 0.45f;
        [SerializeField, Range(0f, 0.15f)] private float warpCompletionSafetyMargin = 0.05f;
        [SerializeField] private LayerMask teleportGroundLayers;

        [Header("Evolution — Eye Pair")]
        [SerializeField] private EnemyEvolutionStage eyeSummonUnlockStage = EnemyEvolutionStage.EvolutionTwo;
        [SerializeField] private GameObject eyePrefab;
        [SerializeField, Range(120f, 180f)] private float eyeSummonCooldown = 150f;
        [SerializeField, Range(1.5f, 2f)] private float eyeSummonCastTime = 1.75f;
        [SerializeField, Min(0f)] private float interruptedEyeRetryDelay = 8f;

        [Header("Wizard VFX (Optional)")]
        [SerializeField] private GameObject healCastVfx;
        [SerializeField] private GameObject healTargetVfx;
        [SerializeField] private GameObject dispelVfx;
        [SerializeField] private GameObject fireBuffVfx;
        [SerializeField] private GameObject frostBuffVfx;
        [SerializeField] private GameObject teleportCastVfx;
        [SerializeField] private GameObject teleportArrivalVfx;
        [SerializeField] private GameObject warpMarkPlayerVfx;
        [SerializeField] private GameObject corruptionStageVfx;
        [SerializeField] private GameObject eyeSummonVfx;

        [Header("Current Cast (Read Only)")]
        [SerializeField] private WizardCastAction currentAction;
        [SerializeField] private Damageable currentTarget;
        [SerializeField, Min(0f)] private float sharedTeleportCooldownRemaining;
        [SerializeField, Min(0)] private int wizardOwnedLivingEyes;
        [SerializeField] private WizardEmpowerGroup activeEmpowerGroup;
        [SerializeField] private WizardElement activeEmpowerElement;
        [SerializeField, Min(0)] private int activeEmpowerTargetCount;

        private Damageable self;
        private WizardFlightMotor flightMotor;
        private SwarmCaller eyeCaller;
        private Coroutine castRoutine;
        private EnemyEvolutionStage evolutionStage;
        private float nextHealTime;
        private float nextEmpowerTime;
        private float nextDispelTime;
        private float nextTeleportTime;
        private float nextEyeRetryTime;
        private WizardElement nextElement = WizardElement.Fire;
        private PlayerWarpStatus markedPlayer;
        private readonly List<Damageable> activeEmpowerTargets = new List<Damageable>();

        public bool IsCasting => castRoutine != null;
        public WizardCastAction CurrentAction => currentAction;
        public bool IsTeleportReady => !IsCasting && Time.time >= nextTeleportTime;
        public int WizardOwnedLivingEyes => eyeCaller != null ? eyeCaller.ActiveSummonCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticClaims()
        {
            HealClaims.Clear();
        }

        private void Awake()
        {
            self = GetComponent<Damageable>();
            flightMotor = GetComponent<WizardFlightMotor>();
            eyeCaller = GetComponent<SwarmCaller>();
            if (eyeCaller == null)
            {
                eyeCaller = gameObject.AddComponent<SwarmCaller>();
            }

            self.DamageResolved += HandleDamageResolved;
            GetComponent<EnemyArchetypeProfile>()?.AddRuntimeArchetype(
                EnemyArchetype.Support | EnemyArchetype.Ranged);
        }

        private void Update()
        {
            sharedTeleportCooldownRemaining = Mathf.Max(0f, nextTeleportTime - Time.time);
            wizardOwnedLivingEyes = WizardOwnedLivingEyes;
            PruneEmpowermentGroup();
        }

        public void ConfigureRuntime(
            StrategicCombatSettings settings,
            WorldDifficultyManager difficultyManager)
        {
            if (eyeCaller == null)
            {
                eyeCaller = GetComponent<SwarmCaller>();
            }

            if (eyePrefab == null && settings != null)
            {
                eyePrefab = settings.AirSwarmPrefab;
            }

            eyeCaller?.ConfigureWizardEyePair(
                settings,
                difficultyManager,
                eyePrefab,
                eyeSummonCooldown);
        }

        public bool HasHealTarget()
        {
            return FindHealTarget() != null;
        }

        public bool TryBeginHeal()
        {
            if (IsCasting || Time.time < nextHealTime)
            {
                return false;
            }

            Damageable target = FindHealTarget();
            if (target == null || !TryClaimHeal(target))
            {
                return false;
            }

            BeginCast(WizardCastAction.Heal, target, HealRoutine(target));
            return true;
        }

        public Damageable FindCriticalAlly()
        {
            return EnemySupportTargeting.FindLowestHealthTarget(
                transform.position,
                Mathf.Max(healRange, empowerRange),
                allyLayers,
                self,
                false,
                true,
                criticalAllyThreshold);
        }

        public bool HasDispelTarget()
        {
            return FindDispelTarget() != null;
        }

        public bool TryBeginDispel()
        {
            if (IsCasting || Time.time < nextDispelTime)
            {
                return false;
            }

            Damageable target = FindDispelTarget();
            if (target == null)
            {
                return false;
            }

            BeginCast(WizardCastAction.Dispel, target, DispelRoutine(target));
            return true;
        }

        public bool HasEmpowerTarget()
        {
            return FindEmpowerTargets(out _).Count > 0;
        }

        public bool TryBeginEmpower()
        {
            if (IsCasting || Time.time < nextEmpowerTime)
            {
                return false;
            }

            List<Damageable> targets = FindEmpowerTargets(out WizardEmpowerGroup group);
            if (targets.Count == 0)
            {
                return false;
            }

            WizardElement element = activeEmpowerGroup == WizardEmpowerGroup.None
                ? nextElement
                : activeEmpowerElement;
            BeginCast(
                WizardCastAction.Empower,
                targets[0],
                EmpowerRoutine(targets, group, element));
            return true;
        }

        public bool TryBeginSelfTeleport(PlayerHealth player)
        {
            if (!IsTeleportReady || player == null)
            {
                return false;
            }

            Vector2 away = (Vector2)transform.position - (Vector2)player.transform.position;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector2.left;
            }

            BeginTeleportCooldown();
            BeginCast(
                WizardCastAction.SelfTeleport,
                null,
                TeleportRoutine(transform, away.normalized, false));
            return true;
        }

        public bool TryBeginAllyTeleport(Damageable target, PlayerHealth player)
        {
            if (!IsTeleportReady || !IsValidTeleportAlly(target))
            {
                return false;
            }

            Vector2 away = player != null
                ? (Vector2)target.transform.position - (Vector2)player.transform.position
                : (Vector2)transform.position - (Vector2)target.transform.position;
            if (away.sqrMagnitude < 0.01f)
            {
                away = (Vector2)transform.position - (Vector2)target.transform.position;
            }

            BeginTeleportCooldown();
            BeginCast(
                WizardCastAction.AllyTeleport,
                target,
                TeleportRoutine(
                    target.transform,
                    away.normalized,
                    !IsFlyingSubject(target.transform)));
            return true;
        }

        public bool CanForceTeleport(PlayerHealth player)
        {
            if (!IsTeleportReady || player == null || !player.gameObject.activeInHierarchy)
            {
                return false;
            }

            PlayerWarpStatus status = player.GetComponent<PlayerWarpStatus>();
            if (status != null && !status.CanBeClaimedBy(this))
            {
                return false;
            }

            PlayerMana mana = player.GetComponent<PlayerMana>();
            return mana != null
                && mana.MaximumMana > 0f
                && mana.CurrentMana / mana.MaximumMana <= lowManaThreshold;
        }

        public bool TryBeginForcePlayerTeleport(PlayerHealth player)
        {
            if (!CanForceTeleport(player))
            {
                return false;
            }

            PlayerWarpStatus status = player.GetComponent<PlayerWarpStatus>();
            if (status == null)
            {
                status = player.gameObject.AddComponent<PlayerWarpStatus>();
            }

            PlayerMana mana = player.GetComponent<PlayerMana>();
            SpinSwordAttack stamina = player.GetComponent<SpinSwordAttack>();
            if (stamina == null)
            {
                return false;
            }

            if (!status.TryBegin(
                this,
                mana,
                stamina,
                lowManaThreshold,
                playerRejectStaminaCostPercent,
                teleportCastTime + warpCompletionSafetyMargin,
                warpMarkPlayerVfx))
            {
                return false;
            }

            markedPlayer = status;
            BeginTeleportCooldown();
            Vector2 away = (Vector2)player.transform.position - (Vector2)transform.position;
            BeginCast(
                WizardCastAction.ForcePlayerTeleport,
                null,
                ForcePlayerTeleportRoutine(player, mana, away.normalized));
            return true;
        }

        public bool CanSummonEyePair()
        {
            return !IsCasting
                && evolutionStage >= eyeSummonUnlockStage
                && Time.time >= nextEyeRetryTime
                && eyeCaller != null
                && eyeCaller.ActiveSummonCount == 0
                && eyeCaller.IsReady;
        }

        public bool TryBeginEyePairSummon()
        {
            if (!CanSummonEyePair())
            {
                return false;
            }

            BeginCast(WizardCastAction.SummonEyes, null, EyeSummonRoutine());
            return true;
        }

        public void RejectForcedPlayerTeleport(PlayerWarpStatus status)
        {
            if (markedPlayer == status && currentAction == WizardCastAction.ForcePlayerTeleport)
            {
                Interrupt();
            }
        }

        public void CancelForcedPlayerTeleport(PlayerWarpStatus status)
        {
            if (markedPlayer == status && currentAction == WizardCastAction.ForcePlayerTeleport)
            {
                Interrupt();
            }
        }

        private IEnumerator HealRoutine(Damageable target)
        {
            SpawnAttachedVfx(healCastVfx, transform, healCastStartup);
            yield return new WaitForSeconds(healCastStartup);
            float channelEndsAt = Time.time + maximumHealChannel;
            while (Time.time < channelEndsAt && IsValidTargetInRange(target, healRange))
            {
                yield return new WaitForSeconds(healTickInterval);
                if (!IsValidTargetInRange(target, healRange)
                    || target.CurrentHealth >= target.MaximumHealth)
                {
                    break;
                }

                int requestedHealing = Mathf.Clamp(
                    Mathf.CeilToInt(target.MaximumHealth * healPercentOfMaximumHealthPerTick),
                    minimumHealPerTick,
                    Mathf.Max(minimumHealPerTick, maximumHealPerTick));
                int actualRestored = target.RestoreHealthResolved(requestedHealing);
                if (actualRestored <= 0)
                {
                    break;
                }

                WizardHealingCorruption corruption =
                    target.GetComponent<WizardHealingCorruption>();
                if (corruption == null)
                {
                    corruption = target.gameObject.AddComponent<WizardHealingCorruption>();
                }

                corruption.RecordActualHealing(
                    actualRestored,
                    corruptionStageThresholds,
                    corruptionStageSlowPercentages,
                    extremeCorruptionTint,
                    maximumPermanentSlow,
                    corruptionStageVfx);
                if (corruption.TryTransformToAuthoredPrefabWhenFullyCorrupted())
                {
                    break;
                }
                SpawnAttachedVfx(healTargetVfx, target.transform, healTickInterval);
            }

            nextHealTime = Time.time + healCooldown;
            ReleaseHealClaim(target);
            EndCast();
        }

        private IEnumerator DispelRoutine(Damageable target)
        {
            yield return new WaitForSeconds(dispelCastTime);
            if (IsValidTargetInRange(target, dispelRange))
            {
                EnemyStatusEffects status = target.GetComponent<EnemyStatusEffects>();
                if (status != null && status.DispelRemovableDebuffs())
                {
                    SpawnAttachedVfx(dispelVfx, target.transform, 1f);
                }
            }

            nextDispelTime = Time.time + dispelCooldown;
            EndCast();
        }

        private IEnumerator EmpowerRoutine(
            List<Damageable> targets,
            WizardEmpowerGroup group,
            WizardElement element)
        {
            yield return new WaitForSeconds(empowerCastTime);
            int appliedCount = 0;
            foreach (Damageable target in targets)
            {
                if (!IsValidTargetInRange(target, empowerRange)
                    || target.CurrentHealth != target.MaximumHealth
                    || ResolveEmpowerGroup(target) != group)
                {
                    continue;
                }

                EnemyElementalEmpowerment empowerment =
                    target.GetComponent<EnemyElementalEmpowerment>();
                if (empowerment == null)
                {
                    empowerment = target.gameObject.AddComponent<EnemyElementalEmpowerment>();
                }

                GameObject visual = element == WizardElement.Fire ? fireBuffVfx : frostBuffVfx;
                if (empowerment.Apply(
                    element,
                    elementalDamageBonus,
                    empowerDuration,
                    frostMovementMultiplier,
                    frostSlowDuration,
                    gameObject,
                    visual))
                {
                    if (!activeEmpowerTargets.Contains(target))
                    {
                        activeEmpowerTargets.Add(target);
                    }

                    appliedCount++;
                }
            }

            if (appliedCount > 0)
            {
                activeEmpowerGroup = group;
                activeEmpowerElement = element;
                nextElement = element == WizardElement.Fire
                    ? WizardElement.Frost
                    : WizardElement.Fire;
            }

            nextEmpowerTime = Time.time + empowerCooldown;
            EndCast();
        }

        private IEnumerator TeleportRoutine(Transform subject, Vector2 direction, bool requireGround)
        {
            SpawnAttachedVfx(teleportCastVfx, transform, teleportCastTime);
            yield return new WaitForSeconds(teleportCastTime);
            if (subject != null
                && subject.gameObject.activeInHierarchy
                && TryFindSafeDestination(subject, direction, requireGround, out Vector2 destination))
            {
                MoveSubject(subject, destination);
                SpawnWorldVfx(teleportArrivalVfx, destination);
            }

            EndCast();
        }

        private IEnumerator ForcePlayerTeleportRoutine(
            PlayerHealth player,
            PlayerMana mana,
            Vector2 direction)
        {
            SpawnAttachedVfx(teleportCastVfx, transform, teleportCastTime);
            float completesAt = Time.time + teleportCastTime;
            while (Time.time < completesAt)
            {
                if (player == null
                    || !player.gameObject.activeInHierarchy
                    || mana == null
                    || mana.MaximumMana <= 0f
                    || mana.CurrentMana / mana.MaximumMana > lowManaThreshold
                    || markedPlayer == null
                    || !markedPlayer.IsMarked)
                {
                    markedPlayer?.ClearFrom(this);
                    markedPlayer = null;
                    EndCast();
                    yield break;
                }

                yield return null;
            }

            if (TryFindSafeDestination(player.transform, direction, true, out Vector2 destination))
            {
                MoveSubject(player.transform, destination);
                SpawnWorldVfx(teleportArrivalVfx, destination);
            }

            markedPlayer?.ClearFrom(this);
            markedPlayer = null;
            EndCast();
        }

        private IEnumerator EyeSummonRoutine()
        {
            SpawnAttachedVfx(eyeSummonVfx, transform, eyeSummonCastTime);
            yield return new WaitForSeconds(eyeSummonCastTime);
            if (eyeCaller == null || !eyeCaller.TrySummon())
            {
                nextEyeRetryTime = Time.time + interruptedEyeRetryDelay;
            }

            EndCast();
        }

        private Damageable FindHealTarget()
        {
            Damageable candidate = EnemySupportTargeting.FindLowestHealthTarget(
                transform.position,
                healRange,
                allyLayers,
                self,
                false,
                true,
                healTargetThreshold);
            if (candidate == null)
            {
                return null;
            }

            return !HealClaims.TryGetValue(candidate, out WizardSupportAbilities owner)
                || owner == null
                || owner == this
                    ? candidate
                    : null;
        }

        private Damageable FindDispelTarget()
        {
            foreach (Damageable candidate in EnemySupportTargeting.CollectAllies(
                transform.position,
                dispelRange,
                allyLayers,
                self,
                false))
            {
                EnemyStatusEffects status = candidate.GetComponent<EnemyStatusEffects>();
                if (status != null && status.HasRemovableDebuff)
                {
                    return candidate;
                }
            }

            return null;
        }

        private List<Damageable> FindEmpowerTargets(out WizardEmpowerGroup selectedGroup)
        {
            PruneEmpowermentGroup();
            selectedGroup = activeEmpowerGroup;
            List<Damageable> skeletons = new List<Damageable>();
            List<Damageable> frontline = new List<Damageable>();
            foreach (Damageable candidate in EnemySupportTargeting.CollectAllies(
                transform.position,
                empowerRange,
                allyLayers,
                self,
                false))
            {
                if (candidate.CurrentHealth != candidate.MaximumHealth)
                {
                    continue;
                }

                EnemyElementalEmpowerment empowerment =
                    candidate.GetComponent<EnemyElementalEmpowerment>();
                if (empowerment != null && empowerment.IsEmpoweredBy(gameObject))
                {
                    continue;
                }

                WizardEmpowerGroup group = ResolveEmpowerGroup(candidate);
                if (group == WizardEmpowerGroup.Skeletons)
                {
                    skeletons.Add(candidate);
                }
                else if (group == WizardEmpowerGroup.Frontline)
                {
                    frontline.Add(candidate);
                }
            }

            List<Damageable> selected;
            if (selectedGroup == WizardEmpowerGroup.Skeletons)
            {
                selected = skeletons;
            }
            else if (selectedGroup == WizardEmpowerGroup.Frontline)
            {
                selected = frontline;
            }
            else if (skeletons.Count > 0)
            {
                selectedGroup = WizardEmpowerGroup.Skeletons;
                selected = skeletons;
            }
            else
            {
                selectedGroup = WizardEmpowerGroup.Frontline;
                selected = frontline;
            }

            EnemySupportTargeting.SortForOffensiveSupport(selected, transform.position);
            int capacity = ResolveEmpowerCapacity(selectedGroup);
            int availableSlots = Mathf.Max(0, capacity - activeEmpowerTargets.Count);
            if (selected.Count > availableSlots)
            {
                selected.RemoveRange(availableSlots, selected.Count - availableSlots);
            }

            return selected;
        }

        private WizardEmpowerGroup ResolveEmpowerGroup(Damageable candidate)
        {
            if (candidate == null)
            {
                return WizardEmpowerGroup.None;
            }

            if (candidate.GetComponent<SkeletonInheritance>() != null)
            {
                return WizardEmpowerGroup.Skeletons;
            }

            EnemyArchetypeProfile profile = candidate.GetComponent<EnemyArchetypeProfile>();
            if (profile == null
                || profile.Includes(EnemyArchetype.Support)
                || profile.Includes(EnemyArchetype.Ranged))
            {
                return WizardEmpowerGroup.None;
            }

            return profile.Includes(EnemyArchetype.Melee)
                || profile.Includes(EnemyArchetype.Tank)
                    ? WizardEmpowerGroup.Frontline
                    : WizardEmpowerGroup.None;
        }

        private int ResolveEmpowerCapacity(WizardEmpowerGroup group)
        {
            bool evolved = evolutionStage >= empowerCapacityEvolutionStage;
            return group == WizardEmpowerGroup.Skeletons
                ? (evolved ? evolvedSkeletonCapacity : normalSkeletonCapacity)
                : group == WizardEmpowerGroup.Frontline
                    ? (evolved ? evolvedFrontlineCapacity : normalFrontlineCapacity)
                    : 0;
        }

        private void PruneEmpowermentGroup()
        {
            for (int index = activeEmpowerTargets.Count - 1; index >= 0; index--)
            {
                Damageable target = activeEmpowerTargets[index];
                EnemyElementalEmpowerment empowerment = target != null
                    ? target.GetComponent<EnemyElementalEmpowerment>()
                    : null;
                if (target == null
                    || !target.gameObject.activeInHierarchy
                    || empowerment == null
                    || !empowerment.IsEmpoweredBy(gameObject))
                {
                    activeEmpowerTargets.RemoveAt(index);
                }
            }

            activeEmpowerTargetCount = activeEmpowerTargets.Count;
            if (activeEmpowerTargetCount == 0)
            {
                activeEmpowerGroup = WizardEmpowerGroup.None;
                activeEmpowerElement = WizardElement.None;
            }
        }

        private bool TryClaimHeal(Damageable target)
        {
            if (HealClaims.TryGetValue(target, out WizardSupportAbilities owner)
                && owner != null
                && owner != this)
            {
                return false;
            }

            HealClaims[target] = this;
            return true;
        }

        private void ReleaseHealClaim(Damageable target)
        {
            if (target != null
                && HealClaims.TryGetValue(target, out WizardSupportAbilities owner)
                && owner == this)
            {
                HealClaims.Remove(target);
            }
        }

        private bool IsValidTargetInRange(Damageable target, float range)
        {
            return target != null
                && target.CurrentHealth > 0
                && target.gameObject.activeInHierarchy
                && ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude
                    <= range * range;
        }

        private bool IsValidTeleportAlly(Damageable target)
        {
            if (!IsValidTargetInRange(target, Mathf.Max(healRange, empowerRange)))
            {
                return false;
            }

            EnemyStagger stagger = target.GetComponent<EnemyStagger>();
            EnemyMeleeCombat melee = target.GetComponent<EnemyMeleeCombat>();
            return (stagger == null || !stagger.IsStaggered)
                && (melee == null || (!melee.IsAttackCommitted && !melee.IsGuardBreakInProgress));
        }

        private static bool IsFlyingSubject(Transform subject)
        {
            return subject != null
                && (subject.GetComponent<WizardFlightMotor>() != null
                    || subject.GetComponent<FlyingSwarmController>() != null);
        }

        private bool TryFindSafeDestination(
            Transform subject,
            Vector2 desiredDirection,
            bool requireGround,
            out Vector2 destination)
        {
            Vector2 direction = desiredDirection.sqrMagnitude > 0.01f
                ? desiredDirection.normalized
                : Vector2.right;
            Vector2[] attempts =
            {
                direction,
                new Vector2(direction.x, 0f).normalized,
                Quaternion.Euler(0f, 0f, 35f) * direction,
                Quaternion.Euler(0f, 0f, -35f) * direction,
                -direction
            };
            foreach (Vector2 attempt in attempts)
            {
                if (attempt.sqrMagnitude <= 0.01f)
                {
                    continue;
                }

                Vector2 candidate = (Vector2)subject.position + attempt.normalized * teleportDistance;
                if (requireGround && !TrySnapToGround(subject, candidate, out candidate))
                {
                    continue;
                }

                if (IsDestinationClear(subject, candidate))
                {
                    destination = candidate;
                    return true;
                }
            }

            destination = subject.position;
            return false;
        }

        private bool TrySnapToGround(Transform subject, Vector2 candidate, out Vector2 grounded)
        {
            int mask = teleportGroundLayers.value != 0 ? teleportGroundLayers.value : ~0;
            RaycastHit2D[] hits = Physics2D.RaycastAll(candidate + Vector2.up * 1.5f, Vector2.down, 3f, mask);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null
                    || hit.collider.isTrigger
                    || hit.collider.transform.IsChildOf(subject)
                    || hit.collider.GetComponentInParent<Damageable>() != null
                    || hit.collider.GetComponentInParent<PlayerHealth>() != null)
                {
                    continue;
                }

                Collider2D subjectCollider = subject.GetComponentInChildren<Collider2D>();
                float halfHeight = subjectCollider != null ? subjectCollider.bounds.extents.y : 0.5f;
                grounded = hit.point + Vector2.up * (halfHeight + 0.05f);
                return true;
            }

            grounded = candidate;
            return false;
        }

        private bool IsDestinationClear(Transform subject, Vector2 destination)
        {
            foreach (Collider2D overlap in Physics2D.OverlapCircleAll(destination, destinationClearanceRadius))
            {
                if (overlap == null || overlap.transform.IsChildOf(subject) || subject.IsChildOf(overlap.transform))
                {
                    continue;
                }

                if (overlap.GetComponentInParent<DeathBoundary>() != null
                    || overlap.GetComponentInParent<PlayerHealth>() != null
                    || overlap.GetComponentInParent<Damageable>() != null
                    || !overlap.isTrigger)
                {
                    return false;
                }
            }

            return true;
        }

        private static void MoveSubject(Transform subject, Vector2 destination)
        {
            WizardFlightMotor wizardMotor = subject.GetComponent<WizardFlightMotor>();
            if (wizardMotor != null)
            {
                wizardMotor.TeleportTo(destination);
                return;
            }

            FlyingSwarmController flyingMotor = subject.GetComponent<FlyingSwarmController>();
            if (flyingMotor != null)
            {
                flyingMotor.TeleportTo(destination);
                return;
            }

            Rigidbody2D body = subject.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = destination;
                body.velocity = Vector2.zero;
            }
            else
            {
                subject.position = destination;
            }
        }

        private void BeginTeleportCooldown()
        {
            nextTeleportTime = Time.time + sharedTeleportCooldown;
        }

        private void BeginCast(
            WizardCastAction action,
            Damageable target,
            IEnumerator routine)
        {
            currentAction = action;
            currentTarget = target;
            flightMotor?.HoldPosition();
            castRoutine = StartCoroutine(routine);
        }

        private void EndCast()
        {
            castRoutine = null;
            currentAction = WizardCastAction.None;
            currentTarget = null;
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (!blocked && appliedDamage > 0 && IsCasting)
            {
                Interrupt();
            }
        }

        public void Interrupt()
        {
            WizardCastAction interruptedAction = currentAction;
            Damageable interruptedTarget = currentTarget;
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
            }

            castRoutine = null;
            ReleaseHealClaim(interruptedTarget);
            markedPlayer?.ClearFrom(this);
            markedPlayer = null;
            if (interruptedAction == WizardCastAction.SummonEyes)
            {
                nextEyeRetryTime = Time.time + interruptedEyeRetryDelay;
            }

            currentAction = WizardCastAction.None;
            currentTarget = null;
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
        }

        private static void SpawnWorldVfx(GameObject prefab, Vector2 position)
        {
            if (prefab != null)
            {
                Object.Instantiate(prefab, position, Quaternion.identity);
            }
        }

        private static void SpawnAttachedVfx(GameObject prefab, Transform target, float lifetime)
        {
            if (prefab == null || target == null)
            {
                return;
            }

            GameObject instance = Object.Instantiate(
                prefab,
                target.position,
                Quaternion.identity,
                target);
            instance.transform.localPosition = Vector3.zero;
            if (lifetime > 0f)
            {
                Object.Destroy(instance, lifetime);
            }
        }

        private void OnDisable()
        {
            Interrupt();
            foreach (Damageable target in activeEmpowerTargets)
            {
                target?.GetComponent<EnemyElementalEmpowerment>()
                    ?.RemoveFrom(gameObject);
            }

            activeEmpowerTargets.Clear();
            activeEmpowerGroup = WizardEmpowerGroup.None;
            activeEmpowerElement = WizardElement.None;
            activeEmpowerTargetCount = 0;
            nextHealTime = 0f;
            nextEmpowerTime = 0f;
            nextDispelTime = 0f;
            nextTeleportTime = 0f;
        }

        private void OnValidate()
        {
            evolvedSkeletonCapacity = Mathf.Max(normalSkeletonCapacity, evolvedSkeletonCapacity);
            evolvedFrontlineCapacity = Mathf.Max(normalFrontlineCapacity, evolvedFrontlineCapacity);
        }

        private void OnDestroy()
        {
            if (self != null)
            {
                self.DamageResolved -= HandleDamageResolved;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.55f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, healRange);
            Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, teleportDistance);
        }
    }
}
