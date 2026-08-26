using System;
using System.Collections;
using System.Collections.Generic;
using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerGuardBreak : MonoBehaviour
    {
        public event Action OffensiveGuardBreakSucceeded;

        [Header("Offensive Guard Break")]
        [SerializeField, Min(0.1f)] private float range = 1.55f;
        [SerializeField, Min(0f)] private float windup = 0.24f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.1f;
        [SerializeField, Min(0f)] private float recovery = 0.4f;
        [SerializeField, Min(0f)] private float cooldown = 0.55f;
        [SerializeField, Min(0f)] private float enemyStaggerDuration = 0.9f;
        [SerializeField, Min(0f)] private float minimumStaggerAfterResistance = 0.55f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("Enemy Guard Break Response")]
        [SerializeField, Min(0f)] private float vulnerabilityDuration = 0.8f;
        [SerializeField, Min(0f)] private float enemyGuardBreakKnockback = 6f;

        [Header("Spin Follow-Up Bash")]
        [SerializeField, Min(0.1f)] private float bashSpeed = 12f;
        [SerializeField, Min(0.1f)] private float bashForwardDistance = 1.35f;
        [SerializeField, Min(0.1f)] private float bashHitRadius = 0.65f;
        [SerializeField, Min(0)] private int bashDamage;
        [SerializeField, Min(0f)] private float bashKnockback = 8f;
        [SerializeField, Min(0f)] private float bashStaggerDuration = 0.65f;
        [SerializeField, Min(0f)] private float bashCooldown = 0.35f;

        [Header("Feedback")]
        [SerializeField] private Color offensiveColor = new Color(1f, 0.25f, 0.08f, 1f);
        [SerializeField] private Color counterColor = new Color(0.3f, 0.95f, 1f, 1f);
        [SerializeField] private GameObject guardBreakVfxPrefab;
        [SerializeField] private Vector2 guardBreakVfxOffset = Vector2.zero;
        [SerializeField] private float guardBreakVfxScale = 1f;

        [Header("Current State (Read Only)")]
        [SerializeField] private bool isGuardBreaking;
        [SerializeField] private bool isBashing;
        [SerializeField] private bool counterWindowAvailable;
        [SerializeField] private float vulnerableUntil;
        [SerializeField] private float externallyInterruptedUntil;

        private ICounterableGuardBreak counterSource;
        private float counterExpiresAt;
        private float nextUseTime;
        private Coroutine attackRoutine;
        private SpinSwordAttack spinAttack;
        private ChargedAttack chargedAttack;
        private PlayerController controller;
        private SidewaysParryAttack parry;
        private PlayerAimDirection aimDirection;
        private PlayerCrowdResponse crowdResponse;
        private PlayerCombatFlow combatFlow;
        private PlayerResourceMastery resourceMastery;
        private Rigidbody2D body;
        private readonly HashSet<Damageable> bashTargets = new HashSet<Damageable>();

        public bool IsGuardBreaking => isGuardBreaking;
        public bool IsBashing => isBashing;
        public bool IsVulnerableFromGuardBreak => Time.time < vulnerableUntil;
        public bool CanUseCombatActions => !isGuardBreaking
            && !IsVulnerableFromGuardBreak
            && Time.time >= externallyInterruptedUntil;

        private void Awake()
        {
            RefreshDependencies();
        }

        private void Start()
        {
            RefreshDependencies();
        }

        private void Update()
        {
            if (counterSource != null && !counterSource.IsGuardBreakInProgress)
            {
                ClearCounterWindow();
            }

            if (counterWindowAvailable && Time.time > counterExpiresAt)
            {
                counterWindowAvailable = false;
            }

            if (!GameInput.GuardBreakPressed)
            {
                return;
            }

            if (counterSource != null)
            {
                if (counterWindowAvailable
                    && counterSource.IsGuardBreakCounterWindowActive
                    && counterSource.TryCounterGuardBreak(gameObject))
                {
                    SeparateAfterCounter(counterSource);
                    ClearCounterWindow();
                    CombatShapeEffect.Create(
                        transform.position + Vector3.up * 0.35f,
                        CombatShape.Diamond,
                        0.72f,
                        counterColor,
                        0.28f);
                    CaveSfx.Play(CaveSfxCue.Whoosh, 0.8f);
                    CaveSfx.Play(CaveSfxCue.Bonus, 0.55f);
                }

                // The same input is consumed by the incoming contest. Pressing it
                // outside the announced window fails instead of becoming a second,
                // unrelated offensive interrupt.
                return;
            }

            if (TryBeginSpinBash())
            {
                return;
            }

            if (crowdResponse == null)
            {
                crowdResponse = GetComponent<PlayerCrowdResponse>();
            }

            if (crowdResponse != null && crowdResponse.TryCounterPush())
            {
                return;
            }

            TryBeginOffensiveGuardBreak();
        }

        public void OpenCounterWindow(ICounterableGuardBreak source, float duration)
        {
            if (source == null || duration <= 0f)
            {
                return;
            }

            counterSource = source;
            counterExpiresAt = Time.time + duration;
            counterWindowAvailable = true;
            CombatShapeEffect.Create(
                transform.position + Vector3.up * 0.35f,
                CombatShape.Chevron,
                0.62f,
                counterColor,
                Mathf.Min(0.28f, duration));
        }

        public void ObserveIncomingGuardBreak(ICounterableGuardBreak source)
        {
            if (source == null)
            {
                return;
            }

            counterSource = source;
            counterWindowAvailable = false;
            counterExpiresAt = 0f;
        }

        public void ApplyEnemyGuardBreak(Vector2 awayFromEnemy)
        {
            ClearCounterWindow();
            SpawnGuardBreakVfx(transform.position);
            vulnerableUntil = Mathf.Max(vulnerableUntil, Time.time + vulnerabilityDuration);
            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }

            Vector2 direction = awayFromEnemy.sqrMagnitude > 0.001f
                ? awayFromEnemy.normalized
                : Vector2.right;
            controller?.ApplyExternalKnockback(
                new Vector2(Mathf.Sign(direction.x), 0.28f).normalized
                    * enemyGuardBreakKnockback,
                vulnerabilityDuration);
        }

        public void ApplyCombatRecoil(Vector2 awayFromDefender, float duration, float speed)
        {
            float resolvedDuration = Mathf.Max(0f, duration);
            vulnerableUntil = Mathf.Max(vulnerableUntil, Time.time + resolvedDuration);
            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }

            Vector2 direction = awayFromDefender.sqrMagnitude > 0.001f
                ? awayFromDefender.normalized
                : Vector2.right;
            controller?.ApplyExternalKnockback(
                new Vector2(Mathf.Sign(direction.x), 0.18f).normalized
                    * Mathf.Max(0f, speed),
                resolvedDuration);
        }

        public void ApplyExternalInterruption(float duration)
        {
            float resolvedDuration = Mathf.Max(0f, duration);
            externallyInterruptedUntil = Mathf.Max(
                externallyInterruptedUntil,
                Time.time + resolvedDuration);
            ClearCounterWindow();
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            EndBashMotion();
            isGuardBreaking = false;
        }

        public void ApplyPlayerStagger(float duration)
        {
            float resolvedDuration = Mathf.Max(0f, duration);
            if (resolvedDuration <= 0f)
            {
                return;
            }

            ApplyExternalInterruption(resolvedDuration);
            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }

            controller?.ApplyExternalControlLock(resolvedDuration);
        }

        private void TryBeginOffensiveGuardBreak()
        {
            if (attackRoutine != null
                || Time.time < nextUseTime
                || (parry != null && parry.IsActive)
                || (spinAttack != null && spinAttack.IsAttacking)
                || (chargedAttack != null
                    && (chargedAttack.IsCharging || chargedAttack.IsAttacking)))
            {
                return;
            }

            attackRoutine = StartCoroutine(PerformOffensiveGuardBreak());
        }

        private bool TryBeginSpinBash()
        {
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            if (combatFlow == null
                || !combatFlow.CanChainSpinToBash
                || attackRoutine != null
                || Time.time < nextUseTime
                || (parry != null && parry.IsActive)
                || (chargedAttack != null
                    && (chargedAttack.IsCharging || chargedAttack.IsAttacking)))
            {
                return false;
            }

            if (!combatFlow.TryConsumeSpinBash())
            {
                return false;
            }

            spinAttack?.StopForCommittedFollowUp();
            attackRoutine = StartCoroutine(PerformSpinBash());
            return true;
        }

        private IEnumerator PerformSpinBash()
        {
            isGuardBreaking = true;
            isBashing = true;
            bashTargets.Clear();
            float facing = ResolveFacingDirection();
            float duration = bashForwardDistance / Mathf.Max(0.1f, bashSpeed);
            controller?.ApplyExternalControlLock(duration);
            CombatShapeEffect.Create(
                transform.position + Vector3.right * facing * bashForwardDistance * 0.5f,
                CombatShape.Arrow,
                Mathf.Max(0.4f, bashForwardDistance * 0.55f),
                offensiveColor,
                duration,
                facing < 0f ? 180f : 0f);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.75f);

            float endsAt = Time.time + duration;
            while (Time.time < endsAt)
            {
                if (body != null)
                {
                    body.velocity = new Vector2(facing * bashSpeed, body.velocity.y);
                }

                ResolveBashHits(facing);
                yield return new WaitForFixedUpdate();
            }

            ResolveBashHits(facing);
            EndBashMotion();
            isGuardBreaking = false;
            nextUseTime = Time.time + bashCooldown;
            attackRoutine = null;
        }

        private void ResolveBashHits(float facing)
        {
            Vector2 center = (Vector2)transform.position
                + Vector2.right * facing * bashHitRadius * 0.7f;
            foreach (Collider2D overlap in Physics2D.OverlapCircleAll(
                center,
                bashHitRadius,
                enemyLayers))
            {
                Damageable target = overlap.GetComponentInParent<Damageable>();
                if (target == null
                    || target.gameObject == gameObject
                    || target.CurrentHealth <= 0
                    || !bashTargets.Add(target))
                {
                    continue;
                }

                if (bashDamage > 0)
                {
                    if (resourceMastery == null)
                    {
                        resourceMastery = GetComponent<PlayerResourceMastery>();
                    }

                    DamageContext context = resourceMastery != null
                        ? resourceMastery.CreatePlayerDamageContext().WithTraits(
                            DamageTrait.Melee | DamageTrait.StaggerNormal)
                        : new DamageContext(
                            gameObject,
                            DamageTrait.Melee | DamageTrait.StaggerNormal);
                    target.TakeDamage(bashDamage, context);
                }

                if (!target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                target.GetComponent<KnockbackReceiver>()?.ApplyKnockback(
                    new Vector2(facing, 0.18f).normalized * bashKnockback);
                target.GetComponent<EnemyStagger>()?.TryStagger(
                    StaggerStrength.Normal,
                    bashStaggerDuration);
                CombatShapeEffect.Create(
                    target.transform.position,
                    CombatShape.Chevron,
                    0.68f,
                    offensiveColor,
                    0.2f,
                    facing < 0f ? 180f : 0f);
            }
        }

        private void EndBashMotion()
        {
            if (isBashing && body != null)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
            }

            isBashing = false;
            bashTargets.Clear();
        }

        private IEnumerator PerformOffensiveGuardBreak()
        {
            isGuardBreaking = true;
            float facing = ResolveFacingDirection();
            Vector2 effectPosition = (Vector2)transform.position + Vector2.right * facing * range * 0.45f;
            CombatShapeEffect.Create(
                effectPosition,
                CombatShape.Wedge,
                range * 0.72f,
                offensiveColor,
                Mathf.Max(0.15f, windup),
                facing < 0f ? 180f : 0f);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.55f);
            yield return new WaitForSeconds(windup);

            ResolveOffensiveHit(facing);
            yield return new WaitForSeconds(activeDuration + recovery);
            isGuardBreaking = false;
            nextUseTime = Time.time + cooldown;
            attackRoutine = null;
        }

        private void ResolveOffensiveHit(float facing)
        {
            Vector2 center = (Vector2)transform.position + Vector2.right * facing * range * 0.55f;
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, range * 0.55f, enemyLayers);
            Damageable closest = null;
            Collider2D closestCollider = null;
            float closestDistance = float.PositiveInfinity;
            foreach (Collider2D overlap in overlaps)
            {
                Damageable candidate = overlap.GetComponentInParent<Damageable>();
                if (candidate == null || candidate.gameObject == gameObject || candidate.CurrentHealth <= 0)
                {
                    continue;
                }

                float horizontal = candidate.transform.position.x - transform.position.x;
                if (Mathf.Sign(horizontal) != Mathf.Sign(facing) && Mathf.Abs(horizontal) > 0.05f)
                {
                    continue;
                }

                float distance = ((Vector2)candidate.transform.position - center).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                    closestCollider = overlap;
                }
            }

            if (closest == null)
            {
                return;
            }

            EnemyDefenseController defense = closest.GetComponent<EnemyDefenseController>();
            EnemyMeleeCombat melee = closest.GetComponent<EnemyMeleeCombat>();
            NecromancerMeleeShield meleeShield = closest.GetComponent<NecromancerMeleeShield>();
            bool brokePosture = defense != null && defense.TryReceiveGuardBreak(gameObject);
            bool caughtWindup = melee != null && melee.IsAttacking && !melee.IsAttackCommitted;
            bool damagedMeleeShield = meleeShield != null
                && meleeShield.TryReceiveGuardBreak();
            if (!brokePosture && !caughtWindup && !damagedMeleeShield)
            {
                return;
            }

            OffensiveGuardBreakSucceeded?.Invoke();

            Vector3 guardBreakImpactPosition = closest.transform.position;

            if (closestCollider != null)
            {
                guardBreakImpactPosition = closestCollider.ClosestPoint(transform.position);
            }

            SpawnGuardBreakVfx(guardBreakImpactPosition);

            if (brokePosture || caughtWindup)
            {
                EnemyStagger stagger = closest.GetComponent<EnemyStagger>();
                stagger?.TryGuardBreakStagger(
                    enemyStaggerDuration,
                    minimumStaggerAfterResistance);
            }
            CombatShapeEffect.Create(
                closest.transform.position,
                CombatShape.Slash,
                0.75f,
                offensiveColor,
                0.22f,
                -20f * facing);
        }

        private void SpawnGuardBreakVfx(Vector3 position)
        {
            if (guardBreakVfxPrefab == null)
            {
                return;
            }

            GameObject vfx = Instantiate(
                guardBreakVfxPrefab,
                position + (Vector3)guardBreakVfxOffset,
                Quaternion.identity);

            vfx.transform.localScale *= guardBreakVfxScale;
        }
        
        private float ResolveFacingDirection()
        {
            if (aimDirection != null)
            {
                aimDirection.ReadDirection();
                return aimDirection.FacingDirection.x;
            }

            float horizontal = GameInput.Horizontal;
            if (!Mathf.Approximately(horizontal, 0f))
            {
                return Mathf.Sign(horizontal);
            }

            SpriteRenderer visual = GetComponentInChildren<SpriteRenderer>();
            return visual != null && visual.flipX ? -1f : 1f;
        }

        private void RefreshDependencies()
        {
            spinAttack = GetComponent<SpinSwordAttack>();
            chargedAttack = GetComponent<ChargedAttack>();
            controller = GetComponent<PlayerController>();
            parry = GetComponent<SidewaysParryAttack>();
            aimDirection = GetComponent<PlayerAimDirection>();
            crowdResponse = GetComponent<PlayerCrowdResponse>();
            combatFlow = GetComponent<PlayerCombatFlow>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            body = GetComponent<Rigidbody2D>();
        }

        private void ClearCounterWindow()
        {
            counterSource = null;
            counterExpiresAt = 0f;
            counterWindowAvailable = false;
        }

        private void SeparateAfterCounter(ICounterableGuardBreak source)
        {
            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }

            MonoBehaviour sourceBehaviour = source as MonoBehaviour;
            if (controller == null || sourceBehaviour == null)
            {
                return;
            }

            float direction = Mathf.Sign(
                transform.position.x - sourceBehaviour.transform.position.x);
            if (Mathf.Approximately(direction, 0f))
            {
                direction = 1f;
            }

            controller.ApplyExternalKnockback(
                new Vector2(direction * 2f, 0.35f),
                0.12f);
        }

        private void OnDisable()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            EndBashMotion();
            isGuardBreaking = false;
            vulnerableUntil = 0f;
            externallyInterruptedUntil = 0f;
            nextUseTime = 0f;
            ClearCounterWindow();
        }
    }
}
