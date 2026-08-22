using System;
using System.Collections;
using Cave.Audio;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class Damageable : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField, Min(0f)] private float damageFlashDuration = 0.15f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f);

        private SpriteRenderer[] renderers;
        private Color[] originalColors;
        private Coroutine flashRoutine;
        private int runtimeMaximumHealth;

        public event Action Died;

        public int CurrentHealth { get; private set; }
        public int MaximumHealth => runtimeMaximumHealth;
        public int BaseMaximumHealth => maxHealth;

        private void Awake()
        {
            runtimeMaximumHealth = maxHealth;
            CurrentHealth = runtimeMaximumHealth;
            renderers = GetComponentsInChildren<SpriteRenderer>();
            originalColors = new Color[renderers.Length];

            for (int index = 0; index < renderers.Length; index++)
            {
                originalColors[index] = renderers[index].color;
            }
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, default);
        }

        public void TakeDamage(int amount, DamageContext damageContext)
        {
            if (amount <= 0 || CurrentHealth <= 0)
            {
                return;
            }

            EnemyDefenseController defense = GetComponent<EnemyDefenseController>();
            if (defense != null && defense.TryBlockDamage(damageContext))
            {
                return;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            CaveSfx.Play(CaveSfxCue.Hit, 0.75f);

            if (CurrentHealth == 0)
            {
                damageContext.ReportKillingBlow();
                Died?.Invoke();
                gameObject.SetActive(false);
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashDamage());
        }

        public void RestoreToFullHealth()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            CurrentHealth = runtimeMaximumHealth;
            RestoreOriginalColors();
        }

        public bool RestoreHealth(int amount)
        {
            if (amount <= 0 || CurrentHealth <= 0 || CurrentHealth >= runtimeMaximumHealth)
            {
                return false;
            }

            CurrentHealth = Mathf.Min(runtimeMaximumHealth, CurrentHealth + amount);
            return true;
        }

        public void SetRuntimeMaximumHealth(int maximumHealth, bool restoreToFull)
        {
            runtimeMaximumHealth = Mathf.Max(1, maximumHealth);
            CurrentHealth = restoreToFull
                ? runtimeMaximumHealth
                : Mathf.Min(CurrentHealth, runtimeMaximumHealth);
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

        private void OnDisable()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            if (renderers == null || originalColors == null)
            {
                return;
            }

            RestoreOriginalColors();
        }

        private void RestoreOriginalColors()
        {
            if (renderers == null || originalColors == null)
            {
                return;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].color = originalColors[index];
                }
            }
        }
    }
}
