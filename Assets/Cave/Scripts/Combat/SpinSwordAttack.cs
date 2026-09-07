using System;
using System.Collections.Generic;
using Cave.Axioms.Elemental;
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
        [SerializeField] private float hitVfxScale = 0.12f;
        [SerializeField] private GameObject hitVfxPrefab;

        private readonly Dictionary<Damageable, int> activeTargetContacts = new Dictionary<Damageable, int>();
        private readonly ElementalAxiomApplicationReceipt axiomApplicationReceipt = new ElementalAxiomApplicationReceipt();
        private readonly Dictionary<object, float> staminaCostModifiers = new Dictionary<object, float>();
        private readonly List<Damageable> inactiveTargetBuffer = new List<Damageable>();
        private Quaternion restingRotation;
        private Vector3 authoredPivotPosition;
        private Vector3 authoredPivotScale;
        private float authoredSwordFacing = 1f;
        private float currentSwordFacing = 1f;
        private float currentStamina;
        private float regenerationStartsAt;
        private bool isAttacking;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;
        private PlayerGuardBreak guardBreak;
        private PlayerAimDirection aimDirection;
        private ChargedAttack chargedAttack;
        private PlayerRecoveryModifiers recoveryModifiers;
        private PlayerCombatFlow combatFlow;
        private FrenzyBreakActivation frenzyActivation;

        public event Action<float, float> StaminaChanged;

        public float CurrentStamina => currentStamina;
        public float MaximumStamina => maximumStamina;
        public float StaminaCostMultiplier => ResolveStaminaCostMultiplier();
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
            guardBreak = GetComponent<PlayerGuardBreak>();
            aimDirection = GetComponent<PlayerAimDirection>();
            chargedAttack = GetComponent<ChargedAttack>();
            recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            combatFlow = GetComponent<PlayerCombatFlow>();
            if (swordPivot != null)
            {
                restingRotation = swordPivot.localRotation;
                authoredPivotPosition = swordPivot.localPosition;
                authoredPivotScale = swordPivot.localScale;
                authoredSwordFacing = ResolveAuthoredSwordFacing();
            }

            currentStamina = maximumStamina;
            if (GetComponent<PlayerBrace>() == null)
            {
                // Brace is an additive player capability. Installing it here keeps
                // manually authored player prefabs and scene instances untouched.
                gameObject.AddComponent<PlayerBrace>();
            }
            RefreshSwordFacing();
            SetSwordVisible(true);
            SetAttackColliderActive(false);
        }

        private void Update()
        {
            RefreshSwordFacing();

            PlayerBrace brace = GetComponent<PlayerBrace>();
            if (!isAttacking && brace != null && brace.IsBraced && GameInput.BasicAttackPressed)
            {
                brace.LeaveForSpin();
                return;
            }

            if (guardBreak == null)
            {
                guardBreak = GetComponent<PlayerGuardBreak>();
            }

            if (guardBreak != null && !guardBreak.CanUseCombatActions)
            {
                if (isAttacking)
                {
                    StopAttack();
                }
                else
                {
                    RegenerateStamina();
                }

                return;
            }

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
            GetComponent<PlayerCurseController>()?.NotifyOffensiveCommitment();
            activeTargetContacts.Clear();
            axiomApplicationReceipt.Clear();
            frenzyActivation = null;
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            combatFlow?.TryCommitFrenzyBreak(
                FrenzyBreakAttackKind.Spin,
                out frenzyActivation);
            SetAttackColliderActive(true);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.65f);
        }

        private void UpdateAttack()
        {
            frenzyActivation?.KeepAlive();
            RemoveInactiveTargetContacts();

            if (!GameInput.BasicAttackHeld || currentStamina <= 0f)
            {
                StopAttack();
                return;
            }

            if (swordPivot != null)
            {
                float spinDirection = currentSwordFacing >= 0f ? -1f : 1f;
                PlayerCurseController curses = GetComponent<PlayerCurseController>();
                swordPivot.Rotate(
                    0f,
                    0f,
                    spinDegreesPerSecond
                        * (curses != null ? curses.AttackSpeedMultiplier : 1f)
                        * spinDirection
                        * Time.deltaTime,
                    Space.Self);
            }

            PlayerBrace brace = GetComponent<PlayerBrace>();
            float braceExitMultiplier = brace != null
                ? brace.ConsumeExitActionStaminaMultiplier()
                : 1f;
            if (!TrySpendStamina(staminaDrainPerSecond * braceExitMultiplier * Time.deltaTime))
            {
                StopAttack();
            }
        }

        public bool TryBeginBraceExitAttack()
        {
            if (isAttacking || currentStamina < MinimumStartStamina)
            {
                return false;
            }

            BeginAttack();
            return true;
        }

        private void StopAttack()
        {
            isAttacking = false;
            frenzyActivation?.Complete();
            frenzyActivation = null;
            activeTargetContacts.Clear();
            regenerationStartsAt = Time.time + regenerationDelay;
            SetAttackColliderActive(false);

            if (swordPivot != null)
            {
                swordPivot.localRotation = restingRotation;
            }
        }

        public void StopForCommittedFollowUp()
        {
            if (isAttacking)
            {
                StopAttack();
            }
        }

        public float PrepareSwordForDirection(Vector2 direction)
        {
            float facing = Mathf.Abs(direction.x) > 0.001f
                ? Mathf.Sign(direction.x)
                : currentSwordFacing;
            ApplySwordFacing(facing);

            Vector2 normalized = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : new Vector2(facing, 0f);
            float angleFromFacing = Mathf.Atan2(
                normalized.y,
                Mathf.Abs(normalized.x)) * Mathf.Rad2Deg;
            return angleFromFacing * facing;
        }

        private void RefreshSwordFacing()
        {
            if (swordPivot == null || (chargedAttack != null && chargedAttack.IsAttacking))
            {
                return;
            }

            float facing = currentSwordFacing;
            if (aimDirection != null)
            {
                aimDirection.ReadDirection();
                facing = aimDirection.FacingDirection.x;
            }
            else if (!Mathf.Approximately(GameInput.Horizontal, 0f))
            {
                facing = Mathf.Sign(GameInput.Horizontal);
            }

            ApplySwordFacing(facing);
        }

        private void ApplySwordFacing(float facing)
        {
            if (swordPivot == null || Mathf.Abs(facing) <= 0.001f)
            {
                return;
            }

            currentSwordFacing = Mathf.Sign(facing);
            float mirror = currentSwordFacing == authoredSwordFacing ? 1f : -1f;
            swordPivot.localPosition = new Vector3(
                authoredPivotPosition.x * mirror,
                authoredPivotPosition.y,
                authoredPivotPosition.z);
            swordPivot.localScale = new Vector3(
                authoredPivotScale.x * mirror,
                authoredPivotScale.y,
                authoredPivotScale.z);
        }

        private float ResolveAuthoredSwordFacing()
        {
            if (swordVisual != null
                && swordVisual.transform.IsChildOf(swordPivot)
                && Mathf.Abs(swordVisual.transform.localPosition.x) > 0.001f)
            {
                return Mathf.Sign(
                    swordVisual.transform.localPosition.x * authoredPivotScale.x);
            }

            return Mathf.Abs(authoredPivotPosition.x) > 0.001f
                ? Mathf.Sign(authoredPivotPosition.x)
                : 1f;
        }

        private void RegenerateStamina()
        {
            if (Time.time < regenerationStartsAt || currentStamina >= maximumStamina)
            {
                return;
            }

            if (recoveryModifiers == null)
            {
                recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            }

            float regenerationMultiplier = recoveryModifiers != null
                ? recoveryModifiers.StaminaRegenerationMultiplier
                : 1f;
            SetStamina(
                currentStamina
                + staminaRegenerationPerSecond * regenerationMultiplier * Time.deltaTime);
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
            return amount >= 0f && currentStamina >= ResolveStaminaCost(amount);
        }

        public bool TrySpendStamina(float amount)
        {
            float resolvedAmount = ResolveStaminaCost(amount);
            if (amount < 0f || currentStamina < resolvedAmount)
            {
                return false;
            }

            if (resolvedAmount > 0f)
            {
                SetStamina(currentStamina - resolvedAmount);
                regenerationStartsAt = Time.time + regenerationDelay;
            }

            return true;
        }

        /// <summary>
        /// Registers a temporary multiplier for authoritative Stamina costs.  Each
        /// source owns its entry so status effects can be removed without affecting
        /// another modifier that happens to be active at the same time.
        /// </summary>
        public void SetStaminaCostModifier(object source, float multiplier)
        {
            if (source == null)
            {
                return;
            }

            staminaCostModifiers[source] = Mathf.Clamp(multiplier, 0.01f, 10f);
        }

        public void RemoveStaminaCostModifier(object source)
        {
            if (source != null)
            {
                staminaCostModifiers.Remove(source);
            }
        }

        private float ResolveStaminaCost(float baseAmount)
        {
            return Mathf.Max(0f, baseAmount) * ResolveStaminaCostMultiplier();
        }

        private float ResolveStaminaCostMultiplier()
        {
            float multiplier = 1f;
            foreach (KeyValuePair<object, float> entry in staminaCostModifiers)
            {
                multiplier *= entry.Value;
            }

            return Mathf.Max(0f, multiplier);
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

        public bool ReduceMaximumStamina(float amount, float minimumMaximumStamina = 1f)
        {
            float permitted = Mathf.Min(
                Mathf.Max(0f, amount),
                Mathf.Max(0f, maximumStamina - Mathf.Max(1f, minimumMaximumStamina)));
            if (permitted <= 0f)
            {
                return false;
            }

            maximumStamina -= permitted;
            currentStamina = Mathf.Min(currentStamina, maximumStamina);
            StaminaChanged?.Invoke(currentStamina, maximumStamina);
            return true;
        }

        public void ResetCurrentStamina()
        {
            if (isAttacking)
            {
                StopAttack();
            }

            SetStamina(maximumStamina);
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

            DamageContext damageContext = resourceMastery != null
                ? resourceMastery.CreateSpinDamageContext(manaWasConsumed).WithTraits(DamageTrait.Melee)
                : default;
            if (isFrenzyCritical)
            {
                damageContext = damageContext.WithTraits(DamageTrait.FrenzyCritical);
            }

            int appliedDamage = damageable.TakeDamageResolved(resolvedDamage, damageContext);
            ElementalAxiomCombatBridge.TryApplyPlayerModeDirectHit(
                gameObject,
                damageable,
                appliedDamage,
                isFrenzyCritical,
                damageContext,
                Time.time,
                axiomApplicationReceipt);
            Vector2 hitDirection = damageable.transform.position - transform.position;
            if (isFrenzyCritical)
            {
                frenzyActivation.ApplyImpact(damageable, hitDirection, appliedDamage);
            }
            if (hitVfxPrefab != null)
            {
                Vector3 hitPosition = other.ClosestPoint(transform.position);

                GameObject vfx = Instantiate(
                    hitVfxPrefab,
                    hitPosition,
                    Quaternion.identity
                );

                vfx.transform.localScale *= hitVfxScale;
            }
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
            frenzyActivation?.Complete();
            frenzyActivation = null;
            activeTargetContacts.Clear();

            if (swordPivot != null)
            {
                swordPivot.localRotation = restingRotation;
            }

            RefreshSwordFacing();
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
