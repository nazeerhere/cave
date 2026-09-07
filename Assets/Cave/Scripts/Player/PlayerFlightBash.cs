using System.Collections.Generic;
using Cave.Axioms.Elemental;
using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Progression;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PlayerController))]
    [RequireComponent(typeof(PlayerMana), typeof(PlayerSpecialMode))]
    [RequireComponent(typeof(PlayerSpecialModeUpgradeState))]
    public sealed class PlayerFlightBash : MonoBehaviour
    {
        [Header("Aerial Heavy Ground Slam")]
        [SerializeField, Min(0.05f)] private float aerialHeavyHoldThreshold = 0.25f;
        [SerializeField, Range(5f, 85f)] private float diagonalDiveAngle = 40f;
        [SerializeField, Min(0f)] private float minimumHorizontalDirection = 0.15f;
        [SerializeField, Min(0f)] private float manaCost = 20f;
        [SerializeField, Min(1)] private int damage = 2;
        [FormerlySerializedAs("speed")]
        [SerializeField, Min(0.1f)] private float diveSpeed = 20f;
        [SerializeField, Min(0.1f)] private float maximumDescentDuration = 1.5f;
        [SerializeField, Min(0f)] private float cooldown = 0.35f;
        [SerializeField, Min(0.1f)] private float impactRadius = 2f;
        [SerializeField, Min(0f)] private float knockback = 12f;
        [SerializeField, Min(0f)] private float heavyStaggerDuration = 0.65f;
        [SerializeField, Range(0f, 1f)] private float upwardKnockbackBias = 0.3f;
        [SerializeField, Range(0f, 1f)] private float minimumFloorNormalY = 0.45f;

        [Header("Flight Tier 3 Ground Smash")]
        [SerializeField, Min(0f)] private float tier3AdditionalManaCost;
        [SerializeField, Min(0.1f)] private float shockwaveRadius = 2.5f;
        [SerializeField, Min(1)] private int shockwaveDamage = 2;
        [SerializeField, Min(0f)] private float shockwaveKnockback = 14f;

        [Header("Impact Presentation")]
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField] private Vector2 impactVfxOffset;
        [SerializeField, Min(0.01f)] private float impactVfxLifetime = 1.25f;
        [SerializeField, Min(0.01f)] private float impactVfxScale = 1f;
        [SerializeField] private Color fallbackShockwaveColor =
            new Color(0.35f, 0.85f, 1f, 0.9f);

        [Header("Current Aerial Heavy State (Read Only)")]
        [SerializeField] private bool isPreparingAerialHeavy;
        [SerializeField] private Vector2 committedDiveDirection = Vector2.down;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private readonly ElementalAxiomApplicationReceipt axiomApplicationReceipt = new ElementalAxiomApplicationReceipt();
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private PlayerController playerController;
        private PlayerFlight playerFlight;
        private PlayerHealth playerHealth;
        private PlayerGuardBreak actionGate;
        private PlayerMana playerMana;
        private PlayerSpecialMode specialMode;
        private PlayerSpecialModeUpgradeState upgrades;
        private PlayerResourceMastery resourceMastery;
        private PlayerDash playerDash;
        private PlayerCombatFlow combatFlow;
        private CommittedAttackCollisionPhasing collisionPhasing;
        private float smashEndsAt;
        private float nextSmashTime;
        private float aerialHeavyStartedAt;
        private float preparedHorizontalDirection;
        private DamageContext damageContext;
        private FrenzyBreakActivation frenzyActivation;
        private bool impactTriggered;
        private CollisionDetectionMode2D originalCollisionDetectionMode;

        // Kept for compatibility with existing movement/action locks that referenced Bash.
        public bool IsBashing { get; private set; }
        public bool IsGroundSmashing => IsBashing;
        public bool IsPreparingAerialHeavy => isPreparingAerialHeavy;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            playerController = GetComponent<PlayerController>();
            playerFlight = GetComponent<PlayerFlight>();
            playerHealth = GetComponent<PlayerHealth>();
            actionGate = GetComponent<PlayerGuardBreak>();
            playerMana = GetComponent<PlayerMana>();
            specialMode = GetComponent<PlayerSpecialMode>();
            upgrades = GetComponent<PlayerSpecialModeUpgradeState>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            playerDash = GetComponent<PlayerDash>();
            combatFlow = GetComponent<PlayerCombatFlow>();
            collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
            if (collisionPhasing == null)
            {
                collisionPhasing = gameObject.AddComponent<CommittedAttackCollisionPhasing>();
            }
            originalCollisionDetectionMode = body.collisionDetectionMode;
        }

        private void OnEnable()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= CancelGroundSmash;
                playerHealth.Died += CancelGroundSmash;
            }
        }

        internal void Configure(SpecialModeTier2Settings settings)
        {
            if (settings == null)
            {
                return;
            }

            // Existing Bash tuning assets remain authoritative and are reinterpreted as
            // Ground Smash descent/impact values so current progression data is preserved.
            manaCost = settings.BashManaCost;
            damage = settings.BashDamage;
            diveSpeed = settings.BashSpeed;
            maximumDescentDuration = settings.GroundSmashMaximumDescentDuration;
            cooldown = settings.BashCooldown;
            impactRadius = settings.GroundSmashImpactRadius;
            knockback = settings.BashKnockback;
            heavyStaggerDuration = settings.GroundSmashHeavyStaggerDuration;
            upwardKnockbackBias = settings.GroundSmashUpwardKnockbackBias;
            if (settings.GroundSmashImpactVfxPrefab != null)
            {
                impactVfxPrefab = settings.GroundSmashImpactVfxPrefab;
            }

            impactVfxLifetime = settings.GroundSmashImpactVfxLifetime;
            impactVfxScale = settings.GroundSmashImpactVfxScale;
            tier3AdditionalManaCost = settings.Tier3BashAdditionalManaCost;
            shockwaveRadius = settings.BashShockwaveRadius;
            shockwaveDamage = settings.BashShockwaveDamage;
            shockwaveKnockback = settings.BashShockwaveKnockback;
        }

        public bool HandleAerialHeavyInput()
        {
            if (IsBashing)
            {
                return true;
            }

            if (isPreparingAerialHeavy)
            {
                if (!GameInput.GameplayInputEnabled
                    || playerController == null
                    || (playerController.IsGrounded && body.velocity.y <= 0.1f))
                {
                    CancelAerialHeavyPreparation();
                    return false;
                }

                if (GameInput.ChargeHeld
                    && Time.time - aerialHeavyStartedAt >= aerialHeavyHoldThreshold)
                {
                    Vector2 direction = ResolveDiagonalDiveDirection();
                    CancelAerialHeavyPreparation();
                    TryCommitGroundSmash(direction);
                }
                else if (GameInput.ChargeReleased)
                {
                    CancelAerialHeavyPreparation();
                    TryCommitGroundSmash(Vector2.down);
                }

                return true;
            }

            if (!GameInput.ChargePressed
                || playerController == null
                || (playerController.IsGrounded && body.velocity.y <= 0.1f)
                || (playerDash != null && playerDash.IsDashing)
                || Time.time < nextSmashTime)
            {
                return false;
            }

            isPreparingAerialHeavy = true;
            aerialHeavyStartedAt = Time.time;
            preparedHorizontalDirection = 0f;
            return true;
        }

        public bool TryStartGroundSmash()
        {
            return TryCommitGroundSmash(Vector2.down);
        }

        private bool TryCommitGroundSmash(Vector2 requestedDirection)
        {
            if (IsBashing
                || (playerDash != null && playerDash.IsDashing)
                || Time.time < nextSmashTime
                || playerController == null
                || (playerController.IsGrounded && body.velocity.y <= 0.1f)
                || playerMana == null
                || !playerMana.TrySpendMana(GetCurrentManaCost(), GetCurrentSkillTier()))
            {
                return false;
            }

            if (resourceMastery == null)
            {
                resourceMastery = GetComponent<PlayerResourceMastery>();
            }

            damageContext = resourceMastery != null
                ? resourceMastery.CreateManaDamageContext().WithTraits(
                    DamageTrait.AreaOfEffect | DamageTrait.StaggerHeavy)
                : new DamageContext(gameObject, DamageTrait.AreaOfEffect | DamageTrait.StaggerHeavy);
            hitTargets.Clear();
            axiomApplicationReceipt.Clear();
            impactTriggered = false;
            committedDiveDirection = requestedDirection.sqrMagnitude > 0.001f
                ? requestedDirection.normalized
                : Vector2.down;
            IsBashing = true;
            smashEndsAt = Time.time + maximumDescentDuration;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerFlight?.StopForGroundSmash();
            GetComponent<SpinSwordAttack>()?.StopForCommittedFollowUp();
            collisionPhasing?.Begin(bodyCollider, committedDiveDirection);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.85f);
            GetComponent<PlayerCurseController>()?.NotifyOffensiveCommitment();
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            frenzyActivation = null;
            combatFlow?.TryCommitFrenzyBreak(
                FrenzyBreakAttackKind.GroundSlam,
                out frenzyActivation);
            combatFlow?.NotifyGroundSlamCommitted();
            body.velocity = committedDiveDirection * diveSpeed;
            return true;
        }

        // Preserves external callers while directing all behavior to Ground Smash.
        public bool TryStartBash(float horizontalInput, float fallbackFacingDirection)
        {
            return TryStartGroundSmash();
        }

        private void FixedUpdate()
        {
            if (!IsBashing)
            {
                return;
            }

            frenzyActivation?.KeepAlive();

            if ((actionGate != null && !actionGate.CanUseCombatActions)
                || Time.time >= smashEndsAt)
            {
                EndGroundSmash(false);
                return;
            }

            body.velocity = committedDiveDirection * diveSpeed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryImpactCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryImpactCollision(collision);
        }

        private void TryImpactCollision(Collision2D collision)
        {
            if (!IsBashing
                || impactTriggered
                || collision == null
                || playerController == null
                || !playerController.IsValidGroundCollider(collision.collider))
            {
                return;
            }

            for (int index = 0; index < collision.contactCount; index++)
            {
                ContactPoint2D contact = collision.GetContact(index);
                if (contact.normal.y >= minimumFloorNormalY)
                {
                    TriggerGroundImpact(contact.point);
                    return;
                }
            }
        }

        private void TriggerGroundImpact(Vector2 impactPoint)
        {
            if (!IsBashing || impactTriggered)
            {
                return;
            }

            impactTriggered = true;
            body.velocity = Vector2.zero;
            SpawnImpactVfx(impactPoint);

            bool tier3Owned = upgrades != null && upgrades.IsTier3Owned(SpecialMode.Flight);
            float resolvedRadius = tier3Owned ? shockwaveRadius : impactRadius;
            int resolvedDamage = tier3Owned ? shockwaveDamage : damage;
            float resolvedKnockback = tier3Owned ? shockwaveKnockback : knockback;

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(impactPoint, resolvedRadius);
            foreach (Collider2D overlap in overlaps)
            {
                Damageable damageable = overlap.GetComponentInParent<Damageable>();
                if (damageable == null || !hitTargets.Add(damageable))
                {
                    continue;
                }

                int frenzyDamage = resolvedDamage;
                bool isFrenzyCritical = frenzyActivation != null
                    && frenzyActivation.TryResolveDamage(
                        resolvedDamage,
                        damageable,
                        out frenzyDamage);
                int impactDamage = isFrenzyCritical ? frenzyDamage : resolvedDamage;
                DamageContext impactContext = isFrenzyCritical
                    ? damageContext.WithTraits(DamageTrait.FrenzyCritical)
                    : damageContext;
                int appliedDamage = damageable.TakeDamageResolved(
                    impactDamage,
                    impactContext);
                ElementalAxiomCombatBridge.TryApplyPlayerModeDirectHit(
                    gameObject,
                    damageable,
                    appliedDamage,
                    isFrenzyCritical,
                    impactContext,
                    Time.time,
                    axiomApplicationReceipt);
                if (isFrenzyCritical)
                {
                    Vector2 criticalDirection = (Vector2)damageable.transform.position - impactPoint;
                    frenzyActivation.ApplyImpact(
                        damageable,
                        criticalDirection,
                        appliedDamage);
                }

                if (!damageable.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 direction = (Vector2)damageable.transform.position - impactPoint;
                if (direction.sqrMagnitude <= 0.001f)
                {
                    direction = Vector2.right;
                }

                direction.y = Mathf.Max(direction.y, upwardKnockbackBias);
                damageable.GetComponent<KnockbackReceiver>()
                    ?.ApplyKnockback(direction.normalized * resolvedKnockback);
                damageable.GetComponent<EnemyStagger>()
                    ?.TryStagger(StaggerStrength.Heavy, heavyStaggerDuration);
            }

            EndGroundSmash(true);
        }

        private void SpawnImpactVfx(Vector2 impactPoint)
        {
            if (impactVfxPrefab == null)
            {
                bool tier3Owned = upgrades != null && upgrades.IsTier3Owned(SpecialMode.Flight);
                AreaPulseEffect.Create(
                    impactPoint,
                    tier3Owned ? shockwaveRadius : impactRadius,
                    fallbackShockwaveColor);
                return;
            }

            GameObject vfx = Instantiate(
                impactVfxPrefab,
                impactPoint + impactVfxOffset,
                Quaternion.identity);
            vfx.transform.localScale *= impactVfxScale;
            Destroy(vfx, impactVfxLifetime);
        }

        private Vector2 GetGroundedImpactPoint()
        {
            if (bodyCollider == null)
            {
                return transform.position;
            }

            Bounds bounds = bodyCollider.bounds;
            return new Vector2(bounds.center.x, bounds.min.y);
        }

        private float GetCurrentManaCost()
        {
            bool tier3Owned = upgrades != null && upgrades.IsTier3Owned(SpecialMode.Flight);
            return manaCost + (tier3Owned ? tier3AdditionalManaCost : 0f);
        }

        private int GetCurrentSkillTier()
        {
            if (upgrades == null)
            {
                upgrades = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            return upgrades != null ? upgrades.GetCurrentTier(SpecialMode.Flight) : 1;
        }

        private void EndGroundSmash(bool impacted)
        {
            IsBashing = false;
            frenzyActivation?.Complete();
            frenzyActivation = null;
            impactTriggered = impacted;
            hitTargets.Clear();
            nextSmashTime = Time.time + cooldown;
            committedDiveDirection = Vector2.down;
            collisionPhasing?.End();
            if (body != null)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
                body.collisionDetectionMode = originalCollisionDetectionMode;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= CancelGroundSmash;
            }

            CancelGroundSmash();
            collisionPhasing?.End();
        }

        private void CaptureMeaningfulHorizontalDirection()
        {
            float input = GameInput.Horizontal;
            if (Mathf.Abs(input) >= minimumHorizontalDirection)
            {
                preparedHorizontalDirection = Mathf.Sign(input);
                return;
            }

            if (body != null && Mathf.Abs(body.velocity.x) >= minimumHorizontalDirection)
            {
                preparedHorizontalDirection = Mathf.Sign(body.velocity.x);
            }
        }

        private Vector2 ResolveDiagonalDiveDirection()
        {
            // Direction is intentionally resolved on commitment, not on initial
            // Heavy press. Releasing horizontal input before commitment selects
            // the vertical fallback, and committed dives never steer afterward.
            float currentHorizontal = GameInput.Horizontal;
            if (Mathf.Abs(currentHorizontal) < minimumHorizontalDirection)
            {
                return Vector2.down;
            }

            float radians = diagonalDiveAngle * Mathf.Deg2Rad;
            return new Vector2(
                Mathf.Cos(radians) * Mathf.Sign(currentHorizontal),
                -Mathf.Sin(radians)).normalized;
        }

        public void CancelAerialHeavyPreparation()
        {
            isPreparingAerialHeavy = false;
            aerialHeavyStartedAt = 0f;
            preparedHorizontalDirection = 0f;
        }

        public void CancelGroundSmash()
        {
            CancelAerialHeavyPreparation();
            if (IsBashing && body != null)
            {
                EndGroundSmash(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = fallbackShockwaveColor;
            Gizmos.DrawWireSphere(GetGroundedImpactPoint(), impactRadius);
        }
    }
}
