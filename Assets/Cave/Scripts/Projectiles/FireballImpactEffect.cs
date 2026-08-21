using UnityEngine;

namespace Cave.Projectiles
{
    public sealed class FireballImpactEffect : MonoBehaviour
    {
        private SpriteRenderer effectRenderer;
        private Color startingColor;
        private Vector3 startingScale;
        private Vector3 endingScale;
        private float visualLifetime;
        private float elapsed;

        public static void Create(
            SpriteRenderer sourceRenderer,
            float lifetime,
            float endScaleMultiplier)
        {
            GameObject effectObject = new GameObject("Fireball Impact");
            effectObject.layer = sourceRenderer.gameObject.layer;
            effectObject.transform.SetPositionAndRotation(
                sourceRenderer.transform.position,
                sourceRenderer.transform.rotation);
            effectObject.transform.SetParent(sourceRenderer.transform.parent, true);

            SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceRenderer.sprite;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.color = sourceRenderer.color;
            renderer.flipX = sourceRenderer.flipX;
            renderer.flipY = sourceRenderer.flipY;
            renderer.drawMode = sourceRenderer.drawMode;
            renderer.size = sourceRenderer.size;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + 1;

            FireballImpactEffect effect = effectObject.AddComponent<FireballImpactEffect>();
            effect.Initialize(renderer, lifetime, endScaleMultiplier);
        }

        private void Initialize(
            SpriteRenderer renderer,
            float lifetime,
            float endScaleMultiplier)
        {
            effectRenderer = renderer;
            visualLifetime = Mathf.Max(0.01f, lifetime);
            startingColor = renderer.color;
            startingScale = transform.localScale;
            endingScale = startingScale * Mathf.Max(1f, endScaleMultiplier);
        }

        private void Update()
        {
            if (effectRenderer == null)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / visualLifetime);
            transform.localScale = Vector3.Lerp(startingScale, endingScale, progress);

            Color color = startingColor;
            color.a *= 1f - progress;
            effectRenderer.color = color;

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
