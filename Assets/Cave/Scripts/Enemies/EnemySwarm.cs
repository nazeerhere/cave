using Cave.Combat;
using Cave.Progression;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemySwarm : MonoBehaviour
    {
        [SerializeField] private bool useSharedSettings = true;
        [SerializeField] private StrategicCombatSettings sharedSettings;

        [Header("Swarm Overrides")]
        [SerializeField, Min(1)] private int maximumHealth = 1;
        [SerializeField, Min(1)] private int contactDamage = 1;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;

        public int MaximumHealth => useSharedSettings && sharedSettings != null
            ? sharedSettings.SwarmMaximumHealth
            : maximumHealth;
        public int ContactDamage => useSharedSettings && sharedSettings != null
            ? sharedSettings.SwarmContactDamage
            : contactDamage;
        public float MoveSpeed => useSharedSettings && sharedSettings != null
            ? sharedSettings.SwarmMoveSpeed
            : moveSpeed;

        private void Awake()
        {
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = gameObject.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(EnemyArchetype.Swarm);
        }

        private void Start()
        {
            ApplyIdentity(GetComponent<EnemyDifficultyScaler>() == null);
        }

        internal void ConfigureIfMissing(StrategicCombatSettings settings)
        {
            if (sharedSettings == null)
            {
                sharedSettings = settings;
            }

            ApplyIdentity(false);
        }

        internal void ApplyIdentity(bool applyHealthAndDamage)
        {
            EnemyController controller = GetComponent<EnemyController>();
            if (controller != null && controller.BaseMoveSpeed > 0f)
            {
                controller.SetArchetypeSpeedMultiplier(MoveSpeed / controller.BaseMoveSpeed);
            }

            if (!applyHealthAndDamage)
            {
                return;
            }

            Damageable damageable = GetComponent<Damageable>();
            SkeletonInheritance inheritance = GetComponent<SkeletonInheritance>();
            if (inheritance != null)
            {
                inheritance.SetWorldScaledBaseMaximumHealth(MaximumHealth, true);
            }
            else
            {
                damageable.SetRuntimeMaximumHealth(MaximumHealth, true);
            }

            EnemyContactDamage contact = GetComponentInChildren<EnemyContactDamage>(true);
            contact?.SetRuntimeDamage(ContactDamage);
        }
    }
}
