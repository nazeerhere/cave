using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyDamageBuffAbility : MonoBehaviour,
        IEnemyInterruptible,
        IEnemySkillEvolutionReceiver
    {
        [SerializeField] private EnemyDamageModifierType buffType = EnemyDamageModifierType.NecromancerBuff;
        [SerializeField, Range(0f, 2f)] private float additiveDamageBonus = 0.25f;
        [SerializeField, Min(0.1f)] private float buffRange = 5f;
        [SerializeField, Min(0.1f)] private float buffDuration = 6f;
        [SerializeField, Min(0f)] private float buffCooldown = 4f;
        [SerializeField, Min(0f)] private float castWindup = 0.35f;
        [SerializeField] private LayerMask allyLayers = ~0;
        [SerializeField] private bool canBuffSelf;
        [SerializeField] private bool preferNonSupportTargets = true;
        [SerializeField] private Color castColor = new Color(0.72f, 0.25f, 1f, 0.85f);

        [Header("Skill Evolution")]
        [SerializeField, Range(0f, 0.25f)] private float evolutionOneBonusIncrease = 0.05f;
        [SerializeField, Range(0f, 0.25f)] private float evolutionTwoBonusIncrease = 0.05f;
        [SerializeField, Range(0f, 1f)] private float maximumAdditiveDamageBonus = 0.45f;
        [SerializeField, Range(1f, 2f)] private float evolutionOneRangeMultiplier = 1.15f;
        [SerializeField, Range(1f, 2f)] private float evolutionTwoRangeMultiplier = 1.3f;
        [SerializeField, Range(1f, 2f)] private float evolutionTwoDurationMultiplier = 1.2f;

        private Damageable self;
        private EnemyStagger stagger;
        private float nextCastTime;
        private float castCompletesAt;
        private Damageable pendingTarget;
        private EnemyHealAbility healAbility;
        private SwarmCaller swarmCaller;
        private EnemyEvolutionStage evolutionStage;
        private bool brainControlled;

        public bool IsCasting => pendingTarget != null;
        public bool IsReady => pendingTarget == null
            && Time.time >= nextCastTime
            && (stagger == null || stagger.CanAct);

        public bool HasEligibleTarget()
        {
            return FindTarget() != null;
        }

        private void Awake()
        {
            self = GetComponent<Damageable>();
            stagger = GetComponent<EnemyStagger>();
            healAbility = GetComponent<EnemyHealAbility>();
            swarmCaller = GetComponent<SwarmCaller>();
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
                    CompleteCast();
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

            if ((healAbility != null && (healAbility.IsCasting || healAbility.HasEligibleTarget()))
                || (swarmCaller != null && swarmCaller.ShouldPrioritizeSummon))
            {
                nextCastTime = Time.time + 0.15f;
                return false;
            }

            Damageable target = FindTarget();
            if (target == null)
            {
                nextCastTime = Time.time + 0.25f;
                return false;
            }

            pendingTarget = target;
            castCompletesAt = Time.time + castWindup;
            Cave.Combat.AreaPulseEffect.Create(transform.position, 0.7f, castColor, castWindup);
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        private Damageable FindTarget()
        {
            return EnemySupportTargeting.FindUnbuffedDamageTarget(
                transform.position,
                EffectiveBuffRange,
                allyLayers,
                self,
                canBuffSelf,
                preferNonSupportTargets,
                buffType);
        }

        private void CompleteCast()
        {
            Damageable target = pendingTarget;
            pendingTarget = null;
            nextCastTime = Time.time + buffCooldown;
            if (target == null
                || !target.gameObject.activeInHierarchy
                || ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude
                    > EffectiveBuffRange * EffectiveBuffRange)
            {
                return;
            }

            EnemyDamageModifiers modifiers = target.GetComponent<EnemyDamageModifiers>();
            if (modifiers == null)
            {
                modifiers = target.gameObject.AddComponent<EnemyDamageModifiers>();
            }

            modifiers.ApplyModifier(
                buffType,
                EffectiveDamageBonus,
                EffectiveBuffDuration,
                gameObject);
            Cave.Combat.AreaPulseEffect.Create(
                target.transform.position,
                evolutionStage == EnemyEvolutionStage.EvolutionTwo ? 0.78f : 0.62f,
                castColor,
                0.25f);
        }

        private float EffectiveDamageBonus => Mathf.Min(
            maximumAdditiveDamageBonus,
            additiveDamageBonus
                + (evolutionStage >= EnemyEvolutionStage.EvolutionOne ? evolutionOneBonusIncrease : 0f)
                + (evolutionStage >= EnemyEvolutionStage.EvolutionTwo ? evolutionTwoBonusIncrease : 0f));

        private float EffectiveBuffRange => buffRange * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
            ? evolutionTwoRangeMultiplier
            : evolutionStage == EnemyEvolutionStage.EvolutionOne
                ? evolutionOneRangeMultiplier
                : 1f);

        private float EffectiveBuffDuration => buffDuration * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
            ? evolutionTwoDurationMultiplier
            : 1f);

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
        }

        public void Interrupt()
        {
            pendingTarget = null;
            nextCastTime = Mathf.Max(nextCastTime, Time.time + buffCooldown * 0.5f);
        }

        private void OnDisable()
        {
            pendingTarget = null;
            nextCastTime = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = castColor;
            Gizmos.DrawWireSphere(transform.position, buffRange);
        }
    }
}
