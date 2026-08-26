using System.Collections;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyHealAbility : MonoBehaviour,
        IEnemyInterruptible,
        IEnemySkillEvolutionReceiver
    {
        [SerializeField, Min(1)] private int healAmount = 2;
        [SerializeField, Min(0f)] private float healCooldown = 4f;
        [SerializeField, Min(0.1f)] private float healRange = 6f;
        [SerializeField, Range(0f, 1f)] private float targetHealthThreshold = 0.7f;
        [SerializeField, Min(0f)] private float castWindup = 0.4f;
        [SerializeField] private LayerMask allyLayers = ~0;
        [SerializeField] private bool canHealSelf;
        [SerializeField] private bool preferNonSupportTargets = true;
        [SerializeField] private Color healColor = new Color(0.25f, 1f, 0.55f, 0.85f);

        [Header("Necromancer Owned-Skeleton Regeneration")]
        [SerializeField] private bool useOwnedSkeletonRegeneration;
        [SerializeField, Range(0.01f, 1f)] private float totalMaximumHealthFraction = 0.25f;
        [SerializeField, Min(0.1f)] private float regenerationDuration = 3f;
        [SerializeField, Min(0.05f)] private float regenerationTickInterval = 0.25f;

        [Header("Necromancer Heal Priority")]
        [SerializeField, Min(0f)] private float generalRankBonus = 2.5f;
        [SerializeField, Min(0f)] private float witnessedDeathWeight = 0.35f;
        [SerializeField, Min(0f)] private float resolvedStrengthWeight = 1f;
        [SerializeField, Range(0.01f, 1f)] private float generalEmergencyHealthFraction = 0.3f;
        [SerializeField, Min(0f)] private float generalEmergencyBonus = 3f;
        [SerializeField, Min(0f)] private float anchorPreservationBonus = 2f;
        [SerializeField, Min(0f)] private float assaultCombatBonus = 1.5f;

        [Header("Heal Priority (Read Only)")]
        [SerializeField] private Damageable currentEvaluatedTarget;
        [SerializeField] private float currentEvaluatedTargetScore;

        [Header("Skill Evolution")]
        [SerializeField, Min(0)] private int evolutionOneAdditionalHeal = 1;
        [SerializeField, Min(0)] private int evolutionTwoAdditionalHeal = 1;
        [SerializeField, Range(0.2f, 1f)] private float evolutionOneCooldownMultiplier = 0.9f;
        [SerializeField, Range(0.2f, 1f)] private float evolutionTwoCooldownMultiplier = 0.8f;
        [SerializeField, Range(1f, 2f)] private float evolutionTwoRangeMultiplier = 1.2f;

        private Damageable self;
        private EnemyStagger stagger;
        private float nextHealTime;
        private float castCompletesAt;
        private Damageable pendingTarget;
        private EnemyEvolutionStage evolutionStage;
        private bool brainControlled;
        private SwarmCaller ownedSummonSource;
        private Coroutine regenerationRoutine;
        private Damageable regenerationTarget;

        public bool IsCasting => pendingTarget != null;
        public Damageable CurrentTarget => pendingTarget != null
            ? pendingTarget
            : regenerationTarget;
        public float CurrentTargetScore => currentEvaluatedTargetScore;
        public bool IsReady => pendingTarget == null
            && regenerationRoutine == null
            && Time.time >= nextHealTime
            && (stagger == null || stagger.CanAct);

        public bool HasEligibleTarget()
        {
            return FindTarget() != null;
        }

        public void ConfigureNecromancerRegeneration(
            SwarmCaller summonSource,
            float totalHealFraction,
            float duration,
            float tickInterval,
            float cooldown,
            float windup,
            float range)
        {
            useOwnedSkeletonRegeneration = true;
            ownedSummonSource = summonSource;
            totalMaximumHealthFraction = Mathf.Clamp(totalHealFraction, 0.01f, 1f);
            regenerationDuration = Mathf.Max(0.1f, duration);
            regenerationTickInterval = Mathf.Clamp(
                tickInterval,
                0.05f,
                regenerationDuration);
            healCooldown = Mathf.Max(0f, cooldown);
            castWindup = Mathf.Max(0f, windup);
            healRange = Mathf.Max(0.1f, range);
            canHealSelf = false;
            preferNonSupportTargets = true;
        }

        private Damageable FindTarget()
        {
            if (useOwnedSkeletonRegeneration)
            {
                if (ownedSummonSource == null)
                {
                    currentEvaluatedTarget = null;
                    currentEvaluatedTargetScore = 0f;
                    return null;
                }

                currentEvaluatedTarget = ownedSummonSource.FindBestOwnedSkeletonForHeal(
                    EffectiveHealRange,
                    generalRankBonus,
                    witnessedDeathWeight,
                    resolvedStrengthWeight,
                    generalEmergencyHealthFraction,
                    generalEmergencyBonus,
                    anchorPreservationBonus,
                    assaultCombatBonus,
                    out currentEvaluatedTargetScore);
                return currentEvaluatedTarget;
            }

            currentEvaluatedTarget = EnemySupportTargeting.FindLowestHealthTarget(
                transform.position,
                EffectiveHealRange,
                allyLayers,
                self,
                canHealSelf,
                preferNonSupportTargets,
                targetHealthThreshold);
            currentEvaluatedTargetScore = currentEvaluatedTarget != null
                ? 1f - currentEvaluatedTarget.CurrentHealth
                    / (float)Mathf.Max(1, currentEvaluatedTarget.MaximumHealth)
                : 0f;
            return currentEvaluatedTarget;
        }

        private void Awake()
        {
            self = GetComponent<Damageable>();
            stagger = GetComponent<EnemyStagger>();
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = gameObject.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(EnemyArchetype.Support);
        }

        private void Update()
        {
            if (pendingTarget != null)
            {
                if (Time.time >= castCompletesAt)
                {
                    CompleteHeal();
                }

                return;
            }

            if (brainControlled)
            {
                return;
            }

            TryUse();
        }

        public bool TryUse()
        {
            if (!IsReady)
            {
                return false;
            }

            Damageable target = FindTarget();
            if (target == null)
            {
                nextHealTime = Time.time + 0.25f;
                return false;
            }

            pendingTarget = target;
            castCompletesAt = Time.time + castWindup;
            Cave.Combat.AreaPulseEffect.Create(transform.position, 0.72f, healColor, castWindup);
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        private void CompleteHeal()
        {
            Damageable target = pendingTarget;
            pendingTarget = null;
            nextHealTime = Time.time + EffectiveHealCooldown;
            if (target == null
                || !target.gameObject.activeInHierarchy
                || ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude
                    > EffectiveHealRange * EffectiveHealRange)
            {
                return;
            }

            if (useOwnedSkeletonRegeneration)
            {
                regenerationRoutine = StartCoroutine(RegenerateSkeleton(target));
                return;
            }

            if (target.RestoreHealth(EffectiveHealAmount))
            {
                Cave.Combat.AreaPulseEffect.Create(
                    target.transform.position,
                    evolutionStage == EnemyEvolutionStage.EvolutionTwo ? 0.82f : 0.68f,
                    healColor,
                    0.3f);
            }
        }

        private IEnumerator RegenerateSkeleton(Damageable target)
        {
            regenerationTarget = target;
            int totalHealing = Mathf.Max(
                1,
                Mathf.RoundToInt(target.MaximumHealth * totalMaximumHealthFraction));
            float elapsed = 0f;
            int scheduledHealing = 0;
            Cave.Combat.AreaPulseEffect.Create(
                target.transform.position,
                0.68f,
                healColor,
                0.3f);

            while (elapsed < regenerationDuration
                && target != null
                && target.gameObject.activeInHierarchy
                && target.CurrentHealth > 0
                && target.CurrentHealth < target.MaximumHealth)
            {
                float interval = Mathf.Min(
                    regenerationTickInterval,
                    regenerationDuration - elapsed);
                yield return new WaitForSeconds(interval);
                elapsed += interval;
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    break;
                }

                int nextScheduledHealing = Mathf.RoundToInt(
                    totalHealing * Mathf.Clamp01(elapsed / regenerationDuration));
                int amount = nextScheduledHealing - scheduledHealing;
                scheduledHealing = nextScheduledHealing;
                if (amount > 0)
                {
                    target.RestoreHealth(amount);
                }
            }

            regenerationRoutine = null;
            regenerationTarget = null;
        }

        private int EffectiveHealAmount => healAmount
            + (evolutionStage >= EnemyEvolutionStage.EvolutionOne ? evolutionOneAdditionalHeal : 0)
            + (evolutionStage >= EnemyEvolutionStage.EvolutionTwo ? evolutionTwoAdditionalHeal : 0);

        private float EffectiveHealCooldown => useOwnedSkeletonRegeneration
            ? healCooldown
            : healCooldown
                * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
                    ? evolutionTwoCooldownMultiplier
                    : evolutionStage == EnemyEvolutionStage.EvolutionOne
                        ? evolutionOneCooldownMultiplier
                        : 1f);

        private float EffectiveHealRange => useOwnedSkeletonRegeneration
            ? healRange
            : healRange
                * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
                    ? evolutionTwoRangeMultiplier
                    : 1f);

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
        }

        public void Interrupt()
        {
            pendingTarget = null;
            nextHealTime = Mathf.Max(nextHealTime, Time.time + EffectiveHealCooldown * 0.5f);
        }

        private void OnDisable()
        {
            pendingTarget = null;
            nextHealTime = 0f;
            if (regenerationRoutine != null)
            {
                StopCoroutine(regenerationRoutine);
                regenerationRoutine = null;
            }

            regenerationTarget = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = healColor;
            Gizmos.DrawWireSphere(transform.position, healRange);
        }
    }
}
