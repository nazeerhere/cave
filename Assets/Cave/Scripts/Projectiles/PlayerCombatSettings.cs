using UnityEngine;

namespace Cave.Projectiles
{
    public sealed class PlayerCombatSettings : ScriptableObject
    {
        [SerializeField] private PlayerProjectile projectilePrefab;
        [SerializeField] private PlayerProjectile heavyProjectilePrefab;

        public PlayerProjectile ProjectilePrefab => projectilePrefab;
        public PlayerProjectile HeavyProjectilePrefab => heavyProjectilePrefab;
    }
}
