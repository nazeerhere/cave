using System;
using System.Collections.Generic;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class SpinSwordAttack : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField, Min(1f)] private float spinDegreesPerSecond = 900f;
        [SerializeField, Min(1)] private int damage = 1;

        [Header("Stamina")]
        [SerializeField, Min(0.01f)] private float maximumStamina = 100f;
        [SerializeField, Min(0f)] private float staminaDrainPerSecond = 35f;
        [SerializeField, Min(0f)] private float staminaRegenerationPerSecond = 25f;
        [SerializeField, Min(0f)] private float regenerationDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float minimumStaminaToBegin = 10f;

        [Header("Sword References")]
        [SerializeField] private Transform swordPivot;
        [SerializeField] private GameObject swordVisual;
        [SerializeField] private Collider2D attackCollider;
        [SerializeField] private LayerMask damageableLayers;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private Quaternion restingRotation;
        private float currentStamina;
        private float regenerationStartsAt;
        private bool isAttacking;

        public event Action<float, float> StaminaChanged;

        public float CurrentStamina => currentStamina;
        public float MaximumStamina => maximumStamina;
        public bool IsAttacking => isAttacking;

        public bool UsesAttackCollider(Collider2D candidate)
        {
            return candidate != null && candidate == attackCollider;
        }

        private void Awake()
        {
            if (swordPivot != null)
            {
                restingRotation = swordPivot.localRotation;
            }

            currentStamina = maximumStamina;
            SetSwordVisible(true);
            SetAttackColliderActive(false);
        }

        private void Update()
        {
            if (isAttacking)
            {
                UpdateAttack();
                return;
            }

            RegenerateStamina();

            if (GameInput.BasicAttackHeld && currentStamina >= MinimumStartStamina)
            {
                BeginAttack();
            }
        }

        private float MinimumStartStamina => Mathf.Min(minimumStaminaToBegin, maximumStamina);

        private void BeginAttack()
        {
            isAttacking = true;
            hitTargets.Clear();
            SetAttackColliderActive(true);
        }

        private void UpdateAttack()
        {
            if (!GameInput.BasicAttackHeld || currentStamina <= 0f)
            {
                StopAttack();
                return;
            }

            if (swordPivot != null)
            {
                swordPivot.Rotate(0f, 0f, -spinDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            SetStamina(currentStamina - staminaDrainPerSecond * Time.deltaTime);
            if (currentStamina <= 0f)
            {
                StopAttack();
            }
        }

        private void StopAttack()
        {
            isAttacking = false;
            regenerationStartsAt = Time.time + regenerationDelay;
            SetAttackColliderActive(false);

            if (swordPivot != null)
            {
                swordPivot.localRotation = restingRotation;
            }
        }

        private void RegenerateStamina()
        {
            if (Time.time < regenerationStartsAt || currentStamina >= maximumStamina)
            {
                return;
            }

            SetStamina(currentStamina + staminaRegenerationPerSecond * Time.deltaTime);
        }

        private void SetStamina(float value)
        {
            float clampedValue = Mathf.Clamp(value, 0f, maximumStamina);
            if (Mathf.Approximately(currentStamina, clampedValue))
            {
                return;
            }

            currentStamina = clampedValue;
            StaminaChanged?.Invoke(currentStamina, maximumStamina);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isAttacking || (damageableLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (damageable != null && hitTargets.Add(damageable))
            {
                damageable.TakeDamage(damage);
            }
        }

        private void OnDisable()
        {
            isAttacking = false;

            if (swordPivot != null)
            {
                swordPivot.localRotation = restingRotation;
            }

            SetSwordVisible(true);
            SetAttackColliderActive(false);
        }

        private void SetSwordVisible(bool visible)
        {
            if (swordVisual != null)
            {
                swordVisual.SetActive(visible);
            }
        }

        private void SetAttackColliderActive(bool active)
        {
            if (attackCollider != null)
            {
                attackCollider.enabled = active;
            }
        }

        private void OnValidate()
        {
            minimumStaminaToBegin = Mathf.Min(minimumStaminaToBegin, maximumStamina);
        }
    }
}
