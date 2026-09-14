using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Presentation-only Anti-Pyre body animator. Gameplay action state remains authoritative.</summary>
    [DisallowMultipleComponent]
    public sealed class GothVisualAnimator : MonoBehaviour
    {
        private enum BodyState { Idle, Move, Fireball, Charge, Release, Burst, Focus, Beam, Ritual, Hurt, Death }

        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer legacyRenderer;
        [SerializeField] private bool corruptedVariant;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] moveFrames;
        [SerializeField] private Sprite[] fireballFrames;
        [SerializeField] private Sprite[] chargeFrames;
        [SerializeField] private Sprite[] releaseFrames;
        [SerializeField] private Sprite[] hurtFrames;
        [SerializeField] private Sprite[] deathFrames;
        [SerializeField] private Sprite[] burstFrames;
        [SerializeField] private Sprite[] focusFrames;
        [SerializeField] private Sprite[] beamFrames;
        [SerializeField] private Sprite[] ritualFrames;
        [SerializeField, Min(1f)] private float idleFps = 4f;
        [SerializeField, Min(1f)] private float walkFps = 8f;
        [SerializeField, Min(1f)] private float castFps = 10f;

        private Rigidbody2D body;
        private Damageable damageable;
        private GothArtilleryAbilities abilities;
        private Sprite[] activeFrames;
        private Sprite displayed;
        private BodyState activeState;
        private float stateStarted;
        private float hurtUntil;
        private bool dead;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            damageable = GetComponent<Damageable>();
            abilities = GetComponent<GothArtilleryAbilities>();
            activeFrames = idleFrames;
            activeState = BodyState.Idle;
            stateStarted = Time.time;
            if (legacyRenderer != null) legacyRenderer.enabled = false;
        }

        private void OnEnable()
        {
            if (damageable == null) return;
            damageable.DamageResolved += Damaged;
            damageable.Died += Died;
        }

        private void OnDisable()
        {
            if (damageable == null) return;
            damageable.DamageResolved -= Damaged;
            damageable.Died -= Died;
        }

        private void LateUpdate()
        {
            if (bodyRenderer == null) return;
            BodyState nextState = ResolveState();
            Sprite[] nextFrames = FramesFor(nextState);
            if (nextState != activeState)
            {
                activeState = nextState;
                activeFrames = nextFrames;
                stateStarted = Time.time;
                displayed = null;
            }
            else if (activeFrames != nextFrames)
            {
                activeFrames = nextFrames;
                displayed = null;
            }

            if (activeFrames == null || activeFrames.Length == 0) return;
            int index = Mathf.FloorToInt((Time.time - stateStarted) * FramesPerSecond(activeState));
            if (Loops(activeState)) index %= activeFrames.Length;
            else index = Mathf.Min(index, activeFrames.Length - 1);
            Sprite sprite = activeFrames[index];
            if (sprite != displayed)
            {
                displayed = sprite;
                bodyRenderer.sprite = sprite;
            }

            if (body != null && Mathf.Abs(body.velocity.x) > .04f) bodyRenderer.flipX = body.velocity.x < 0f;
        }

        private BodyState ResolveState()
        {
            if (dead || (damageable != null && damageable.CurrentHealth <= 0)) return BodyState.Death;
            if (!corruptedVariant && Time.time < hurtUntil) return BodyState.Hurt;
            if (abilities != null)
            {
                switch (abilities.CurrentAction)
                {
                    case GothActionState.Volley:
                    case GothActionState.MeteorFireball:
                        return BodyState.Fireball;
                    case GothActionState.AntimatterBurst:
                        return corruptedVariant ? BodyState.Burst : (abilities.CurrentPhase == GothActionPhase.Startup ? BodyState.Charge : BodyState.Release);
                    case GothActionState.FocusOrb:
                        return corruptedVariant ? BodyState.Focus : BodyState.Charge;
                    case GothActionState.Beam:
                        return corruptedVariant ? BodyState.Beam : (abilities.CurrentPhase == GothActionPhase.Startup ? BodyState.Charge : BodyState.Release);
                    case GothActionState.MeteorStorm:
                        return corruptedVariant ? BodyState.Ritual : BodyState.Charge;
                    case GothActionState.Repulse:
                        return corruptedVariant ? BodyState.Burst : BodyState.Charge;
                    case GothActionState.PartitionField:
                        return corruptedVariant ? BodyState.Ritual : BodyState.Charge;
                }
            }
            return body != null && Mathf.Abs(body.velocity.x) > .04f ? BodyState.Move : BodyState.Idle;
        }

        private Sprite[] FramesFor(BodyState state)
        {
            switch (state)
            {
                case BodyState.Move: return moveFrames;
                case BodyState.Fireball: return fireballFrames;
                case BodyState.Charge: return chargeFrames;
                case BodyState.Release: return releaseFrames;
                case BodyState.Burst: return burstFrames;
                case BodyState.Focus: return focusFrames;
                case BodyState.Beam: return beamFrames;
                case BodyState.Ritual: return ritualFrames;
                case BodyState.Hurt: return hurtFrames;
                case BodyState.Death: return deathFrames;
                default: return idleFrames;
            }
        }

        private float FramesPerSecond(BodyState state)
        {
            if (state == BodyState.Idle) return idleFps;
            if (state == BodyState.Move) return walkFps;
            return castFps;
        }

        private static bool Loops(BodyState state) => state == BodyState.Idle || state == BodyState.Move;
        private void Damaged(DamageContext _, bool blocked, int applied) { if (!blocked && applied > 0) hurtUntil = Time.time + .2f; }
        private void Died() { dead = true; stateStarted = Time.time; }

        public void ResetForSpawn()
        {
            dead = false;
            hurtUntil = 0f;
            activeState = BodyState.Idle;
            activeFrames = idleFrames;
            displayed = null;
            stateStarted = Time.time;
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = true;
                bodyRenderer.color = Color.white;
                if (idleFrames != null && idleFrames.Length > 0) bodyRenderer.sprite = idleFrames[0];
            }
            if (legacyRenderer != null) legacyRenderer.enabled = false;
        }

        public void Configure(SpriteRenderer configuredRenderer, SpriteRenderer configuredLegacy, bool corrupt,
            Sprite[] idle, Sprite[] move, Sprite[] fireball, Sprite[] charge, Sprite[] release, Sprite[] hurt, Sprite[] death,
            Sprite[] burst, Sprite[] focus, Sprite[] beam, Sprite[] ritual)
        {
            bodyRenderer = configuredRenderer;
            legacyRenderer = configuredLegacy;
            corruptedVariant = corrupt;
            idleFrames = idle;
            moveFrames = move;
            fireballFrames = fireball;
            chargeFrames = charge;
            releaseFrames = release;
            hurtFrames = hurt;
            deathFrames = death;
            burstFrames = burst;
            focusFrames = focus;
            beamFrames = beam;
            ritualFrames = ritual;
            activeFrames = idle;
            activeState = BodyState.Idle;
            displayed = null;
            stateStarted = Time.time;
            if (legacyRenderer != null) legacyRenderer.enabled = false;
        }
    }
}
