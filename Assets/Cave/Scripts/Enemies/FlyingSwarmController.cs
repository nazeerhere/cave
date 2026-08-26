using System.Collections.Generic;
using Cave.Audio;
using Cave.Combat;
using Cave.Player;
using Cave.Progression;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Damageable))]
    public sealed class FlyingSwarmController : MonoBehaviour
    {
        [Header("Hover Movement")]
        [SerializeField, Min(0f)] private float hoverHeight = 2.5f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float horizontalFollowRange = 4f;
        [SerializeField, Min(0.1f)] private float verticalFollowRange = 3f;
        [SerializeField, Min(0f)] private float separationRadius = 0.75f;
        [SerializeField, Min(0f)] private float separationStrength = 1.5f;

        [Header("Dive Attack")]
        [SerializeField, Min(0f)] private float attackCooldown = 2.5f;
        [SerializeField, Min(0f)] private float attackTelegraphDuration = 0.4f;
        [SerializeField, Min(0.1f)] private float diveSpeed = 7f;
        [SerializeField, Min(0.01f)] private float diveDuration = 0.4f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.35f;
        [SerializeField, Min(1)] private int contactDamage = 1;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.65f, 0.15f, 1f);

        [Header("References")]
        [SerializeField] private Transform target;

        [Header("Current State (Read Only)")]
        [SerializeField] private FlyingSwarmState currentState = FlyingSwarmState.Hovering;

        private static readonly HashSet<FlyingSwarmController> ActiveControllers =
            new HashSet<FlyingSwarmController>();

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private CommittedAttackCollisionPhasing collisionPhasing;
        private SpriteRenderer[] renderers;
        private Color[] restingColors;
        private Vector3 restingScale;
        private Vector2 diveDirection;
        private float stateEndsAt;
        private float nextAttackTime;
        private float horizontalSlotOffset;
        private float movementMultiplier = 1f;
        private float difficultySpeedMultiplier = 1f;
        private float movementSuspendedUntil;
        private int runtimeDamage;
        private bool damagedDuringDive;

        public float BaseMoveSpeed => moveSpeed;
        public int BaseDamage => contactDamage;

        internal void Configure(StrategicCombatSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            hoverHeight = settings.EyeHoverHeight;
            moveSpeed = settings.EyeMoveSpeed;
            horizontalFollowRange = settings.EyeHorizontalFollowRange;
            verticalFollowRange = settings.EyeVerticalFollowRange;
            separationRadius = settings.EyeSeparationRadius;
            attackCooldown = settings.EyeAttackCooldown;
            attackTelegraphDuration = settings.EyeAttackTelegraphDuration;
            diveSpeed = settings.EyeDiveSpeed;
            diveDuration = settings.EyeDiveDuration;
            contactDamage = settings.EyeContactDamage;
            runtimeDamage = contactDamage;
            nextAttackTime = Time.time + attackCooldown;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveControllers()
        {
            ActiveControllers.Clear();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
            if (collisionPhasing == null)
            {
                collisionPhasing = gameObject.AddComponent<CommittedAttackCollisionPhasing>();
            }
            body.gravityScale = 0f;
            body.freezeRotation = true;
            runtimeDamage = contactDamage;
            horizontalSlotOffset = ((Mathf.Abs(GetInstanceID()) % 7) - 3) * 0.22f;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            restingColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                restingColors[index] = renderers[index].color;
            }

            restingScale = transform.localScale;
        }

        private void OnEnable()
        {
            ActiveControllers.Add(this);
            currentState = FlyingSwarmState.Hovering;
            nextAttackTime = Time.time + attackCooldown;
            RestoreFeedback();
        }

        private void FixedUpdate()
        {
            if (Time.time < movementSuspendedUntil)
            {
                return;
            }

            EnsureTarget();
            if (target == null)
            {
                body.velocity = Vector2.zero;
                return;
            }

            switch (currentState)
            {
                case FlyingSwarmState.Telegraphing:
                    body.velocity = Vector2.Lerp(body.velocity, Vector2.zero, 0.35f);
                    if (Time.time >= stateEndsAt)
                    {
                        BeginDive();
                    }

                    break;
                case FlyingSwarmState.Diving:
                    body.velocity = diveDirection * diveSpeed * difficultySpeedMultiplier;
                    if (Time.time >= stateEndsAt)
                    {
                        BeginRecovery();
                    }

                    break;
                case FlyingSwarmState.Recovering:
                    MoveTowardHoverPoint(0.7f);
                    if (Time.time >= stateEndsAt)
                    {
                        currentState = FlyingSwarmState.Hovering;
                        nextAttackTime = Time.time + attackCooldown;
                        RestoreFeedback();
                    }

                    break;
                default:
                    MoveTowardHoverPoint(1f);
                    if (Time.time >= nextAttackTime && IsWithinAttackRange())
                    {
                        BeginTelegraph();
                    }

                    break;
            }
        }

        private void MoveTowardHoverPoint(float speedScale)
        {
            Vector2 playerPosition = target.position;
            Vector2 desired = playerPosition + new Vector2(horizontalSlotOffset, hoverHeight);
            Vector2 offset = desired - body.position;
            offset.x = Mathf.Clamp(offset.x, -horizontalFollowRange, horizontalFollowRange);
            offset.y = Mathf.Clamp(offset.y, -verticalFollowRange, verticalFollowRange);
            Vector2 desiredVelocity = offset.sqrMagnitude > 0.01f
                ? offset.normalized * moveSpeed * difficultySpeedMultiplier * movementMultiplier * speedScale
                : Vector2.zero;
            desiredVelocity += CalculateSeparation();
            body.velocity = Vector2.Lerp(body.velocity, desiredVelocity, 0.18f);
        }

        private Vector2 CalculateSeparation()
        {
            if (separationRadius <= 0f || separationStrength <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 separation = Vector2.zero;
            foreach (FlyingSwarmController other in ActiveControllers)
            {
                if (other == null || other == this || !other.isActiveAndEnabled)
                {
                    continue;
                }

                Vector2 difference = body.position - other.body.position;
                float distance = difference.magnitude;
                if (distance > 0.001f && distance < separationRadius)
                {
                    separation += difference.normalized * (1f - distance / separationRadius);
                }
            }

            return separation * separationStrength;
        }

        private bool IsWithinAttackRange()
        {
            Vector2 difference = target.position - transform.position;
            return Mathf.Abs(difference.x) <= horizontalFollowRange
                && Mathf.Abs(difference.y) <= verticalFollowRange + hoverHeight;
        }

        private void BeginTelegraph()
        {
            currentState = FlyingSwarmState.Telegraphing;
            stateEndsAt = Time.time + attackTelegraphDuration;
            SetFeedback(telegraphColor, 1.14f);
        }

        private void BeginDive()
        {
            Vector2 direction = target != null
                ? (Vector2)target.position - body.position
                : Vector2.down;
            diveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
            damagedDuringDive = false;
            collisionPhasing?.Begin(bodyCollider);
            currentState = FlyingSwarmState.Diving;
            stateEndsAt = Time.time + diveDuration;
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.55f);
        }

        private void BeginRecovery()
        {
            collisionPhasing?.End();
            currentState = FlyingSwarmState.Recovering;
            stateEndsAt = Time.time + recoveryDuration;
            RestoreFeedback();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHitPlayer(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryHitPlayer(other);
        }

        private void TryHitPlayer(Collider2D other)
        {
            if (currentState != FlyingSwarmState.Diving || damagedDuringDive)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            damagedDuringDive = true;
            DamageContext context = new DamageContext(
                gameObject,
                DamageTrait.Direct | DamageTrait.Melee);
            EnemyDamageModifiers modifiers = GetComponent<EnemyDamageModifiers>();
            int damage = modifiers != null
                ? modifiers.ResolveDamage(runtimeDamage)
                : runtimeDamage;
            playerHealth.TryTakeDamage(damage, context);
            BeginRecovery();
        }

        private void EnsureTarget()
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                return;
            }

            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            target = player != null ? player.transform : null;
        }

        public void SetMovementSpeedMultiplier(float multiplier)
        {
            movementMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetDifficultySpeedMultiplier(float multiplier)
        {
            difficultySpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetRuntimeDamage(int damage)
        {
            runtimeDamage = Mathf.Max(1, damage);
        }

        public void SuspendMovement(float duration)
        {
            movementSuspendedUntil = Mathf.Max(movementSuspendedUntil, Time.time + duration);
        }

        public void ResetForRespawn()
        {
            collisionPhasing?.End();
            currentState = FlyingSwarmState.Hovering;
            movementMultiplier = 1f;
            movementSuspendedUntil = 0f;
            nextAttackTime = Time.time + attackCooldown;
            body.velocity = Vector2.zero;
            RestoreFeedback();
        }

        private void SetFeedback(Color color, float scale)
        {
            transform.localScale = restingScale * scale;
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

        private void OnDisable()
        {
            collisionPhasing?.End();
            ActiveControllers.Remove(this);
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }

            RestoreFeedback();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, separationRadius);
        }

        private enum FlyingSwarmState
        {
            Hovering,
            Telegraphing,
            Diving,
            Recovering
        }
    }
}
