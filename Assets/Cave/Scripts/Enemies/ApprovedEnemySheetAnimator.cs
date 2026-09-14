using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Presentation-only frame player for the approved Detective, Brute, and
    /// Corrupt Brute sheets. Gameplay components remain the source of truth for
    /// all movement, attack, damage, and crowd-control decisions.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class ApprovedEnemySheetAnimator : MonoBehaviour
    {
        public enum VisualRole
        {
            Detective,
            CorruptDetective,
            Brute,
            CorruptBrute,
            Troll,
            CorruptTroll
        }

        private enum VisualState
        {
            Idle,
            Walk,
            Run,
            Light,
            Heavy,
            Grab,
            Study,
            Utility,
            Hurt,
            Death,
            LegacyFallback
        }

        [Header("Approved Sheet")]
        [SerializeField] private VisualRole visualRole;
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private SpriteRenderer legacyBodyRenderer;
        [SerializeField] private SpriteRenderer[] legacyGrabRenderers;
        [SerializeField] private bool sourceFacesRight = true;

        [Header("Frame Groups")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private Sprite[] runFrames;
        [SerializeField] private Sprite[] lightFrames;
        [SerializeField] private Sprite[] heavyFrames;
        [SerializeField] private Sprite[] grabFrames;
        [SerializeField] private Sprite[] studyFrames;
        [SerializeField] private Sprite[] utilityFrames;
        [SerializeField] private Sprite[] hurtFrames;
        [SerializeField] private Sprite[] deathFrames;

        [Header("Playback")]
        [SerializeField, Min(0.01f)] private float movingThreshold = 0.08f;
        [SerializeField, Min(0.01f)] private float runningThreshold = 2.8f;
        [SerializeField, Min(0.01f)] private float facingThreshold = 0.05f;
        [SerializeField, Min(0.01f)] private float hurtDuration = 0.2f;
        [SerializeField, Min(1f)] private float idleFramesPerSecond = 4f;
        [SerializeField, Min(1f)] private float walkFramesPerSecond = 8f;
        [SerializeField, Min(1f)] private float runFramesPerSecond = 10f;
        [SerializeField, Min(1f)] private float actionFramesPerSecond = 10f;

        private Rigidbody2D body;
        private Damageable damageable;
        private EnemyMeleeCombat melee;
        private BruteControlAbilities bruteControl;
        private EnemyDefenseController defense;
        private DetectiveBrain detectiveBrain;
        private EnemyPoisonShooter poisonShooter;
        private DetectiveStunGrenadeAbility stunGrenade;
        private DetectiveSuppressionZoneAbility suppressionZone;
        private DetectiveCloneAbility cloneAbility;
        private DetectiveSacrificialAttack sacrificialAttack;
        private VisualState currentState;
        private Sprite[] currentFrames;
        private Sprite displayedSprite;
        private float stateStartedAt;
        private float hurtUntil;
        private bool deathTriggered;
        private float facingDirection = 1f;

        private void Awake()
        {
            ResolveComponents();
            currentState = VisualState.Idle;
            currentFrames = idleFrames;
            stateStartedAt = Time.time;
            SetLegacyFallback(false);
        }

        private void Start()
        {
            // Brains may add capabilities in their own Awake methods. One late
            // resolve avoids an execution-order dependency without a recurring scan.
            ResolveComponents();
        }

        private void OnEnable()
        {
            if (damageable != null)
            {
                damageable.DamageResolved += HandleDamageResolved;
                damageable.Died += HandleDied;
            }

            if (damageable == null || damageable.CurrentHealth > 0)
            {
                ResetForSpawn();
            }
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.DamageResolved -= HandleDamageResolved;
                damageable.Died -= HandleDied;
            }
        }

        private void LateUpdate()
        {
            if (visualRenderer == null)
            {
                return;
            }

            VisualState nextState = ResolveState();
            if (nextState != currentState)
            {
                currentState = nextState;
                currentFrames = FramesFor(nextState);
                stateStartedAt = Time.time;
            }

            bool useLegacyFallback = currentState == VisualState.LegacyFallback;
            SetLegacyFallback(useLegacyFallback);
            visualRenderer.enabled = !useLegacyFallback;
            if (useLegacyFallback)
            {
                return;
            }

            UpdateFacing();
            Sprite nextSprite = ResolveFrame();
            if (nextSprite != displayedSprite)
            {
                displayedSprite = nextSprite;
                visualRenderer.sprite = nextSprite;
            }
        }

        /// <summary>Called by the editor installer; never changes gameplay objects.</summary>
        public void Configure(
            VisualRole role,
            SpriteRenderer approvedRenderer,
            SpriteRenderer legacyRenderer,
            SpriteRenderer[] legacyGrabVisuals,
            Sprite[] idle,
            Sprite[] walk,
            Sprite[] run,
            Sprite[] light,
            Sprite[] heavy,
            Sprite[] grab,
            Sprite[] study,
            Sprite[] utility,
            Sprite[] hurt,
            Sprite[] death,
            bool facesRight)
        {
            visualRole = role;
            visualRenderer = approvedRenderer;
            legacyBodyRenderer = legacyRenderer;
            legacyGrabRenderers = legacyGrabVisuals;
            idleFrames = idle;
            walkFrames = walk;
            runFrames = run;
            lightFrames = light;
            heavyFrames = heavy;
            grabFrames = grab;
            studyFrames = study;
            utilityFrames = utility;
            hurtFrames = hurt;
            deathFrames = death;
            sourceFacesRight = facesRight;
            currentState = VisualState.Idle;
            currentFrames = idleFrames;
            displayedSprite = null;
            stateStartedAt = Time.time;
            SetLegacyFallback(false);
        }

        private void ResolveComponents()
        {
            body = body != null ? body : GetComponent<Rigidbody2D>();
            damageable = damageable != null ? damageable : GetComponent<Damageable>();
            melee = melee != null ? melee : GetComponent<EnemyMeleeCombat>();
            bruteControl = bruteControl != null ? bruteControl : GetComponent<BruteControlAbilities>();
            defense = defense != null ? defense : GetComponent<EnemyDefenseController>();
            detectiveBrain = detectiveBrain != null ? detectiveBrain : GetComponent<DetectiveBrain>();
            poisonShooter = poisonShooter != null ? poisonShooter : GetComponentInChildren<EnemyPoisonShooter>(true);
            stunGrenade = stunGrenade != null ? stunGrenade : GetComponent<DetectiveStunGrenadeAbility>();
            suppressionZone = suppressionZone != null ? suppressionZone : GetComponent<DetectiveSuppressionZoneAbility>();
            cloneAbility = cloneAbility != null ? cloneAbility : GetComponent<DetectiveCloneAbility>();
            sacrificialAttack = sacrificialAttack != null ? sacrificialAttack : GetComponent<DetectiveSacrificialAttack>();
        }

        private VisualState ResolveState()
        {
            if (deathTriggered || (damageable != null && damageable.CurrentHealth <= 0))
            {
                return VisualState.Death;
            }

            if (Time.time < hurtUntil)
            {
                return VisualState.Hurt;
            }

            if (visualRole == VisualRole.Detective || visualRole == VisualRole.CorruptDetective)
            {
                if (detectiveBrain != null && detectiveBrain.IsStudying)
                {
                    return VisualState.Study;
                }

                // There is no authored sacrificial-detonation row. Reuse the
                // approved utility presentation rather than flashing legacy art.
                if (sacrificialAttack != null && sacrificialAttack.IsBusy)
                {
                    return VisualState.Utility;
                }

                if ((stunGrenade != null && stunGrenade.IsBusy)
                    || (suppressionZone != null && suppressionZone.IsBusy)
                    || (cloneAbility != null && cloneAbility.IsBusy))
                {
                    return VisualState.Utility;
                }

                if (poisonShooter != null && poisonShooter.IsBusy)
                {
                    return VisualState.Light;
                }
            }

            if (bruteControl != null && bruteControl.IsBusy)
            {
                // Corrupt Brute has approved grapple art. The normal Brute does
                // not; its existing visual is retained during the live grab.
                return visualRole == VisualRole.CorruptBrute
                    ? VisualState.Grab
                    : VisualState.LegacyFallback;
            }

            if ((visualRole == VisualRole.Brute || visualRole == VisualRole.CorruptBrute
                    || visualRole == VisualRole.Troll || visualRole == VisualRole.CorruptTroll)
                && defense != null && defense.IsActivelyBlocking)
            {
                return VisualState.Utility;
            }

            if (melee != null && melee.IsAttacking)
            {
                return melee.CurrentSequenceDecision == EnemyMeleeDecision.Charged
                    ? VisualState.Heavy
                    : VisualState.Light;
            }

            float speed = body != null ? Mathf.Abs(body.velocity.x) : 0f;
            if (speed >= runningThreshold && runFrames != null && runFrames.Length > 0)
            {
                return VisualState.Run;
            }

            return speed >= movingThreshold ? VisualState.Walk : VisualState.Idle;
        }

        private Sprite[] FramesFor(VisualState state)
        {
            switch (state)
            {
                case VisualState.Walk: return NonEmptyOr(walkFrames, idleFrames);
                case VisualState.Run: return NonEmptyOr(runFrames, walkFrames, idleFrames);
                case VisualState.Light: return NonEmptyOr(lightFrames, idleFrames);
                case VisualState.Heavy: return NonEmptyOr(heavyFrames, lightFrames, idleFrames);
                case VisualState.Grab: return NonEmptyOr(grabFrames, idleFrames);
                case VisualState.Study: return NonEmptyOr(studyFrames, idleFrames);
                case VisualState.Utility: return NonEmptyOr(utilityFrames, idleFrames);
                case VisualState.Hurt: return NonEmptyOr(hurtFrames, idleFrames);
                case VisualState.Death: return NonEmptyOr(deathFrames, idleFrames);
                default: return idleFrames;
            }
        }

        private Sprite ResolveFrame()
        {
            if (currentFrames == null || currentFrames.Length == 0)
            {
                return null;
            }

            bool loop = currentState == VisualState.Idle
                || currentState == VisualState.Walk
                || currentState == VisualState.Run
                || currentState == VisualState.Study;
            float framesPerSecond = currentState == VisualState.Idle
                ? idleFramesPerSecond
                : currentState == VisualState.Walk
                    ? walkFramesPerSecond
                    : currentState == VisualState.Run
                        ? runFramesPerSecond
                        : actionFramesPerSecond;
            int index = Mathf.FloorToInt((Time.time - stateStartedAt) * framesPerSecond);
            index = loop ? index % currentFrames.Length : Mathf.Min(index, currentFrames.Length - 1);
            return currentFrames[index];
        }

        private void UpdateFacing()
        {
            if (body != null && Mathf.Abs(body.velocity.x) >= facingThreshold)
            {
                facingDirection = Mathf.Sign(body.velocity.x);
            }

            bool flip = sourceFacesRight ? facingDirection < 0f : facingDirection > 0f;
            if (visualRenderer.flipX != flip)
            {
                visualRenderer.flipX = flip;
            }
        }

        private void SetLegacyFallback(bool enabled)
        {
            if (legacyBodyRenderer != null)
            {
                legacyBodyRenderer.enabled = enabled;
            }

            if (legacyGrabRenderers == null)
            {
                return;
            }

            for (int index = 0; index < legacyGrabRenderers.Length; index++)
            {
                SpriteRenderer legacyRenderer = legacyGrabRenderers[index];
                if (legacyRenderer != null)
                {
                    legacyRenderer.enabled = enabled;
                }
            }
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (!blocked && appliedDamage > 0 && !deathTriggered && damageable != null && damageable.CurrentHealth > 0)
            {
                hurtUntil = Time.time + hurtDuration;
            }
        }

        private void HandleDied()
        {
            deathTriggered = true;
        }

        /// <summary>Clears presentation-only terminal state when the live actor is reused.</summary>
        public void ResetForSpawn()
        {
            deathTriggered = false;
            hurtUntil = 0f;
            currentState = VisualState.Idle;
            currentFrames = idleFrames;
            displayedSprite = null;
            stateStartedAt = Time.time;
            if (visualRenderer != null)
            {
                visualRenderer.enabled = true;
                visualRenderer.color = Color.white;
                if (idleFrames != null && idleFrames.Length > 0) visualRenderer.sprite = idleFrames[0];
            }
            SetLegacyFallback(false);
        }

        private static Sprite[] NonEmptyOr(Sprite[] first, params Sprite[][] fallbacks)
        {
            if (first != null && first.Length > 0)
            {
                return first;
            }

            for (int index = 0; index < fallbacks.Length; index++)
            {
                Sprite[] candidate = fallbacks[index];
                if (candidate != null && candidate.Length > 0)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
