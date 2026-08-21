using System;
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
            CurrentHealth = maxHealth;
        }

        public bool TryTakeDamage(int amount)
        {
            if (amount <= 0 || Time.time < invulnerableUntil)
            {
                return false;
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
    }
}
