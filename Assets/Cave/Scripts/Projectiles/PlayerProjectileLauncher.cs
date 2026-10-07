using System;
using Cave.Audio;
using Cave.Combat;
using Cave.InputSystem;
using Cave.Player;
using Cave.Progression;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Projectiles
{
    [RequireComponent(typeof(PlayerMana), typeof(PlayerSpecialMode), typeof(PlayerDamageBoost))]
    [RequireComponent(typeof(PlayerAimDirection))]
    public sealed class PlayerProjectileLauncher : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private PlayerProjectile projectilePrefab;
        [Tooltip("Optional Inspector-editable heavy/explosive variant. Falls back to the standard projectile while an existing scene override has not assigned it.")]
        [SerializeField] private PlayerProjectile heavyProjectilePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Vector2 spawnOffset = new Vector2(0.8f, 0.1f);
        [FormerlySerializedAs("fireCooldown")]
        [SerializeField, Min(0.05f)] private float heldFireInterval = 0.35f;
        [SerializeField, Min(0.01f)] private float manaCost = 10f;

        [Header("Heavy Projectile")]
        [SerializeField, Min(0.01f)] private float heavyMinimumChargeTime = 0.35f;
        [SerializeField, Min(0.01f)] private float heavyMaximumChargeTime = 1.2f;
        [FormerlySerializedAs("heavyManaCost")]
        [FormerlySerializedAs("tierOneHeavyStaminaCost")]
        [SerializeField, Min(0f)] private float heavyManaCost = ChargedProjectilePolicy.DefaultHeavyManaCost;
        [FormerlySerializedAs("heavyDamageMultiplier")]
        [SerializeField, Min(1f)] private float heavyMinimumDamageMultiplier = ChargedProjectilePolicy.DefaultHeavyMinimumDamageMultiplier;
        [SerializeField, Min(1f)] private float heavyMinimumVisualScale = ChargedProjectilePolicy.DefaultHeavyMinimumVisualScale;
        [FormerlySerializedAs("heavyProjectileScale")]
        [SerializeField, Min(1f)] private float heavyMaximumVisualScale = ChargedProjectilePolicy.DefaultHeavyMaximumVisualScale;
        [SerializeField, Min(0.1f)] private float heavyExplosionRadius = ChargedProjectilePolicy.DefaultHeavyExplosionRadius;
        [SerializeField, Min(0f)] private float heavyRecovery = 0.18f;

        private PlayerMana playerMana;
        private PlayerSpecialMode specialMode;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;
        private PlayerSpecialModeUpgradeState upgradeState;
        private PlayerAimDirection aimDirection;
        private PlayerController playerController;
        private PlayerCombatFlow combatFlow;
        private float nextFireTime;
        private bool heavyRequested;
        private bool heavyCharging;
        private bool heavyManaCommitted;
        private float heavyChargeStartedAt;
        private int heavyChargeTier;
        private GameObject heavyChargeVisual;
        private SpriteRenderer heavyChargeRenderer;

        public event Action<string> FeedbackRequested;
        public event Action<Vector2> ProjectileFired;

        public float ManaCost => manaCost;
        public bool IsHeavyCharging => heavyCharging;
        public bool IsHeavyReady => heavyCharging && heavyManaCommitted;
        public float HeavyChargeNormalized => heavyCharging
            ? ChargedProjectilePolicy.HeavyChargeNormalized(
                Time.time - heavyChargeStartedAt,
                heavyMinimumChargeTime,
                heavyMaximumChargeTime)
            : 0f;

        internal void UseProjectilePrefabIfMissing(PlayerProjectile defaultPrefab)
        {
            if (projectilePrefab == null)
            {
                projectilePrefab = defaultPrefab;
            }
        }

        internal void UseHeavyProjectilePrefabIfMissing(PlayerProjectile defaultPrefab)
        {
            if (heavyProjectilePrefab == null)
            {
                heavyProjectilePrefab = defaultPrefab;
            }
        }

        private void Awake()
        {
            playerMana = GetComponent<PlayerMana>();
            specialMode = GetComponent<PlayerSpecialMode>();
            damageBoost = GetComponent<PlayerDamageBoost>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            aimDirection = GetComponent<PlayerAimDirection>();
            playerController = GetComponent<PlayerController>();
            combatFlow = GetComponent<PlayerCombatFlow>();
            EnsureSpawnPoint();
        }

        private void Update()
        {
            if (heavyCharging)
            {
                UpdateHeavyCharge();
                return;
            }

            if (heavyRequested)
            {
                if (!GameInput.FireProjectileHeld || !GameInput.ChargeHeld)
                {
                    heavyRequested = false;
                }
                else if (Time.time >= nextFireTime)
                {
                    BeginHeavyCharge();
                }

                return;
            }

            if (GameInput.FireProjectileHeld && GameInput.ChargePressed)
            {
                // A rapid shot already spawned remains valid; this only blocks
                // the next repeat and transfers the held projectile channel at
                // its normal post-shot boundary.
                heavyRequested = true;
                if (Time.time >= nextFireTime)
                {
                    BeginHeavyCharge();
                }

                return;
            }

            if (GameInput.FireProjectilePressed)
            {
                TryFire(true);
            }
            else if (GameInput.FireProjectileHeld && Time.time >= nextFireTime)
            {
                TryFire(false);
            }
        }

        public bool TryFire()
        {
            return TryFire(true);
        }

        private bool TryFire(bool allowResourceFeedback)
        {
            if (heavyCharging || heavyRequested)
            {
                return false;
            }

            PlayerBrace brace = GetComponent<PlayerBrace>();
            if (brace != null && brace.IsActionLocked)
            {
                return false;
            }

            PlayerGuardBreak guardBreak = GetComponent<PlayerGuardBreak>();
            if ((guardBreak != null && !guardBreak.CanUseCombatActions)
                || projectilePrefab == null
                || Time.time < nextFireTime)
            {
                return false;
            }

            if (upgradeState == null)
            {
                upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            int currentTier = upgradeState != null
                ? upgradeState.GetCurrentTier(specialMode.CurrentMode)
                : 1;
            if (!playerMana.TrySpendMana(manaCost, currentTier))
            {
                nextFireTime = Time.time + EffectiveFireInterval;
                if (allowResourceFeedback)
                {
                    FeedbackRequested?.Invoke("Not enough mana.");
                }

                return false;
            }

            if (aimDirection == null)
            {
                aimDirection = GetComponent<PlayerAimDirection>();
            }

            Vector2 direction = aimDirection != null ? aimDirection.ReadDirection() : Vector2.right;
            if (IsGroundedPureDownAim(direction))
            {
                direction = aimDirection != null ? aimDirection.FacingDirection : Vector2.right;
            }

            Vector2 localSpawnPosition = direction * Mathf.Abs(spawnOffset.x) + Vector2.up * spawnOffset.y;
            spawnPoint.localPosition = new Vector3(localSpawnPosition.x, localSpawnPosition.y, 0f);

            float projectileAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            PlayerProjectile projectile = Instantiate(
                projectilePrefab,
                spawnPoint.position,
                Quaternion.Euler(0f, 0f, projectileAngle));
            int projectileDamage = damageBoost.ResolveProjectileDamage(projectile.BaseDamage);
            if (resourceMastery == null)
            {
                resourceMastery = GetComponent<PlayerResourceMastery>();
            }

            DamageContext damageContext = resourceMastery != null
                ? resourceMastery.CreateManaDamageContext()
                : default;
            if (upgradeState != null && upgradeState.Settings != null)
            {
                projectileDamage += upgradeState.Settings.GetProjectileDamageBonus(
                    specialMode.CurrentMode,
                    currentTier);
            }

            int maximumEnemyHits = ResolveMaximumEnemyHits(specialMode.CurrentMode, currentTier);
            FrenzyBreakActivation frenzyActivation = null;
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            combatFlow?.TryCommitFrenzyBreak(
                FrenzyBreakAttackKind.Projectile,
                out frenzyActivation);
            projectile.SetFrenzyBreakActivation(frenzyActivation);
            projectile.SetOwner(gameObject);
            projectile.Launch(
                direction,
                specialMode.CurrentMode,
                projectileDamage,
                damageContext,
                maximumEnemyHits,
                currentTier,
                upgradeState != null ? upgradeState.Settings : null);
            CaveSfx.Play(CaveSfxCue.Shot, 0.8f);
            nextFireTime = Time.time + EffectiveFireInterval;
            ProjectileFired?.Invoke(direction);
            return true;
        }

        private void BeginHeavyCharge()
        {
            if (heavyCharging || !GameInput.FireProjectileHeld || !GameInput.ChargeHeld
                || !CanUseProjectileChannel())
            {
                heavyRequested = false;
                return;
            }

            if (upgradeState == null)
            {
                upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            heavyRequested = false;
            heavyCharging = true;
            heavyManaCommitted = false;
            heavyChargeStartedAt = Time.time;
            heavyChargeTier = upgradeState != null
                ? upgradeState.GetCurrentTier(specialMode.CurrentMode)
                : 1;
            EnsureHeavyChargeVisual();
            UpdateHeavyChargeVisual();
        }

        private void UpdateHeavyCharge()
        {
            if (!GameInput.GameplayInputEnabled || !CanUseProjectileChannel())
            {
                CancelHeavyCharge();
                return;
            }

            UpdateHeavyChargeVisual();
            float elapsed = Time.time - heavyChargeStartedAt;
            if (!heavyManaCommitted
                && ChargedProjectilePolicy.IsReady(elapsed, heavyMinimumChargeTime))
            {
                if (playerMana == null || !playerMana.TrySpendMana(heavyManaCost, heavyChargeTier))
                {
                    FeedbackRequested?.Invoke("Not enough mana for Heavy Projectile.");
                    CancelHeavyCharge();
                    return;
                }

                heavyManaCommitted = true;
            }

            if (!GameInput.FireProjectileReleased)
            {
                return;
            }

            if (!heavyManaCommitted)
            {
                CancelHeavyCharge();
                return;
            }

            FireHeavyProjectile();
        }

        private bool CanUseProjectileChannel()
        {
            PlayerBrace brace = GetComponent<PlayerBrace>();
            if (brace != null && brace.IsActionLocked)
            {
                return false;
            }

            PlayerGuardBreak guardBreak = GetComponent<PlayerGuardBreak>();
            return (guardBreak == null || guardBreak.CanUseCombatActions)
                && (heavyProjectilePrefab != null || projectilePrefab != null);
        }

        private void FireHeavyProjectile()
        {
            if (!heavyCharging || !heavyManaCommitted || !CanUseProjectileChannel())
            {
                CancelHeavyCharge();
                return;
            }

            Vector2 direction = aimDirection != null ? aimDirection.ReadDirection() : Vector2.right;
            if (IsGroundedPureDownAim(direction))
            {
                direction = aimDirection != null ? aimDirection.FacingDirection : Vector2.right;
            }

            Vector2 localSpawnPosition = direction * Mathf.Abs(spawnOffset.x) + Vector2.up * spawnOffset.y;
            spawnPoint.localPosition = new Vector3(localSpawnPosition.x, localSpawnPosition.y, 0f);
            float projectileAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            PlayerProjectile heavyPrefab = heavyProjectilePrefab != null ? heavyProjectilePrefab : projectilePrefab;
            PlayerProjectile projectile = Instantiate(
                heavyPrefab,
                spawnPoint.position,
                Quaternion.Euler(0f, 0f, projectileAngle));
            int equivalentRegularDamage = damageBoost.ResolveProjectileDamage(projectile.BaseDamage);
            if (upgradeState != null && upgradeState.Settings != null)
            {
                equivalentRegularDamage += upgradeState.Settings.GetProjectileDamageBonus(
                    specialMode.CurrentMode,
                    heavyChargeTier);
            }

            float committedVisualScale = ChargedProjectilePolicy.HeavyVisualScale(
                HeavyChargeNormalized,
                heavyMinimumVisualScale,
                heavyMaximumVisualScale);
            int projectileDamage = ChargedProjectilePolicy.ResolveHeavyDamage(
                equivalentRegularDamage,
                committedVisualScale,
                heavyMinimumVisualScale,
                heavyMinimumDamageMultiplier);
            DamageContext damageContext = resourceMastery != null
                ? resourceMastery.CreateManaDamageContext()
                : default;

            FrenzyBreakActivation frenzyActivation = null;
            combatFlow?.TryCommitFrenzyBreak(FrenzyBreakAttackKind.Projectile, out frenzyActivation);
            projectile.SetFrenzyBreakActivation(frenzyActivation);
            projectile.SetOwner(gameObject);
            projectile.Launch(
                direction,
                specialMode.CurrentMode,
                projectileDamage,
                damageContext,
                1,
                heavyChargeTier,
                upgradeState != null ? upgradeState.Settings : null);
            projectile.ConfigureHeavyProjectile(committedVisualScale, heavyExplosionRadius);

            CaveSfx.Play(CaveSfxCue.Shot, 0.95f);
            nextFireTime = Time.time + Mathf.Max(EffectiveFireInterval, heavyRecovery);
            ProjectileFired?.Invoke(direction);
            ResetHeavyCharge();
        }

        private void CancelHeavyCharge()
        {
            // Mana is only spent at the ready/commit threshold. A committed
            // charge intentionally remains spent if a later interruption wins.
            ResetHeavyCharge();
        }

        private void ResetHeavyCharge()
        {
            heavyRequested = false;
            heavyCharging = false;
            heavyManaCommitted = false;
            heavyChargeStartedAt = 0f;
            if (heavyChargeVisual != null)
            {
                heavyChargeVisual.SetActive(false);
            }
        }

        private void EnsureHeavyChargeVisual()
        {
            if (heavyChargeVisual != null)
            {
                heavyChargeVisual.SetActive(true);
                return;
            }

            GameObject visual = new GameObject("Heavy Projectile Charge Visual");
            visual.transform.SetParent(spawnPoint, false);
            heavyChargeRenderer = visual.AddComponent<SpriteRenderer>();
            SpriteRenderer source = projectilePrefab != null
                ? projectilePrefab.GetComponentInChildren<SpriteRenderer>(true)
                : null;
            if (source != null)
            {
                heavyChargeRenderer.sprite = source.sprite;
                heavyChargeRenderer.sortingLayerID = source.sortingLayerID;
                heavyChargeRenderer.sortingOrder = source.sortingOrder + 2;
            }

            heavyChargeRenderer.color = new Color(0.55f, 0.84f, 1f, 0.9f);
            heavyChargeVisual = visual;
        }

        private void UpdateHeavyChargeVisual()
        {
            if (heavyChargeVisual == null)
            {
                return;
            }

            float progress = HeavyChargeNormalized;
            float scale = Mathf.Lerp(0.45f,
                ChargedProjectilePolicy.HeavyVisualScale(
                    progress,
                    heavyMinimumVisualScale,
                    heavyMaximumVisualScale),
                ChargedProjectilePolicy.NormalizedCharge(
                    Time.time - heavyChargeStartedAt,
                    heavyMinimumChargeTime));
            heavyChargeVisual.transform.localScale = new Vector3(scale, scale, 1f);
            if (heavyChargeRenderer != null)
            {
                Color color = heavyChargeRenderer.color;
                color.a = Mathf.Lerp(0.35f, IsHeavyReady ? 1f : 0.8f, progress);
                heavyChargeRenderer.color = color;
            }
        }

        private void OnDisable()
        {
            ResetHeavyCharge();
        }

        private float EffectiveFireInterval
        {
            get
            {
                PlayerCurseController curses = GetComponent<PlayerCurseController>();
                return heldFireInterval / Mathf.Max(
                    0.01f,
                    curses != null ? curses.AttackSpeedMultiplier : 1f);
            }
        }

        private bool IsGroundedPureDownAim(Vector2 direction)
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            return playerController != null
                && playerController.IsGrounded
                && Mathf.Abs(direction.x) < 0.01f
                && direction.y < -0.99f;
        }

        private int ResolveMaximumEnemyHits(SpecialMode mode, int tier)
        {
            if (upgradeState == null
                || upgradeState.Settings == null
                || tier < 2)
            {
                return 1;
            }

            return upgradeState.Settings.GetProjectilePierceCount(mode, tier);
        }

        private void EnsureSpawnPoint()
        {
            if (spawnPoint != null)
            {
                return;
            }

            GameObject spawnPointObject = new GameObject("Player Projectile Spawn");
            spawnPointObject.transform.SetParent(transform, false);
            spawnPointObject.transform.localPosition = new Vector3(spawnOffset.x, spawnOffset.y, 0f);
            spawnPoint = spawnPointObject.transform;
        }
    }

    /// <summary>Small pure charge seam used by the projectile action and its verifier.</summary>
    public static class ChargedProjectilePolicy
    {
        public const float DefaultHeavyManaCost = 25f;
        public const float DefaultHeavyMinimumDamageMultiplier = 3f;
        public const float DefaultHeavyMinimumVisualScale = 1.4f;
        public const float DefaultHeavyMaximumVisualScale = 2.2f;
        public const float DefaultHeavyExplosionRadius = 3.5f;

        public static float NormalizedCharge(float elapsed, float maximumDuration)
        {
            return Mathf.Clamp01(Mathf.Max(0f, elapsed) / Mathf.Max(0.01f, maximumDuration));
        }

        public static bool IsReady(float elapsed, float minimumDuration)
        {
            return elapsed >= Mathf.Max(0f, minimumDuration);
        }

        /// <summary>Charge starts progressing Heavy size only after commitment.</summary>
        public static float HeavyChargeNormalized(float elapsed, float minimumDuration, float maximumDuration)
        {
            float minimum = Mathf.Max(0f, minimumDuration);
            float maximum = Mathf.Max(minimum + .0001f, maximumDuration);
            return Mathf.Clamp01((Mathf.Max(0f, elapsed) - minimum) / (maximum - minimum));
        }

        public static float HeavyVisualScale(float normalizedCharge, float minimumScale, float maximumScale)
        {
            float minimum = Mathf.Max(.01f, minimumScale);
            return Mathf.Lerp(minimum, Mathf.Max(minimum, maximumScale), Mathf.Clamp01(normalizedCharge));
        }

        /// <summary>
        /// One final nearest-integer rounding boundary: regular upgrades are
        /// already present in equivalentRegularDamage, and Frenzy remains a
        /// downstream impact rule.
        /// </summary>
        public static int ResolveHeavyDamage(
            int equivalentRegularDamage,
            float visualScale,
            float minimumVisualScale,
            float minimumDamageMultiplier)
        {
            float baseline = Mathf.Max(1, equivalentRegularDamage);
            float sizeRatio = Mathf.Max(.01f, visualScale) / Mathf.Max(.01f, minimumVisualScale);
            return Mathf.Max(1, Mathf.RoundToInt(baseline * Mathf.Max(1f, minimumDamageMultiplier) * sizeRatio));
        }

        public static int HeavyMaximumEnemyHits => 1;
    }
}
