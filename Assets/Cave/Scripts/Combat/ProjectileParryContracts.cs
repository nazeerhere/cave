using UnityEngine;

namespace Cave.Combat
{
    public interface IPlayerParryableProjectile
    {
        bool CanBeParried { get; }

        bool Parry(
            GameObject newOwner,
            Vector2 fallbackDirection,
            float speedMultiplier,
            float damageMultiplier);

        bool DeflectToGround(GameObject newOwner, Vector2 targetPoint);
    }

    public interface IEnemyParryableProjectile
    {
        bool CanBeEnemyParried { get; }
        bool TryEnemyParry(GameObject defender);
    }
}
