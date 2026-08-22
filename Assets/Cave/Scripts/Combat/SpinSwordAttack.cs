using System;
using System.Collections.Generic;
using Cave.Audio;
using Cave.InputSystem;
using Cave.Player;
using Cave.Progression;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class SpinSwordAttack : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField, Min(1f)] private float spinDegreesPerSecond = 900f;
        [SerializeField, Min(1)] private int damage = 1;

        [Header("Stamina")]
        [SerializeField, Min(0.01f)] private float maximumStamina = 300f;
        [SerializeField, Min(0f)] private float staminaDrainPerSecond = 35f;
        [SerializeField, Min(0f)] private float staminaRegenerationPerSecond = 25f;
        [SerializeField, Min(0f)] private float regenerationDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float minimumStaminaToBegin = 10f;

        [Header("Sword References")]
        [SerializeField] private Transform swordPivot;
        [SerializeField] private GameObject swordVisual;
        [SerializeField] private Collider2D attackCollider;
        [SerializeField] private LayerMask damageableLayers;

        private readonly Dictionary<Damageable, int> activeTargetContacts = new Dictionary<Damageable, int>();
        private readonly List<Damageable> inactiveTargetBuffer = new List<Damageable>();
        private Quaternion restingRotation;
        private float currentStamina;
        private float regenerationStartsAt;
        private bool isAttacking;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;

        public event Action<float, float> StaminaChanged;

        public float CurrentStamina => currentStamina;
        public float MaximumStamina => maximumStamina;
        public bool IsAttacking => isAttacking;
        public GameObject SwordVisualObject => swordVisual;
        public Transform SwordPivot => swordPivot;

        public bool UsesAttackCollider(Collider2D candidate)
        {
            return candidate != null && candidate == attackCollider;
        }

        private void Awake()
        {
            damageBoost = GetComponent<PlayerDamageBoost>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
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
            activeTargetContacts.Clear();
            SetAttackColliderActive(true);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.65f);
        }

        private void UpdateAttack()
        {
            RemoveInactiveTargetContacts();

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
            activeTargetContacts.Clear();
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

        public bool RestoreStamina(float amount)
        {
            if (amount <= 0f || currentStamina >= maximumStamina)
            {
                return false;
            }

            SetStamina(currentStamina + amount);
            return true;
        }

        public bool CanSpendStamina(float amount)
        {
            return amount >= 0f && currentStamina >= amount;
        }

        public bool TrySpendStamina(float amount)
        {
            if (amount < 0f || currentStamina < amount)
            {
                return false;
            }

            if (amount > 0f)
            {
                SetStamina(currentStamina - amount);
                regenerationStartsAt = Time.time + regenerationDelay;
            }

            return true;
        }

        public bool IncreaseMaximumStamina(float amount, bool addIncreaseToCurrentStamina = true)
        {
            if (amount <= 0f)
            {
                return false;
            }

            maximumStamina += amount;
            if (addIncreaseToCurrentStamina)
            {
                currentStamina = Mathf.Min(maximumStamina, currentStamina + amount);
            }

            StaminaChanged?.Invoke(currentStamina, maximumStamina);
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isAttacking || (damageableLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (damageable == null
                || damageable.CurrentHealth <= 0
                || !damageable.gameObject.activeInHierarchy)
            {
                return;
            }

            if (activeTargetContacts.TryGetValue(damageable, out int contactCount))
            {
                activeTargetContacts[damageable] = contactCount + 1;
                return;
            }

            activeTargetContacts.Add(damageable, 1);
            if (damageBoost == null)
            {
                damageBoost = GetComponent<PlayerDamageBoost>();
            }

            bool manaWasConsumed = false;
            int resolvedDamage = damageBoost != null
                ? damageBoost.ResolveSpinDamage(damage, out manaWasConsumed)
                : damage;
            if (resourceMastery == null)
            {
                resourceMastery = GetComponent<PlayerResourceMastery>();
            }

            DamageContext damageContext = resourceMastery != null
                ? resourceMastery.CreateSpinDamageContext(manaWasConsumed).WithTraits(DamageTrait.Melee)
                : default;
            damageable.TakeDamage(resolvedDamage, damageContext);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if ((damageableLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (damageable == null || !activeTargetContacts.TryGetValue(damageable, out int contactCount))
            {
                return;
            }

            if (contactCount > 1)
            {
                activeTargetContacts[damageable] = contactCount - 1;
            }
            else
            {
                activeTargetContacts.Remove(damageable);
            }
        }

        private void RemoveInactiveTargetContacts()
        {
            inactiveTargetBuffer.Clear();
            foreach (KeyValuePair<Damageable, int> contact in activeTargetContacts)
            {
                if (contact.Key == null || !contact.Key.gameObject.activeInHierarchy)
                {
                    inactiveTargetBuffer.Add(contact.Key);
                }
            }

            foreach (Damageable damageable in inactiveTargetBuffer)
            {
                activeTargetContacts.Remove(damageable);
            }
        }

        private void OnDisable()
        {
            isAttacking = false;
            activeTargetContacts.Clear();

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
