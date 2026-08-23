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

        public bool IsCasting => pendingTarget != null;
        public bool IsReady => pendingTarget == null
            && Time.time >= nextHealTime
            && (stagger == null || stagger.CanAct);

        public bool HasEligibleTarget()
        {
            return EnemySupportTargeting.FindLowestHealthTarget(
                transform.position,
                EffectiveHealRange,
                allyLayers,
                self,
                canHealSelf,
                preferNonSupportTargets,
                targetHealthThreshold) != null;
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

            Damageable target = EnemySupportTargeting.FindLowestHealthTarget(
                transform.position,
                EffectiveHealRange,
                allyLayers,
                self,
                canHealSelf,
                preferNonSupportTargets,
                targetHealthThreshold);
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

            if (target.RestoreHealth(EffectiveHealAmount))
            {
                Cave.Combat.AreaPulseEffect.Create(
                    target.transform.position,
                    evolutionStage == EnemyEvolutionStage.EvolutionTwo ? 0.82f : 0.68f,
                    healColor,
                    0.3f);
            }
        }

        private int EffectiveHealAmount => healAmount
            + (evolutionStage >= EnemyEvolutionStage.EvolutionOne ? evolutionOneAdditionalHeal : 0)
            + (evolutionStage >= EnemyEvolutionStage.EvolutionTwo ? evolutionTwoAdditionalHeal : 0);

        private float EffectiveHealCooldown => healCooldown * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
            ? evolutionTwoCooldownMultiplier
            : evolutionStage == EnemyEvolutionStage.EvolutionOne
                ? evolutionOneCooldownMultiplier
                : 1f);

        private float EffectiveHealRange => healRange * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
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
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = healColor;
            Gizmos.DrawWireSphere(transform.position, healRange);
        }
    }
}
