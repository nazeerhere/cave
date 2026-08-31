using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using UnityEngine;

namespace Cave.Progression
{
    /// <summary>Guaranteed, final-life-only Brute shard credit; independent of ordinary drops.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class CorruptedBruteGeneralShardReward : MonoBehaviour
    {
        private Damageable damageable;
        private PlayerPermanentProgression creditedProgression;
        private bool granted;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            granted = false;
            creditedProgression = null;
            damageable.DamageResolved += HandleDamageResolved;
            damageable.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.DamageResolved -= HandleDamageResolved;
                damageable.Died -= HandleDied;
            }
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (!blocked && appliedDamage > 0 && damageable.CurrentHealth == 0)
            {
                creditedProgression = context.PermanentProgressionSource;
            }
        }

        private void HandleDied()
        {
            if (GetComponent<EnemyCorruptionLifecycle>()?.SuppressDeathRewards == true)
            {
                return;
            }

            if (granted || creditedProgression == null)
            {
                return;
            }

            granted = true;
            creditedProgression.AddGeneralShards(1);
        }
    }
}
