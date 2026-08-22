using System.Collections;
using Cave.Audio;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum EnemyMeleePreset
    {
        Custom,
        Skeleton,
        Brute,
        Troll
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyMeleeCombat : MonoBehaviour, IEnemyInterruptible
    {
        [Header("Targeting")]
        [SerializeField] private PlayerHealth target;
        [SerializeField, Min(0.1f)] private float awarenessRange = 8f;
        [SerializeField] private EnemyMeleePreset probabilityPreset = EnemyMeleePreset.Custom;

        [Header("Basic Melee Attack")]
        [SerializeField, Min(1)] private int basicDamage = 1;
        [SerializeField, Min(0.1f)] private float basicRange = 1.25f;
        [SerializeField, Min(0f)] private float basicWindup = 0.2f;
        [SerializeField, Min(0.01f)] private float basicActiveDuration = 0.1f;
        [SerializeField, Min(0f)] private float basicRecovery = 0.35f;
        [SerializeField, Min(0f)] private float basicCooldown = 0.7f;

        [Header("Charged Melee Attack")]
        [SerializeField, Range(0f, 1f)] private float chargedAttackChance = 0.2f;
        [SerializeField, Min(1)] private int chargedDamage = 3;
        [SerializeField, Min(0.1f)] private float chargedRange = 1.55f;
        [SerializeField, Min(0f)] private float chargedWindup = 0.75f;
        [SerializeField, Min(0.01f)] private float chargedActiveDuration = 0.14f;
        [SerializeField, Min(0f)] private float chargedRecovery = 0.7f;
        [SerializeField, Min(0f)] private float chargedCooldown = 1.35f;
        [SerializeField] private bool chargedAttackIsPiercing;

        [Header("Feint")]
        [SerializeField, Range(0f, 1f)] private float feintChance = 0.15f;
        [SerializeField, Min(0f)] private float feintDelay = 0.3f;

        [Header("Guard Break")]
        [SerializeField, Range(0f, 1f)] private float guardBreakChance = 0.15f;
        [SerializeField, Min(1)] private int guardBreakDamage = 2;
        [SerializeField, Min(0.1f)] private float guardBreakRange = 1.35f;
        [SerializeField, Min(0f)] private float guardBreakWindup = 0.65f;
        [SerializeField, Min(0.01f)] private float guardBreakActiveDuration = 0.12f;
        [SerializeField, Min(0f)] private float guardBreakRecovery = 0.65f;
        [SerializeField, Min(0f)] private float guardBreakCooldown = 1.25f;

        [Header("Telegraph Hooks")]
        [SerializeField] private Color basicTelegraphColor = new Color(0.35f, 0.72f, 1f, 1f);
        [SerializeField] private Color chargedTelegraphColor = new Color(1f, 0.52f, 0.12f, 1f);
        [SerializeField] private Color feintTelegraphColor = new Color(0.68f, 0.68f, 0.74f, 1f);
        [SerializeField] private Color guardBreakTelegraphColor = new Color(1f, 0.15f, 0.08f, 1f);

        private EnemyController movement;
        private EnemyDefenseController defense;
        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private SpriteRenderer[] renderers;
        private Color[] restingColors;
        private Coroutine attackRoutine;
        private float nextActionTime;
        private Vector3 restingScale;
        private int runtimeBasicDamage;
        private int runtimeChargedDamage;
        private int runtimeGuardBreakDamage;

        public bool IsAttacking => attackRoutine != null;

        private void Awake()
        {
            movement = GetComponent<EnemyController>();
            defense = GetComponent<EnemyDefenseController>();
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            GetComponent<EnemyArchetypeProfile>().AddRuntimeArchetype(EnemyArchetype.Melee);
            restingScale = transform.localScale;
            runtimeBasicDamage = basicDamage;
            runtimeChargedDamage = chargedDamage;
            runtimeGuardBreakDamage = guardBreakDamage;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            restingColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                restingColors[index] = renderers[index].color;
            }
        }

        private void Update()
        {
            EnsureTarget();
            if (target == null || attackRoutine != null || Time.time < nextActionTime)
            {
                return;
            }

            Vector2 toTarget = target.transform.position - transform.position;
            if (toTarget.sqrMagnitude > awarenessRange * awarenessRange)
            {
                return;
            }

            defense?.FaceDirection(toTarget.x);
            if ((stagger != null && !stagger.CanAct)
                || (defense != null && !defense.CanStartAttack)
                || Mathf.Abs(toTarget.x) > MaximumAttackRange())
            {
                return;
            }

            attackRoutine = StartCoroutine(PerformChosenAttack());
        }

        private IEnumerator PerformChosenAttack()
        {
            SidewaysParryAttack playerGuard = target.GetComponent<SidewaysParryAttack>();
            bool guardHeld = playerGuard != null && playerGuard.IsGuardHeld;
            float resolvedGuardBreakChance = ResolveGuardBreakChance();
            if (guardHeld
                && resolvedGuardBreakChance > 0f
                && Random.value < resolvedGuardBreakChance)
            {
                yield return PerformAttack(
                    runtimeGuardBreakDamage,
                    guardBreakRange,
                    guardBreakWindup,
                    guardBreakActiveDuration,
                    guardBreakRecovery,
                    guardBreakCooldown,
                    DamageTrait.GuardBreak,
                    guardBreakTelegraphColor,
                    1.18f);
                yield break;
            }

            float resolvedFeintChance = ResolveFeintChance();
            if (guardHeld && resolvedFeintChance > 0f && Random.value < resolvedFeintChance)
            {
                ShowTelegraph(feintTelegraphColor, 0.94f);
                CaveSfx.Play(CaveSfxCue.Whoosh, 0.35f);
                yield return new WaitForSeconds(Mathf.Max(0.01f, basicWindup * 0.65f));
                RestoreFeedback();
                yield return new WaitForSeconds(feintDelay);
                yield return PerformAttack(
                    runtimeBasicDamage,
                    basicRange,
                    basicWindup,
                    basicActiveDuration,
                    basicRecovery,
                    basicCooldown,
                    DamageTrait.Direct,
                    basicTelegraphColor,
                    1.07f);
                yield break;
            }

            PlayerAttackState playerAttackState = target.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttackState != null && playerAttackState.IsActivelyAttacking;
            float contextualChargedChance = playerCommitted
                ? chargedAttackChance
                : chargedAttackChance * 0.35f;
            if (!guardHeld
                && contextualChargedChance > 0f
                && Random.value < contextualChargedChance)
            {
                DamageTrait chargedTraits = chargedAttackIsPiercing
                    ? DamageTrait.Piercing
                    : DamageTrait.Direct;
                yield return PerformAttack(
                    runtimeChargedDamage,
                    chargedRange,
                    chargedWindup,
                    chargedActiveDuration,
                    chargedRecovery,
                    chargedCooldown,
                    chargedTraits,
                    chargedTelegraphColor,
                    1.15f);
                yield break;
            }

            yield return PerformAttack(
                runtimeBasicDamage,
                basicRange,
                basicWindup,
                basicActiveDuration,
                basicRecovery,
                basicCooldown,
                DamageTrait.Direct,
                basicTelegraphColor,
                1.07f);
        }

        private IEnumerator PerformAttack(
            int damage,
            float range,
            float windup,
            float activeDuration,
            float recovery,
            float cooldown,
            DamageTrait extraTraits,
            Color telegraphColor,
            float telegraphScale)
        {
            movement?.SuspendMovement(windup + activeDuration);
            ShowTelegraph(telegraphColor, telegraphScale);
            Cave.Combat.AreaPulseEffect.Create(transform.position, range, telegraphColor, Mathf.Max(0.15f, windup));
            yield return new WaitForSeconds(windup);

            if (target != null && IsTargetWithinRange(range))
            {
                int resolvedDamage = damageModifiers != null
                    ? damageModifiers.ResolveDamage(damage)
                    : damage;
                DamageTrait traits = DamageTrait.Melee | extraTraits;
                target.TryTakeDamage(resolvedDamage, new DamageContext(gameObject, traits));
                CaveSfx.Play(CaveSfxCue.Whoosh, 0.65f);
            }

            yield return new WaitForSeconds(activeDuration);
            RestoreFeedback();
            yield return new WaitForSeconds(recovery);
            nextActionTime = Time.time + cooldown;
            attackRoutine = null;
        }

        private bool IsTargetWithinRange(float range)
        {
            Vector2 delta = target.transform.position - transform.position;
            return Mathf.Abs(delta.x) <= range && Mathf.Abs(delta.y) <= range * 1.15f;
        }

        private float MaximumAttackRange()
        {
            return Mathf.Max(basicRange, chargedRange, guardBreakRange);
        }

        private float ResolveFeintChance()
        {
            switch (probabilityPreset)
            {
                case EnemyMeleePreset.Skeleton:
                    return 0f;
                case EnemyMeleePreset.Brute:
                    return 0.15f;
                case EnemyMeleePreset.Troll:
                    return 0.2f;
                default:
                    return feintChance;
            }
        }

        private float ResolveGuardBreakChance()
        {
            switch (probabilityPreset)
            {
                case EnemyMeleePreset.Skeleton:
                    return 0f;
                case EnemyMeleePreset.Brute:
                    return 0.15f;
                case EnemyMeleePreset.Troll:
                    return 0.25f;
                default:
                    return guardBreakChance;
            }
        }

        private void EnsureTarget()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                target = FindObjectOfType<PlayerHealth>();
            }
        }

        public void SetRuntimeDamageScale(float multiplier)
        {
            float scale = Mathf.Max(0f, multiplier);
            runtimeBasicDamage = Mathf.Max(1, Mathf.RoundToInt(basicDamage * scale));
            runtimeChargedDamage = Mathf.Max(1, Mathf.RoundToInt(chargedDamage * scale));
            runtimeGuardBreakDamage = Mathf.Max(1, Mathf.RoundToInt(guardBreakDamage * scale));
        }

        private void ShowTelegraph(Color color, float scale)
        {
            transform.localScale = new Vector3(
                restingScale.x * scale,
                restingScale.y * scale,
                restingScale.z);
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = color;
                }
            }
        }

        private void RestoreFeedback()
        {
            transform.localScale = restingScale;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].color = restingColors[index];
                }
            }
        }

        public void Interrupt()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            RestoreFeedback();
            nextActionTime = Mathf.Max(nextActionTime, Time.time + basicRecovery);
        }

        private void OnDisable()
        {
            Interrupt();
            nextActionTime = 0f;
        }
    }
}
