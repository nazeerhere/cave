using System.Collections;
using System.Collections.Generic;
using Cave.Audio;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Player;
using Cave.Progression;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Combat
{
    [RequireComponent(typeof(PlayerAimDirection))]
    public sealed class ChargedAttack : MonoBehaviour
    {
        [Header("Charge")]
        [SerializeField, Min(0.01f)] private float minimumChargeTime = 0.25f;
        [SerializeField, Min(0.01f)] private float maximumChargeTime = 1.5f;

        [Header("Attack")]
        [FormerlySerializedAs("damage")]
        [SerializeField, Range(1, 3)] private int minimumDamage = 1;
        [SerializeField, Range(1, 3)] private int maximumDamage = 3;
        [SerializeField, Min(0f)] private float baseKnockback = 5f;
        [SerializeField, Min(0f)] private float maximumKnockback = 10f;
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
        private float nextAttackTime;
        private bool isCharging;
        private bool isAttacking;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;
        private PlayerAimDirection aimDirection;
        private SpinSwordAttack spinSwordAttack;
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

            currentDamage = minimumDamage;
            SetChargeIndicatorActive(false);
            SetAttackActive(false);
        }

        private void Update()
        {
            if (isCharging && !GameInput.GameplayInputEnabled)
            {
                CancelCharge();
                return;
            }

            if (!isCharging && !isAttacking && Time.time >= nextAttackTime && GameInput.ChargePressed)
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
            }

            damageable.TakeDamage(resolvedDamage, damageContext);

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
            chargeStartedAt = Time.time;
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
                return;
            }

            float chargeAmount = Mathf.InverseLerp(minimumChargeTime, maximumChargeTime, heldDuration);
            currentKnockback = Mathf.Lerp(baseKnockback, maximumKnockback, chargeAmount);
            currentDamage = CalculateBaseDamage(heldDuration);
            currentAttackDirection = aimDirection != null
                ? aimDirection.ReadDirection()
                : Vector2.right;
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.9f);
            StartCoroutine(PerformAttack());
        }

        public int CalculateBaseDamage(float heldDuration)
        {
            float normalizedCharge = Mathf.InverseLerp(
                minimumChargeTime,
                maximumChargeTime,
                Mathf.Clamp(heldDuration, minimumChargeTime, maximumChargeTime));
            int damageStepCount = maximumDamage - minimumDamage + 1;
            int damageStep = Mathf.Min(
                damageStepCount - 1,
                Mathf.FloorToInt(normalizedCharge * damageStepCount));
            return minimumDamage + damageStep;
        }

        private void CancelCharge()
        {
            isCharging = false;
            SetChargeIndicatorActive(false);
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
                swordPivot.localRotation = restingSwordRotation * Quaternion.Euler(0f, 0f, angle);
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
            SetChargeIndicatorActive(false);
            SetAttackActive(false);
            RestoreAttackOrientation();
        }

        private void OnValidate()
        {
            maximumChargeTime = Mathf.Max(minimumChargeTime, maximumChargeTime);
            minimumDamage = Mathf.Clamp(minimumDamage, 1, 3);
            maximumDamage = Mathf.Clamp(maximumDamage, minimumDamage, 3);
        }
    }
}
