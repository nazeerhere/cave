using System.Collections;
using System.Collections.Generic;
using Cave.Enemies;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class ChargedAttack : MonoBehaviour
    {
        [Header("Charge")]
        [SerializeField, Min(0.01f)] private float minimumChargeTime = 0.25f;
        [SerializeField, Min(0.01f)] private float maximumChargeTime = 1.5f;

        [Header("Attack")]
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float baseKnockback = 5f;
        [SerializeField, Min(0f)] private float maximumKnockback = 10f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.2f;
        [SerializeField, Min(0f)] private float cooldown = 0.5f;
        [SerializeField] private LayerMask damageableLayers;

        [Header("References")]
        [SerializeField] private GameObject chargeIndicator;
        [SerializeField] private GameObject attackVisual;
        [SerializeField] private Collider2D attackCollider;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private float chargeStartedAt;
        private float currentKnockback;
        private float nextAttackTime;
        private bool isCharging;
        private bool isAttacking;

        public bool IsCharging => isCharging;

        private void Awake()
        {
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

            damageable.TakeDamage(damage);

            if (!damageable.gameObject.activeInHierarchy)
            {
                return;
            }

            KnockbackReceiver receiver = damageable.GetComponent<KnockbackReceiver>();
            if (receiver != null)
            {
                float horizontalDirection = Mathf.Sign(damageable.transform.position.x - transform.position.x);
                if (Mathf.Approximately(horizontalDirection, 0f))
                {
                    horizontalDirection = 1f;
                }

                Vector2 direction = new Vector2(horizontalDirection, 0.2f).normalized;
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
            StartCoroutine(PerformAttack());
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
            SetAttackActive(true);

            yield return new WaitForSeconds(activeDuration);

            SetAttackActive(false);
            isAttacking = false;
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
                attackVisual.SetActive(active);
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
        }
    }
}
