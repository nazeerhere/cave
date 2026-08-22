using System.Collections;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] targetRenderers;
        [SerializeField, Min(0f)] private float duration = 0.12f;
        [SerializeField] private Color flashColor = Color.white;

        private Color[] originalColors;
        private Coroutine flashRoutine;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponents<SpriteRenderer>();
            }

            originalColors = new Color[targetRenderers.Length];
            for (int index = 0; index < targetRenderers.Length; index++)
            {
                if (targetRenderers[index] != null)
                {
                    originalColors[index] = targetRenderers[index].color;
                }
            }
        }

        public void Play()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            RestoreOriginalColors();
            flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            foreach (SpriteRenderer targetRenderer in targetRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.color = flashColor;
                }
            }

            yield return new WaitForSeconds(duration);
            RestoreOriginalColors();
            flashRoutine = null;
        }

        private void OnDisable()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            RestoreOriginalColors();
        }

        private void RestoreOriginalColors()
        {
            if (originalColors == null)
            {
                return;
            }

            for (int index = 0; index < targetRenderers.Length; index++)
            {
                if (targetRenderers[index] != null)
                {
                    targetRenderers[index].color = originalColors[index];
                }
            }
        }
    }
}
