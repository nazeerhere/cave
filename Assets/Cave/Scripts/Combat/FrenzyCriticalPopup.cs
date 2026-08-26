using UnityEngine;

namespace Cave.Combat
{
    [DisallowMultipleComponent]
    public sealed class FrenzyCriticalPopup : MonoBehaviour
    {
        private const float Lifetime = 0.9f;
        private const float VerticalTravel = 0.65f;

        private TextMesh label;
        private Color baseColor;
        private Vector3 startPosition;
        private float createdAt;

        public static void Create(Damageable target, int appliedDamage, Color color)
        {
            if (target == null || appliedDamage <= 0)
            {
                return;
            }

            GameObject popupObject = new GameObject("Frenzy Critical Damage")
            {
                hideFlags = HideFlags.DontSave
            };
            popupObject.transform.position = target.transform.position + Vector3.up * 0.7f;
            FrenzyCriticalPopup popup = popupObject.AddComponent<FrenzyCriticalPopup>();
            SpriteRenderer targetRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
            popup.Initialize(appliedDamage, color, targetRenderer);
        }

        private void Initialize(
            int appliedDamage,
            Color color,
            SpriteRenderer targetRenderer)
        {
            label = gameObject.AddComponent<TextMesh>();
            label.text = "CRITICAL! " + appliedDamage;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 44;
            label.characterSize = 0.075f;
            label.fontStyle = FontStyle.Bold;
            label.color = color;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerID = targetRenderer != null
                    ? targetRenderer.sortingLayerID
                    : 0;
                renderer.sortingOrder = targetRenderer != null
                    ? targetRenderer.sortingOrder + 40
                    : 260;
            }

            baseColor = color;
            startPosition = transform.position;
            createdAt = Time.time;
        }

        private void LateUpdate()
        {
            float progress = Mathf.Clamp01((Time.time - createdAt) / Lifetime);
            transform.position = startPosition + Vector3.up * (VerticalTravel * progress);
            transform.rotation = Quaternion.identity;
            if (label != null)
            {
                Color faded = baseColor;
                faded.a *= 1f - progress;
                label.color = faded;
            }

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
