using UnityEngine;

namespace Cave.Missions
{
    public sealed class MissionMapBootstrap : MonoBehaviour
    {
        private MissionControllerBase activeController;
        private MissionSpawnDirector spawnDirector;

        private void Start()
        {
            Debug.Log("[Cave][Mission] MissionMapBootstrap.Start entered for " + gameObject.scene.name + ".", this);
            MissionRunContext context = MissionRunContext.Current;
            if (context == null || !context.HasMission || context.SelectedScene != gameObject.scene.name) return;
            activeController = (MissionControllerBase)gameObject.AddComponent(
                MissionControllerSelection.ResolveControllerType(context.SelectedMode));

            Debug.Log("[Cave][Mission] Mission controller created: " + activeController.GetType().Name + ".", this);
            MissionObjectiveHud hud = MissionObjectiveHud.Create();
            hud.Bind(activeController, context);
            spawnDirector = GetComponent<MissionSpawnDirector>();
            if (spawnDirector == null)
            {
                spawnDirector = gameObject.AddComponent<MissionSpawnDirector>();
            }

            spawnDirector.Initialize(context);
            activeController.Initialize(context);
            Debug.Log("[Cave][Mission] Mission controller initialized.", this);
        }
    }

    /// <summary>Pure mode-to-controller routing used by both the production
    /// bootstrap and deterministic verification. Unknown/legacy Hunt remains
    /// Classic Sweep; Sovereign Gambit can never silently fall through.</summary>
    public static class MissionControllerSelection
    {
        public static System.Type ResolveControllerType(CaveGameMode mode)
        {
            switch (mode)
            {
                case CaveGameMode.CorruptionPurge: return typeof(CorruptionPurgeMissionController);
                case CaveGameMode.CorruptBounty: return typeof(CorruptBountyMissionController);
                case CaveGameMode.Containment: return typeof(ContainmentMissionController);
                case CaveGameMode.SovereignGambit: return typeof(SovereignGambitMissionController);
                default: return typeof(ClassicSweepMissionController);
            }
        }
    }
}
