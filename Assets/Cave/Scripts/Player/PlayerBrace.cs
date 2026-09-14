using System;
using Cave.Audio;
using Cave.Combat;
using Cave.InputSystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Player
{
    public enum BraceStage { None, Quick, Full, Deep }

    /// <summary>Owns the staged Brace grammar while Guard remains owned by SidewaysParryAttack.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(SpinSwordAttack))]
    public sealed class PlayerBrace : MonoBehaviour
    {
        [Header("Entry")]
        [SerializeField, Range(0.01f, 1f)] private float minimumStaminaFraction = 0.01f;
        [Header("Quick Brace")]
        [SerializeField, Min(0.01f)] private float quickBraceDuration = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float quickMovementMultiplier = 0.72f;
        [FormerlySerializedAs("exitActionStaminaMultiplier")]
        [SerializeField, Range(0.01f, 1f)] private float quickFollowUpStaminaMultiplier = 0.5f;
        [Header("Full Brace")]
        [FormerlySerializedAs("movementMultiplier")]
        [SerializeField, Range(0.1f, 1f)] private float fullMovementMultiplier = 0.33f;
        [SerializeField, Min(0f)] private float earlyRecoveryPerSecond = 75f;
        [SerializeField, Min(0f)] private float sustainedRecoveryPerSecond = 32f;
        [SerializeField, Min(0f)] private float earlyRecoveryDuration = 2.5f;
        [Header("Deep Brace")]
        [SerializeField, Min(0f)] private float deepManaRecoveryPerSecond = 4f;
        [SerializeField, Min(0f)] private float deepSettlingDelay = 0.4f;
        [SerializeField, Min(0f)] private float deepExitRecovery = 0.2f;
        [Header("Deflect / Exit")]
        [SerializeField, Min(0.05f)] private float deflectResponseWindow = 0.2f;
        [Header("Optional Feedback")]
        [SerializeField] private GameObject enterVfxPrefab;
        [SerializeField] private GameObject exitVfxPrefab;
        [SerializeField] private AudioClip enterClip;
        [SerializeField] private AudioClip exitClip;
        [Header("Runtime (Read Only)")]
        [SerializeField] private BraceStage stage;
        [SerializeField] private float braceElapsed;
        [SerializeField] private float deflectWindowRemaining;

        private PlayerController controller;
        private SpinSwordAttack stamina;
        private SidewaysParryAttack guard;
        private PlayerGuardBreak guardBreak;
        private PlayerStunStatus stun;
        private PlayerHealth health;
        private PlayerMana mana;
        private PlayerRecoveryModifiers recoveryModifiers;
        private ChargedAttack chargedAttack;
        private float braceStartedAt;
        private float deepEnteredAt;
        private float deepExitUntil;
        private float deflectExpiresAt;
        private bool exitDiscountPending;

        public event Action<BraceStage, BraceStage> BraceStageChanged;
        public BraceStage CurrentStage => stage;
        public bool IsBraced => stage != BraceStage.None;
        public bool IsQuickBrace => stage == BraceStage.Quick;
        public bool IsFullBrace => stage == BraceStage.Full;
        public bool IsDeepBrace => stage == BraceStage.Deep;
        public bool IsActionLocked => stage == BraceStage.Deep || Time.time < deepExitUntil;
        public bool IsDeflectWindowActive => stage == BraceStage.Full && Time.time <= deflectExpiresAt;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stamina = GetComponent<SpinSwordAttack>();
            guard = GetComponent<SidewaysParryAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            stun = GetComponent<PlayerStunStatus>();
            health = GetComponent<PlayerHealth>();
            mana = GetComponent<PlayerMana>();
            recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            chargedAttack = GetComponent<ChargedAttack>();
        }

        private void OnEnable()
        {
            if (health != null) { health.Died += ClearBrace; health.Respawned += ClearBrace; }
        }

        private void OnDisable()
        {
            if (health != null) { health.Died -= ClearBrace; health.Respawned -= ClearBrace; }
            ClearBrace();
        }

        private void Update()
        {
            if (stage == BraceStage.None)
            {
                if (Time.time >= deepExitUntil) controller?.SetBraceMovementMultiplier(1f);
                return;
            }

            braceElapsed = Mathf.Max(0f, Time.time - braceStartedAt);
            deflectWindowRemaining = Mathf.Max(0f, deflectExpiresAt - Time.time);
            if (stage == BraceStage.Deep) { UpdateDeepBrace(); return; }
            if (!CanContinueBrace()) { ClearBrace(); return; }
            if (stage == BraceStage.Quick && braceElapsed >= quickBraceDuration)
            {
                SetStage(BraceStage.Full);
                controller?.SetBraceMovementMultiplier(fullMovementMultiplier);
            }

            if (stage == BraceStage.Full) RestoreFullBraceStamina();
        }

        public bool TryEnterFromGuardBreak()
        {
            if (stage == BraceStage.Full) return TryEnterDeepBrace();
            if (stage != BraceStage.None || guard == null || !guard.IsBraceEntryWindowActive || stamina == null
                || stamina.CurrentStamina < stamina.MaximumStamina * minimumStaminaFraction || controller == null
                || !controller.IsGrounded || controller.IsExternallyMovementLocked
                || (stun != null && stun.IsStunned) || (health != null && health.CurrentHealth <= 0)) return false;

            braceStartedAt = Time.time;
            deepExitUntil = 0f;
            deflectExpiresAt = 0f;
            exitDiscountPending = false;
            SetStage(BraceStage.Quick);
            controller.SetBraceMovementMultiplier(quickMovementMultiplier);
            Spawn(enterVfxPrefab);
            CaveSfx.PlayClip(enterClip, 0.7f);
            return true;
        }

        public bool TryDeflectIncoming(DamageContext context)
        {
            if (stage != BraceStage.Full || context.HasTrait(DamageTrait.Piercing)
                || context.HasTrait(DamageTrait.AreaOfEffect) || context.HasTrait(DamageTrait.GuardBreak)) return false;
            deflectExpiresAt = Time.time + deflectResponseWindow;
            if (GameInput.BasicAttackPressed) return LeaveForSpin();
            return GameInput.ChargePressed && LeaveForHeavy();
        }

        public bool LeaveForSpin()
        {
            if (!BeginActionExit()) return false;
            if (stamina != null && stamina.TryBeginBraceExitAttack()) return true;
            exitDiscountPending = false;
            return false;
        }

        public bool LeaveForHeavy()
        {
            if (!BeginActionExit()) return false;
            if (chargedAttack != null && chargedAttack.TryBeginBraceExitCharge()) return true;
            exitDiscountPending = false;
            return false;
        }

        public float ConsumeExitActionStaminaMultiplier()
        {
            if (!exitDiscountPending) return 1f;
            exitDiscountPending = false;
            return quickFollowUpStaminaMultiplier;
        }

        public void BreakForEnemyGuardBreak() { ClearBrace(); }

        private bool TryEnterDeepBrace()
        {
            if (stage != BraceStage.Full || controller == null || controller.IsExternallyMovementLocked
                || (stun != null && stun.IsStunned)) return false;
            deepEnteredAt = Time.time;
            deflectExpiresAt = 0f;
            exitDiscountPending = false;
            SetStage(BraceStage.Deep);
            controller.SetBraceMovementMultiplier(0f);
            guard?.CancelForDeepBrace();
            return true;
        }

        private void UpdateDeepBrace()
        {
            if (!CanContinueDeepBrace()) { ExitDeepBrace(); return; }
            controller?.SetBraceMovementMultiplier(0f);
            // Deep Brace is an extension of Full Brace, not a replacement for it:
            // preserve the same stamina recovery calculation (and its Tower modifier)
            // while Deep's separate settling delay gates only its mana recovery.
            RestoreFullBraceStamina();
            if (Time.time < deepEnteredAt + deepSettlingDelay || mana == null) return;
            if (recoveryModifiers == null) recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            float multiplier = recoveryModifiers != null ? recoveryModifiers.ManaRegenerationMultiplier : 1f;
            mana.RestoreMana(deepManaRecoveryPerSecond * multiplier * Time.deltaTime);
        }

        private void RestoreFullBraceStamina()
        {
            if (stamina == null) return;
            if (recoveryModifiers == null) recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            float multiplier = recoveryModifiers != null ? recoveryModifiers.StaminaRegenerationMultiplier : 1f;
            float rate = braceElapsed <= earlyRecoveryDuration ? earlyRecoveryPerSecond : sustainedRecoveryPerSecond;
            stamina.RestoreStamina(rate * multiplier * Time.deltaTime);
        }

        private bool BeginActionExit()
        {
            if (stage != BraceStage.Quick && stage != BraceStage.Full) return false;
            exitDiscountPending = stage == BraceStage.Quick;
            EndBrace();
            return true;
        }

        private bool CanContinueBrace()
        {
            return guard != null && guard.IsGuardHeld && guard.CurrentPhase == ParryPhase.Guard && controller != null
                && !controller.IsExternallyMovementLocked && (stun == null || !stun.IsStunned)
                && (guardBreak == null || guardBreak.CanUseCombatActions);
        }

        private bool CanContinueDeepBrace()
        {
            return GameInput.ParryHeld && controller != null && !controller.IsExternallyMovementLocked
                && (stun == null || !stun.IsStunned) && (guardBreak == null || guardBreak.CanUseCombatActions)
                && (health == null || health.CurrentHealth > 0);
        }

        private void ExitDeepBrace()
        {
            SetStage(BraceStage.None);
            braceElapsed = 0f;
            deflectExpiresAt = 0f;
            deflectWindowRemaining = 0f;
            deepExitUntil = Time.time + deepExitRecovery;
            controller?.SetBraceMovementMultiplier(0f);
            Spawn(exitVfxPrefab);
            CaveSfx.PlayClip(exitClip, 0.6f);
        }

        private void ClearBrace()
        {
            exitDiscountPending = false;
            deepExitUntil = 0f;
            EndBrace();
        }

        private void EndBrace()
        {
            if (stage == BraceStage.None) return;
            SetStage(BraceStage.None);
            braceElapsed = 0f;
            deflectExpiresAt = 0f;
            deflectWindowRemaining = 0f;
            controller?.SetBraceMovementMultiplier(1f);
            Spawn(exitVfxPrefab);
            CaveSfx.PlayClip(exitClip, 0.6f);
        }

        private void SetStage(BraceStage newStage)
        {
            if (stage == newStage) return;
            BraceStage previous = stage;
            stage = newStage;
            BraceStageChanged?.Invoke(previous, newStage);
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab != null) Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }
}
