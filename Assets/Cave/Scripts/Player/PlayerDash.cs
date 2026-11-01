using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    public enum PlayerDashMode
    {
        None,
        Normal,
        Guard,
        Phase
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerController), typeof(SpinSwordAttack))]
    public sealed class PlayerDash : MonoBehaviour
    {
        [Header("Dash")]
        [SerializeField, Min(0f)] private float staminaCost = 30f;
        [SerializeField, Min(0f)] private float dashSpeed = 18f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.15f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.25f;

        [Header("Guard Dash")]
        [SerializeField, Min(0.01f)] private float guardDashMinimumDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float guardDashMaximumDuration = 0.5f;
        [SerializeField, Min(0f)] private float guardDashInitialStamina = 8f;
        [SerializeField, Min(0f)] private float guardDashStaminaPerSecond = 60f;
        [SerializeField, Min(0.1f)] private float guardDashSpeed = 15f;

        [Header("Offensive Phase Dash")]
        [SerializeField, Min(0.1f)] private float phaseDashDistance = 4.4f;
        [SerializeField, Min(0f)] private float phaseDashStamina = 40f;
        [SerializeField, Min(0f)] private float phaseDashCooldown = 1.25f;
        [SerializeField, Min(1)] private int phaseDashDamage = 1;
        [SerializeField, Min(0.01f)] private float phaseDashDuration = 0.22f;
        [SerializeField, Min(0f)] private float phaseDashBlockRecovery = 0.3f;
        [SerializeField, Min(0f)] private float phaseDashBlockRecoilSpeed = 5.5f;

        [Header("Cross Step Follow-up")]
        [SerializeField, Min(0.05f)] private float crossStepFollowUpWindow = 0.55f;
        [SerializeField, Min(0.1f)] private float crossStepSpeed = 22f;
        [SerializeField, Min(0f)] private float crossStepExtraClearance = 0.6f;
        [SerializeField, Min(0f)] private float crossStepBlockPushback = 0.5f;
        [SerializeField, Min(0f)] private float crossStepBlockRecovery = 0.2f;

        private Rigidbody2D body;
        private SpinSwordAttack stamina;
        private float facingDirection = 1f;
        private float dashDirection;
        private float activeDashSpeed;
        private float dashEndsAt;
        private float nextDashTime;
        private PlayerFlightBash flightBash;
        private PlayerCrowdResponse crowdResponse;
        private PlayerGuardBreak actionGate;
        private PlayerController controller;
        private Collider2D bodyCollider;
        private CommittedAttackCollisionPhasing collisionPhasing;
        private PlayerCrossStep crossStep;
        private SidewaysParryAttack guard;
        private PlayerBrace brace;
        private PlayerHealth playerHealth;
        private PlayerResourceMastery resourceMastery;
        private ChargedAttack chargedAttack;
        private PlayerDashMode dashMode;
        private float dashStartedAt;
        private Vector2 previousPhasePosition;
        private readonly RaycastHit2D[] phaseHitBuffer = new RaycastHit2D[32];
        private readonly System.Collections.Generic.HashSet<Damageable> phaseTargets =
            new System.Collections.Generic.HashSet<Damageable>();
        private bool phaseResolutionBlocked;

        public static bool IsPhaseDashChordActive => GameInput.DashPressed
            && GameInput.ChargeHeld
            && !GameInput.ParryHeld;
        public static bool IsContextualDashChordActive => GameInput.DashPressed
            && (GameInput.ParryHeld || GameInput.ChargeHeld);

        public bool IsDashing { get; private set; }
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public PlayerDashMode CurrentMode => IsDashing ? dashMode : PlayerDashMode.None;
        public float GuardDashMinimumDuration => guardDashMinimumDuration;
        public float GuardDashMaximumDuration => guardDashMaximumDuration;
        public float PhaseDashDistance => phaseDashDistance;
        public float PhaseDashStaminaCost => phaseDashStamina;
        public float PhaseDashCooldown => phaseDashCooldown;

        internal void SetFacingDirection(float direction)
        {
            if (Mathf.Abs(direction) > 0.001f)
            {
                facingDirection = Mathf.Sign(direction);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stamina = GetComponent<SpinSwordAttack>();
            flightBash = GetComponent<PlayerFlightBash>();
            crowdResponse = GetComponent<PlayerCrowdResponse>();
            actionGate = GetComponent<PlayerGuardBreak>();
            controller = GetComponent<PlayerController>();
            bodyCollider = GetComponent<Collider2D>();
            guard = GetComponent<SidewaysParryAttack>();
            brace = GetComponent<PlayerBrace>();
            playerHealth = GetComponent<PlayerHealth>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            chargedAttack = GetComponent<ChargedAttack>();
            collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
            if (collisionPhasing == null)
            {
                collisionPhasing = gameObject.AddComponent<CommittedAttackCollisionPhasing>();
            }

            crossStep = GetComponent<PlayerCrossStep>();
            if (crossStep == null)
            {
                crossStep = gameObject.AddComponent<PlayerCrossStep>();
            }

            crossStep.Configure(
                crossStepFollowUpWindow,
                crossStepSpeed,
                crossStepExtraClearance,
                crossStepBlockPushback,
                crossStepBlockRecovery);
        }

        private void OnEnable()
        {
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerInterrupted;
                playerHealth.Died += HandlePlayerInterrupted;
                playerHealth.Respawned -= HandlePlayerInterrupted;
                playerHealth.Respawned += HandlePlayerInterrupted;
            }
        }

        internal void Configure(ProgressionDifficultySettings settings)
        {
            if (settings == null)
            {
                return;
            }

            staminaCost = settings.DashStaminaCost;
            dashSpeed = settings.DashSpeed;
            dashDuration = settings.DashDuration;
            dashCooldown = settings.DashCooldown;
        }

        private void Update()
        {
            if (brace == null) brace = GetComponent<PlayerBrace>();
            if (brace != null && brace.IsActionLocked)
            {
                return;
            }

            float horizontalInput = GameInput.Horizontal;
            if (!Mathf.Approximately(horizontalInput, 0f))
            {
                facingDirection = Mathf.Sign(horizontalInput);
            }

            if (GameInput.DashPressed)
            {
                PlayerGuardBreak guardBreak = GetComponent<PlayerGuardBreak>();
                if (guardBreak != null && !guardBreak.CanUseCombatActions)
                {
                    if (crossStep != null
                        && crossStep.CanStartDuringCurrentActionLock
                        && crossStep.TryStart())
                    {
                        return;
                    }

                    return;
                }

                // Sustained Guard owns Dash before Heavy, Slip, Cross Step, or
                // the ordinary dodge. Deep Brace is rejected by the action lock
                // above and never leaks into this defensive movement.
                if (guard == null) guard = GetComponent<SidewaysParryAttack>();
                if (guard != null && guard.IsGuardHeld)
                {
                    TryStartGuardDash(horizontalInput);
                    return;
                }

                if (crowdResponse == null)
                {
                    crowdResponse = GetComponent<PlayerCrowdResponse>();
                }

                if (crowdResponse != null
                    && crowdResponse.TrySlip(horizontalInput, facingDirection))
                {
                    return;
                }

                // Cross Step remains ahead of the new Heavy+Dash chord without
                // displacing Defensive Slip's established priority.
                if (crossStep != null && crossStep.TryStart())
                {
                    return;
                }

                if (GameInput.ChargeHeld)
                {
                    TryStartPhaseDash(horizontalInput);
                    return;
                }

                TryStartDash(horizontalInput);
            }
        }

        private void FixedUpdate()
        {
            if (!IsDashing)
            {
                return;
            }

            if (brace == null) brace = GetComponent<PlayerBrace>();
            if (brace != null && brace.IsActionLocked)
            {
                EndDash();
                return;
            }

            if (actionGate != null && !actionGate.CanUseCombatActions)
            {
                EndDash();
                return;
            }

            if (controller != null && controller.IsExternallyMovementLocked)
            {
                EndDash();
                return;
            }

            if (dashMode == PlayerDashMode.Guard)
            {
                float elapsed = Time.time - dashStartedAt;
                if (Time.time >= dashEndsAt)
                {
                    EndDash();
                    return;
                }

                if (elapsed >= guardDashMinimumDuration)
                {
                    if (!GameInput.DashHeld || GameInput.DashReleased)
                    {
                        EndDash();
                        return;
                    }

                    if (guard == null || !guard.IsGuardHeld
                        || !stamina.TrySpendStamina(guardDashStaminaPerSecond * Time.fixedDeltaTime))
                    {
                        EndDash();
                        return;
                    }
                }
            }
            else if (dashMode == PlayerDashMode.Phase)
            {
                ResolvePhaseDashSweep(previousPhasePosition, body.position);
                previousPhasePosition = body.position;
                if (!IsDashing)
                {
                    return;
                }
            }

            if (Time.time >= dashEndsAt)
            {
                EndDash();
                return;
            }

            body.velocity = new Vector2(dashDirection * activeDashSpeed, body.velocity.y);
        }

        public bool TryStartDash(float horizontalInput)
        {
            PlayerBrace brace = GetComponent<PlayerBrace>();
            if (brace != null && brace.IsActionLocked)
            {
                return false;
            }

            if (flightBash == null)
            {
                flightBash = GetComponent<PlayerFlightBash>();
            }

            if (IsDashing
                || (flightBash != null && flightBash.IsBashing)
                || (controller != null && controller.IsExternallyMovementLocked)
                || Time.time < nextDashTime
                || stamina == null
                || !stamina.TrySpendStamina(staminaCost))
            {
                return false;
            }

            dashDirection = Mathf.Approximately(horizontalInput, 0f)
                ? facingDirection
                : Mathf.Sign(horizontalInput);
            facingDirection = dashDirection;
            IsDashing = true;
            dashMode = PlayerDashMode.Normal;
            dashStartedAt = Time.time;
            activeDashSpeed = dashSpeed;
            dashEndsAt = Time.time + dashDuration;
            nextDashTime = dashEndsAt + dashCooldown;
            // Normal Dodge deliberately keeps character bodies solid. Target-only
            // Cross Step owns its own, earned collision exception.
            body.velocity = new Vector2(dashDirection * dashSpeed, body.velocity.y);
            CaveSfx.Play(CaveSfxCue.PlayerDash, 0.75f);
            return true;
        }

        public bool TryStartGuardDash(float horizontalInput)
        {
            if (IsDashing || guard == null || !guard.IsGuardHeld
                || brace != null && brace.IsDeepBrace
                || controller != null && controller.IsExternallyMovementLocked
                || Time.time < nextDashTime
                || stamina == null
                || !stamina.TrySpendStamina(guardDashInitialStamina))
            {
                return false;
            }

            dashDirection = Mathf.Approximately(horizontalInput, 0f)
                ? facingDirection
                : Mathf.Sign(horizontalInput);
            facingDirection = dashDirection;
            IsDashing = true;
            dashMode = PlayerDashMode.Guard;
            dashStartedAt = Time.time;
            activeDashSpeed = guardDashSpeed;
            dashEndsAt = dashStartedAt + Mathf.Max(
                guardDashMinimumDuration,
                guardDashMaximumDuration);
            nextDashTime = dashStartedAt;
            body.velocity = new Vector2(dashDirection * activeDashSpeed, body.velocity.y);
            CaveSfx.Play(CaveSfxCue.PlayerDash, 0.7f);
            return true;
        }

        public bool TryStartPhaseDash(float horizontalInput)
        {
            if (IsDashing
                || guard != null && guard.IsGuardHeld
                || brace != null && brace.IsActionLocked
                || flightBash != null && flightBash.IsBashing
                || chargedAttack != null && chargedAttack.IsAttacking
                || controller != null && controller.IsExternallyMovementLocked
                || Time.time < nextDashTime
                || stamina == null
                || !stamina.TrySpendStamina(phaseDashStamina))
            {
                return false;
            }

            dashDirection = Mathf.Approximately(horizontalInput, 0f)
                ? facingDirection
                : Mathf.Sign(horizontalInput);
            facingDirection = dashDirection;
            chargedAttack?.CancelUncommittedChargeForPhaseDash();
            IsDashing = true;
            dashMode = PlayerDashMode.Phase;
            dashStartedAt = Time.time;
            float resolvedDuration = Mathf.Max(0.01f, phaseDashDuration);
            activeDashSpeed = phaseDashDistance / resolvedDuration;
            dashEndsAt = dashStartedAt + resolvedDuration;
            nextDashTime = dashEndsAt + phaseDashCooldown;
            phaseTargets.Clear();
            previousPhasePosition = body.position;
            collisionPhasing.BeginAlongPath(bodyCollider, Vector2.right * dashDirection, phaseDashDistance);
            body.velocity = new Vector2(dashDirection * activeDashSpeed, body.velocity.y);
            CaveSfx.Play(CaveSfxCue.PlayerDash, 0.95f);
            return true;
        }

        private void ResolvePhaseDashSweep(Vector2 start, Vector2 end)
        {
            Vector2 travel = end - start;
            float distance = travel.magnitude;
            Vector2 direction = distance > 0.0001f
                ? travel / distance
                : Vector2.right * dashDirection;
            Vector2 size = bodyCollider != null ? bodyCollider.bounds.size : Vector2.one;
            int hitCount = Physics2D.BoxCastNonAlloc(
                start,
                size,
                0f,
                direction,
                phaseHitBuffer,
                Mathf.Max(distance, activeDashSpeed * Time.fixedDeltaTime));
            for (int index = 0; index < hitCount && IsDashing; index++)
            {
                Collider2D hit = phaseHitBuffer[index].collider;
                Damageable target = hit != null ? hit.GetComponentInParent<Damageable>() : null;
                if (target == null
                    || !HostileMobQuery.IsRealHostile(target)
                    || !phaseTargets.Add(target))
                {
                    continue;
                }

                if (resourceMastery == null)
                {
                    resourceMastery = GetComponent<PlayerResourceMastery>();
                }
                DamageContext context = resourceMastery != null
                    ? resourceMastery.CreatePlayerDamageContext().WithTraits(
                        DamageTrait.Melee | DamageTrait.Direct | DamageTrait.StaggerNormal)
                    : new DamageContext(gameObject, DamageTrait.Melee | DamageTrait.Direct);
                phaseResolutionBlocked = false;
                target.DamageResolved += ObservePhaseDashResolution;
                target.TakeDamageResolved(phaseDashDamage, context);
                target.DamageResolved -= ObservePhaseDashResolution;
                if (MovementFrenzyGeometryRules.ShouldStopPhaseDash(phaseResolutionBlocked))
                {
                    Vector2 away = (Vector2)transform.position - (Vector2)target.transform.position;
                    EndDash();
                    // EnemyDefenseController already applies its authoritative
                    // recoil. Only protection layers that report a block without
                    // a movement lock need this fallback recovery.
                    if (controller == null || !controller.IsExternallyMovementLocked)
                    {
                        actionGate?.ApplyCombatRecoil(
                            away,
                            phaseDashBlockRecovery,
                            phaseDashBlockRecoilSpeed);
                    }
                }
            }
        }

        private void ObservePhaseDashResolution(DamageContext context, bool blocked, int appliedDamage)
        {
            if (context.Source == gameObject)
            {
                phaseResolutionBlocked |= blocked;
            }
        }

        public bool TryStartSpecialDash(
            float horizontalInput,
            float fallbackFacing,
            float speedMultiplier,
            float duration)
        {
            if (IsDashing || (flightBash != null && flightBash.IsBashing))
            {
                return false;
            }

            dashDirection = Mathf.Approximately(horizontalInput, 0f)
                ? Mathf.Approximately(fallbackFacing, 0f) ? facingDirection : Mathf.Sign(fallbackFacing)
                : Mathf.Sign(horizontalInput);
            facingDirection = dashDirection;
            IsDashing = true;
            dashMode = PlayerDashMode.Normal;
            dashStartedAt = Time.time;
            activeDashSpeed = dashSpeed * Mathf.Max(0.1f, speedMultiplier);
            dashEndsAt = Time.time + Mathf.Max(0.01f, duration);
            nextDashTime = Mathf.Max(nextDashTime, dashEndsAt + dashCooldown);
            // Defensive Slip manages its dedicated collision window itself.
            body.velocity = new Vector2(
                dashDirection * activeDashSpeed,
                body.velocity.y);
            CaveSfx.Play(CaveSfxCue.PlayerDash, 0.9f);
            return true;
        }

        private void EndDash()
        {
            PlayerDashMode endingMode = dashMode;
            IsDashing = false;
            dashMode = PlayerDashMode.None;
            phaseTargets.Clear();
            if (endingMode == PlayerDashMode.Guard)
            {
                nextDashTime = Mathf.Max(nextDashTime, Time.time + dashCooldown);
            }
            collisionPhasing?.End();
            body.velocity = new Vector2(0f, body.velocity.y);
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerInterrupted;
                playerHealth.Respawned -= HandlePlayerInterrupted;
            }
            if (IsDashing && body != null)
            {
                EndDash();
            }
            collisionPhasing?.End();
        }

        private void HandlePlayerInterrupted()
        {
            if (IsDashing)
            {
                EndDash();
            }
        }

        private void OnDestroy()
        {
            collisionPhasing?.End();
        }
    }
}
