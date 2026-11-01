using System;
using UnityEngine;

namespace Cave.Missions
{
    public enum ContractTier
    {
        Initiate = 1,
        Adventurer = 2,
        Veteran = 3
    }

    public enum ContractRating
    {
        None = 0,
        Completed = 1,
        Proficient = 2,
        Mastered = 3
    }

    public enum RecoveryContractObjectiveType
    {
        Unavailable = 0,
        FloraSamples = 1,
        SupplyCaches = 2
    }

    [Serializable]
    public sealed class ContractDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ContractTier tier;
        [SerializeField, TextArea] private string objective;
        [SerializeField, TextArea] private string trainingFocus;
        [SerializeField, Min(0)] private int coinReward;
        [SerializeField] private RecoveryContractObjectiveType objectiveType;
        [SerializeField, Min(1)] private int requiredCount = 1;
        [SerializeField, Min(1f)] private float proficientSeconds = 90f;
        [SerializeField, Min(1f)] private float masteredSeconds = 45f;

        public string Id => id;
        public string DisplayName => displayName;
        public ContractTier Tier => tier;
        public string Objective => objective;
        public string TrainingFocus => trainingFocus;
        public int CoinReward => coinReward;
        public RecoveryContractObjectiveType ObjectiveType => objectiveType;
        public int RequiredCount => Mathf.Max(1, requiredCount);
        public bool IsImplemented => objectiveType != RecoveryContractObjectiveType.Unavailable;

        public ContractRating EvaluateRating(float elapsedSeconds)
        {
            if (elapsedSeconds <= masteredSeconds) return ContractRating.Mastered;
            if (elapsedSeconds <= proficientSeconds) return ContractRating.Proficient;
            return ContractRating.Completed;
        }

        internal static ContractDefinition Create(
            string contractId,
            string name,
            ContractTier contractTier,
            string objectiveText,
            string focus,
            int reward,
            RecoveryContractObjectiveType type = RecoveryContractObjectiveType.Unavailable,
            int count = 1,
            float proficientTime = 90f,
            float masteredTime = 45f)
        {
            return new ContractDefinition
            {
                id = contractId,
                displayName = name,
                tier = contractTier,
                objective = objectiveText,
                trainingFocus = focus,
                coinReward = Mathf.Max(0, reward),
                objectiveType = type,
                requiredCount = Mathf.Max(1, count),
                proficientSeconds = Mathf.Max(1f, proficientTime),
                masteredSeconds = Mathf.Clamp(masteredTime, 1f, Mathf.Max(1f, proficientTime))
            };
        }
    }

}
