using Cave.Combat;
using Cave.Progression;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyTank : MonoBehaviour
    {
        [SerializeField] private bool useSharedSettings = true;
        [SerializeField] private StrategicCombatSettings sharedSettings;

        [Header("Tank Overrides")]
        [SerializeField, Min(1f)] private float healthMultiplier = 3f;
        [SerializeField, Min(1f)] private float damageMultiplier = 1.5f;
        [SerializeField, Range(0f, 0.95f)] private float knockbackResistance = 0.7f;
        [SerializeField] private bool contactAttackIsPiercing = true;

        [Header("Guard Break")]
        [SerializeField, Range(0f, 1f)] private float guardBreakChance = 0.2f;
        [SerializeField, Min(0f)] private float guardBreakWindup = 0.65f;
        [SerializeField, Min(1)] private int guardBreakDamage = 2;

        public float HealthMultiplier => useSharedSettings && sharedSettings != null
            ? sharedSettings.TankHealthMultiplier
            : healthMultiplier;
        public float DamageMultiplier => useSharedSettings && sharedSettings != null
            ? sharedSettings.TankDamageMultiplier
            : damageMultiplier;
        public float KnockbackResistance => useSharedSettings && sharedSettings != null
            ? sharedSettings.TankKnockbackResistance
            : knockbackResistance;
        public bool ContactAttackIsPiercing => useSharedSettings && sharedSettings != null
            ? sharedSettings.TankContactIsPiercing
            : contactAttackIsPiercing;

        private void Awake()
        {
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = gameObject.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(EnemyArchetype.Tank);
            ApplyNonHealthIdentity();
        }

        private void Start()
        {
            if (GetComponent<EnemyDifficultyScaler>() == null)
            {
                Damageable damageable = GetComponent<Damageable>();
                damageable.SetRuntimeMaximumHealth(
                    Mathf.CeilToInt(damageable.BaseMaximumHealth * HealthMultiplier),
                    true);
            }
        }

        internal void ConfigureIfMissing(StrategicCombatSettings settings)
        {
            if (sharedSettings == null)
            {
                sharedSettings = settings;
            }

            ApplyNonHealthIdentity();
        }

        private void ApplyNonHealthIdentity()
        {
            KnockbackReceiver receiver = GetComponent<KnockbackReceiver>();
            receiver?.SetKnockbackResistance(KnockbackResistance);
            EnemyStagger stagger = GetComponent<EnemyStagger>();
            stagger?.SetStaggerResistance(KnockbackResistance);

            EnemyContactDamage contactDamage = GetComponentInChildren<EnemyContactDamage>(true);
            if (contactDamage == null)
            {
                return;
            }

            contactDamage.SetArchetypeDamageMultiplier(DamageMultiplier);
            contactDamage.ConfigureGuardBreak(
                guardBreakChance,
                guardBreakWindup,
                guardBreakDamage);
            if (ContactAttackIsPiercing)
            {
                contactDamage.AddRuntimeDamageTraits(DamageTrait.Piercing);
            }
        }
    }
}
