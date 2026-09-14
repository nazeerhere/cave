using System;
using Cave.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Missions
{
    [Serializable]
    public struct MissionRewardResult
    {
        public int currency;
        public int healthPotions;
        public int manaPotions;
        public int oblivionDisks;
        public string rareRewardId;
        public float performanceBonus;
    }

    [DisallowMultipleComponent]
    public sealed class MissionRunContext : MonoBehaviour
    {
        public const string HubScene = "00_StartingRoom";
        private static MissionRunContext current;
        [SerializeField] private CaveGameMode selectedMode;
        [SerializeField] private string selectedMapId;
        [SerializeField] private string selectedScene;
        [SerializeField] private int runSeed;
        [SerializeField] private MissionLifecycleState state;
        [SerializeField] private MissionRewardResult reward;

        public static MissionRunContext Current => current;
        public CaveGameMode SelectedMode => selectedMode;
        public string SelectedMapId => selectedMapId;
        public string SelectedScene => selectedScene;
        public int RunSeed => runSeed;
        public MissionLifecycleState State => state;
        public MissionRewardResult Reward => reward;
        public bool HasMission => !string.IsNullOrEmpty(selectedScene);
        public event Action<MissionLifecycleState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (current != null) return;
            GameObject host = new GameObject("Mission Run Context");
            current = host.AddComponent<MissionRunContext>();
            DontDestroyOnLoad(host);
        }

        public void Begin(CaveGameMode mode, CaveMapDefinition map, int seed)
        {
            selectedMode = mode;
            selectedMapId = map.mapId;
            selectedScene = map.sceneName;
            runSeed = seed;
            reward = default;
            SetState(MissionLifecycleState.Setup);
        }

        public void SetState(MissionLifecycleState next)
        {
            if (state == next) return;
            state = next;
            StateChanged?.Invoke(next);
        }

        public void SetReward(MissionRewardResult result) { reward = result; }

        public void ClearRuntime(bool preserveLastSelection = true)
        {
            state = MissionLifecycleState.None;
            reward = default;
            runSeed = 0;
            if (!preserveLastSelection)
            {
                selectedMode = CaveGameMode.ClassicSweep;
                selectedMapId = null;
            }
            selectedScene = null;
            StateChanged?.Invoke(state);
        }

        public void ReturnToHub()
        {
            ClearRuntime(true);
            if (PlayerRunPersistence.PrepareTransition(HubScene, "StartingRoomSpawn"))
                SceneManager.LoadSceneAsync(HubScene, LoadSceneMode.Single);
            else
                SceneManager.LoadSceneAsync(HubScene, LoadSceneMode.Single);
        }

        private void Awake()
        {
            if (current != null && current != this) { Destroy(gameObject); return; }
            current = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
