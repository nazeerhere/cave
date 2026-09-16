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
            switch (context.SelectedMode)
            {
                case CaveGameMode.CorruptionPurge: activeController = gameObject.AddComponent<CorruptionPurgeMissionController>(); break;
                case CaveGameMode.CorruptBounty: activeController = gameObject.AddComponent<CorruptBountyMissionController>(); break;
                case CaveGameMode.Containment: activeController = gameObject.AddComponent<ContainmentMissionController>(); break;
                default: activeController = gameObject.AddComponent<ClassicSweepMissionController>(); break;
            }

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
}
