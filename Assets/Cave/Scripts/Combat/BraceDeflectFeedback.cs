using UnityEngine;

namespace Cave.Combat
{
    /// <summary>
    /// Presentation-only, one-shot contact burst for a confirmed Brace I
    /// deflection. It is created only by the already-authoritative result path.
    /// </summary>
    internal sealed class BraceDeflectFeedback : MonoBehaviour
    {
        private const string SpritePath = "VFX/Player/Brace/cyan_energy_slash_burst";
        private const float PixelsPerUnit = 128f;
        private const float StartScale = .19f;
        private const float EndScale = .29f;

        private static Sprite effectSprite;
        private static bool spriteLoadAttempted;

        private SpriteRenderer spriteRenderer;
        private Color color;
        private float duration;
        private float startedAt;

        internal static void Create(
            Vector2 position,
            float facingDirection,
            Color feedbackColor,
            float lifetime,
            int sortingOrder)
        {
            Sprite sprite = LoadSprite();
            if (sprite == null) return;

            GameObject visual = new GameObject("Brace Deflect Burst");
            visual.transform.position = position;
            BraceDeflectFeedback feedback = visual.AddComponent<BraceDeflectFeedback>();
            feedback.Initialize(sprite, facingDirection, feedbackColor, lifetime, sortingOrder);
        }

        private void Initialize(
            Sprite sprite,
            float facingDirection,
            Color feedbackColor,
            float lifetime,
            int sortingOrder)
        {
            color = feedbackColor;
            duration = Mathf.Max(.01f, lifetime);
            startedAt = Time.time;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.flipX = facingDirection < 0f;
            spriteRenderer.color = color;
            transform.localScale = Vector3.one * StartScale;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(StartScale, EndScale, progress);
            Color faded = color;
            faded.a *= 1f - progress;
            spriteRenderer.color = faded;
            if (progress >= 1f) Destroy(gameObject);
        }

        private static Sprite LoadSprite()
        {
            if (!spriteLoadAttempted)
            {
                spriteLoadAttempted = true;
                effectSprite = Resources.Load<Sprite>(SpritePath);
                if (effectSprite == null)
                {
                    Texture2D texture = Resources.Load<Texture2D>(SpritePath);
                    if (texture != null)
                    {
                        effectSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(.5f, .5f), PixelsPerUnit);
                    }
                }
            }

            return effectSprite;
        }
    }
}
