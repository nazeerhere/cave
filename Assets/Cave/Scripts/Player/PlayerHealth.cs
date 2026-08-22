using System;
using Cave.Combat;
using Cave.World;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(PlayerRespawn))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField, Min(0f)] private float postHitInvulnerability = 0.75f;

        private PlayerRespawn playerRespawn;
        private PlayerStrengthShield strengthShield;
        private PlayerStrengthDeflection strengthDeflection;
        private SidewaysParryAttack guard;
        private float invulnerableUntil;

        public event Action<int, int> HealthChanged;
        public event Action DamageTaken;
        public event Action Died;
        public event Action Respawned;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            playerRespawn = GetComponent<PlayerRespawn>();
            strengthShield = GetComponent<PlayerStrengthShield>();
            strengthDeflection = GetComponent<PlayerStrengthDeflection>();
            guard = GetComponent<SidewaysParryAttack>();
            CurrentHealth = maxHealth;
        }

        public bool TryTakeDamage(int amount)
        {
            return TryTakeDamage(amount, default);
        }

        public bool TryTakeDamage(int amount, DamageContext damageContext)
        {
            if (amount <= 0 || Time.time < invulnerableUntil)
            {
                return false;
            }

            if (strengthShield == null)
            {
                strengthShield = GetComponent<PlayerStrengthShield>();
            }

            if (strengthDeflection == null)
            {
                strengthDeflection = GetComponent<PlayerStrengthDeflection>();
            }

            if (guard == null)
            {
                guard = GetComponent<SidewaysParryAttack>();
            }

            int resolvedDamage = amount;
            if (guard != null && guard.TryGuardMelee(ref resolvedDamage, damageContext))
            {
                return true;
            }

            amount = resolvedDamage;

            if (strengthDeflection != null && strengthDeflection.TryDeflect(damageContext))
            {
                return true;
            }

            if (strengthShield != null && strengthShield.TryAbsorbDamage(amount))
            {
                invulnerableUntil = Time.time + postHitInvulnerability;
                return true;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            invulnerableUntil = Time.time + postHitInvulnerability;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            DamageTaken?.Invoke();
            Debug.Log("Player health: " + CurrentHealth + "/" + maxHealth, this);

            if (CurrentHealth == 0)
            {
                Died?.Invoke();
                CurrentHealth = maxHealth;
                playerRespawn.Respawn();
                HealthChanged?.Invoke(CurrentHealth, maxHealth);
                Respawned?.Invoke();
                Debug.Log("Player respawned with full health.", this);
            }

            return true;
        }

        public bool RestoreHealth(int amount)
        {
            if (amount <= 0 || CurrentHealth >= maxHealth)
            {
                return false;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            return true;
        }

        public bool IncreaseMaxHealth(int amount, bool addIncreaseToCurrentHealth = true)
        {
            if (amount <= 0)
            {
                return false;
            }

            maxHealth += amount;
            if (addIncreaseToCurrentHealth)
            {
                CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            }

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            return true;
        }
    }
}
