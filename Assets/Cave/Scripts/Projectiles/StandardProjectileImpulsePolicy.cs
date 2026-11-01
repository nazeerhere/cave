using UnityEngine;

namespace Cave.Projectiles
{
    /// <summary>
    /// Pure impulse policy for ordinary projectiles. Target-specific resistance
    /// remains authoritative in KnockbackReceiver; structural response is
    /// deliberately half-strength while environment tuning is provisional.
    /// </summary>
    public static class StandardProjectileImpulsePolicy
    {
        public const float DestructibleResistanceMultiplier = .5f;

        public static float Resolve(float intrinsicImpulse, float impactSpeed, float authoredBaseSpeed)
        {
            float speedRatio = Mathf.Max(0f, impactSpeed) / Mathf.Max(.01f, authoredBaseSpeed);
            return Mathf.Max(0f, intrinsicImpulse) * speedRatio;
        }
    }
}
