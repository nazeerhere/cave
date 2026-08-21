using System.Collections;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class Damageable : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField, Min(0f)] private float damageFlashDuration = 0.12f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f);

        private SpriteRenderer[] renderers;
        private Color[] originalColors;
        private Coroutine flashRoutine;

        public int CurrentHealth { get; private set; }

        private void Awake()
        {
            CurrentHealth = maxHealth;
            renderers = GetComponentsInChildren<SpriteRenderer>();
            originalColors = new Color[renderers.Length];

            for (int index = 0; index < renderers.Length; index++)
            {
                originalColors[index] = renderers[index].color;
            }
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || CurrentHealth <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

            if (CurrentHealth == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashDamage());
        }

        private IEnumerator FlashDamage()
        {
            SetRendererColors(damageFlashColor);
            yield return new WaitForSeconds(damageFlashDuration);

            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].color = originalColors[index];
            }

            flashRoutine = null;
        }

        private void SetRendererColors(Color color)
        {
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                spriteRenderer.color = color;
            }
        }
    }
}
