using System;
using Cave.Player;
using UnityEngine;

namespace Cave.Missions
{
    public readonly struct ContractCompletionRecord
    {
        public ContractCompletionRecord(int completionCount, ContractRating bestRating)
        {
            CompletionCount = Mathf.Max(0, completionCount);
            BestRating = bestRating;
        }

        public int CompletionCount { get; }
        public ContractRating BestRating { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerContractRecords : MonoBehaviour
    {
        private const string Prefix = "Cave.Contracts.v1.";
        private const string CountSuffix = ".CompletionCount";
        private const string RatingSuffix = ".BestRating";

        public event Action<string, ContractCompletionRecord> RecordChanged;

        public static PlayerContractRecords Ensure(GameObject player)
        {
            if (player == null) return null;
            PlayerContractRecords records = player.GetComponent<PlayerContractRecords>();
            return records != null ? records : player.AddComponent<PlayerContractRecords>();
        }

        public ContractCompletionRecord GetRecord(string contractId)
        {
            if (string.IsNullOrWhiteSpace(contractId))
                return new ContractCompletionRecord(0, ContractRating.None);

            int count = Mathf.Max(0, PlayerPrefs.GetInt(Key(contractId, CountSuffix), 0));
            ContractRating rating = (ContractRating)Mathf.Clamp(
                PlayerPrefs.GetInt(Key(contractId, RatingSuffix), 0),
                (int)ContractRating.None,
                (int)ContractRating.Mastered);
            return new ContractCompletionRecord(count, rating);
        }

        public ContractCompletionRecord RecordCompletion(string contractId, ContractRating rating)
        {
            if (string.IsNullOrWhiteSpace(contractId) || rating == ContractRating.None)
                return GetRecord(contractId);

            ContractCompletionRecord before = GetRecord(contractId);
            int count = before.CompletionCount == int.MaxValue
                ? int.MaxValue
                : before.CompletionCount + 1;
            ContractRating best = rating > before.BestRating ? rating : before.BestRating;
            PlayerPrefs.SetInt(Key(contractId, CountSuffix), count);
            PlayerPrefs.SetInt(Key(contractId, RatingSuffix), (int)best);
            PlayerPrefs.Save();
            ContractCompletionRecord result = new ContractCompletionRecord(count, best);
            RecordChanged?.Invoke(contractId, result);
            return result;
        }

        public void ResetForNewGame()
        {
            ResetKnownRecords();
        }

        public static void ResetKnownRecords()
        {
            ContractCatalog catalog = ContractCatalog.LoadOrCreateDefaults();
            ResetRecords(catalog.Contracts);
            PlayerPrefs.Save();
        }

        internal static void ResetRecords(ContractDefinition[] definitions)
        {
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Length; i++)
                {
                    if (definitions[i] != null) DeleteRecord(definitions[i].Id);
                }
            }
        }

        internal static void DeleteRecord(string contractId)
        {
            if (string.IsNullOrWhiteSpace(contractId)) return;
            PlayerPrefs.DeleteKey(Key(contractId, CountSuffix));
            PlayerPrefs.DeleteKey(Key(contractId, RatingSuffix));
        }

        private static string Key(string contractId, string suffix)
        {
            return Prefix + contractId + suffix;
        }
    }

    public static class ContractRewardService
    {
        /// <summary>
        /// Settles the approved base payout once for the active attempt. Rating
        /// is persisted for presentation only; Phase 2 intentionally grants no
        /// rating-based coin bonus.
        /// </summary>
        public static bool TrySettle(
            MissionRunContext context,
            ContractDefinition definition,
            long attemptId,
            ContractRating rating,
            PlayerCurrency currency,
            PlayerContractRecords records,
            out int coinsGranted)
        {
            coinsGranted = 0;
            if (context == null
                || definition == null
                || currency == null
                || records == null
                || rating == ContractRating.None
                || !definition.IsImplemented
                || !context.TryMarkContractRewardSettled(attemptId, definition.Id))
            {
                return false;
            }

            coinsGranted = definition.CoinReward;
            currency.AddCurrency(coinsGranted);
            records.RecordCompletion(definition.Id, rating);
            context.RecordContractCompletion(definition.Id, rating);
            return true;
        }
    }
}
