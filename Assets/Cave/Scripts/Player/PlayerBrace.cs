using System;
using Cave.Audio;
using Cave.Axioms;
using Cave.Axioms.Elemental;
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
        [Header("Deep Brace")]
        [SerializeField, Min(0f)] private float deepExitRecovery = 0.2f;
        [Header("Deflect / Exit")]
        [SerializeField, Min(0.05f)] private float deflectResponseWindow = 0.2f;
        [SerializeField, Min(0f)] private float braceRetentionDuration = 1.5f;
        [SerializeField, Min(0f)] private float quickFollowUpVulnerabilityDuration = 0.35f;
        [SerializeField, Min(0f)] private float quickFollowUpInterruptDuration = 0.25f;
        [Header("Optional Feedback")]
        [SerializeField] private GameObject enterVfxPrefab;
        [SerializeField] private GameObject exitVfxPrefab;
        [SerializeField] private AudioClip enterClip;
        [SerializeField] private AudioClip exitClip;
        [Header("Runtime (Read Only)")]
        [SerializeField] private BraceStage stage;
        [SerializeField] private float braceElapsed;
        [SerializeField] private float deflectWindowRemaining;
        [SerializeField] private float braceRetentionRemaining;

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
        private float deepExitUntil;
        private float deflectExpiresAt;
        private float braceRetentionExpiresAt;
        private float quickFollowUpVulnerableUntil;
        private bool exitDiscountPending;
        private bool quickFollowUpPunished;

        public event Action<BraceStage, BraceStage> BraceStageChanged;
        public BraceStage CurrentStage => stage;
        public bool IsBraced => stage != BraceStage.None;
        public bool IsQuickBrace => stage == BraceStage.Quick;
        public bool IsFullBrace => stage == BraceStage.Full;
        public bool IsDeepBrace => stage == BraceStage.Deep;
        public bool IsActionLocked => stage == BraceStage.Deep || Time.time < deepExitUntil;
        public bool IsDeflectWindowActive => Time.time <= deflectExpiresAt;
        public bool HasBraceRetention => Time.time <= braceRetentionExpiresAt;

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
            if (HasBraceRetention
                && ((guardBreak != null && !guardBreak.CanUseCombatActions)
                    || (stun != null && stun.IsStunned)
                    || (health != null && health.CurrentHealth <= 0f)))
            {
                braceRetentionExpiresAt = 0f;
            }

            braceRetentionRemaining = Mathf.Max(0f, braceRetentionExpiresAt - Time.time);

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

            if (stage == BraceStage.Full) RestoreBraceResources();
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
            if (HasBraceRetention)
            {
                braceRetentionExpiresAt = 0f;
                SetStage(BraceStage.Full);
                controller.SetBraceMovementMultiplier(fullMovementMultiplier);
                Spawn(enterVfxPrefab);
                return true;
            }

            SetStage(BraceStage.Quick);
            controller.SetBraceMovementMultiplier(quickMovementMultiplier);
            Spawn(enterVfxPrefab);
            return true;
        }

        public bool TryDeflectIncoming(DamageContext context)
        {
            if (context.HasTrait(DamageTrait.Piercing)
                || context.HasTrait(DamageTrait.AreaOfEffect)
                || context.HasTrait(DamageTrait.GuardBreak)
                || context.HasTrait(DamageTrait.Projectile)
                || context.HasTrait(DamageTrait.Unblockable)
                || !context.HasTrait(DamageTrait.Melee)
                || Time.time > deflectExpiresAt)
            {
                return false;
            }

            // The window is armed when the valid Full-Brace offensive action starts,
            // never by sampling fresh input at incoming-hit time.
            deflectExpiresAt = 0f;
            braceRetentionExpiresAt = Time.time + braceRetentionDuration;
            return true;
        }

        /// <summary>One bounded recovery interruption for an unsafe Quick-Brace follow-up.</summary>
        public bool TryPunishUnsafeQuickFollowUp(DamageContext context)
        {
            if (quickFollowUpPunished
                || Time.time > quickFollowUpVulnerableUntil
                || !context.HasTrait(DamageTrait.Melee)
                || context.HasTrait(DamageTrait.Projectile)
                || context.HasTrait(DamageTrait.AreaOfEffect))
            {
                return false;
            }

            quickFollowUpPunished = true;
            quickFollowUpVulnerableUntil = 0f;
            guardBreak?.ApplyExternalInterruption(quickFollowUpInterruptDuration);
            controller?.ApplyExternalControlLock(quickFollowUpInterruptDuration);
            return true;
        }

        public bool LeaveForSpin()
        {
            bool wasQuick = stage == BraceStage.Quick;
            bool wasFull = stage == BraceStage.Full;
            if (!BeginActionExit()) return false;
            if (stamina != null && stamina.TryBeginBraceExitAttack())
            {
                ArmOffensiveCommitment(wasQuick, wasFull);
                AxiomPhenomenonApplicationBridge.Apply(gameObject, AxiomKind.Potential, 1f, gameObject, null, Time.time);
                return true;
            }
            exitDiscountPending = false;
            return false;
        }

        public bool LeaveForHeavy()
        {
            bool wasQuick = stage == BraceStage.Quick;
            bool wasFull = stage == BraceStage.Full;
            if (!BeginActionExit()) return false;
            if (chargedAttack != null && chargedAttack.TryBeginBraceExitCharge())
            {
                ArmOffensiveCommitment(wasQuick, wasFull);
                AxiomPhenomenonApplicationBridge.Apply(gameObject, AxiomKind.Potential, 1f, gameObject, null, Time.time);
                return true;
            }
            exitDiscountPending = false;
            return false;
        }

        /// <summary>Consumes no state transition; a successful Burst remains in Deep Brace.</summary>
        public bool TryUseResonantRestorationBurst()
        {
            if (!IsDeepBrace || mana == null)
            {
                return false;
            }

            PlayerResonantCarapace carapace = GetComponent<PlayerResonantCarapace>();
            int restored;
            return carapace != null && carapace.TryRestoreWithBurst(mana, out restored);
        }

        public float ConsumeExitActionStaminaMultiplier()
        {
            if (!exitDiscountPending) return 1f;
            exitDiscountPending = false;
            return quickFollowUpStaminaMultiplier;
        }

        public void BreakForEnemyGuardBreak() { ClearBrace(); }

        private void ArmOffensiveCommitment(bool wasQuick, bool wasFull)
        {
            // A Brace attack cannot retain ordinary sustained-Guard protection.
            // This makes the Quick-Brace risk observable through the existing
            // damage/interruption path instead of granting an accidental block.
            guard?.CancelForBraceOffense();

            if (wasQuick)
            {
                // Queue a normal Guard only after this attack has completed;
                // the guard authority performs the held-input and prohibition
                // checks, so Brace and Guard can never overlap defensively.
                guard?.QueueGuardAfterBraceAttack();
                quickFollowUpPunished = false;
                quickFollowUpVulnerableUntil = Time.time + quickFollowUpVulnerabilityDuration;
            }

            if (wasFull)
            {
                deflectExpiresAt = Time.time + deflectResponseWindow;
            }
        }

        private bool TryEnterDeepBrace()
        {
            if (stage != BraceStage.Full || controller == null || controller.IsExternallyMovementLocked
                || (stun != null && stun.IsStunned)) return false;
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
            RestoreBraceResources();
        }

        private void RestoreBraceResources()
        {
            float maximumStamina = stamina != null ? stamina.MaximumStamina : 0f;
            float maximumMana = mana != null ? mana.MaximumMana : 0f;
            BraceResourceRecovery recovery = BraceResourceRecoveryPolicy.Resolve(
                stage,
                maximumStamina,
                maximumMana,
                Time.deltaTime);

            if (recoveryModifiers == null) recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            if (stamina != null && recovery.Stamina > 0f)
            {
                float multiplier = recoveryModifiers != null ? recoveryModifiers.StaminaRegenerationMultiplier : 1f;
                stamina.RestoreStamina(recovery.Stamina * multiplier);
            }

            if (mana != null && recovery.Mana > 0f)
            {
                float multiplier = recoveryModifiers != null ? recoveryModifiers.ManaRegenerationMultiplier : 1f;
                float resolvedMana = recovery.Mana * multiplier;
                PlayerResonantCarapace carapace = GetComponent<PlayerResonantCarapace>();
                float diverted = carapace != null && IsDeepBrace
                    ? carapace.DivertDeepBraceMana(resolvedMana)
                    : 0f;
                mana.RestoreMana(Mathf.Max(0f, resolvedMana - diverted));
            }
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
        }

        private void ClearBrace()
        {
            exitDiscountPending = false;
            deepExitUntil = 0f;
            deflectExpiresAt = 0f;
            deflectWindowRemaining = 0f;
            braceRetentionExpiresAt = 0f;
            quickFollowUpVulnerableUntil = 0f;
            quickFollowUpPunished = false;
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
        }

        private void SetStage(BraceStage newStage)
        {
            if (stage == newStage) return;
            BraceStage previous = stage;
            stage = newStage;
            BraceStageChanged?.Invoke(previous, newStage);
            PlayStageCue(newStage);
        }

        // Legacy inspector clips stay serialized for compatibility but no
        // longer play alongside the semantic cue route.
        private static void PlayStageCue(BraceStage next)
        {
            switch (next)
            {
                case BraceStage.Quick:
                    CaveSfx.Play(CaveSfxCue.BraceQuick, .7f);
                    break;
                case BraceStage.Full:
                    CaveSfx.Play(CaveSfxCue.BraceFull, .7f);
                    break;
                case BraceStage.Deep:
                    CaveSfx.Play(CaveSfxCue.BraceDeep, .75f);
                    break;
                case BraceStage.None:
                    CaveSfx.Play(CaveSfxCue.BraceExit, .6f);
                    break;
            }
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab != null) Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }
}
