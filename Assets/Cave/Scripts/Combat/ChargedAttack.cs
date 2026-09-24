using System;
using System.Collections;
using System.Collections.Generic;
using Cave.Axioms.Elemental;
using Cave.Axioms.Convergence;
using Cave.Audio;
using Cave.Axioms.Phase;
using Cave.Axioms.Vfx;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Player;
using Cave.Progression;
using UnityEngine;

namespace Cave.Combat
{
    [RequireComponent(typeof(PlayerAimDirection))]
    public sealed class ChargedAttack : MonoBehaviour
    {
        public event Action<Damageable> HeavyTargetHit;

        [Header("Charge Timing")]
        [SerializeField, Min(0.01f)] private float minimumChargeTime = 0.25f;
        [SerializeField, Min(0.01f)] private float chargedTwoThreshold = 0.67f;
        [SerializeField, Min(0.01f)] private float maximumChargeTime = 1.5f;

        [Header("Damage Tiers")]
        [SerializeField, Range(1, 100)] private int chargedOneDamage = 4;
        [SerializeField, Range(1, 100)] private int chargedTwoDamage = 9;
        [SerializeField, Range(1, 100)] private int chargedThreeDamage = 18;

        [Header("Knockback")]
        [SerializeField, Min(0f)] private float baseKnockback = 5f;
        [SerializeField, Min(0f)] private float maximumKnockback = 10f;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] private float activeDuration = 0.2f;
        [SerializeField, Min(0f)] private float cooldown = 0.5f;
        [SerializeField, Min(0f)] private float directionalReach = 1.1f;
        [SerializeField] private LayerMask damageableLayers;

        [Header("Tier-1 Heavy Chain")]
        [SerializeField, Min(0.05f)] private float continuationWindow = 0.38f;
        [SerializeField, Min(0f)] private float breakerStrikeDelay = 0.05f;
        [SerializeField, Min(0f)] private float reversalStrikeDelay = 0.04f;
        [SerializeField, Min(0f)] private float reaperStrikeDelay = 0.07f;
        [SerializeField, Min(0f)] private float driveStrikeDelay = 0.06f;
        [SerializeField, Min(0f)] private float judgmentStrikeDelay = 0.1f;
        [SerializeField, Min(0f)] private float breakerRecovery = 0.14f;
        [SerializeField, Min(0f)] private float reversalRecovery = 0.12f;
        [SerializeField, Min(0f)] private float reaperRecovery = 0.19f;
        [SerializeField, Min(0f)] private float driveRecovery = 0.2f;
        [SerializeField, Min(0f)] private float judgmentRecovery = 0.28f;

        [Header("References")]
        [SerializeField] private GameObject chargeIndicator;
        [SerializeField] private GameObject attackVisual;
        [SerializeField] private Collider2D attackCollider;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private readonly ElementalAxiomApplicationReceipt axiomApplicationReceipt = new ElementalAxiomApplicationReceipt();
        private float chargeStartedAt;
        private float chargeStartingDuration;
        private float currentKnockback;
        private int currentDamage;
        private int currentChargeTier;
        private float nextAttackTime;
        private int currentAttackSequence;
        private bool isCharging;
        private bool isAttacking;
        // Explicit Tier-1 state: 0..4 maps to H1 Breaker through H5 Judgment.
        private int heavyChainIndex;
        private int activeHeavyIndex;
        private bool chainWindowOpen;
        private float chainWindowEndsAt;
        private int heavyVisualRevision;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;
        private PlayerAimDirection aimDirection;
        private SpinSwordAttack spinSwordAttack;
        private PlayerGuardBreak guardBreak;
        private PlayerCombatFlow combatFlow;
        private PlayerFlightBash groundSlam;
        private PlayerCurseController curseController;
        private FrenzyBreakActivation frenzyActivation;
        private Transform attackTransform;
        private Transform swordPivot;
        private Vector2 currentAttackDirection = Vector2.right;
        private Vector3 restingAttackPosition;
        private Quaternion restingAttackRotation;
        private Quaternion restingSwordRotation;
        private PhaseCombatState ownPhaseCombatState;
        private AxiomVfxPresenter axiomVfxPresenter;
        private PlayerHealth playerHealth;
        private PlayerSwordCosmetics swordCosmetics;

