using System;
using Cave.Combat;
using Cave.Enemies;
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

            PlayerCurseController curses = GetComponent<PlayerCurseController>();
            if (curses != null)
            {
                amount = curses.ResolveIncomingDamage(amount);
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            invulnerableUntil = Time.time + postHitInvulnerability;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            DamageTaken?.Invoke();
            if (damageContext.Source != null)
            {
                damageContext.Source
                    .GetComponentInParent<EnemyElementalEmpowerment>()
                    ?.ApplyOnHit(this);
            }
            Debug.Log("Player health: " + CurrentHealth + "/" + maxHealth, this);

            if (CurrentHealth == 0)
            {
                curses?.NotifyPlayerDeath(damageContext.Source);
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

        public void ApplyElementalReactionDamage(
            int amount,
            GameObject source,
            Vector2 explosionOrigin,
            float outwardForce)
        {
            if (amount <= 0 || CurrentHealth <= 0)
            {
                return;
            }

            PlayerCurseController curses = GetComponent<PlayerCurseController>();
            int resolvedDamage = curses != null ? curses.ResolveIncomingDamage(amount) : amount;
            CurrentHealth = Mathf.Max(0, CurrentHealth - resolvedDamage);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            DamageTaken?.Invoke();
            Vector2 direction = (Vector2)transform.position - explosionOrigin;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.up;
            }

            GetComponent<PlayerController>()?.ApplyExternalKnockback(
                direction.normalized * Mathf.Max(0f, outwardForce),
                0.15f);
            if (CurrentHealth != 0)
            {
                return;
            }

            curses?.NotifyPlayerDeath(source);
            Died?.Invoke();
            CurrentHealth = maxHealth;
            playerRespawn.Respawn();
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            Respawned?.Invoke();
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

        public bool ReduceMaxHealth(int amount, int minimumMaximumHealth = 1)
        {
            int permitted = Mathf.Min(
                Mathf.Max(0, amount),
                Mathf.Max(0, maxHealth - Mathf.Max(1, minimumMaximumHealth)));
            if (permitted <= 0)
            {
                return false;
            }

            maxHealth -= permitted;
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            return true;
        }
    }
}
