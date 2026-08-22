using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyDamageBuffAbility : MonoBehaviour, IEnemyInterruptible
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

        private Damageable self;
        private EnemyStagger stagger;
        private float nextCastTime;
        private float castCompletesAt;
        private Damageable pendingTarget;
        private EnemyHealAbility healAbility;
        private SwarmCaller swarmCaller;

        public bool IsCasting => pendingTarget != null;

        private void Awake()
        {
            self = GetComponent<Damageable>();
            stagger = GetComponent<EnemyStagger>();
            healAbility = GetComponent<EnemyHealAbility>();
            swarmCaller = GetComponent<SwarmCaller>();
            GetComponent<EnemyArchetypeProfile>().AddRuntimeArchetype(EnemyArchetype.Support);
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

            if (Time.time < nextCastTime || (stagger != null && !stagger.CanAct))
            {
                return;
            }

            if ((healAbility != null && (healAbility.IsCasting || healAbility.HasEligibleTarget()))
                || (swarmCaller != null && swarmCaller.ShouldPrioritizeSummon))
            {
                nextCastTime = Time.time + 0.15f;
                return;
            }

            Damageable target = EnemySupportTargeting.FindUnbuffedDamageTarget(
                transform.position,
                buffRange,
                allyLayers,
                self,
                canBuffSelf,
                preferNonSupportTargets,
                buffType);
            if (target == null)
            {
                nextCastTime = Time.time + 0.25f;
                return;
            }

            pendingTarget = target;
            castCompletesAt = Time.time + castWindup;
            Cave.Combat.AreaPulseEffect.Create(transform.position, 0.7f, castColor, castWindup);
        }

        private void CompleteCast()
        {
            Damageable target = pendingTarget;
            pendingTarget = null;
            nextCastTime = Time.time + buffCooldown;
            if (target == null
                || !target.gameObject.activeInHierarchy
                || ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude
                    > buffRange * buffRange)
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
                additiveDamageBonus,
                buffDuration,
                gameObject);
            Cave.Combat.AreaPulseEffect.Create(target.transform.position, 0.62f, castColor, 0.25f);
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
