using UnityEngine;

namespace Cave.Projectiles
{
    /// <summary>
    /// Presentation-only lifecycle for a deployed Manifestation Origin. The
    /// player-owned origin authority remains responsible for every gameplay
    /// decision; this component only animates an already-created renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManifestationOriginVisual : MonoBehaviour
    {
        private const float DeploymentDuration = .24f;
        private const float RecallDuration = .20f;
        private const float FireResponseDuration = .16f;

        private SpriteRenderer primary;
        private SpriteRenderer glow;
        private Vector3 authoredScale;
        private Color authoredColor;
        private Color glowColor;
        private float createdAt;
        private float fireResponseEndsAt;
        private float recallStartedAt = -1f;
        private bool configured;

        public bool IsRecalling => recallStartedAt >= 0f;

        public void Configure(
            SpriteRenderer renderer,
            ManifestationOrigin origin,
            float visualScale,
            int sortingOrder)
        {
            primary = renderer;
            authoredScale = Vector3.one * Mathf.Max(.01f, visualScale);
            authoredColor = origin == ManifestationOrigin.Anchor
                ? new Color(1f, 1f, 1f, .94f)
                : new Color(1f, 1f, 1f, .91f);
            glowColor = origin == ManifestationOrigin.Anchor
                ? new Color(.58f, .25f, 1f, .13f)
                : new Color(.85f, .66f, 1f, .12f);

            primary.color = new Color(authoredColor.r, authoredColor.g, authoredColor.b, 0f);
            primary.sortingOrder = sortingOrder;

            GameObject glowObject = new GameObject("Phase Glow");
            glowObject.transform.SetParent(transform, false);
            glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = primary.sprite;
            glow.sortingLayerID = primary.sortingLayerID;
            glow.sortingOrder = sortingOrder - 1;
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            glowObject.transform.localScale = Vector3.one * 1.035f;

            transform.localScale = authoredScale * .82f;
            createdAt = Time.time;
            configured = true;
        }

        /// <summary>Called only after an ordinary projectile has committed.</summary>
        public void PlayFiringResponse()
        {
            if (!configured || IsRecalling) return;
            fireResponseEndsAt = Mathf.Max(fireResponseEndsAt, Time.time + FireResponseDuration);
        }

        public void PlayRecall()
        {
            if (!configured)
            {
                Destroy(gameObject);
                return;
            }
            if (!IsRecalling) recallStartedAt = Time.time;
        }

        private void Update()
        {
            if (!configured || primary == null) return;

            float now = Time.time;
            if (IsRecalling)
            {
                float recall = Mathf.Clamp01((now - recallStartedAt) / RecallDuration);
                float retained = 1f - Mathf.SmoothStep(0f, 1f, recall);
                Apply(authoredScale * Mathf.Lerp(1f, .72f, recall),
                    authoredColor.a * retained, glowColor.a * retained);
                if (recall >= 1f) Destroy(gameObject);
                return;
            }

            float deployment = Mathf.Clamp01((now - createdAt) / DeploymentDuration);
            float deployEase = Mathf.SmoothStep(0f, 1f, deployment);
            float phase = Mathf.Sin((now - createdAt) * 2.4f) * .008f;
            float fire = fireResponseEndsAt > now
                ? Mathf.Clamp01((fireResponseEndsAt - now) / FireResponseDuration)
                : 0f;
            float response = Mathf.Sin(fire * Mathf.PI);
            Vector3 scale = authoredScale * (Mathf.Lerp(.82f, 1f, deployEase) + phase + response * .055f);
            float primaryAlpha = deployEase * Mathf.Clamp01(authoredColor.a + response * .06f);
            float glowAlpha = deployEase * Mathf.Clamp01(glowColor.a + response * .24f);
            Apply(scale, primaryAlpha, glowAlpha);
        }

        private void Apply(Vector3 scale, float primaryAlpha, float glowAlpha)
        {
            transform.localScale = scale;
            primary.color = new Color(authoredColor.r, authoredColor.g, authoredColor.b,
                Mathf.Clamp01(primaryAlpha));
            if (glow != null)
            {
                glow.color = new Color(glowColor.r, glowColor.g, glowColor.b,
                    Mathf.Clamp01(glowAlpha));
            }
        }
    }
}
