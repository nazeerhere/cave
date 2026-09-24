using System;
using System.Collections;
using Cave.Audio;
using Cave.Axioms.Phase;
using Cave.Enemies;
using Cave.Player;
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
        private bool suppressNextDeactivation;
        private Color persistentTint = Color.white;
        private PhaseCombatState phaseCombatState;
        private bool runtimeInitialized;

        public event Action Died;
        /// <summary>Death context for presentation/reward systems that need the
        /// resolved killer without changing the long-standing parameterless
        /// death event used by existing gameplay.</summary>
        public event Action<DamageContext> DiedWithContext;
        public event Action<DamageContext, bool, int> DamageResolved;

        public int CurrentHealth { get; private set; }
        public int MaximumHealth => runtimeMaximumHealth;
        public int BaseMaximumHealth => maxHealth;

        private void Awake()
        {
            InitializeRuntimeState();
        }

        /// <summary>
        /// Resolves health and presentation state for explicit runtime factories
        /// and deterministic verification without invoking Unity lifecycle
        /// methods manually. Repeated calls preserve live health and tint state.
        /// </summary>
        public void InitializeRuntimeState()
        {
            if (!runtimeInitialized)
            {
                runtimeMaximumHealth = maxHealth;
                CurrentHealth = runtimeMaximumHealth;
                runtimeInitialized = true;
            }

            if (renderers != null && originalColors != null)
            {
                return;
            }

            renderers = GetComponentsInChildren<SpriteRenderer>(true);
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
            InitializeRuntimeState();
            if (amount <= 0 || CurrentHealth <= 0)
            {
                return 0;
            }

            if (damageContext.PermanentProgressionSource != null)
            {
                amount = damageContext.PermanentProgressionSource.ResolvePlayerDamage(amount);
            }

            damageContext = CurseAltarZone.BindPlayerDamageZone(damageContext);
            amount = CurseAltarZone.ResolvePlayerAttackDamage(
                damageContext,
                this,
                amount,
                out bool altarCritical);
            if (altarCritical)
            {
                damageContext = damageContext.WithTraits(DamageTrait.AltarCritical);
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

            if (phaseCombatState == null)
            {
                phaseCombatState = GetComponent<PhaseCombatState>();
            }

            if (phaseCombatState != null)
            {
                amount = phaseCombatState.ResolveIncomingDamage(amount);
            }

            int healthBeforeDamage = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            int appliedDamage = healthBeforeDamage - CurrentHealth;
            PhaseCombatState.TryConsumeOpeningOnSuccessfulHit(
                damageContext.Source,
                gameObject,
                appliedDamage,
                damageContext,
                Time.time);
            DamageResolved?.Invoke(
                damageContext,
                false,
                appliedDamage);
            CurseAltarZone.NotifyPlayerDamageResolved(damageContext, this, appliedDamage);
            CaveSfx.Play(CaveSfxCue.Hit, 0.75f);

            if (CurrentHealth == 0)
            {
                GetComponent<EnemyCorruptionLifecycle>()?.PrepareForDeath();
                damageContext.ReportKillingBlow(this);
                CurseAltarZone.NotifyEnemyDied(this, damageContext);
                PlayerSwordCosmetics.NotifyPlayerDefeatedEnemy(this, damageContext);
                DiedWithContext?.Invoke(damageContext);
                Died?.Invoke();
                if (!suppressNextDeactivation)
                {
                    gameObject.SetActive(false);
                }

                suppressNextDeactivation = false;
                return appliedDamage;
            }

            if (damageContext.IsPlayerDamage)
            {
                EnemyStagger stagger = GetComponent<EnemyStagger>();
                if (stagger != null && damageContext.HasTrait(DamageTrait.StaggerHeavy))
                {
                    stagger.TryStagger(
                        StaggerStrength.Heavy,
                        stagger.GetBaseDuration(StaggerStrength.Heavy)
                            * CurseAltarZone.ResolvePlayerStaggerMultiplier(
                                damageContext,
                                StaggerStrength.Heavy));
                }
                else if (stagger != null && damageContext.HasTrait(DamageTrait.StaggerNormal))
                {
                    stagger.TryStagger(
                        StaggerStrength.Normal,
                        stagger.GetBaseDuration(StaggerStrength.Normal)
                            * CurseAltarZone.ResolvePlayerStaggerMultiplier(
                                damageContext,
                                StaggerStrength.Normal));
                }
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            if (Application.isPlaying)
            {
                flashRoutine = StartCoroutine(FlashDamage());
            }
            return appliedDamage;
        }

        public void RestoreToFullHealth()
        {
            InitializeRuntimeState();
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
            return RestoreHealthResolved(amount) > 0;
        }

        public int RestoreHealthResolved(int amount)
        {
            InitializeRuntimeState();
            if (amount <= 0 || CurrentHealth <= 0 || CurrentHealth >= runtimeMaximumHealth)
            {
                return 0;
            }

            int healthBeforeRestore = CurrentHealth;
            CurrentHealth = Mathf.Min(runtimeMaximumHealth, CurrentHealth + amount);
            return CurrentHealth - healthBeforeRestore;
        }

        /// <summary>
        /// Explicit encounter seam for forms which transition on defeat rather
        /// than being permanently removed. It applies to one resolved death
        /// only and must be requested from a death subscriber.
        /// </summary>
        public void SuppressNextDeactivation()
        {
            suppressNextDeactivation = true;
        }

        public void SetPersistentTint(Color tint)
        {
            InitializeRuntimeState();
            persistentTint = new Color(
                Mathf.Clamp01(tint.r),
                Mathf.Clamp01(tint.g),
                Mathf.Clamp01(tint.b),
                Mathf.Clamp01(tint.a));
            ReapplyPersistentTint();
        }

        public void ReapplyPersistentTint()
        {
            InitializeRuntimeState();
            if (flashRoutine == null)
            {
                RestoreOriginalColors();
            }
        }

        public void SetRuntimeMaximumHealth(int maximumHealth, bool restoreToFull)
        {
            InitializeRuntimeState();
            runtimeMaximumHealth = Mathf.Max(1, maximumHealth);
            CurrentHealth = restoreToFull
                ? runtimeMaximumHealth
                : Mathf.Min(CurrentHealth, runtimeMaximumHealth);
        }

        public void SetRuntimeMaximumHealthPreservingRatio(int maximumHealth)
        {
            InitializeRuntimeState();
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
                renderers[index].color = MultiplyColor(originalColors[index], persistentTint);
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

            persistentTint = Color.white;
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
                    renderers[index].color = MultiplyColor(originalColors[index], persistentTint);
                }
            }
        }

        private static Color MultiplyColor(Color authoredColor, Color tint)
        {
            return new Color(
                authoredColor.r * tint.r,
                authoredColor.g * tint.g,
                authoredColor.b * tint.b,
                authoredColor.a * tint.a);
        }
    }
}
