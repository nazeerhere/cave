using System.Collections;
using System.Collections.Generic;
using Cave.Audio;
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

        [Header("References")]
        [SerializeField] private GameObject chargeIndicator;
        [SerializeField] private GameObject attackVisual;
        [SerializeField] private Collider2D attackCollider;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private float chargeStartedAt;
        private float currentKnockback;
        private int currentDamage;
        private int currentChargeTier;
        private float nextAttackTime;
        private bool isCharging;
        private bool isAttacking;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;
        private PlayerAimDirection aimDirection;
        private SpinSwordAttack spinSwordAttack;
        private PlayerGuardBreak guardBreak;
        private PlayerCombatFlow combatFlow;
        private PlayerFlightBash groundSlam;
        private FrenzyBreakActivation frenzyActivation;
        private Transform attackTransform;
        private Transform swordPivot;
        private Vector2 currentAttackDirection = Vector2.right;
        private Vector3 restingAttackPosition;
        private Quaternion restingAttackRotation;
        private Quaternion restingSwordRotation;

        public bool IsCharging => isCharging;
        public bool IsAttacking => isAttacking;

        private void Awake()
        {
            damageBoost = GetComponent<PlayerDamageBoost>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            aimDirection = GetComponent<PlayerAimDirection>();
            spinSwordAttack = GetComponent<SpinSwordAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            combatFlow = GetComponent<PlayerCombatFlow>();
            groundSlam = GetComponent<PlayerFlightBash>();
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
                manaWasConsumed |= frenzyActivation.ManaInfused;
            }
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

            int appliedDamage = damageable.TakeDamageResolved(resolvedDamage, damageContext);
            if (isFrenzyCritical)
            {
                frenzyActivation.ApplyImpact(
                    damageable,
                    currentAttackDirection,
                    appliedDamage);
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
            isCharging = true;
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            int startingTier = combatFlow != null ? combatFlow.ConsumeChargedStartingTier() : 0;
            float startingDuration = startingTier >= 2
                ? chargedTwoThreshold
                : startingTier == 1
                    ? minimumChargeTime
                    : 0f;
            chargeStartedAt = Time.time - startingDuration;
            SetChargeIndicatorActive(true);
            UpdateChargeIndicator();
        }

        private void ReleaseCharge()
        {
            float heldDuration = Mathf.Min(Time.time - chargeStartedAt, maximumChargeTime);
            isCharging = false;
            SetChargeIndicatorActive(false);

            if (heldDuration < minimumChargeTime)
            {
                combatFlow?.NotifyChargedCancelled();
                return;
            }

            float chargeAmount = Mathf.InverseLerp(minimumChargeTime, maximumChargeTime, heldDuration);
            currentKnockback = Mathf.Lerp(baseKnockback, maximumKnockback, chargeAmount);
            currentChargeTier = CalculateChargeTier(heldDuration);
            currentDamage = DamageForTier(currentChargeTier);
            frenzyActivation = null;
            combatFlow?.TryCommitFrenzyBreak(
                FrenzyBreakAttackKind.Charged,
                out frenzyActivation);
            currentAttackDirection = aimDirection != null
                ? aimDirection.ReadDirection()
                : Vector2.right;
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
            combatFlow?.NotifyChargedCancelled();
        }

        private IEnumerator PerformAttack()
        {
            isAttacking = true;
            nextAttackTime = Time.time + activeDuration + cooldown;
            hitTargets.Clear();
            OrientAttack(currentAttackDirection);
            SetAttackActive(true);

            yield return new WaitForSeconds(activeDuration);

            SetAttackActive(false);
            RestoreAttackOrientation();
            isAttacking = false;
            frenzyActivation?.Complete();
            frenzyActivation = null;
        }

        private void OrientAttack(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (attackTransform != null)
            {
                Vector3 directionalOffset = (Vector3)(direction.normalized * directionalReach);
                attackTransform.localPosition = restingAttackPosition + directionalOffset;
                attackTransform.localRotation = restingAttackRotation * Quaternion.Euler(0f, 0f, angle);
            }

            if (swordPivot != null)
            {
                float swordAngle = spinSwordAttack != null
                    ? spinSwordAttack.PrepareSwordForDirection(direction)
                    : angle;
                swordPivot.localRotation = restingSwordRotation
                    * Quaternion.Euler(0f, 0f, swordAngle);
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
                swordPivot.localRotation = restingSwordRotation;
            }
        }

        private void UpdateChargeIndicator()
        {
            if (chargeIndicator == null)
            {
                return;
            }

            float chargeAmount = Mathf.Clamp01((Time.time - chargeStartedAt) / maximumChargeTime);
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
            StopAllCoroutines();
            isCharging = false;
            isAttacking = false;
            frenzyActivation?.Complete();
            frenzyActivation = null;
            combatFlow?.NotifyChargedCancelled();
            SetChargeIndicatorActive(false);
            SetAttackActive(false);
            RestoreAttackOrientation();
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
