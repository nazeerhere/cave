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
        public event Action<DamageContext, bool, int> DamageResolved;

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
            TakeDamageResolved(amount, damageContext);
        }

        public int TakeDamageResolved(int amount, DamageContext damageContext)
        {
            if (amount <= 0 || CurrentHealth <= 0)
            {
                return 0;
            }

            if (damageContext.PermanentProgressionSource != null)
            {
                amount = damageContext.PermanentProgressionSource.ResolvePlayerDamage(amount);
            }

            EnemyDefenseController defense = GetComponent<EnemyDefenseController>();
            if (defense != null && defense.TryBlockDamage(damageContext))
            {
                DamageResolved?.Invoke(damageContext, true, 0);
                return 0;
            }

            NecromancerMeleeShield meleeShield = GetComponent<NecromancerMeleeShield>();
            if (meleeShield != null)
            {
                amount = meleeShield.ResolveIncomingDamage(amount, damageContext);
                if (amount <= 0)
                {
                    DamageResolved?.Invoke(damageContext, false, 0);
                    return 0;
                }
            }

            int healthBeforeDamage = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            int appliedDamage = healthBeforeDamage - CurrentHealth;
            DamageResolved?.Invoke(
                damageContext,
                false,
                appliedDamage);
            CaveSfx.Play(CaveSfxCue.Hit, 0.75f);

            if (CurrentHealth == 0)
            {
                damageContext.ReportKillingBlow();
                Died?.Invoke();
                gameObject.SetActive(false);
                return appliedDamage;
            }

            if (damageContext.IsPlayerDamage)
            {
                EnemyStagger stagger = GetComponent<EnemyStagger>();
                if (damageContext.HasTrait(DamageTrait.StaggerHeavy))
                {
                    stagger?.TryStagger(StaggerStrength.Heavy);
                }
                else if (damageContext.HasTrait(DamageTrait.StaggerNormal))
                {
                    stagger?.TryStagger(StaggerStrength.Normal);
                }
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashDamage());
            return appliedDamage;
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

        public void SetRuntimeMaximumHealthPreservingRatio(int maximumHealth)
        {
            int previousMaximum = Mathf.Max(1, runtimeMaximumHealth);
            float healthFraction = CurrentHealth / (float)previousMaximum;
            runtimeMaximumHealth = Mathf.Max(1, maximumHealth);
            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                return;
            }

            CurrentHealth = Mathf.Clamp(
                Mathf.RoundToInt(runtimeMaximumHealth * healthFraction),
                1,
                runtimeMaximumHealth);
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
