using UnityEngine;

namespace Cave.Projectiles
{
    public sealed class PlayerCombatSettings : ScriptableObject
    {
        [SerializeField] private PlayerProjectile projectilePrefab;

        public PlayerProjectile ProjectilePrefab => projectilePrefab;
    }
}
