using Cave.Combat;
using Cave.Enemies;
using Cave.Missions;
using Cave.Player;
using UnityEngine;

namespace Cave.Progression
{
    /// <summary>Guaranteed once-per-final-life General Shard credit for corrupted enemies.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyCorruptionLifecycle))]
    public sealed class CorruptedEnemyGeneralShardReward : MonoBehaviour
    {
        private Damageable damageable;
        private EnemyCorruptionLifecycle corruption;
        private PlayerPermanentProgression creditedProgression;
        private bool granted;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            corruption = GetComponent<EnemyCorruptionLifecycle>();
        }

        private void OnEnable()
        {
            granted = false;
            creditedProgression = null;
            damageable.DamageResolved += HandleDamageResolved;
            damageable.Died += HandleDied;
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
            if (granted || creditedProgression == null || !corruption.IsCorrupted
                || corruption.SuppressDeathRewards)
            {
                return;
            }

            granted = true;
            creditedProgression.AddGeneralShards(ResolveReward());
        }

        private int ResolveReward()
        {
            if (GetComponent<BountyTarget>() != null) return 3;
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            return profile != null && profile.Includes(EnemyArchetype.Tank) ? 2 : 1;
        }

        private void OnDisable()
        {
            if (damageable == null) return;
            damageable.DamageResolved -= HandleDamageResolved;
            damageable.Died -= HandleDied;
        }
    }
}