        public bool IsCharging => isCharging;
        public bool IsAttacking => isAttacking;
        public int CurrentAttackSequence => currentAttackSequence;
        public int HeavyChainIndex => heavyChainIndex;
        public int ActiveHeavyIndex => activeHeavyIndex;
        public int HeavyVisualRevision => heavyVisualRevision;
        public int HeavyVisualFrameStart => isCharging ? 0 : 2;

        private void Awake()
        {
            ResolveAuthoredHitboxReferences();
            damageBoost = GetComponent<PlayerDamageBoost>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            aimDirection = GetComponent<PlayerAimDirection>();
            spinSwordAttack = GetComponent<SpinSwordAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            combatFlow = GetComponent<PlayerCombatFlow>();
            groundSlam = GetComponent<PlayerFlightBash>();
            curseController = GetComponent<PlayerCurseController>();
            ownPhaseCombatState = GetComponent<PhaseCombatState>();
            axiomVfxPresenter = GetComponent<AxiomVfxPresenter>();
            playerHealth = GetComponent<PlayerHealth>();
            swordCosmetics = GetComponent<PlayerSwordCosmetics>();
            swordPivot = spinSwordAttack != null ? spinSwordAttack.SwordPivot : null;
            attackTransform = attackCollider != null
                ? attackCollider.transform
                : attackVisual != null
                    ? attackVisual.transform
                    : null;
            if (attackTransform != null)
            {
                restingAttackPosition = attackTransform.localPosition;
                restingAttackRotation = attackTransform.localRotation;
            }

            if (swordPivot != null)
            {
                restingSwordRotation = swordPivot.localRotation;
            }

            currentDamage = chargedOneDamage;
            currentChargeTier = 1;
            SetChargeIndicatorActive(false);
            SetAttackActive(false);
            activeHeavyIndex = 0;
        }

