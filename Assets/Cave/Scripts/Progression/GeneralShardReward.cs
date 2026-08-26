using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(SkeletonInheritance))]
    public sealed class GeneralShardReward : MonoBehaviour
    {
        [SerializeField, Min(1)] private int baseShards = 1;
        [SerializeField, Min(1)] private int witnessedDeathsPerBonusShard = 4;

        private Damageable damageable;
        private SkeletonInheritance inheritance;
        private PlayerPermanentProgression creditedProgression;
        private bool rewardGranted;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            inheritance = GetComponent<SkeletonInheritance>();
        }

        private void OnEnable()
        {
            rewardGranted = false;
            creditedProgression = null;
            damageable.DamageResolved += HandleDamageResolved;
            damageable.Died += HandleDied;
        }

        private void OnDisable()
        {
            damageable.DamageResolved -= HandleDamageResolved;
            damageable.Died -= HandleDied;
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int damageApplied)
        {
            if (damageable.CurrentHealth != 0)
            {
                return;
            }

            creditedProgression = !blocked && damageApplied > 0
                ? context.PermanentProgressionSource
                : null;
        }

        private void HandleDied()
        {
            if (rewardGranted
                || creditedProgression == null
                || inheritance.Rank != SkeletonRank.General)
            {
                return;
            }

            rewardGranted = true;
            int reward = baseShards
                + Mathf.FloorToInt(
                    inheritance.WitnessedDeaths
                    / (float)Mathf.Max(1, witnessedDeathsPerBonusShard));
            creditedProgression.AddGeneralShards(Mathf.Max(1, reward));
        }
    }
}
