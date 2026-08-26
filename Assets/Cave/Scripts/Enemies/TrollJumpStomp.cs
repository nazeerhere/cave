using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum TrollJumpStompState
    {
        Locked,
        Ready,
        Windup,
        Airborne,
        Impact,
        Recovering
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(EnemyMeleeCombat))]
    public sealed class TrollJumpStomp : MonoBehaviour,
        IEnemyInterruptible,
        IEnemyInterruptPolicy,
        IEnemySkillEvolutionReceiver
    {
        [Header("Evolution Unlock")]
        [SerializeField] private EnemyEvolutionStage minimumEvolutionStage =
            EnemyEvolutionStage.EvolutionTwo;

        [Header("Contextual Usage")]
        [SerializeField, Min(0.1f)] private float minimumUseDistance = 2.25f;
        [SerializeField, Min(0.1f)] private float maximumUseDistance = 6f;
        [SerializeField, Range(0f, 1f)] private float normalUseChance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float guardingPlayerUseChance = 0.65f;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.35f;
        [SerializeField, Min(0f)] private float cooldown = 5.5f;

        [Header("Jump Commitment")]
        [SerializeField, Min(0.05f)] private float jumpWindup = 0.65f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 4f;
        [SerializeField, Min(0.1f)] private float jumpTravelDuration = 0.75f;
        [SerializeField, Min(0.1f)] private float maximumHorizontalTravel = 5f;
        [SerializeField, Min(0f)] private float landingRecovery = 1f;

        [Header("Landing Validation")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.1f)] private float groundSearchHeight = 6f;
        [SerializeField, Min(0.1f)] private float groundSearchDistance = 12f;

        [Header("Impact")]
        [SerializeField, Min(0.1f)] private float impactRadius = 3f;
        [SerializeField, Min(1)] private int impactDamage = 4;
        [SerializeField, Min(0f)] private float impactKnockback = 14f;
        [SerializeField, Min(0f)] private float playerControlLockDuration = 0.25f;
        [SerializeField] private LayerMask playerLayers = ~0;

        [Header("VFX")]
        [SerializeField] private Color windupColor = new Color(1f, 0.35f, 0.08f, 0.9f);
        [SerializeField] private Color landingColor = new Color(1f, 0.7f, 0.12f, 1f);
        [SerializeField, Range(0.5f, 1f)] private float crouchScaleY = 0.72f;
        [SerializeField, Min(1f)] private float landingScale = 1.18f;

        [Header("Current State (Read Only)")]
        [SerializeField] private TrollJumpStompState currentState = TrollJumpStompState.Locked;
        [SerializeField] private Vector2 committedLandingPosition;
        [SerializeField] private bool isCommitted;
        [SerializeField] private bool isInterruptible = true;

        private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private EnemyController movement;
        private EnemyMeleeCombat meleeCombat;
        private EnemyDefenseController defense;
        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private CommittedAttackCollisionPhasing collisionPhasing;
        private PlayerHealth target;
        private Coroutine stompRoutine;
        private EnemyEvolutionStage evolutionStage;
        private Vector3 restingScale;
        private float restingGravityScale;
        private float nextDecisionTime;
        private float nextStompTime;
        private float runtimeDamageScale = 1f;
        private bool brainControlled;

        public bool IsStomping => stompRoutine != null;
        public TrollJumpStompState CurrentState => currentState;
        public bool IsUnlocked => evolutionStage >= minimumEvolutionStage;
        public bool IsReady => IsUnlocked
            && stompRoutine == null
            && Time.time >= nextStompTime
            && (stagger == null || stagger.CanAct)
            && (defense == null || defense.CanStartAttack)
            && meleeCombat.IsAvailableForMajorAbility;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            movement = GetComponent<EnemyController>();
            meleeCombat = GetComponent<EnemyMeleeCombat>();
            defense = GetComponent<EnemyDefenseController>();
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
            if (collisionPhasing == null)
            {
                collisionPhasing = gameObject.AddComponent<CommittedAttackCollisionPhasing>();
            }
            restingScale = transform.localScale;
            restingGravityScale = body.gravityScale;
        }

        private void Start()
        {
            defense = GetComponent<EnemyDefenseController>();
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
        }

        private void Update()
        {
            RefreshCommitmentDebug();
            if (brainControlled || Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            EnsureTarget();
            if (!CanUse(target))
            {
                return;
            }

            SidewaysParryAttack guard = target.GetComponent<SidewaysParryAttack>();
            float useChance = guard != null && guard.IsGuardHeld
                ? guardingPlayerUseChance
                : normalUseChance;
            if (Random.value > useChance)
            {
                return;
            }

            TryUse(target);
        }

        public bool CanUse(PlayerHealth requestedTarget)
        {
            if (!IsReady || requestedTarget == null || !requestedTarget.gameObject.activeInHierarchy)
            {
                return false;
            }

            float horizontalDistance = Mathf.Abs(requestedTarget.transform.position.x - transform.position.x);
            return horizontalDistance >= minimumUseDistance
                && horizontalDistance <= maximumUseDistance
                && TryResolveLandingPosition(requestedTarget.transform.position, out _);
        }

        public bool TryUse(PlayerHealth requestedTarget)
        {
            if (!IsReady
                || requestedTarget == null
                || !TryResolveLandingPosition(
                    requestedTarget.transform.position,
                    out Vector2 landingPosition))
            {
                return false;
            }

            float horizontalDistance = Mathf.Abs(requestedTarget.transform.position.x - transform.position.x);
            if (horizontalDistance < minimumUseDistance || horizontalDistance > maximumUseDistance)
            {
                return false;
            }

            target = requestedTarget;
            committedLandingPosition = landingPosition;
            stompRoutine = StartCoroutine(PerformStomp());
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        public bool CanBeInterruptedBy(StaggerStrength strength)
        {
            return currentState != TrollJumpStompState.Airborne
                && currentState != TrollJumpStompState.Impact
                && currentState != TrollJumpStompState.Recovering;
        }

        private IEnumerator PerformStomp()
        {
            float totalCommitment = jumpWindup + jumpTravelDuration + landingRecovery;
            meleeCombat.SuspendForMajorAbility(totalCommitment);
            defense?.SuspendForMajorAbility(totalCommitment);
            movement?.SuspendMovement(totalCommitment);

            currentState = TrollJumpStompState.Windup;
            body.velocity = Vector2.zero;
            transform.localScale = new Vector3(
                restingScale.x,
                restingScale.y * crouchScaleY,
                restingScale.z);
            AreaPulseEffect.Create(transform.position, 0.8f, windupColor, jumpWindup);
            AreaPulseEffect.Create(committedLandingPosition, impactRadius, windupColor, jumpWindup);
            yield return new WaitForSeconds(jumpWindup);

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            currentState = TrollJumpStompState.Airborne;
            collisionPhasing?.Begin(bodyCollider);
            transform.localScale = restingScale;
            Vector2 start = body.position;
            restingGravityScale = body.gravityScale;
            body.gravityScale = 0f;
            body.velocity = Vector2.zero;

            float elapsed = 0f;
            while (elapsed < jumpTravelDuration)
            {
                elapsed += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(elapsed / jumpTravelDuration);
                Vector2 linearPosition = Vector2.Lerp(start, committedLandingPosition, progress);
                linearPosition.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                body.MovePosition(linearPosition);
                yield return new WaitForFixedUpdate();
            }

            body.MovePosition(committedLandingPosition);
            body.velocity = Vector2.zero;
            body.gravityScale = restingGravityScale;
            currentState = TrollJumpStompState.Impact;
            ResolveImpact();
            collisionPhasing?.End();

            transform.localScale = restingScale * landingScale;
            yield return new WaitForSeconds(0.12f);
            transform.localScale = restingScale;

            currentState = TrollJumpStompState.Recovering;
            yield return new WaitForSeconds(Mathf.Max(0f, landingRecovery - 0.12f));
            nextStompTime = Time.time + cooldown;
            currentState = TrollJumpStompState.Ready;
            stompRoutine = null;
        }

        private void ResolveImpact()
        {
            damagedPlayers.Clear();
            AreaPulseEffect.Create(body.position, impactRadius, landingColor, 0.32f);
            AreaPulseEffect.Create(body.position, impactRadius * 0.45f, Color.white, 0.18f);
            CombatShapeEffect.Create(
                body.position + Vector2.left * impactRadius * 0.35f,
                CombatShape.Arrow,
                impactRadius * 0.32f,
                landingColor,
                0.24f,
                180f);
            CombatShapeEffect.Create(
                body.position + Vector2.right * impactRadius * 0.35f,
                CombatShape.Arrow,
                impactRadius * 0.32f,
                landingColor,
                0.24f);

            foreach (Collider2D overlap in Physics2D.OverlapCircleAll(
                body.position,
                impactRadius,
                playerLayers))
            {
                PlayerHealth player = overlap.GetComponentInParent<PlayerHealth>();
                if (player == null || !damagedPlayers.Add(player))
                {
                    continue;
                }

                int scaledDamage = Mathf.Max(1, Mathf.RoundToInt(impactDamage * runtimeDamageScale));
                int resolvedDamage = damageModifiers != null
                    ? damageModifiers.ResolveDamage(scaledDamage)
                    : scaledDamage;
                bool hit = player.TryTakeDamage(
                    resolvedDamage,
                    new DamageContext(
                        gameObject,
                        DamageTrait.Melee | DamageTrait.AreaOfEffect));
                if (!hit || impactKnockback <= 0f)
                {
                    continue;
                }

                Vector2 outward = (Vector2)player.transform.position - body.position;
                if (outward.sqrMagnitude <= 0.001f)
                {
                    outward = Vector2.right;
                }

                outward = new Vector2(outward.normalized.x, Mathf.Max(0.45f, outward.normalized.y));
                PlayerController controller = player.GetComponent<PlayerController>();
                controller?.ApplyExternalKnockback(
                    outward.normalized * impactKnockback,
                    playerControlLockDuration);
            }
        }

        private bool TryResolveLandingPosition(Vector2 requestedTarget, out Vector2 landingPosition)
        {
            float horizontalOffset = Mathf.Clamp(
                requestedTarget.x - body.position.x,
                -maximumHorizontalTravel,
                maximumHorizontalTravel);
            float targetX = body.position.x + horizontalOffset;
            Vector2 rayOrigin = new Vector2(
                targetX,
                Mathf.Max(body.position.y, requestedTarget.y) + groundSearchHeight);
            RaycastHit2D[] groundHits = Physics2D.RaycastAll(
                rayOrigin,
                Vector2.down,
                groundSearchDistance,
                groundLayers);
            foreach (RaycastHit2D hit in groundHits)
            {
                if (!IsValidEnvironmentCollider(hit.collider))
                {
                    continue;
                }

                float feetOffset = body.position.y - bodyCollider.bounds.min.y;
                Vector2 candidate = new Vector2(targetX, hit.point.y + feetOffset);
                if (HasBlockingGeometry(body.position, candidate)
                    || !HasClearJumpArc(body.position, candidate))
                {
                    continue;
                }

                landingPosition = candidate;
                return true;
            }

            landingPosition = body.position;
            return false;
        }

        private bool HasClearJumpArc(Vector2 start, Vector2 destination)
        {
            Vector2 checkSize = bodyCollider.bounds.size * 0.7f;
            for (int index = 1; index < 10; index++)
            {
                float progress = index / 10f;
                Vector2 checkPosition = Vector2.Lerp(start, destination, progress);
                checkPosition.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                foreach (Collider2D overlap in Physics2D.OverlapBoxAll(
                    checkPosition,
                    checkSize,
                    0f,
                    groundLayers))
                {
                    if (IsValidEnvironmentCollider(overlap))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool HasBlockingGeometry(Vector2 start, Vector2 destination)
        {
            Vector2 clearance = Vector2.up * Mathf.Max(0.15f, bodyCollider.bounds.extents.y * 0.35f);
            foreach (RaycastHit2D hit in Physics2D.LinecastAll(
                start + clearance,
                destination + clearance,
                groundLayers))
            {
                if (IsValidEnvironmentCollider(hit.collider))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidEnvironmentCollider(Collider2D candidate)
        {
            return candidate != null
                && !candidate.isTrigger
                && !candidate.transform.IsChildOf(transform)
                && candidate.GetComponentInParent<PlayerHealth>() == null
                && candidate.GetComponentInParent<Damageable>() == null;
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
            runtimeDamageScale = Mathf.Max(0f, multiplier);
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
            if (evolutionStage < minimumEvolutionStage && stompRoutine != null)
            {
                Interrupt();
            }

            if (stompRoutine == null)
            {
                currentState = evolutionStage >= minimumEvolutionStage
                    ? TrollJumpStompState.Ready
                    : TrollJumpStompState.Locked;
            }
        }

        public void Interrupt()
        {
            if (stompRoutine != null)
            {
                StopCoroutine(stompRoutine);
                stompRoutine = null;
            }

            RestorePhysicalState();
            nextStompTime = Mathf.Max(nextStompTime, Time.time + landingRecovery);
            currentState = evolutionStage >= minimumEvolutionStage
                ? TrollJumpStompState.Ready
                : TrollJumpStompState.Locked;
        }

        private void RefreshCommitmentDebug()
        {
            isInterruptible = CanBeInterruptedBy(StaggerStrength.Normal);
            isCommitted = !isInterruptible && stompRoutine != null;
        }

        private void RestorePhysicalState()
        {
            collisionPhasing?.End();
            transform.localScale = restingScale;
            if (body != null)
            {
                body.gravityScale = restingGravityScale;
            }
        }

        private void OnDisable()
        {
            Interrupt();
            nextDecisionTime = 0f;
            damagedPlayers.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = landingColor;
            Gizmos.DrawWireSphere(
                Application.isPlaying ? (Vector3)committedLandingPosition : transform.position,
                impactRadius);
        }
    }
}
