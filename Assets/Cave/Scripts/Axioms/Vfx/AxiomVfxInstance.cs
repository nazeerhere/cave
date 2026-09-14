using UnityEngine;

namespace Cave.Axioms.Vfx
{
    /// <summary>Short-lived or state-bound sprite presentation. Only active effects update.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AxiomVfxInstance : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = .25f;
        [SerializeField, Min(0.01f)] private float endScaleMultiplier = 1.12f;
        [SerializeField] private float rotationsPerSecond = .25f;

        private SpriteRenderer spriteRenderer;
        private Transform followTarget;
        private Vector3 localOffset;
        private float startedAt;
        private float baseScale;
        private Sprite[] frames;
        private float framesPerSecond;
        private int displayedFrame = -1;
        private bool persistent;
        private Color initialColor;

        public void Play(Transform target, Vector3 offset, float resolvedLifetime, float scale, bool shouldPersist)
        {
            spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
            followTarget = target;
            localOffset = offset;
            lifetime = Mathf.Max(.01f, resolvedLifetime);
            persistent = shouldPersist;
            startedAt = Time.time;
            baseScale = Mathf.Max(.01f, scale);
            transform.localScale = Vector3.one * baseScale;
            if (spriteRenderer != null)
            {
                initialColor = spriteRenderer.color;
            }

            FollowTarget();
        }

        public void Refresh(float resolvedLifetime)
        {
            lifetime = Mathf.Max(.01f, resolvedLifetime);
            startedAt = Time.time;
        }

        public void SetAnimation(Sprite[] incomingFrames, float fps)
        {
            frames = incomingFrames;
            framesPerSecond = Mathf.Max(.01f, fps);
            displayedFrame = -1;
            UpdateAnimationFrame(0f);
        }

        public void Stop()
        {
            Destroy(gameObject);
        }

        private void Update()
        {
            FollowTarget();
            float elapsed = Time.time - startedAt;
            UpdateAnimationFrame(elapsed);
            float progress = Mathf.Clamp01(elapsed / lifetime);
            transform.Rotate(0f, 0f, rotationsPerSecond * 360f * Time.deltaTime);

            if (spriteRenderer != null)
            {
                Color color = initialColor;
                color.a *= persistent
                    ? .55f + .22f * (1f + Mathf.Sin(elapsed * 4f))
                    : 1f - progress;
                spriteRenderer.color = color;
            }

            if (!persistent)
            {
                transform.localScale = Vector3.one * baseScale * Mathf.Lerp(1f, endScaleMultiplier, progress);
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateAnimationFrame(float elapsed)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0) return;
            int frame = Mathf.FloorToInt(elapsed * framesPerSecond) % frames.Length;
            if (frame == displayedFrame) return;
            displayedFrame = frame;
            spriteRenderer.sprite = frames[frame];
        }

        private void FollowTarget()
        {
            if (followTarget != null)
            {
                transform.position = followTarget.position + localOffset;
            }
        }
    }
}
