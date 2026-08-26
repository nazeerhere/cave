using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using Cave.Progression;
using Cave.Projectiles;
using Cave.World;
using Cave.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Pickups
{
    public static class Sprint9RuntimeInstaller
    {
        private const string SettingsResourceName = "EnemyDropSettings";
        private const string PlayerCombatSettingsResourceName = "PlayerCombatSettings";
        private const string ProgressionSettingsResourceName = "ProgressionDifficultySettings";
        private const string Tier2SettingsResourceName = "SpecialModeTier2Settings";
        private const string StrategicSettingsResourceName = "StrategicCombatSettings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureInitialScene()
        {
            ConfigureLoadedObjects();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureLoadedObjects();
        }

        private static void ConfigureLoadedObjects()
        {
            EnemyDropSettings settings = Resources.Load<EnemyDropSettings>(SettingsResourceName);
            PlayerCombatSettings combatSettings = Resources.Load<PlayerCombatSettings>(
                PlayerCombatSettingsResourceName);
            ProgressionDifficultySettings progressionSettings = Resources.Load<ProgressionDifficultySettings>(
                ProgressionSettingsResourceName);
            SpecialModeTier2Settings tier2Settings = Resources.Load<SpecialModeTier2Settings>(
                Tier2SettingsResourceName);
            StrategicCombatSettings strategicSettings = Resources.Load<StrategicCombatSettings>(
                StrategicSettingsResourceName);

            foreach (Damageable damageable in Object.FindObjectsOfType<Damageable>(true))
            {
                if (!damageable.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (settings != null && !damageable.TryGetComponent(out EnemyDropper _))
                {
                    EnemyDropper dropper = damageable.gameObject.AddComponent<EnemyDropper>();
                    dropper.UseSharedSettings(settings);
                }

                if (damageable.TryGetComponent(out SkeletonInheritance _))
                {
                    if (!damageable.TryGetComponent(out GeneralShardReward _))
                    {
                        damageable.gameObject.AddComponent<GeneralShardReward>();
                    }

                    if (!damageable.TryGetComponent(out GeneralExperienceIndicator _))
                    {
                        damageable.gameObject.AddComponent<GeneralExperienceIndicator>();
                    }
                }

                if (damageable.TryGetComponent(out EnemyController _)
                    || damageable.TryGetComponent(out FlyingSwarmController _))
                {
                    EnemyStatusEffects statusEffects;
                    if (!damageable.TryGetComponent(out statusEffects))
                    {
                        statusEffects = damageable.gameObject.AddComponent<EnemyStatusEffects>();
                    }

                    statusEffects.Configure(tier2Settings);
                }

                if ((damageable.TryGetComponent(out EnemyController _)
                        || damageable.TryGetComponent(out FlyingSwarmController _))
                    && !damageable.TryGetComponent(out EnemyRespawner _))
                {
                    damageable.gameObject.AddComponent<EnemyRespawner>();
                }
            }

            WorldDifficultyManager difficultyManager = Object.FindObjectOfType<WorldDifficultyManager>();
            foreach (PlayerHealth playerHealth in Object.FindObjectsOfType<PlayerHealth>(true))
            {
                if (!playerHealth.gameObject.scene.IsValid())
                {
                    continue;
                }

                PlayerRunPersistence runPersistence;
                if (!playerHealth.TryGetComponent(out runPersistence))
                {
                    runPersistence = playerHealth.gameObject.AddComponent<PlayerRunPersistence>();
                }

                if (!runPersistence.IsAuthoritative)
                {
                    continue;
                }

                if (!playerHealth.TryGetComponent(out HitFlash _))
                {
                    playerHealth.gameObject.AddComponent<HitFlash>();
                }

                if (!playerHealth.TryGetComponent(out PlayerHitFeedback _))
                {
                    playerHealth.gameObject.AddComponent<PlayerHitFeedback>();
                }

                if (!playerHealth.TryGetComponent(out PlayerGuardBreak _))
                {
                    playerHealth.gameObject.AddComponent<PlayerGuardBreak>();
                }

                if (!playerHealth.TryGetComponent(out PlayerMana _))
                {
                    playerHealth.gameObject.AddComponent<PlayerMana>();
                }

                if (!playerHealth.TryGetComponent(out PlayerCurrency _))
                {
                    playerHealth.gameObject.AddComponent<PlayerCurrency>();
                }

                if (!playerHealth.TryGetComponent(out PlayerLandmineInventory _))
                {
                    playerHealth.gameObject.AddComponent<PlayerLandmineInventory>();
                }

                if (!playerHealth.TryGetComponent(out PlayerSpecialMode _))
                {
                    playerHealth.gameObject.AddComponent<PlayerSpecialMode>();
                }

                if (!playerHealth.TryGetComponent(out PlayerAimDirection _))
                {
                    playerHealth.gameObject.AddComponent<PlayerAimDirection>();
                }

                if (!playerHealth.TryGetComponent(out PlayerCurseController _))
                {
                    playerHealth.gameObject.AddComponent<PlayerCurseController>();
                }

                if (!playerHealth.TryGetComponent(out PlayerRunResourceReset _))
                {
                    playerHealth.gameObject.AddComponent<PlayerRunResourceReset>();
                }

                if (!playerHealth.TryGetComponent(out PlayerCurseAltarController _))
                {
                    playerHealth.gameObject.AddComponent<PlayerCurseAltarController>();
                }

                if (!playerHealth.TryGetComponent(out DetectiveCurseHordeDirector _))
                {
                    playerHealth.gameObject.AddComponent<DetectiveCurseHordeDirector>();
                }

                if (!playerHealth.TryGetComponent(out PlayerDamageBoost _))
                {
                    playerHealth.gameObject.AddComponent<PlayerDamageBoost>();
                }

                if (!playerHealth.TryGetComponent(out PlayerFlight _))
                {
                    playerHealth.gameObject.AddComponent<PlayerFlight>();
                }

                PlayerSpecialModeUpgradeState upgradeState;
                if (!playerHealth.TryGetComponent(out upgradeState))
                {
                    upgradeState = playerHealth.gameObject.AddComponent<PlayerSpecialModeUpgradeState>();
                }

                upgradeState.Configure(tier2Settings);

                PlayerResourceShop resourceShop;
                if (!playerHealth.TryGetComponent(out resourceShop))
                {
                    resourceShop = playerHealth.gameObject.AddComponent<PlayerResourceShop>();
                }

                resourceShop.Configure(tier2Settings);

                PlayerStrengthShield strengthShield;
                if (!playerHealth.TryGetComponent(out strengthShield))
                {
                    strengthShield = playerHealth.gameObject.AddComponent<PlayerStrengthShield>();
                }

                strengthShield.Configure(tier2Settings);

                PlayerSkillVisuals skillVisuals;
                if (!playerHealth.TryGetComponent(out skillVisuals))
                {
                    skillVisuals = playerHealth.gameObject.AddComponent<PlayerSkillVisuals>();
                }

                skillVisuals.Configure(tier2Settings);

                PlayerAttackState attackState;
                if (!playerHealth.TryGetComponent(out attackState))
                {
                    attackState = playerHealth.gameObject.AddComponent<PlayerAttackState>();
                }

                PlayerStrengthDeflection strengthDeflection;
                if (!playerHealth.TryGetComponent(out strengthDeflection))
                {
                    strengthDeflection = playerHealth.gameObject.AddComponent<PlayerStrengthDeflection>();
                }

                strengthDeflection.Configure(tier2Settings);

                if (playerHealth.TryGetComponent(out SpinSwordAttack _))
                {
                    if (!playerHealth.TryGetComponent(out PlayerCombatFlow _))
                    {
                        playerHealth.gameObject.AddComponent<PlayerCombatFlow>();
                    }

                    PlayerDash playerDash;
                    if (!playerHealth.TryGetComponent(out playerDash))
                    {
                        playerDash = playerHealth.gameObject.AddComponent<PlayerDash>();
                    }

                    playerDash.Configure(progressionSettings);

                    PlayerFlightBash flightBash;
                    if (!playerHealth.TryGetComponent(out flightBash))
                    {
                        flightBash = playerHealth.gameObject.AddComponent<PlayerFlightBash>();
                    }

                    flightBash.Configure(tier2Settings);

                    PlayerResourceMastery resourceMastery;
                    if (!playerHealth.TryGetComponent(out resourceMastery))
                    {
                        resourceMastery = playerHealth.gameObject.AddComponent<PlayerResourceMastery>();
                    }

                    resourceMastery.Configure(progressionSettings);

                    if (!playerHealth.TryGetComponent(out PlayerPermanentProgression _))
                    {
                        playerHealth.gameObject.AddComponent<PlayerPermanentProgression>();
                    }

                    if (!playerHealth.TryGetComponent(out PlayerPermanentProgressionHud _))
                    {
                        playerHealth.gameObject.AddComponent<PlayerPermanentProgressionHud>();
                    }

                    if (playerHealth.GetComponent<SidewaysParryAttack>() != null
                        && !playerHealth.TryGetComponent(out PlayerCrowdResponse _))
                    {
                        playerHealth.gameObject.AddComponent<PlayerCrowdResponse>();
                    }

                    if (difficultyManager == null)
                    {
                        difficultyManager = playerHealth.gameObject.AddComponent<WorldDifficultyManager>();
                    }

                    difficultyManager.Configure(resourceMastery, progressionSettings);
                }

                PlayerProjectileLauncher launcher;
                if (!playerHealth.TryGetComponent(out launcher))
                {
                    launcher = playerHealth.gameObject.AddComponent<PlayerProjectileLauncher>();
                }

                if (combatSettings != null)
                {
                    launcher.UseProjectilePrefabIfMissing(combatSettings.ProjectilePrefab);
                }

                if (!playerHealth.TryGetComponent(out PlayerActionObserver _))
                {
                    playerHealth.gameObject.AddComponent<PlayerActionObserver>();
                }
            }

            if (difficultyManager == null)
            {
                return;
            }

            foreach (Damageable damageable in Object.FindObjectsOfType<Damageable>(true))
            {
                bool isEnemy = damageable.GetComponent<EnemyController>() != null
                    || damageable.GetComponent<FlyingSwarmController>() != null
                    || damageable.GetComponentInChildren<EnemyContactDamage>(true) != null
                    || damageable.GetComponentInChildren<EnemyShooter>(true) != null
                    || damageable.GetComponent<EnemyRespawner>() != null;
                if (!isEnemy || !damageable.gameObject.scene.IsValid())
                {
                    continue;
                }

                EnemyArchetypeProfile profile;
                if (!damageable.TryGetComponent(out profile))
                {
                    profile = damageable.gameObject.AddComponent<EnemyArchetypeProfile>();
                }

                bool isFlyingRanged = damageable.GetComponent<FlyingSwarmController>() != null;
                if (damageable.GetComponentInChildren<EnemyShooter>(true) != null
                    || damageable.GetComponentInChildren<EnemyPoisonShooter>(true) != null
                    || isFlyingRanged)
                {
                    profile.AddRuntimeArchetype(EnemyArchetype.Ranged);
                }

                if (damageable.GetComponent<EnemyController>() != null
                    || (!isFlyingRanged
                        && damageable.GetComponentInChildren<EnemyContactDamage>(true) != null))
                {
                    profile.AddRuntimeArchetype(EnemyArchetype.Melee);
                }

                if (!damageable.TryGetComponent(out EnemyStagger _))
                {
                    damageable.gameObject.AddComponent<EnemyStagger>();
                }

                EnemyTank tank = damageable.GetComponent<EnemyTank>();
                if (tank != null)
                {
                    if (!damageable.TryGetComponent(out KnockbackReceiver _))
                    {
                        damageable.gameObject.AddComponent<KnockbackReceiver>();
                    }

                    if (!damageable.TryGetComponent(out EnemyDamageModifiers _))
                    {
                        damageable.gameObject.AddComponent<EnemyDamageModifiers>();
                    }

                    EnemyDefenseController tankDefense;
                    if (!damageable.TryGetComponent(out tankDefense))
                    {
                        tankDefense = damageable.gameObject.AddComponent<EnemyDefenseController>();
                    }

                    tankDefense.ConfigurePreset(EnemyDefensePreset.Troll);
                }

                tank?.ConfigureIfMissing(strategicSettings);
                EnemySwarm swarm = damageable.GetComponent<EnemySwarm>();
                swarm?.ConfigureIfMissing(strategicSettings);
                EnemySupportAura support = damageable.GetComponent<EnemySupportAura>();
                support?.ConfigureIfMissing(strategicSettings, difficultyManager);

                EnemyDropper dropper = damageable.GetComponent<EnemyDropper>();
                dropper?.ConfigureDifficulty(difficultyManager);

                EnemyDifficultyScaler scaler;
                if (!damageable.TryGetComponent(out scaler))
                {
                    scaler = damageable.gameObject.AddComponent<EnemyDifficultyScaler>();
                }

                scaler.Configure(difficultyManager);

            }

            foreach (SwarmCaller caller in Object.FindObjectsOfType<SwarmCaller>(true))
            {
                if (caller.gameObject.scene.IsValid())
                {
                    caller.ConfigureIfMissing(strategicSettings, difficultyManager);
                }
            }

            foreach (EncounterGroup encounter in Object.FindObjectsOfType<EncounterGroup>(true))
            {
                if (encounter.gameObject.scene.IsValid())
                {
                    encounter.ConfigureIfMissing(strategicSettings, difficultyManager);
                }
            }
        }
    }
}