        private void OnEnable()
        {
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null) return;
            playerHealth.Died -= ResetHeavyState;
            playerHealth.Died += ResetHeavyState;
            playerHealth.Respawned -= ResetHeavyState;
            playerHealth.Respawned += ResetHeavyState;
        }

        /// <summary>
        /// Repairs only the known Player prefab omission at runtime when an older
        /// serialized prefab has lost the three charged-hitbox references. This
        /// is intentionally a one-time, direct child lookup; it is not a scene
        /// scan and never broadens the damage mask beyond the project's dedicated
        /// Damageable layer.
        /// </summary>
        private void ResolveAuthoredHitboxReferences()
        {
            Transform hitboxTransform = transform.Find("Charged Attack Hitbox");
            Transform indicatorTransform = transform.Find("Charge Indicator");
            if (hitboxTransform == null)
            {
                return;
            }

            // Historical prefab data pointed at an unrelated prefab asset rather
            // than the Player's authored indicator child. Use only that child.
            if (indicatorTransform != null
                && (chargeIndicator == null || !chargeIndicator.transform.IsChildOf(transform)))
            {
                chargeIndicator = indicatorTransform.gameObject;
            }

            if (attackCollider == null)
            {
                attackCollider = hitboxTransform.GetComponent<Collider2D>();
            }

            if (attackVisual == null && hitboxTransform.GetComponent<SpriteRenderer>() != null)
            {
                attackVisual = hitboxTransform.gameObject;
            }

            if (damageableLayers.value == 0)
            {
                int damageableLayer = LayerMask.NameToLayer("Damageable");
                if (damageableLayer >= 0)
                {
                    damageableLayers = 1 << damageableLayer;
                }
            }
        }

        private void Update()
        {
            if (guardBreak == null)
            {
                guardBreak = GetComponent<PlayerGuardBreak>();
            }

            if (guardBreak != null && !guardBreak.CanUseCombatActions)
            {
                groundSlam?.CancelAerialHeavyPreparation();
                if (isCharging)
                {
                    CancelCharge();
                }

                return;
            }

            if (isCharging && !GameInput.GameplayInputEnabled)
            {
                CancelCharge();
                return;
            }

            if (!GameInput.GameplayInputEnabled)
            {
                groundSlam?.CancelAerialHeavyPreparation();
                return;
            }

            if (chainWindowOpen && Time.time > chainWindowEndsAt)
            {
                ResetChain();
            }

            PlayerBrace brace = GetComponent<PlayerBrace>();
            if (brace != null && brace.IsActionLocked)
            {
                groundSlam?.CancelAerialHeavyPreparation();
                if (isCharging)
                {
                    CancelCharge();
                }

                return;
            }

            bool projectileChannelHeld = GameInput.FireProjectileHeld;
            if (!isCharging && !isAttacking && !projectileChannelHeld
                && brace != null && brace.IsBraced && GameInput.ChargePressed)
            {
                brace.LeaveForHeavy();
                return;
            }

            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            if (groundSlam == null)
            {
                groundSlam = GetComponent<PlayerFlightBash>();
            }

            if (!isCharging
                && !isAttacking
                && !projectileChannelHeld
                && groundSlam != null
                && groundSlam.HandleAerialHeavyInput())
            {
                return;
            }

            bool heldFormalFollowUp = combatFlow != null
                && combatFlow.ChargedStartingTier > 0
                && GameInput.ChargeHeld;
            if (!isCharging
                && !isAttacking
                && Time.time >= nextAttackTime
                && !projectileChannelHeld
                && (GameInput.ChargePressed || heldFormalFollowUp))
            {
                BeginCharge();
            }

            if (!isCharging)
            {
                return;
            }

            UpdateChargeIndicator();

            if (GameInput.ChargeReleased)
            {
                ReleaseCharge();
            }
        }

        public void HandleHit(Collider2D other)
        {
            if (!isAttacking || (damageableLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (damageable == null || !hitTargets.Add(damageable))
            {
                return;
            }

            if (damageBoost == null)
            {
                damageBoost = GetComponent<PlayerDamageBoost>();
            }

            bool manaWasConsumed = false;
            int resolvedDamage = damageBoost != null
                ? damageBoost.ResolveChargedDamage(currentDamage, out manaWasConsumed)
                : currentDamage;
            int frenzyDamage = resolvedDamage;
            bool isFrenzyCritical = frenzyActivation != null
                && frenzyActivation.TryResolveDamage(
                    resolvedDamage,
                    damageable,
                    out frenzyDamage);
            if (isFrenzyCritical)
            {
                resolvedDamage = frenzyDamage;
            }
            manaWasConsumed |= frenzyActivation != null && frenzyActivation.ManaInfused;
            if (resourceMastery == null)
            {
                resourceMastery = GetComponent<PlayerResourceMastery>();
            }

            DamageContext damageContext = default;
            if (resourceMastery != null)
            {
                damageContext = manaWasConsumed
                    ? resourceMastery.CreateManaDamageContext().WithTraits(DamageTrait.Melee)
                    : resourceMastery.CreatePlayerDamageContext().WithTraits(DamageTrait.Melee);
                damageContext = damageContext.WithTraits(
                    currentChargeTier >= 3
                        ? DamageTrait.StaggerHeavy
                        : DamageTrait.StaggerNormal);
            }

            if (isFrenzyCritical)
            {
                damageContext = damageContext.WithTraits(DamageTrait.FrenzyCritical);
            }

            PhaseCombatState phaseBeforeHit = damageable.GetComponent<PhaseCombatState>();
            bool hadLatentPhaseBeforeHit = phaseBeforeHit != null && phaseBeforeHit.LatentStacks > 0;
            bool hadActivePhaseExposureBeforeHit = phaseBeforeHit != null && phaseBeforeHit.HasActiveExposure;
            bool imaginaryWasActive = HasActiveImaginaryState();
            Vector3 hitPosition = other.ClosestPoint(transform.position);
            int appliedDamage = damageable.TakeDamageResolved(resolvedDamage, damageContext);
            ElementalAxiomCombatBridge.TryApplyPlayerModeDirectHit(
                gameObject,
                damageable,
                appliedDamage,
                isFrenzyCritical,
                damageContext,
                Time.time,
                axiomApplicationReceipt);
            if (appliedDamage > 0 && hadLatentPhaseBeforeHit)
            {
                PhaseCombatState.TryCollapseFromChargedHit(
                    damageable.gameObject,
                    currentChargeTier,
                    gameObject,
                    Time.time);
            }
            if (imaginaryWasActive && appliedDamage > 0)
            {
                PlayImaginaryImpact(hitPosition);
            }
            if (appliedDamage > 0)
            {
                AxiomConvergenceState.TryRecognize(
                    gameObject,
                    damageable.gameObject,
                    currentChargeTier,
                    isFrenzyCritical,
                    hadActivePhaseExposureBeforeHit,
                    currentAttackSequence,
                    Time.time);
            }
            HeavyTargetHit?.Invoke(damageable);
            if (frenzyActivation != null
                && (frenzyActivation.ManaInfused || isFrenzyCritical))
            {
                frenzyActivation.ApplyImpact(
                    damageable,
                    currentAttackDirection,
                    appliedDamage,
                    isFrenzyCritical);
            }

            if (!damageable.gameObject.activeInHierarchy)
            {
                return;
            }

            KnockbackReceiver receiver = damageable.GetComponent<KnockbackReceiver>();
            if (receiver != null)
            {
                Vector2 direction = currentAttackDirection.sqrMagnitude > 0.001f
                    ? currentAttackDirection
                    : Vector2.right;
                receiver.ApplyKnockback(direction * currentKnockback);
            }
        }

        private void BeginCharge()
        {
            // Heavy owns the shared sword immediately; Spin cannot keep draining
            // Stamina or leave its collider/rotation active beneath the windup.
            spinSwordAttack?.StopForCommittedFollowUp();
            chainWindowOpen = false;
            chainWindowEndsAt = 0f;
            isCharging = true;
            activeHeavyIndex = Mathf.Clamp(heavyChainIndex, 0, 4);
            heavyVisualRevision++;
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            int startingTier = combatFlow != null ? combatFlow.ConsumeChargedStartingTier() : 0;
            float startingDuration = StartingDurationForTier(
                startingTier,
                minimumChargeTime,
                chargedTwoThreshold,
                maximumChargeTime);
            chargeStartingDuration = startingDuration;
            chargeStartedAt = Time.time;
            SetChargeIndicatorActive(true);
            PrepareHeavyPresentation();
            UpdateChargeIndicator();
        }

        private bool HasActiveImaginaryState()
        {
            if (ownPhaseCombatState == null)
            {
                ownPhaseCombatState = GetComponent<PhaseCombatState>();
            }

            return ownPhaseCombatState != null && ownPhaseCombatState.HasOpening;
        }

        private void PlayImaginaryImpact(Vector3 hitPosition)
        {
            if (axiomVfxPresenter == null)
            {
                axiomVfxPresenter = GetComponent<AxiomVfxPresenter>();
            }

            axiomVfxPresenter?.PlayImaginaryImpact(hitPosition);
        }

        public bool TryBeginBraceExitCharge()
        {
            if (isCharging || isAttacking || Time.time < nextAttackTime)
            {
                return false;
            }

            BeginCharge();
            return true;
        }

        private void ReleaseCharge()
        {
            float heldDuration = CurrentChargeDuration;
            isCharging = false;
            SetChargeIndicatorActive(false);

            float chargeAmount = Mathf.InverseLerp(minimumChargeTime, maximumChargeTime, heldDuration);
            currentKnockback = Mathf.Lerp(baseKnockback, maximumKnockback, chargeAmount);
            currentChargeTier = CalculateChargeTier(heldDuration);
            currentDamage = DamageForTier(currentChargeTier);
            frenzyActivation = null;
            curseController?.NotifyOffensiveCommitment();
            combatFlow?.TryCommitFrenzyBreak(
                FrenzyBreakAttackKind.Charged,
                out frenzyActivation);
            currentAttackDirection = aimDirection != null
                ? aimDirection.ReadDirection()
                : Vector2.right;
            currentAttackDirection = ResolveHeavyDirection(currentAttackDirection, activeHeavyIndex);
            heavyVisualRevision++;
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.9f);
            StartCoroutine(PerformAttack());
            combatFlow?.NotifyChargedCommitted();
        }

        public int CalculateBaseDamage(float heldDuration)
        {
            return DamageForTier(CalculateChargeTier(heldDuration));
        }

        public int CalculateChargeTier(float heldDuration)
        {
            if (heldDuration >= maximumChargeTime)
            {
                return 3;
            }

            return heldDuration >= chargedTwoThreshold ? 2 : 1;
        }

        internal static float StartingDurationForTier(
            int startingTier,
            float minimumTierOneDuration,
            float tierTwoDuration,
            float tierThreeDuration)
        {
            if (startingTier >= 3) return tierThreeDuration;
            if (startingTier == 2) return tierTwoDuration;
            return startingTier == 1 ? minimumTierOneDuration : 0f;
        }

        private int DamageForTier(int tier)
        {
            if (tier >= 3)
            {
                return chargedThreeDamage;
            }

            return tier == 2 ? chargedTwoDamage : chargedOneDamage;
        }

        private void CancelCharge()
        {
            isCharging = false;
            SetChargeIndicatorActive(false);
            SetAttackActive(false);
            ResetChain();
            RestoreAttackOrientation();
            combatFlow?.NotifyChargedCancelled();
        }

        private IEnumerator PerformAttack()
        {
            currentAttackSequence++;
            float attackSpeed = ResolveAttackSpeedMultiplier();
            float resolvedActiveDuration = activeDuration / attackSpeed;
            float resolvedStrikeDelay = StrikeDelayFor(activeHeavyIndex) / attackSpeed;
            float resolvedRecovery = RecoveryFor(activeHeavyIndex) / attackSpeed;
            isAttacking = true;
            nextAttackTime = Time.time + resolvedStrikeDelay + resolvedActiveDuration + resolvedRecovery;
            hitTargets.Clear();
            axiomApplicationReceipt.Clear();
            OrientAttack(currentAttackDirection);

            if (resolvedStrikeDelay > 0f)
            {
                yield return new WaitForSeconds(resolvedStrikeDelay);
            }
            SetAttackActive(true);

            yield return new WaitForSeconds(resolvedActiveDuration);

            SetAttackActive(false);
            if (resolvedRecovery > 0f)
            {
                yield return new WaitForSeconds(resolvedRecovery);
            }
            RestoreAttackOrientation();
            isAttacking = false;
            frenzyActivation?.Complete();
            frenzyActivation = null;
            ResolveChainAfterAttack();
        }

        private void OrientAttack(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (attackTransform != null)
            {
                if (swordCosmetics == null)
                {
                    swordCosmetics = GetComponent<PlayerSwordCosmetics>();
                }

                float reachMultiplier = swordCosmetics != null
                    ? swordCosmetics.EffectiveReachMultiplier
                    : 1f;
                Vector3 directionalOffset = (Vector3)(direction.normalized
                    * directionalReach * reachMultiplier);
                attackTransform.localPosition = restingAttackPosition + directionalOffset;
                attackTransform.localRotation = restingAttackRotation * Quaternion.Euler(0f, 0f, angle);
            }

            if (swordPivot != null)
            {
                if (spinSwordAttack != null)
                {
                    spinSwordAttack.ApplyHeavyPresentationPose(
                        direction,
                        SwordAngleFor(activeHeavyIndex),
                        SwordOffsetFor(activeHeavyIndex));
                }
                else
                {
                    swordPivot.localRotation = restingSwordRotation
                        * Quaternion.Euler(0f, 0f, angle + SwordAngleFor(activeHeavyIndex));
                }
            }
        }

        private void RestoreAttackOrientation()
        {
            if (attackTransform != null)
            {
                attackTransform.localPosition = restingAttackPosition;
                attackTransform.localRotation = restingAttackRotation;
            }

            if (swordPivot != null)
            {
                if (spinSwordAttack != null) spinSwordAttack.RestoreSwordPresentation();
                else swordPivot.localRotation = restingSwordRotation;
            }
        }

        private void PrepareHeavyPresentation()
        {
            Vector2 direction = aimDirection != null ? aimDirection.ReadDirection() : Vector2.right;
            if (spinSwordAttack != null)
            {
                spinSwordAttack.ApplyHeavyPresentationPose(
                    direction,
                    SwordAngleFor(activeHeavyIndex),
                    SwordOffsetFor(activeHeavyIndex));
            }
        }

        private void ResolveChainAfterAttack()
        {
            // Tier 2/3 are commitment finishers; H5 is the Tier-1 finisher.
            if (currentChargeTier >= 2 || activeHeavyIndex >= 4)
            {
                ResetChain();
                return;
            }

            heavyChainIndex = activeHeavyIndex + 1;
            chainWindowOpen = true;
            // The old serialized cooldown remains a pacing ceiling for legacy
            // prefabs; the authored continuation window stays authoritative.
            chainWindowEndsAt = Time.time + Mathf.Min(continuationWindow, Mathf.Max(0.05f, cooldown));
        }

        private void ResetChain()
        {
            heavyChainIndex = 0;
            activeHeavyIndex = 0;
            chainWindowOpen = false;
            chainWindowEndsAt = 0f;
        }

        private void ResetHeavyState()
        {
            StopAllCoroutines();
            isCharging = false;
            isAttacking = false;
            SetChargeIndicatorActive(false);
            SetAttackActive(false);
            frenzyActivation?.Complete();
            frenzyActivation = null;
            nextAttackTime = 0f;
            ResetChain();
            RestoreAttackOrientation();
        }

        private float StrikeDelayFor(int index)
        {
            switch (index)
            {
                case 1: return reversalStrikeDelay;
                case 2: return reaperStrikeDelay;
                case 3: return driveStrikeDelay;
                case 4: return judgmentStrikeDelay;
                default: return breakerStrikeDelay;
            }
        }

        private float RecoveryFor(int index)
        {
            switch (index)
            {
                case 1: return reversalRecovery;
                case 2: return reaperRecovery;
                case 3: return driveRecovery;
                case 4: return judgmentRecovery;
                default: return breakerRecovery;
            }
        }

        private static Vector2 ResolveHeavyDirection(Vector2 direction, int index)
        {
            Vector2 fallback = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            switch (index)
            {
                case 0: return (fallback + Vector2.down * 0.22f).normalized; // Breaker
                case 1: // Reversal: take the rising cut back across the opening line.
                    return new Vector2(-Mathf.Sign(fallback.x == 0f ? 1f : fallback.x) * 0.45f, 0.9f).normalized;
                case 2: return new Vector2(Mathf.Sign(fallback.x == 0f ? 1f : fallback.x), 0f); // Reaper
                case 3: return new Vector2(Mathf.Sign(fallback.x == 0f ? 1f : fallback.x), 0f); // Drive
                case 4: return (fallback + Vector2.down * 0.38f).normalized; // Judgment
                default: return fallback;
            }
        }

        private static float SwordAngleFor(int index)
        {
            switch (index)
            {
                case 0: return -34f;
                case 1: return 52f;
                case 2: return 0f;
                case 3: return -18f;
                case 4: return -68f;
                default: return 0f;
            }
        }

        private static Vector2 SwordOffsetFor(int index)
        {
            switch (index)
            {
                case 0: return new Vector2(0.03f, -0.02f);
                case 1: return new Vector2(-0.02f, 0.05f);
                case 2: return new Vector2(0.04f, 0f);
                case 3: return new Vector2(0.08f, -0.01f);
                case 4: return new Vector2(0f, 0.07f);
                default: return Vector2.zero;
            }
        }

        private void UpdateChargeIndicator()
        {
            if (chargeIndicator == null)
            {
                return;
            }

            float chargeAmount = Mathf.Clamp01(CurrentChargeDuration / maximumChargeTime);
            float scale = Mathf.Lerp(0.4f, 1.1f, chargeAmount);
            chargeIndicator.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void SetChargeIndicatorActive(bool active)
        {
            if (chargeIndicator != null)
            {
                chargeIndicator.SetActive(active);
            }
        }

        private void SetAttackActive(bool active)
        {
            if (attackVisual != null)
            {
                bool isPersistentSword = spinSwordAttack != null
                    && attackVisual == spinSwordAttack.SwordVisualObject;
                if (!isPersistentSword)
                {
                    attackVisual.SetActive(active);
                }
                else if (active)
                {
                    attackVisual.SetActive(true);
                }
            }

            if (attackCollider != null)
            {
                attackCollider.enabled = active;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= ResetHeavyState;
                playerHealth.Respawned -= ResetHeavyState;
            }
            ResetHeavyState();
            combatFlow?.NotifyChargedCancelled();
        }

        private float CurrentChargeDuration => Mathf.Min(
            maximumChargeTime,
            chargeStartingDuration
                + Mathf.Max(0f, Time.time - chargeStartedAt) * ResolveAttackSpeedMultiplier());

        private float ResolveAttackSpeedMultiplier()
        {
            return curseController != null
                ? Mathf.Max(0.01f, curseController.AttackSpeedMultiplier)
                : 1f;
        }

        private void OnValidate()
        {
            chargedTwoThreshold = Mathf.Max(minimumChargeTime, chargedTwoThreshold);
            maximumChargeTime = Mathf.Max(chargedTwoThreshold, maximumChargeTime);
            chargedOneDamage = Mathf.Clamp(chargedOneDamage, 1, 100);
            chargedTwoDamage = Mathf.Clamp(chargedTwoDamage, 1, 100);
            chargedThreeDamage = Mathf.Clamp(chargedThreeDamage, 1, 100);
        }
    }
}
