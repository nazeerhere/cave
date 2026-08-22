using System;
using UnityEngine;

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

    [DisallowMultipleComponent]
    public sealed class EnemyArchetypeProfile : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetypes = EnemyArchetype.Melee;

        public EnemyArchetype Archetypes => archetypes;

        public bool Includes(EnemyArchetype archetype)
        {
            return (archetypes & archetype) != 0;
        }

        public void AddRuntimeArchetype(EnemyArchetype archetype)
        {
            archetypes |= archetype;
        }
    }
}
