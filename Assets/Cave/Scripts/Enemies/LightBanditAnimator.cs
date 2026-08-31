using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Presentation bridge for the imported Light Bandit controller. It never
    /// moves, damages, or targets; Cave combat components remain authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(Rigidbody2D), typeof(Damageable))]
    public sealed class LightBanditAnimator : MonoBehaviour
    {
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int AirSpeed = Animator.StringToHash("AirSpeed");
        private static readonly int AnimState = Animator.StringToHash("AnimState");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Hurt = Animator.StringToHash("Hurt");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Death = Animator.StringToHash("Death");
        private static readonly int Recover = Animator.StringToHash("Recover");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer visualRenderer;

        [Header("Locomotion Presentation")]
        [SerializeField, Min(0f)] private float movementStartThreshold = 0.12f;
        [SerializeField, Min(0f)] private float movementStopThreshold = 0.06f;
        [SerializeField, Min(0f)] private float facingDirectionThreshold = 0.05f;

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool grounded;
        [SerializeField] private bool moving;
        [SerializeField] private bool deathTriggered;

        private Rigidbody2D body;
        private Damageable damageable;
        private global::Sensor_Bandit groundSensor;
        private LightBanditBrain brain;
        private LightBanditCombat skirmisher;
        private EnemyMeleeCombat melee;
        private PlayerHealth player;
        private bool attackWasActive;
        private LightBanditAction lastSkirmisherAction;
        private float facingDirection = -1f;

        private void Awake()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            visualRenderer = visualRenderer != null ? visualRenderer : GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            damageable = GetComponent<Damageable>();
            groundSensor = GetComponentInChildren<global::Sensor_Bandit>(true);
            brain = GetComponent<LightBanditBrain>();
            skirmisher = GetComponent<LightBanditCombat>();
            melee = GetComponent<EnemyMeleeCombat>();

            // The imported demo reads keyboard/mouse input and writes Rigidbody2D
            // velocity. Disable it only at runtime; do not modify vendor assets.
            global::Bandit packageDemo = GetComponent<global::Bandit>();
            if (packageDemo != null)
            {
                packageDemo.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (damageable != null)
            {
                damageable.DamageResolved += HandleDamageResolved;
                damageable.Died += HandleDied;
            }

            deathTriggered = false;
            attackWasActive = false;
            lastSkirmisherAction = LightBanditAction.None;
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.DamageResolved -= HandleDamageResolved;
                damageable.Died -= HandleDied;
            }
        }

        private void Update()
        {
            if (animator == null || body == null)
            {
                return;
            }

            // LightBanditBrain can add its skirmisher capability during its own
            // Awake. Resolve late without imposing a component-order contract.
            if (brain == null)
            {
                brain = GetComponent<LightBanditBrain>();
            }

            if (skirmisher == null)
            {
                skirmisher = GetComponent<LightBanditCombat>();
            }

            if (melee == null)
            {
                melee = GetComponent<EnemyMeleeCombat>();
            }

            bool wasGrounded = grounded;
            grounded = groundSensor != null
                ? groundSensor.State()
                : Mathf.Abs(body.velocity.y) <= movementStopThreshold;
            animator.SetBool(Grounded, grounded);
            animator.SetFloat(AirSpeed, body.velocity.y);

            if (wasGrounded && !grounded && body.velocity.y > movementStopThreshold)
            {
                animator.SetTrigger(Jump);
            }

            UpdateActionPresentation();
            UpdateLocomotionPresentation();
            UpdateFacing();
        }

        /// <summary>Called only when an authored corrupted replacement is spawned.</summary>
        public void PlayRecover()
        {
            if (animator == null)
            {
                return;
            }

            deathTriggered = false;
            animator.SetTrigger(Recover);
        }

        private void UpdateActionPresentation()
        {
            bool meleeAttacking = melee != null && melee.IsAttacking;
            if (meleeAttacking && !attackWasActive)
            {
                animator.SetTrigger(Attack);
            }

            attackWasActive = meleeAttacking;
            LightBanditAction action = skirmisher != null ? skirmisher.CurrentAction : LightBanditAction.None;
            bool dashAttackStarted = action != lastSkirmisherAction
                && (action == LightBanditAction.DashSlash || action == LightBanditAction.DashBash);
            if (dashAttackStarted)
            {
                animator.SetTrigger(Attack);
            }

            lastSkirmisherAction = action;
        }

        private void UpdateLocomotionPresentation()
        {
            LightBanditAction action = skirmisher != null ? skirmisher.CurrentAction : LightBanditAction.None;
            bool committedMovement = action == LightBanditAction.Backstep
                || action == LightBanditAction.Sidestep
                || action == LightBanditAction.DashFeint
                || action == LightBanditAction.DashSlash
                || action == LightBanditAction.DashBash;
            float horizontalSpeed = Mathf.Abs(body.velocity.x);
            moving = moving
                ? horizontalSpeed > movementStopThreshold || committedMovement
                : horizontalSpeed >= movementStartThreshold || committedMovement;

            if (moving)
            {
                animator.SetInteger(AnimState, 2);
                return;
            }

            bool combatIdle = brain != null && brain.CurrentState != MobBrainState.Patrol
                && brain.CurrentState != MobBrainState.ReturnToPatrol;
            animator.SetInteger(AnimState, combatIdle ? 1 : 0);
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(body.velocity.x) >= facingDirectionThreshold)
            {
                facingDirection = Mathf.Sign(body.velocity.x);
            }
            else
            {
                if (player == null || !player.gameObject.activeInHierarchy)
                {
                    player = FindObjectOfType<PlayerHealth>();
                }

                if (player != null)
                {
                    float towardPlayer = player.transform.position.x - transform.position.x;
                    if (Mathf.Abs(towardPlayer) >= facingDirectionThreshold)
                    {
                        facingDirection = Mathf.Sign(towardPlayer);
                    }
                }
            }

            if (visualRenderer != null)
            {
                // The imported controller's authored +X scale faces left. Flip
                // only the renderer to face right; root physics never flips.
                visualRenderer.flipX = facingDirection > 0f;
            }
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (!blocked && appliedDamage > 0 && !deathTriggered && damageable.CurrentHealth > 0)
            {
                animator.SetTrigger(Hurt);
            }
        }

        private void HandleDied()
        {
            if (deathTriggered)
            {
                return;
            }

            deathTriggered = true;
            animator.SetTrigger(Death);
        }
    }
}
