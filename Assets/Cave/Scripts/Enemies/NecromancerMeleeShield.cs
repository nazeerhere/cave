using System;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum NecromancerShieldState
    {
        Locked,
        Full,
        Damaged,
        Regenerating,
        Broken
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class NecromancerMeleeShield : MonoBehaviour,
        IEnemySkillEvolutionReceiver
    {
        [Header("Evolution Unlock")]
        [SerializeField] private EnemyEvolutionStage minimumEvolutionStage =
            EnemyEvolutionStage.EvolutionOne;

        [Header("Melee Shield")]
        [SerializeField, Min(1f)] private float maximumShieldHealth = 4f;
        [SerializeField, Min(0f)] private float regenerationDelay = 1.75f;
        [SerializeField, Min(0f)] private float regenerationFractionPerSecond = 0.25f;
        [SerializeField, Min(0f)] private float breakLockout = 5f;
        [SerializeField, Min(0f)] private float guardBreakShieldDamage = 2f;

        [Header("Current Shield (Read Only)")]
        [SerializeField] private NecromancerShieldState currentState =
            NecromancerShieldState.Locked;
        [SerializeField, Min(0f)] private float currentShieldHealth;
        [SerializeField, Min(0f)] private float regenerationStartsAt;

        private bool unlocked;

        public event Action<NecromancerShieldState> ShieldStateChanged;
        public event Action<float, float> ShieldHit;
        public event Action ShieldBroken;
        public event Action ShieldRestored;

        public NecromancerShieldState CurrentState => currentState;
        public float CurrentShieldHealth => currentShieldHealth;
        public float MaximumShieldHealth => maximumShieldHealth;
        public bool IsUnlocked => unlocked;
        public bool IsActive => unlocked
            && currentShieldHealth > 0f
            && currentState != NecromancerShieldState.Broken;
        public bool IsRegenerating => currentState == NecromancerShieldState.Regenerating;

        public void Configure(
            EnemyEvolutionStage unlockStage,
            float maximumHealth,
            float regenDelay,
            float regenFractionPerSecond,
            float brokenLockout,
            float guardBreakDamage)
        {
            minimumEvolutionStage = unlockStage;
            maximumShieldHealth = Mathf.Max(1f, maximumHealth);
            regenerationDelay = Mathf.Max(0f, regenDelay);
            regenerationFractionPerSecond = Mathf.Max(0f, regenFractionPerSecond);
            breakLockout = Mathf.Max(0f, brokenLockout);
            guardBreakShieldDamage = Mathf.Max(0f, guardBreakDamage);
            currentShieldHealth = Mathf.Min(currentShieldHealth, maximumShieldHealth);
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            bool shouldUnlock = stage >= minimumEvolutionStage;
            if (!shouldUnlock)
            {
                unlocked = false;
                currentShieldHealth = 0f;
                regenerationStartsAt = 0f;
                SetState(NecromancerShieldState.Locked);
                return;
            }

            if (unlocked)
            {
                return;
            }

            unlocked = true;
            currentShieldHealth = maximumShieldHealth;
            regenerationStartsAt = 0f;
            SetState(NecromancerShieldState.Full);
            ShieldRestored?.Invoke();
        }

        internal int ResolveIncomingDamage(int amount, DamageContext context)
        {
            if (!unlocked || amount <= 0 || !IsShieldDamagingMelee(context))
            {
                return amount;
            }

            if (currentShieldHealth <= 0f)
            {
                regenerationStartsAt = Mathf.Max(
                    regenerationStartsAt,
                    Time.time + regenerationDelay);
                return amount;
            }

            float absorbed = ApplyShieldDamage(amount);

            return Mathf.Max(0, Mathf.CeilToInt(amount - absorbed));
        }

        internal bool TryReceiveGuardBreak()
        {
            if (!unlocked || currentShieldHealth <= 0f || guardBreakShieldDamage <= 0f)
            {
                return false;
            }

            ApplyShieldDamage(guardBreakShieldDamage);
            return true;
        }

        private float ApplyShieldDamage(float amount)
        {
            float previousHealth = currentShieldHealth;
            currentShieldHealth = Mathf.Max(0f, currentShieldHealth - Mathf.Max(0f, amount));
            float absorbed = previousHealth - currentShieldHealth;
            regenerationStartsAt = Time.time + regenerationDelay;
            ShieldHit?.Invoke(absorbed, currentShieldHealth);

            if (currentShieldHealth <= 0f)
            {
                regenerationStartsAt = Time.time + breakLockout;
                SetState(NecromancerShieldState.Broken);
                ShieldBroken?.Invoke();
            }
            else
            {
                SetState(NecromancerShieldState.Damaged);
            }

            return absorbed;
        }

        private void Update()
        {
            if (!unlocked || currentShieldHealth >= maximumShieldHealth)
            {
                return;
            }

            if (Time.time < regenerationStartsAt)
            {
                return;
            }

            SetState(NecromancerShieldState.Regenerating);
            currentShieldHealth = Mathf.Min(
                maximumShieldHealth,
                currentShieldHealth
                    + maximumShieldHealth * regenerationFractionPerSecond * Time.deltaTime);
            if (currentShieldHealth >= maximumShieldHealth)
            {
                currentShieldHealth = maximumShieldHealth;
                SetState(NecromancerShieldState.Full);
                ShieldRestored?.Invoke();
            }
        }

        private static bool IsShieldDamagingMelee(DamageContext context)
        {
            return context.Source != null
                && context.Source.GetComponentInParent<PlayerHealth>() != null
                && context.HasTrait(DamageTrait.Melee)
                && !context.HasTrait(DamageTrait.Projectile)
                && !context.HasTrait(DamageTrait.AreaOfEffect)
                && !context.HasTrait(DamageTrait.GuardBreak);
        }

        private void SetState(NecromancerShieldState state)
        {
            if (currentState == state)
            {
                return;
            }

            currentState = state;
            ShieldStateChanged?.Invoke(currentState);
        }

        private void OnDisable()
        {
            regenerationStartsAt = 0f;
            currentShieldHealth = unlocked ? maximumShieldHealth : 0f;
            currentState = unlocked
                ? NecromancerShieldState.Full
                : NecromancerShieldState.Locked;
        }
    }
}
