using UnityEngine;

namespace Cave.Progression
{
    [CreateAssetMenu(menuName = "Cave/Progression Difficulty Settings", fileName = "ProgressionDifficultySettings")]
    public sealed class ProgressionDifficultySettings : ScriptableObject
    {
        [Header("Player Mastery")]
        [SerializeField, Range(0f, 1f)] private float criticalHealthThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float criticalStaminaThreshold = 0.10f;
        [SerializeField, Range(0f, 1f)] private float criticalManaThreshold = 0.10f;
        [SerializeField, Min(1)] private int healthIncreasePerMastery = 1;
        [SerializeField, Min(0.01f)] private float staminaIncreasePerMastery = 1f;
        [SerializeField, Min(0.01f)] private float manaIncreasePerMastery = 1f;
        [SerializeField] private bool addIncreaseToCurrentResource = true;

        [Header("Player Dash")]
        [SerializeField, Min(0f)] private float dashStaminaCost = 30f;
        [SerializeField, Min(0f)] private float dashSpeed = 18f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.15f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.25f;

        [Header("World Difficulty")]
        [SerializeField, Min(0.0001f)] private float growthPerDifficultyTier = 0.08f;
        [SerializeField, Min(0f)] private float difficultyExponentPerTier = 0.18f;
        [SerializeField, Min(0f)] private float healthGrowthWeight = 1f;
        [SerializeField, Min(0f)] private float staminaGrowthWeight = 1f;
        [SerializeField, Min(0f)] private float manaGrowthWeight = 1f;
        [SerializeField] private bool logDifficultyTierChanges = true;

        [Header("Enemy Scaling")]
        [SerializeField, Min(0f)] private float enemyHealthScalingStrength = 1f;
        [SerializeField, Min(0f)] private float enemyDamageScalingStrength = 0.5f;
        [SerializeField, Min(0f)] private float enemyMoveSpeedScalingStrength = 0.20f;
        [SerializeField, Min(0f)] private float enemyProjectileSpeedScalingStrength = 0.15f;
        [SerializeField, Min(0f)] private float enemyFireRateScalingStrength = 0.15f;
        [SerializeField, Min(0.01f)] private float minimumEnemyFireInterval = 0.5f;

        [Header("Enemy Respawn Scaling")]
        [SerializeField] private bool scaleRespawnDelay = true;
        [SerializeField, Min(0f)] private float respawnDelayScalingStrength = 0.10f;
        [SerializeField, Min(0f)] private float minimumRespawnDelay = 2f;

        [Header("Fireball Evolution")]
        [SerializeField, Min(0)] private int evolutionStartTier = 3;
        [SerializeField, Min(0.01f)] private float splitDistance = 4f;
        [SerializeField, Range(1, 5)] private int splitCount = 3;
        [SerializeField, Range(0f, 180f)] private float splitSpreadAngle = 25f;
        [SerializeField, Min(0f)] private float splitTelegraphDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float childSpeedMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float childDamageMultiplier = 1f;
        [SerializeField, Range(1, 5)] private int maximumSplitCount = 5;

        public float CriticalHealthThreshold => criticalHealthThreshold;
        public float CriticalStaminaThreshold => criticalStaminaThreshold;
        public float CriticalManaThreshold => criticalManaThreshold;
        public int HealthIncreasePerMastery => healthIncreasePerMastery;
        public float StaminaIncreasePerMastery => staminaIncreasePerMastery;
        public float ManaIncreasePerMastery => manaIncreasePerMastery;
        public bool AddIncreaseToCurrentResource => addIncreaseToCurrentResource;
        public float DashStaminaCost => dashStaminaCost;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public float GrowthPerDifficultyTier => growthPerDifficultyTier;
        public float DifficultyExponentPerTier => difficultyExponentPerTier;
        public float HealthGrowthWeight => healthGrowthWeight;
        public float StaminaGrowthWeight => staminaGrowthWeight;
        public float ManaGrowthWeight => manaGrowthWeight;
        public bool LogDifficultyTierChanges => logDifficultyTierChanges;
        public float EnemyHealthScalingStrength => enemyHealthScalingStrength;
        public float EnemyDamageScalingStrength => enemyDamageScalingStrength;
        public float EnemyMoveSpeedScalingStrength => enemyMoveSpeedScalingStrength;
        public float EnemyProjectileSpeedScalingStrength => enemyProjectileSpeedScalingStrength;
        public float EnemyFireRateScalingStrength => enemyFireRateScalingStrength;
        public float MinimumEnemyFireInterval => minimumEnemyFireInterval;
        public bool ScaleRespawnDelay => scaleRespawnDelay;
        public float RespawnDelayScalingStrength => respawnDelayScalingStrength;
        public float MinimumRespawnDelay => minimumRespawnDelay;
        public int EvolutionStartTier => evolutionStartTier;
        public float SplitDistance => splitDistance;
        public int SplitCount => Mathf.Min(splitCount, maximumSplitCount);
        public float SplitSpreadAngle => splitSpreadAngle;
        public float SplitTelegraphDuration => splitTelegraphDuration;
        public float ChildSpeedMultiplier => childSpeedMultiplier;
        public float ChildDamageMultiplier => childDamageMultiplier;
        public int MaximumSplitCount => maximumSplitCount;

        private void OnValidate()
        {
            maximumSplitCount = Mathf.Clamp(maximumSplitCount, 1, 5);
            splitCount = Mathf.Clamp(splitCount, 1, maximumSplitCount);
        }
    }
}
