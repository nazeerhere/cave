using UnityEngine;

namespace Cave.Progression
{
    [CreateAssetMenu(menuName = "Cave/Strategic Combat Settings", fileName = "StrategicCombatSettings")]
    public sealed class StrategicCombatSettings : ScriptableObject
    {
        [Header("Tank Archetype")]
        [SerializeField, Min(1f)] private float tankHealthMultiplier = 3f;
        [SerializeField, Min(1f)] private float tankDamageMultiplier = 1.5f;
        [SerializeField, Range(0f, 0.95f)] private float tankKnockbackResistance = 0.7f;
        [SerializeField] private bool tankContactIsPiercing = true;

        [Header("Support Archetype")]
        [SerializeField, Min(0.1f)] private float supportRadius = 5f;
        [SerializeField, Min(1f)] private float supportMoveSpeedMultiplier = 1.2f;
        [SerializeField, Min(1)] private int maximumSupportedAllies = 6;
        [SerializeField, Min(0.1f)] private float supportHealInterval = 3f;
        [SerializeField, Min(1)] private int supportHealAmount = 1;

        [Header("Swarm Archetype")]
        [SerializeField, Min(1)] private int swarmMaximumHealth = 1;
        [SerializeField, Min(1)] private int swarmContactDamage = 1;
        [SerializeField, Min(0.1f)] private float swarmMoveSpeed = 4f;

        [Header("Swarm Visual Prefabs")]
        [SerializeField] private GameObject groundSwarmPrefab;
        [SerializeField] private GameObject airSwarmPrefab;

        [Header("Eye Air Swarm")]
        [SerializeField, Min(0f)] private float eyeHoverHeight = 2.5f;
        [SerializeField, Min(0.1f)] private float eyeMoveSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float eyeHorizontalFollowRange = 4f;
        [SerializeField, Min(0.1f)] private float eyeVerticalFollowRange = 3f;
        [SerializeField, Min(0f)] private float eyeSeparationRadius = 0.75f;
        [SerializeField, Min(0f)] private float eyeAttackCooldown = 2.5f;
        [SerializeField, Min(0f)] private float eyeAttackTelegraphDuration = 0.4f;
        [SerializeField, Min(0.1f)] private float eyeDiveSpeed = 7f;
        [SerializeField, Min(0.01f)] private float eyeDiveDuration = 0.4f;
        [SerializeField, Min(1)] private int eyeContactDamage = 1;

        [Header("Swarm Caller")]
        [SerializeField, Min(1)] private int swarmSpawnCount = 3;
        [SerializeField, Min(0f)] private float swarmSpawnInterval = 0.25f;
        [SerializeField, Min(1)] private int maximumActiveSummons = 6;
        [SerializeField, Min(0.1f)] private float summonCooldown = 10f;
        [SerializeField, Min(0f)] private float summonSpawnRadius = 1.5f;

        [Header("Archetype Difficulty Eligibility")]
        [SerializeField, Min(0)] private int meleeMinimumTier;
        [SerializeField, Min(0)] private int rangedMinimumTier;
        [SerializeField, Min(0)] private int mixedEncounterMinimumTier = 2;
        [SerializeField, Min(0)] private int supportMinimumTier = 4;
        [SerializeField, Min(0)] private int tankMinimumTier = 5;
        [SerializeField, Min(0)] private int swarmMinimumTier = 6;

        public float TankHealthMultiplier => tankHealthMultiplier;
        public float TankDamageMultiplier => tankDamageMultiplier;
        public float TankKnockbackResistance => tankKnockbackResistance;
        public bool TankContactIsPiercing => tankContactIsPiercing;
        public float SupportRadius => supportRadius;
        public float SupportMoveSpeedMultiplier => supportMoveSpeedMultiplier;
        public int MaximumSupportedAllies => maximumSupportedAllies;
        public float SupportHealInterval => supportHealInterval;
        public int SupportHealAmount => supportHealAmount;
        public int SwarmMaximumHealth => swarmMaximumHealth;
        public int SwarmContactDamage => swarmContactDamage;
        public float SwarmMoveSpeed => swarmMoveSpeed;
        public GameObject GroundSwarmPrefab => groundSwarmPrefab;
        public GameObject AirSwarmPrefab => airSwarmPrefab;
        public float EyeHoverHeight => eyeHoverHeight;
        public float EyeMoveSpeed => eyeMoveSpeed;
        public float EyeHorizontalFollowRange => eyeHorizontalFollowRange;
        public float EyeVerticalFollowRange => eyeVerticalFollowRange;
        public float EyeSeparationRadius => eyeSeparationRadius;
        public float EyeAttackCooldown => eyeAttackCooldown;
        public float EyeAttackTelegraphDuration => eyeAttackTelegraphDuration;
        public float EyeDiveSpeed => eyeDiveSpeed;
        public float EyeDiveDuration => eyeDiveDuration;
        public int EyeContactDamage => eyeContactDamage;
        public int SwarmSpawnCount => swarmSpawnCount;
        public float SwarmSpawnInterval => swarmSpawnInterval;
        public int MaximumActiveSummons => maximumActiveSummons;
        public float SummonCooldown => summonCooldown;
        public float SummonSpawnRadius => summonSpawnRadius;
        public int MeleeMinimumTier => meleeMinimumTier;
        public int RangedMinimumTier => rangedMinimumTier;
        public int MixedEncounterMinimumTier => mixedEncounterMinimumTier;
        public int SupportMinimumTier => supportMinimumTier;
        public int TankMinimumTier => tankMinimumTier;
        public int SwarmMinimumTier => swarmMinimumTier;
    }
}
