using UnityEngine;

namespace Cave.Enemies
{
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
