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
        [SerializeField] private bool contractAttempt;
        [SerializeField] private string activeContractId;
        [SerializeField] private long contractAttemptSequence;
        [SerializeField] private long activeContractAttemptId;
        [SerializeField] private bool contractRewardSettled;
        [SerializeField] private string lastCompletedContractId;
        [SerializeField] private ContractRating lastContractRating;

        public static MissionRunContext Current => current;
        public CaveGameMode SelectedMode => selectedMode;
        public string SelectedMapId => selectedMapId;
        public string SelectedScene => selectedScene;
        public int RunSeed => runSeed;
        public MissionLifecycleState State => state;
        public MissionRewardResult Reward => reward;
        public bool HasMission => !string.IsNullOrEmpty(selectedScene);
        public bool IsContractAttempt => contractAttempt;
        public string ActiveContractId => activeContractId;
        public long ActiveContractAttemptId => activeContractAttemptId;
        public bool ContractRewardSettled => contractRewardSettled;
        public string LastCompletedContractId => lastCompletedContractId;
        public ContractRating LastContractRating => lastContractRating;
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
            ClearActiveContractAttempt();
            selectedMode = mode;
            selectedMapId = map.mapId;
            selectedScene = map.sceneName;
            runSeed = seed;
            reward = default;
            SetState(MissionLifecycleState.Setup);
        }

        /// <summary>
        /// Starts a contract inside the existing mission lifecycle. Recovery
        /// contracts currently run as Nexus activities, so the selected scene
        /// remains the hub and no second mission-state authority is created.
        /// </summary>
        public long BeginContract(ContractDefinition definition, int seed)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) return 0L;

            contractAttemptSequence = contractAttemptSequence == long.MaxValue
                ? 1L
                : contractAttemptSequence + 1L;
            contractAttempt = true;
            activeContractId = definition.Id;
            activeContractAttemptId = contractAttemptSequence;
            contractRewardSettled = false;
            selectedMode = CaveGameMode.ClassicSweep;
            selectedMapId = "contract:" + definition.Id;
            selectedScene = HubScene;
            runSeed = seed;
            reward = default;
            SetState(MissionLifecycleState.Setup);
            return activeContractAttemptId;
        }

        public bool TryMarkContractRewardSettled(long attemptId, string contractId)
        {
            if (!contractAttempt
                || activeContractAttemptId == 0L
                || attemptId != activeContractAttemptId
                || !string.Equals(activeContractId, contractId, StringComparison.Ordinal)
                || contractRewardSettled
                || state != MissionLifecycleState.PrimaryObjectiveComplete)
            {
                return false;
            }

            contractRewardSettled = true;
            return true;
        }

        public void RecordContractCompletion(string contractId, ContractRating rating)
        {
            if (!contractRewardSettled
                || !string.Equals(activeContractId, contractId, StringComparison.Ordinal)) return;

            lastCompletedContractId = contractId;
            lastContractRating = rating;
        }

        public void EndContractAttempt()
        {
            ClearActiveContractAttempt();
            state = MissionLifecycleState.None;
            reward = default;
            runSeed = 0;
            selectedScene = null;
            StateChanged?.Invoke(state);
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
            ClearActiveContractAttempt();
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

        private void ClearActiveContractAttempt()
        {
            contractAttempt = false;
            activeContractId = null;
            activeContractAttemptId = 0L;
            contractRewardSettled = false;
        }
    }
}
