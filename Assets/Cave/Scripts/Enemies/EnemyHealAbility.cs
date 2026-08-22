using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyHealAbility : MonoBehaviour, IEnemyInterruptible
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

        private Damageable self;
        private EnemyStagger stagger;
        private float nextHealTime;
        private float castCompletesAt;
        private Damageable pendingTarget;

        public bool IsCasting => pendingTarget != null;

        public bool HasEligibleTarget()
        {
            return EnemySupportTargeting.FindLowestHealthTarget(
                transform.position,
                healRange,
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
            GetComponent<EnemyArchetypeProfile>().AddRuntimeArchetype(EnemyArchetype.Support);
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

            if (Time.time < nextHealTime || (stagger != null && !stagger.CanAct))
            {
                return;
            }

            Damageable target = EnemySupportTargeting.FindLowestHealthTarget(
                transform.position,
                healRange,
                allyLayers,
                self,
                canHealSelf,
                preferNonSupportTargets,
                targetHealthThreshold);
            if (target == null)
            {
                nextHealTime = Time.time + 0.25f;
                return;
            }

            pendingTarget = target;
            castCompletesAt = Time.time + castWindup;
            Cave.Combat.AreaPulseEffect.Create(transform.position, 0.72f, healColor, castWindup);
        }

        private void CompleteHeal()
        {
            Damageable target = pendingTarget;
            pendingTarget = null;
            nextHealTime = Time.time + healCooldown;
            if (target == null
                || !target.gameObject.activeInHierarchy
                || ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude
                    > healRange * healRange)
            {
                return;
            }

            if (target.RestoreHealth(healAmount))
            {
                Cave.Combat.AreaPulseEffect.Create(target.transform.position, 0.68f, healColor, 0.3f);
            }
        }

        public void Interrupt()
        {
            pendingTarget = null;
            nextHealTime = Mathf.Max(nextHealTime, Time.time + healCooldown * 0.5f);
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
