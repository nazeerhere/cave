using Cave.Audio;
using Cave.Combat;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// A deliberate low-stamina Guard state. Guard itself remains owned by
    /// SidewaysParryAttack; Brace only owns contextual entry, recovery and exit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(SpinSwordAttack))]
    public sealed class PlayerBrace : MonoBehaviour
    {
        [Header("Entry")]
        [SerializeField, Range(0.01f, 1f)] private float minimumStaminaFraction = 0.01f;

        [Header("Active Brace")]
        [SerializeField, Range(0.1f, 1f)] private float movementMultiplier = 0.33f;
        [SerializeField, Min(0f)] private float earlyRecoveryPerSecond = 75f;
        [SerializeField, Min(0f)] private float sustainedRecoveryPerSecond = 32f;
        [SerializeField, Min(0f)] private float earlyRecoveryDuration = 2.5f;

        [Header("Deflect / Exit")]
        [SerializeField, Min(0.05f)] private float deflectResponseWindow = 0.2f;
        [SerializeField, Range(0.01f, 1f)] private float exitActionStaminaMultiplier = 0.5f;

        [Header("Optional Feedback")]
        [SerializeField] private GameObject enterVfxPrefab;
        [SerializeField] private GameObject exitVfxPrefab;
        [SerializeField] private AudioClip enterClip;
        [SerializeField] private AudioClip exitClip;

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool isBraced;
        [SerializeField] private float braceElapsed;
        [SerializeField] private float deflectWindowRemaining;

        private PlayerController controller;
        private SpinSwordAttack stamina;
        private SidewaysParryAttack guard;
        private PlayerGuardBreak guardBreak;
        private PlayerStunStatus stun;
        private PlayerHealth health;
        private ChargedAttack chargedAttack;
        private float braceStartedAt;
        private float deflectExpiresAt;
        private bool exitDiscountPending;

        public bool IsBraced => isBraced;
        public bool IsDeflectWindowActive => isBraced && Time.time <= deflectExpiresAt;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stamina = GetComponent<SpinSwordAttack>();
            guard = GetComponent<SidewaysParryAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            stun = GetComponent<PlayerStunStatus>();
            health = GetComponent<PlayerHealth>();
            chargedAttack = GetComponent<ChargedAttack>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += ClearBrace;
                health.Respawned += ClearBrace;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= ClearBrace;
                health.Respawned -= ClearBrace;
            }

            ClearBrace();
        }

        private void Update()
        {
            if (!isBraced)
            {
                return;
            }

            braceElapsed = Mathf.Max(0f, Time.time - braceStartedAt);
            deflectWindowRemaining = Mathf.Max(0f, deflectExpiresAt - Time.time);
            if (!CanContinueBrace())
            {
                ClearBrace();
                return;
            }

            stamina?.RestoreStamina(
                (braceElapsed <= earlyRecoveryDuration
                    ? earlyRecoveryPerSecond
                    : sustainedRecoveryPerSecond) * Time.deltaTime);
        }

        public bool TryEnterFromGuardBreak()
        {
            if (isBraced
                || guard == null
                || !guard.IsBraceEntryWindowActive
                || stamina == null
                || stamina.CurrentStamina < stamina.MaximumStamina * minimumStaminaFraction
                || controller == null
                || !controller.IsGrounded
                || controller.IsExternallyMovementLocked
                || (stun != null && stun.IsStunned)
                || (health != null && health.CurrentHealth <= 0))
            {
                return false;
            }

            isBraced = true;
            braceStartedAt = Time.time;
            deflectExpiresAt = 0f;
            exitDiscountPending = false;
            controller.SetBraceMovementMultiplier(movementMultiplier);
            Spawn(enterVfxPrefab);
            CaveSfx.PlayClip(enterClip, 0.7f);
            return true;
        }

        /// <summary>Called synchronously when a melee hit is about to resolve.</summary>
        public bool TryDeflectIncoming(DamageContext context)
        {
            if (!isBraced)
            {
                return false;
            }

            // Guard Break, piercing and area hits retain their existing answers.
            // Unblockable Frenzy attacks are still allowed: Unblockable is
            // intentionally separate from parry/deflect eligibility.
            if (context.HasTrait(DamageTrait.Piercing)
                || context.HasTrait(DamageTrait.AreaOfEffect)
                || context.HasTrait(DamageTrait.GuardBreak))
            {
                return false;
            }

            deflectExpiresAt = Time.time + deflectResponseWindow;
            if (GameInput.BasicAttackPressed)
            {
                return LeaveForSpin();
            }

            return GameInput.ChargePressed && LeaveForHeavy();
        }

        public bool LeaveForSpin()
        {
            if (!BeginExit())
            {
                return false;
            }

            if (stamina != null && stamina.TryBeginBraceExitAttack())
            {
                return true;
            }

            exitDiscountPending = false;
            return false;
        }

        public bool LeaveForHeavy()
        {
            if (!BeginExit())
            {
                return false;
            }

            if (chargedAttack != null && chargedAttack.TryBeginBraceExitCharge())
            {
                return true;
            }

            exitDiscountPending = false;
            return false;
        }

        public float ConsumeExitActionStaminaMultiplier()
        {
            if (!exitDiscountPending)
            {
                return 1f;
            }

            exitDiscountPending = false;
            return exitActionStaminaMultiplier;
        }

        public void BreakForEnemyGuardBreak()
        {
            ClearBrace();
        }

        private bool BeginExit()
        {
            if (!isBraced)
            {
                return false;
            }

            exitDiscountPending = true;
            EndBrace();
            return true;
        }

        private bool CanContinueBrace()
        {
            return guard != null
                && guard.IsGuardHeld
                && guard.CurrentPhase == ParryPhase.Guard
                && controller != null
                && !controller.IsExternallyMovementLocked
                && (stun == null || !stun.IsStunned)
                && (guardBreak == null || guardBreak.CanUseCombatActions);
        }

        private void ClearBrace()
        {
            exitDiscountPending = false;
            EndBrace();
        }

        private void EndBrace()
        {
            if (!isBraced)
            {
                return;
            }

            isBraced = false;
            braceElapsed = 0f;
            deflectExpiresAt = 0f;
            deflectWindowRemaining = 0f;
            controller?.SetBraceMovementMultiplier(1f);
            Spawn(exitVfxPrefab);
            CaveSfx.PlayClip(exitClip, 0.6f);
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab != null)
            {
                Instantiate(prefab, transform.position, Quaternion.identity);
            }
        }
    }
}
