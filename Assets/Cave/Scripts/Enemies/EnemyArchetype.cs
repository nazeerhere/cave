using System;

namespace Cave.Enemies
{
    [Flags]
    public enum EnemyArchetype
    {
        None = 0,
        Melee = 1 << 0,
        Ranged = 1 << 1,
        Tank = 1 << 2,
        Support = 1 << 3,
        Swarm = 1 << 4
    }
}
