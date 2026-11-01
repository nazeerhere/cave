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
        [SerializeField, Min(0f)] private float heavyMinimumKnockback = ChargedProjectilePolicy.DefaultHeavyMinimumKnockback;
        [SerializeField, Min(0f)] private float heavyMaximumKnockback = ChargedProjectilePolicy.DefaultHeavyMaximumKnockback;
        [SerializeField, Min(0f)] private float heavyRecovery = 0.18f;

        private PlayerMana playerMana;
        private PlayerSpecialMode specialMode;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;
        private PlayerSpecialModeUpgradeState upgradeState;
        private PlayerAimDirection aimDirection;
        private PlayerController playerController;
        private PlayerCombatFlow combatFlow;
        private PlayerFirePatternState firePatterns;
        private PlayerManifestationOrigin manifestationOrigin;
        private bool manifestationDeploymentReleaseGate;
        private float nextFireTime;
        private int burstShotsRemaining;
        private float nextBurstShotAt;
        private FirePatternProfile committedBurstProfile;
        private float spoolStartedAt = -1f;
        private bool heavyRequested;
        private bool heavyCharging;
        private bool heavyManaCommitted;
        private float heavyChargeStartedAt;
        private int heavyChargeTier;
        private int heavyChargePresentationStage;
        private GameObject heavyChargeVisual;
        private SpriteRenderer heavyChargeRenderer;

        public event Action<string> FeedbackRequested;
        public event Action<Vector2> ProjectileFired;
        /// <summary>Actual spawn/initialization only; never an input-intent signal.</summary>
        public event Action<SpecialMode, bool> ProjectileCommitted;
        public event Action HeavyChargeStarted;
        public event Action<int> HeavyChargeStageChanged;
        public event Action HeavyChargeCancelled;
        public event Action HeavyChargeReleased;

        public float ManaCost => manaCost;
        public PlayerManifestationOrigin ManifestationOriginState => manifestationOrigin;
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
            firePatterns = PlayerFirePatternState.EnsureOn(gameObject);
            manifestationOrigin = PlayerManifestationOrigin.EnsureOn(gameObject);
            EnsureSpawnPoint();
        }

        private void OnEnable()
        {
            if (manifestationOrigin == null) manifestationOrigin = PlayerManifestationOrigin.EnsureOn(gameObject);
            if (manifestationOrigin != null) manifestationOrigin.FeedbackRequested += HandleManifestationFeedback;
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

            if (manifestationDeploymentReleaseGate)
            {
                if (!GameInput.FireProjectileHeld) manifestationDeploymentReleaseGate = false;
                return;
            }

            if (GameInput.FireProjectileHeld && GameInput.ChargePressed)
            {
                // A rapid shot already spawned remains valid; this only blocks
                // the next repeat and transfers the held projectile channel at
                // its normal post-shot boundary.
                // A Heavy request takes ownership of this channel. A committed
                // Burst is intentionally cancelled here so delayed ordinary
                // shots cannot leak into a Heavy charge.
                CancelPendingStandardFire();
                heavyRequested = true;
                if (Time.time >= nextFireTime)
                {
                    BeginHeavyCharge();
                }

                return;
            }

            if (burstShotsRemaining > 0)
            {
                UpdateCommittedBurst();
                return;
            }

            if (!GameInput.FireProjectileHeld)
            {
                spoolStartedAt = -1f;
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

            if (firePatterns == null)
            {
                firePatterns = PlayerFirePatternState.EnsureOn(gameObject);
            }


            if (manifestationOrigin == null)
            {
                manifestationOrigin = PlayerManifestationOrigin.EnsureOn(gameObject);
                if (manifestationOrigin != null)
                {
                    manifestationOrigin.FeedbackRequested -= HandleManifestationFeedback;
                    manifestationOrigin.FeedbackRequested += HandleManifestationFeedback;
                }
            }
            if (manifestationOrigin != null && manifestationOrigin.TryConsumeDeploymentPress())
            {
                CancelPendingStandardFire();
                manifestationDeploymentReleaseGate = true;
                return false;
            }

            FirePattern pattern = firePatterns != null
                ? firePatterns.SelectedPattern
                : FirePattern.Automatic;
            FirePatternProfile profile = firePatterns != null
                ? firePatterns.ProfileFor(pattern)
                : default;
            if (pattern == FirePattern.Burst)
            {
                return StartBurst(profile, allowResourceFeedback);
            }

            return CommitStandardGroup(profile, pattern, allowResourceFeedback);
        }

        private bool StartBurst(FirePatternProfile profile, bool allowResourceFeedback)
        {
            Vector2 position;
            Vector2 direction;
            if (!TryResolveStandardLaunchContext(out position, out direction, allowResourceFeedback)
                || !CommitStandardShot(profile, position, direction, allowResourceFeedback))
            {
                nextFireTime = Time.time + ResolvePatternCadence(profile, FirePattern.Burst);
                return false;
            }

            committedBurstProfile = profile;
            burstShotsRemaining = profile.ProjectileCount - 1;
            nextBurstShotAt = Time.time + Mathf.Max(.01f, profile.burstSpacing);
            if (burstShotsRemaining <= 0)
            {
                nextFireTime = Time.time + ResolvePatternCadence(profile, FirePattern.Burst);
            }

            return true;
        }

        private void UpdateCommittedBurst()
        {
            if (Time.time < nextBurstShotAt)
            {
                return;
            }

            if (!CanUseProjectileChannel()
                || !TryResolveStandardLaunchContext(out Vector2 position, out Vector2 direction, false)
                || !CommitStandardShot(committedBurstProfile, position, direction, false))
            {
                CancelPendingStandardFire();
                nextFireTime = Time.time + ResolvePatternCadence(committedBurstProfile, FirePattern.Burst);
                return;
            }

            burstShotsRemaining--;
            if (burstShotsRemaining > 0)
            {
                nextBurstShotAt = Time.time + Mathf.Max(.01f, committedBurstProfile.burstSpacing);
            }
            else
            {
                nextFireTime = Time.time + ResolvePatternCadence(committedBurstProfile, FirePattern.Burst);
            }
        }

        private bool CommitStandardGroup(
            FirePatternProfile profile,
            FirePattern pattern,
            bool allowResourceFeedback)
        {
            int count = profile.ProjectileCount;
            int tier = CurrentProjectileTier;
            float perProjectileMana = ResolvePatternManaCost(profile);
            Vector2 launchPosition;
            Vector2 direction;
            if (!TryResolveStandardLaunchContext(out launchPosition, out direction, allowResourceFeedback))
            {
                nextFireTime = Time.time + ResolvePatternCadence(profile, pattern);
                return false;
            }
            if (playerMana == null
                || !playerMana.TrySpendMana(perProjectileMana * count, tier))
            {
                nextFireTime = Time.time + ResolvePatternCadence(profile, pattern);
                if (allowResourceFeedback) FeedbackRequested?.Invoke("Not enough mana.");
                return false;
            }

            float start = -profile.spreadAngle * .5f;
            float step = count <= 1 ? 0f : profile.spreadAngle / (count - 1);
            for (int index = 0; index < count; index++)
            {
                FireStandardProjectile(profile, launchPosition,
                    Rotate(direction, start + step * index), tier);
            }

            nextFireTime = Time.time + ResolvePatternCadence(profile, pattern);
            return true;
        }

        private bool CommitStandardShot(
            FirePatternProfile profile,
            Vector2 position,
            Vector2 direction,
            bool allowResourceFeedback)
        {
            int tier = CurrentProjectileTier;
            if (playerMana == null || !playerMana.TrySpendMana(ResolvePatternManaCost(profile), tier))
            {
                if (allowResourceFeedback) FeedbackRequested?.Invoke("Not enough mana.");
                return false;
            }

            FireStandardProjectile(profile, position, direction, tier);
            return true;
        }

        private void FireStandardProjectile(
            FirePatternProfile profile,
            Vector2 launchPosition,
            Vector2 direction,
            int currentTier)
        {
            float projectileAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            PlayerProjectile projectile = Instantiate(
                projectilePrefab,
                launchPosition,
                Quaternion.Euler(0f, 0f, projectileAngle));
            int projectileDamage = damageBoost.ResolveProjectileDamage(projectile.BaseDamage);
            if (resourceMastery == null) resourceMastery = GetComponent<PlayerResourceMastery>();
            DamageContext damageContext = resourceMastery != null
                ? resourceMastery.CreateManaDamageContext()
                : default;
            if (upgradeState != null && upgradeState.Settings != null)
            {
                projectileDamage += upgradeState.Settings.GetProjectileDamageBonus(
                    specialMode.CurrentMode, currentTier);
            }

            projectileDamage = Mathf.Max(1, Mathf.RoundToInt(projectileDamage * profile.DamageMultiplier));
            int maximumEnemyHits = ResolveMaximumEnemyHits(specialMode.CurrentMode, currentTier);
            FrenzyBreakActivation frenzyActivation = null;
            if (combatFlow == null) combatFlow = GetComponent<PlayerCombatFlow>();
            combatFlow?.TryCommitFrenzyBreak(FrenzyBreakAttackKind.Projectile, out frenzyActivation);
            projectile.SetFrenzyBreakActivation(frenzyActivation);
            projectile.SetOwner(gameObject);
            projectile.ConfigureStandardPattern(profile.projectileSpeed, profile.intrinsicImpulse);
            projectile.Launch(direction, specialMode.CurrentMode, projectileDamage, damageContext,
                maximumEnemyHits, currentTier, upgradeState != null ? upgradeState.Settings : null);
            CaveSfx.PlayAt(CaveSfxCue.PlayerProjectileLaunch, launchPosition, 0.8f);
            ProjectileFired?.Invoke(direction);
            ProjectileCommitted?.Invoke(specialMode.CurrentMode, false);
        }

        private int CurrentProjectileTier
        {
            get
            {
                if (upgradeState == null) upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
                return upgradeState != null ? upgradeState.GetCurrentTier(specialMode.CurrentMode) : 1;
            }
        }

        private float ResolvePatternManaCost(FirePatternProfile profile)
        {
            return profile.manaCostPerProjectile > 0f ? profile.manaCostPerProjectile : manaCost;
        }

        private float ResolvePatternCadence(FirePatternProfile profile, FirePattern pattern)
        {
            float maximum = profile.cadence > 0f ? profile.cadence : heldFireInterval;
            if (pattern == FirePattern.Burst)
            {
                maximum = Mathf.Max(maximum, profile.recovery);
            }
            if (pattern == FirePattern.SpoolUp)
            {
                if (spoolStartedAt < 0f) spoolStartedAt = Time.time;
                float minimum = profile.minimumCadence > 0f ? profile.minimumCadence : maximum;
                float normalized = Mathf.Clamp01((Time.time - spoolStartedAt)
                    / Mathf.Max(.01f, profile.spoolDuration));
                maximum = Mathf.Lerp(maximum, Mathf.Min(maximum, minimum), normalized);
            }

            PlayerCurseController curses = GetComponent<PlayerCurseController>();
            return Mathf.Max(.01f, Mathf.Max(0f, maximum) / Mathf.Max(.01f,
                curses != null ? curses.AttackSpeedMultiplier : 1f));
        }

        private Vector2 ReadFireDirection()
        {
            if (aimDirection == null) aimDirection = GetComponent<PlayerAimDirection>();
            Vector2 direction = aimDirection != null ? aimDirection.ReadDirection() : Vector2.right;
            return IsGroundedPureDownAim(direction)
                ? (aimDirection != null ? aimDirection.FacingDirection : Vector2.right)
                : direction;
        }

        private bool TryResolveStandardLaunchContext(
            out Vector2 position,
            out Vector2 direction,
            bool allowFeedback)
        {
            if (manifestationOrigin != null && manifestationOrigin.IsDeployed)
            {
                if (manifestationOrigin.TryGetLaunchContext(out position, out direction)) return true;
                position = transform.position;
                direction = Vector2.right;
                if (allowFeedback) FeedbackRequested?.Invoke("Manifestation muzzle is obstructed.");
                return false;
            }

            direction = ReadFireDirection();
            Vector2 localSpawnPosition = direction * Mathf.Abs(spawnOffset.x) + Vector2.up * spawnOffset.y;
            spawnPoint.localPosition = new Vector3(localSpawnPosition.x, localSpawnPosition.y, 0f);
            position = spawnPoint.position;
            return true;
        }

        private void HandleManifestationFeedback(string message)
        {
            FeedbackRequested?.Invoke(message);
        }

        public string DescribePattern(FirePattern pattern)
        {
            if (firePatterns == null) firePatterns = PlayerFirePatternState.EnsureOn(gameObject);
            FirePatternProfile profile = firePatterns.ProfileFor(pattern);
            int tier = CurrentProjectileTier;
            float speed = profile.projectileSpeed > 0f
                ? profile.projectileSpeed
                : projectilePrefab != null ? projectilePrefab.AuthoredSpeed : 0f;
            float cadence = profile.cadence > 0f ? profile.cadence : heldFireInterval;
            float perShotMana = ResolvePatternManaCost(profile);
            int baseDamage = projectilePrefab != null ? projectilePrefab.BaseDamage : 0;
            int effectiveDamage = baseDamage;
            if (projectilePrefab != null && damageBoost != null)
            {
                effectiveDamage = damageBoost.ResolveProjectileDamage(baseDamage);
                if (upgradeState != null && upgradeState.Settings != null)
                {
                    effectiveDamage += upgradeState.Settings.GetProjectileDamageBonus(
                        specialMode.CurrentMode, tier);
                }
                effectiveDamage = Mathf.Max(1, Mathf.RoundToInt(
                    effectiveDamage * profile.DamageMultiplier));
            }
            float impact = StandardProjectileImpulsePolicy.Resolve(
                profile.intrinsicImpulse, speed,
                projectilePrefab != null ? projectilePrefab.AuthoredSpeed : Mathf.Max(.01f, speed));
            string repeat = pattern == FirePattern.Burst
                ? profile.ProjectileCount + " shots, " + profile.burstSpacing.ToString("0.00") + "s spacing"
                : pattern == FirePattern.SpoolUp
                    ? "held ramp " + cadence.ToString("0.00") + "s → "
                        + profile.minimumCadence.ToString("0.00") + "s over "
                        + profile.spoolDuration.ToString("0.00") + "s"
                    : profile.ProjectileCount > 1 ? profile.ProjectileCount + " simultaneous" : "held repeat";
            return "DAMAGE  " + baseDamage + " base / " + effectiveDamage + " effective"
                + "    MANA  " + perShotMana.ToString("0.##") + " each"
                + "    ACTIVATION  unavailable\n"
                + "CADENCE  " + cadence.ToString("0.00") + "s"
                + "    COUNT  " + profile.ProjectileCount
                + "    REPEAT  " + repeat
                + "    RECOVERY  " + profile.recovery.ToString("0.00") + "s\n"
                + "SPREAD  " + profile.spreadAngle.ToString("0.#") + "°"
                + "    SPEED  " + speed.ToString("0.#")
                + "    LAUNCH IMPULSE  " + profile.intrinsicImpulse.ToString("0.##")
                + "    IMPACT  " + impact.ToString("0.##")
                + "    PATH TIER  " + tier;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            return (Quaternion.Euler(0f, 0f, degrees) * direction).normalized;
        }

        private void CancelPendingStandardFire()
        {
            burstShotsRemaining = 0;
            nextBurstShotAt = 0f;
            spoolStartedAt = -1f;
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
            heavyChargePresentationStage = 0;
            heavyChargeTier = upgradeState != null
                ? upgradeState.GetCurrentTier(specialMode.CurrentMode)
                : 1;
            EnsureHeavyChargeVisual();
            UpdateHeavyChargeVisual();
            HeavyChargeStarted?.Invoke();
            // No temporary charge loop is started. This one-shot remains
            // intentionally silent until an authored profile supplies it.
            CaveSfx.PlayAt(CaveSfxCue.HeavyCharge, spawnPoint.position, .65f);
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
            NotifyHeavyChargeStage(elapsed);
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

            // Capture charge once at release. Damage, visual scale, and
            // terminal force must all describe this same committed Heavy.
            float committedCharge = HeavyChargeNormalized;
            float committedVisualScale = ChargedProjectilePolicy.HeavyVisualScale(
                committedCharge,
                heavyMinimumVisualScale,
                heavyMaximumVisualScale);
            float committedKnockback = ChargedProjectilePolicy.HeavyKnockback(
                committedCharge,
                heavyMinimumKnockback,
                heavyMaximumKnockback);
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
            projectile.ConfigureHeavyProjectile(
                committedVisualScale,
                heavyExplosionRadius,
                committedKnockback,
                committedCharge);

            CaveSfx.PlayAt(CaveSfxCue.PlayerProjectileLaunch, spawnPoint.position, 0.95f);
            nextFireTime = Time.time + Mathf.Max(EffectiveFireInterval, heavyRecovery);
            ProjectileFired?.Invoke(direction);
            ProjectileCommitted?.Invoke(specialMode.CurrentMode, true);
            HeavyChargeReleased?.Invoke();
            ResetHeavyCharge();
        }

        private void CancelHeavyCharge()
        {
            // Mana is only spent at the ready/commit threshold. A committed
            // charge intentionally remains spent if a later interruption wins.
            bool wasCharging = heavyCharging;
            ResetHeavyCharge();
            if (wasCharging)
            {
                HeavyChargeCancelled?.Invoke();
            }
        }

        private void ResetHeavyCharge()
        {
            heavyRequested = false;
            heavyCharging = false;
            heavyManaCommitted = false;
            heavyChargeStartedAt = 0f;
            heavyChargePresentationStage = 0;
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

        private void NotifyHeavyChargeStage(float elapsed)
        {
            int nextStage = elapsed >= heavyMaximumChargeTime ? 2
                : elapsed >= heavyMinimumChargeTime ? 1 : 0;
            if (nextStage <= heavyChargePresentationStage)
            {
                return;
            }

            heavyChargePresentationStage = nextStage;
            HeavyChargeStageChanged?.Invoke(nextStage);
            CaveSfx.PlayAt(CaveSfxCue.HeavyChargeStage, spawnPoint.position, .65f);
        }

        private void OnDisable()
        {
            if (manifestationOrigin != null)
                manifestationOrigin.FeedbackRequested -= HandleManifestationFeedback;
            CancelHeavyCharge();
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
        // Calibrated against the existing charged melee (5-10), landmine (11),
        // and bash (12) forces. Heavy's terminal AoE begins stronger than a
        // minimum melee charge and reaches the current high-impact ceiling.
        public const float DefaultHeavyMinimumKnockback = 6f;
        public const float DefaultHeavyMaximumKnockback = 12f;

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
        /// Resolves the one force value captured when Heavy releases. Later
        /// projectile lifetime has no access to charge input or elapsed time.
        /// </summary>
        public static float HeavyKnockback(float normalizedCharge, float minimumKnockback, float maximumKnockback)
        {
            float minimum = Mathf.Max(0f, minimumKnockback);
            return Mathf.Lerp(minimum, Mathf.Max(minimum, maximumKnockback), Mathf.Clamp01(normalizedCharge));
        }

        /// <summary>
        /// Uses explosion-centre displacement when it exists. Exact-centre
        /// targets use the captured travel vector, with a fixed final fallback
        /// so the result is deterministic rather than dependent on physics
        /// query order.
        /// </summary>
        public static Vector2 HeavyExplosionDirection(Vector2 explosionCenter, Vector2 targetPosition, Vector2 travelDirection)
        {
            Vector2 outward = targetPosition - explosionCenter;
            if (outward.sqrMagnitude > .0001f)
            {
                return outward.normalized;
            }

            return travelDirection.sqrMagnitude > .0001f
                ? travelDirection.normalized
                : Vector2.right;
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
