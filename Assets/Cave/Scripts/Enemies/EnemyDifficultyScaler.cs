using Cave.Combat;
using Cave.Progression;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyDifficultyScaler : MonoBehaviour
    {
        [SerializeField] private WorldDifficultyManager difficultyManager;

        private Damageable damageable;
        private EnemyController enemyController;
        private EnemyContactDamage contactDamage;
        private EnemyShooter enemyShooter;
        private EnemyPoisonShooter poisonShooter;
        private EnemyMeleeCombat meleeCombat;
        private EnemyRespawner enemyRespawner;
        private EnemyTank enemyTank;
        private EnemySwarm enemySwarm;
        private SkeletonInheritance skeletonInheritance;
        private FlyingSwarmController flyingSwarm;
        private EnemySkillEvolution skillEvolution;
        private TrollJumpStomp trollJumpStomp;
        private EnemyCriticalResistance criticalResistance;

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            damageable = GetComponent<Damageable>();
            enemyController = GetComponent<EnemyController>();
            contactDamage = GetComponentInChildren<EnemyContactDamage>(true);
            enemyShooter = GetComponentInChildren<EnemyShooter>(true);
            poisonShooter = GetComponentInChildren<EnemyPoisonShooter>(true);
            meleeCombat = GetComponentInChildren<EnemyMeleeCombat>(true);
            enemyRespawner = GetComponent<EnemyRespawner>();
            enemyTank = GetComponent<EnemyTank>();
            enemySwarm = GetComponent<EnemySwarm>();
            skeletonInheritance = GetComponent<SkeletonInheritance>();
            flyingSwarm = GetComponent<FlyingSwarmController>();
            skillEvolution = GetComponent<EnemySkillEvolution>();
            trollJumpStomp = GetComponent<TrollJumpStomp>();
            criticalResistance = GetComponent<EnemyCriticalResistance>();
        }

        internal void Configure(WorldDifficultyManager manager)
        {
            CacheComponents();
            if (GetComponent<EnemyDamageModifiers>() == null)
            {
                gameObject.AddComponent<EnemyDamageModifiers>();
            }

            Unsubscribe();
            difficultyManager = manager;
            Subscribe();
            if (meleeCombat != null && meleeCombat.IsTrollPreset && trollJumpStomp == null)
            {
                trollJumpStomp = gameObject.AddComponent<TrollJumpStomp>();
            }

            EnsureMobBrain();
            skeletonInheritance = GetComponent<SkeletonInheritance>();

            if (skillEvolution == null)
            {
                skillEvolution = gameObject.AddComponent<EnemySkillEvolution>();
            }

            skillEvolution.Configure(difficultyManager);
            if (criticalResistance == null)
            {
                criticalResistance = gameObject.AddComponent<EnemyCriticalResistance>();
            }

            criticalResistance.Configure(difficultyManager);
            ApplyForSpawn();
        }

        private void EnsureMobBrain()
        {
            if (enemyController == null || GetComponent<MobBrainBase>() != null)
            {
                return;
            }

            EnemyHealAbility heal = GetComponentInChildren<EnemyHealAbility>(true);
            EnemyDamageBuffAbility buff = GetComponentInChildren<EnemyDamageBuffAbility>(true);
            SwarmCaller summon = GetComponentInChildren<SwarmCaller>(true);
            if (poisonShooter != null)
            {
                gameObject.AddComponent<DetectiveBrain>();
            }
            else if (meleeCombat != null && meleeCombat.IsTrollPreset)
            {
                gameObject.AddComponent<TrollBrain>();
            }
            else if (meleeCombat != null && meleeCombat.IsBrutePreset)
            {
                gameObject.AddComponent<BruteBrain>();
            }
            else if (meleeCombat != null && enemySwarm != null)
            {
                gameObject.AddComponent<SkeletonBrain>();
            }
            else if (summon != null && buff != null)
            {
                gameObject.AddComponent<NecromancerBrain>();
            }
            else if (heal != null && buff != null)
            {
                gameObject.AddComponent<WizardBrain>();
            }
        }

        public void ApplyForSpawn()
        {
            if (damageable == null)
            {
                CacheComponents();
            }

            if (difficultyManager == null)
            {
                return;
            }

            ProgressionDifficultySettings settings = difficultyManager.Settings;
            if (settings == null)
            {
                return;
            }

            if (criticalResistance == null)
            {
                criticalResistance = GetComponent<EnemyCriticalResistance>();
            }

            criticalResistance?.Configure(difficultyManager);
            criticalResistance?.RollForLife();

            int archetypeBaseHealth = enemySwarm != null
                ? enemySwarm.MaximumHealth
                : damageable.BaseMaximumHealth;
            float tankHealthMultiplier = enemyTank != null ? enemyTank.HealthMultiplier : 1f;
            int scaledHealth = Mathf.Max(
                archetypeBaseHealth,
                Mathf.CeilToInt(
                    archetypeBaseHealth
                    * tankHealthMultiplier
                    * difficultyManager.GetStatScale(settings.EnemyHealthScalingStrength)));
            if (skeletonInheritance != null)
            {
                if (damageable.CurrentHealth <= 0)
                {
                    skeletonInheritance.ResetForNewLife();
                }

                skeletonInheritance.SetWorldScaledBaseMaximumHealth(scaledHealth, true);
            }
            else
            {
                damageable.SetRuntimeMaximumHealth(scaledHealth, true);
            }

            if (contactDamage != null)
            {
                int archetypeBaseDamage = enemySwarm != null
                    ? enemySwarm.ContactDamage
                    : contactDamage.BaseContactDamage;
                int scaledDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        archetypeBaseDamage
                        * difficultyManager.GetStatScale(settings.EnemyDamageScalingStrength)));
                contactDamage.SetRuntimeDamage(scaledDamage);
            }

            if (meleeCombat != null)
            {
                float tankDamageMultiplier = enemyTank != null ? enemyTank.DamageMultiplier : 1f;
                float meleeDamageScale = tankDamageMultiplier
                    * difficultyManager.GetStatScale(settings.EnemyDamageScalingStrength);
                meleeCombat.SetRuntimeDamageScale(meleeDamageScale);
                trollJumpStomp?.SetRuntimeDamageScale(meleeDamageScale);
            }

            if (enemyController != null)
            {
                if (enemySwarm != null && enemyController.BaseMoveSpeed > 0f)
                {
                    enemyController.SetArchetypeSpeedMultiplier(
                        enemySwarm.MoveSpeed / enemyController.BaseMoveSpeed);
                }

                enemyController.SetDifficultySpeedMultiplier(
                    difficultyManager.GetStatScale(settings.EnemyMoveSpeedScalingStrength));
            }

            if (flyingSwarm != null)
            {
                flyingSwarm.SetDifficultySpeedMultiplier(
                    difficultyManager.GetStatScale(settings.EnemyMoveSpeedScalingStrength));
                int scaledFlyingDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        flyingSwarm.BaseDamage
                        * difficultyManager.GetStatScale(settings.EnemyDamageScalingStrength)));
                flyingSwarm.SetRuntimeDamage(scaledFlyingDamage);
            }

            if (enemyShooter != null)
            {
                enemyShooter.SetDifficultyManager(difficultyManager);
                int scaledProjectileDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        enemyShooter.BaseProjectileDamage
                        * difficultyManager.GetStatScale(settings.EnemyDamageScalingStrength)));
                float scaledProjectileSpeed = enemyShooter.BaseProjectileSpeed
                    * difficultyManager.GetStatScale(settings.EnemyProjectileSpeedScalingStrength);
                float scaledFireInterval = Mathf.Max(
                    settings.MinimumEnemyFireInterval,
                    enemyShooter.BaseFireInterval
                    / difficultyManager.GetStatScale(settings.EnemyFireRateScalingStrength));
                enemyShooter.SetRuntimeDifficultyValues(
                    scaledProjectileDamage,
                    scaledProjectileSpeed,
                    scaledFireInterval);
            }

            if (poisonShooter != null)
            {
                float damageScale = difficultyManager.GetStatScale(
                    settings.EnemyDamageScalingStrength);
                int scaledDirectDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(poisonShooter.BaseDirectDamage * damageScale));
                int scaledPoisonDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(poisonShooter.BasePoisonDamage * damageScale));
                float scaledProjectileSpeed = poisonShooter.BaseProjectileSpeed
                    * difficultyManager.GetStatScale(settings.EnemyProjectileSpeedScalingStrength);
                float scaledFireCooldown = Mathf.Max(
                    settings.MinimumEnemyFireInterval,
                    poisonShooter.BaseFireCooldown
                        / difficultyManager.GetStatScale(settings.EnemyFireRateScalingStrength));
                poisonShooter.SetRuntimeDifficultyValues(
                    scaledDirectDamage,
                    scaledPoisonDamage,
                    scaledProjectileSpeed,
                    scaledFireCooldown);
            }

            ApplyRespawnDelay();
        }

        private void ApplyRespawnDelay()
        {
            if (enemyRespawner == null || difficultyManager == null || difficultyManager.Settings == null)
            {
                return;
            }

            ProgressionDifficultySettings settings = difficultyManager.Settings;
            float delay = enemyRespawner.BaseRespawnDelay;
            if (settings.ScaleRespawnDelay)
            {
                delay = Mathf.Max(
                    settings.MinimumRespawnDelay,
                    delay / difficultyManager.GetStatScale(settings.RespawnDelayScalingStrength));
            }

            enemyRespawner.SetRuntimeRespawnDelay(delay);
        }

        private void HandleDifficultyChanged()
        {
            // Living enemies keep their current combat stats. The next respawn reapplies all current scaling.
            ApplyRespawnDelay();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= HandleDifficultyChanged;
                difficultyManager.DifficultyChanged += HandleDifficultyChanged;
            }
        }

        private void Unsubscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= HandleDifficultyChanged;
            }
        }
    }
}
