using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Presentation-only playback for the approved individual Eye frame exports.
    /// EyeBrain remains authoritative for every movement, targeting, attack, and
    /// damage decision.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EyeBrain), typeof(Damageable))]
    public sealed class EyeVisualAnimator : MonoBehaviour
    {
        private enum VisualState { Idle, Hover, Drift, Cast, Attack, Special, Hurt, Death }

        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private bool corruptVariant;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] hoverFrames;
        [SerializeField] private Sprite[] driftFrames;
        [SerializeField] private Sprite[] castFrames;
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField] private Sprite[] specialFrames;
        [SerializeField] private Sprite[] hurtFrames;
        [SerializeField] private Sprite[] deathFrames;
        [SerializeField, Min(1f)] private float idleFramesPerSecond = 5f;
        [SerializeField, Min(1f)] private float movingFramesPerSecond = 9f;
        [SerializeField, Min(1f)] private float actionFramesPerSecond = 10f;
        [SerializeField, Min(0.01f)] private float hurtDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float movingThreshold = 0.08f;

        private EyeBrain eye;
        private Damageable damageable;
        private Rigidbody2D body;
        private VisualState currentState;
        private Sprite[] currentFrames;
        private Sprite displayedSprite;
        private float stateStartedAt;
        private float hurtUntil;
        private bool deathTriggered;

        public bool IsCorruptVariant => corruptVariant;

        private void Awake()
        {
            eye = GetComponent<EyeBrain>();
            damageable = GetComponent<Damageable>();
            body = GetComponent<Rigidbody2D>();
            if (visualRenderer == null) visualRenderer = GetComponent<SpriteRenderer>();
            ResetForSpawn();
        }

        private void OnEnable()
        {
            damageable.DamageResolved += HandleDamageResolved;
            damageable.Died += HandleDied;
            if (damageable.CurrentHealth > 0) ResetForSpawn();
        }

        private void OnDisable()
        {
            if (damageable == null) return;
            damageable.DamageResolved -= HandleDamageResolved;
            damageable.Died -= HandleDied;
        }

        private void LateUpdate()
        {
            if (visualRenderer == null) return;
            VisualState next = ResolveState();
            if (next != currentState)
            {
                currentState = next;
                currentFrames = FramesFor(next);
                stateStartedAt = Time.time;
            }

            Sprite nextSprite = ResolveFrame();
            if (nextSprite != null && nextSprite != displayedSprite)
            {
                displayedSprite = nextSprite;
                visualRenderer.sprite = nextSprite;
            }
        }

        /// <summary>Editor-only setup entry point; it changes presentation references only.</summary>
        public void Configure(
            SpriteRenderer renderer,
            bool isCorrupt,
            Sprite[] idle,
            Sprite[] hover,
            Sprite[] drift,
            Sprite[] cast,
            Sprite[] attack,
            Sprite[] special,
            Sprite[] hurt,
            Sprite[] death)
        {
            visualRenderer = renderer;
            corruptVariant = isCorrupt;
            idleFrames = idle;
            hoverFrames = hover;
            driftFrames = drift;
            castFrames = cast;
            attackFrames = attack;
            specialFrames = special;
            hurtFrames = hurt;
            deathFrames = death;
            ResetForSpawn();
        }

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
                if (idleFrames != null && idleFrames.Length > 0) visualRenderer.sprite = idleFrames[0];
            }
        }

        private VisualState ResolveState()
        {
            if (deathTriggered || (damageable != null && damageable.CurrentHealth <= 0)) return VisualState.Death;
            if (Time.time < hurtUntil) return VisualState.Hurt;
            switch (eye != null ? eye.PresentationState : EyePresentationState.Hover)
            {
                case EyePresentationState.Gaze: return VisualState.Cast;
                case EyePresentationState.Dive: return VisualState.Attack;
                case EyePresentationState.Special: return VisualState.Special;
                case EyePresentationState.Recover: return VisualState.Hover;
                default:
                    return body != null && body.velocity.sqrMagnitude >= movingThreshold * movingThreshold
                        ? VisualState.Drift
                        : VisualState.Hover;
            }
        }

        private Sprite[] FramesFor(VisualState state)
        {
            switch (state)
            {
                case VisualState.Hover: return NonEmptyOr(hoverFrames, idleFrames);
                case VisualState.Drift: return NonEmptyOr(driftFrames, hoverFrames, idleFrames);
                case VisualState.Cast: return NonEmptyOr(castFrames, hoverFrames, idleFrames);
                case VisualState.Attack: return NonEmptyOr(attackFrames, hoverFrames, idleFrames);
                case VisualState.Special: return NonEmptyOr(specialFrames, castFrames, hoverFrames, idleFrames);
                case VisualState.Hurt: return NonEmptyOr(hurtFrames, hoverFrames, idleFrames);
                case VisualState.Death: return NonEmptyOr(deathFrames, hurtFrames, idleFrames);
                default: return idleFrames;
            }
        }

        private Sprite ResolveFrame()
        {
            if (currentFrames == null || currentFrames.Length == 0) return null;
            bool loop = currentState == VisualState.Idle || currentState == VisualState.Hover || currentState == VisualState.Drift;
            float fps = loop
                ? (currentState == VisualState.Idle ? idleFramesPerSecond : movingFramesPerSecond)
                : actionFramesPerSecond;
            int index = Mathf.FloorToInt((Time.time - stateStartedAt) * fps);
            index = loop ? index % currentFrames.Length : Mathf.Min(index, currentFrames.Length - 1);
            return currentFrames[index];
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int damageApplied)
        {
            if (!blocked && damageApplied > 0 && damageable.CurrentHealth > 0)
            {
                hurtUntil = Time.time + hurtDuration;
            }
        }

        private void HandleDied() { deathTriggered = true; }

        private static Sprite[] NonEmptyOr(Sprite[] first, params Sprite[][] fallbacks)
        {
            if (first != null && first.Length > 0) return first;
            for (int index = 0; index < fallbacks.Length; index++)
            {
                Sprite[] candidate = fallbacks[index];
                if (candidate != null && candidate.Length > 0) return candidate;
            }
            return null;
        }
    }
}
