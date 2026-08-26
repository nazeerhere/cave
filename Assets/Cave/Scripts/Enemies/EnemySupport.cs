using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemySupport : MonoBehaviour
    {
        private EnemyArchetypeProfile archetypeProfile;

        public bool IsSupportRole => archetypeProfile != null
            && archetypeProfile.Includes(EnemyArchetype.Support);

        private void Awake()
        {
            ApplyRoleIdentity();
        }

        public void ApplyRoleIdentity()
        {
            if (archetypeProfile == null)
            {
                archetypeProfile = GetComponent<EnemyArchetypeProfile>();
            }

            archetypeProfile?.AddRuntimeArchetype(EnemyArchetype.Support);
        }
    }
}
