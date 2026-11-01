using Cave.Player;
using UnityEngine;

namespace Cave.Missions
{
    public static class ContractRecoveryVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyCatalog(out failure)) return false;
            if (!VerifyRatings(out failure)) return false;
            if (!VerifyRewardsAndAttempts(out failure)) return false;
            failure = null;
            return true;
        }

        private static bool VerifyCatalog(out string failure)
        {
            ContractCatalog catalog = ContractCatalog.CreateRuntimeDefaults();
            ContractDefinition[] entries = catalog.Contracts;
            bool valid = entries != null
                && entries.Length == 8
                && Reward(catalog, ContractCatalog.HerbalistsRequestId) == 10
                && Reward(catalog, ContractCatalog.SalvageRecoveryId) == 13
                && Reward(catalog, ContractCatalog.BreakersTrialId) == 15
                && Reward(catalog, ContractCatalog.VerminPurgeId) == 18
                && Reward(catalog, ContractCatalog.HazardSweepId) == 40
                && Reward(catalog, ContractCatalog.CaravanProtectionId) == 49
                && Reward(catalog, ContractCatalog.AxiomProvingGroundId) == 135
                && Reward(catalog, ContractCatalog.SovereignGauntletId) == 180
                && CountImplemented(entries) == 2
                && catalog.Find(ContractCatalog.HerbalistsRequestId).RequiredCount == 3
                && catalog.Find(ContractCatalog.SalvageRecoveryId).RequiredCount == 3;
            Object.DestroyImmediate(catalog);
            failure = valid ? null : "The eight-contract roster, payouts, or Phase 2 availability changed.";
            return valid;
        }

        private static bool VerifyRatings(out string failure)
        {
            ContractDefinition definition = ContractDefinition.Create(
                "rating_verification", "RATING", ContractTier.Initiate, "", "", 1,
                RecoveryContractObjectiveType.FloraSamples, 3, 90f, 45f);
            bool valid = definition.EvaluateRating(100f) == ContractRating.Completed
                && definition.EvaluateRating(60f) == ContractRating.Proficient
                && definition.EvaluateRating(30f) == ContractRating.Mastered;
            failure = valid ? null : "Completed / Proficient / Mastered thresholds are not deterministic.";
            return valid;
        }

        private static bool VerifyRewardsAndAttempts(out string failure)
        {
            const string verificationId = "contract_recovery_verification_only";
            PlayerContractRecords.DeleteRecord(verificationId);
            GameObject player = new GameObject("Contract Verification Player");
            PlayerCurrency currency = player.AddComponent<PlayerCurrency>();
            PlayerContractRecords records = player.AddComponent<PlayerContractRecords>();
            GameObject contextObject = new GameObject("Contract Verification Context");
            MissionRunContext context = contextObject.AddComponent<MissionRunContext>();
            ContractDefinition definition = ContractDefinition.Create(
                verificationId, "RECOVERY", ContractTier.Initiate, "", "", 10,
                RecoveryContractObjectiveType.FloraSamples, 3, 90f, 45f);

            long first = context.BeginContract(definition, 101);
            bool setup = context.IsContractAttempt
                && context.State == MissionLifecycleState.Setup
                && context.ActiveContractAttemptId == first;
            context.SetState(MissionLifecycleState.Active);
            int collected = definition.RequiredCount - 1;
            bool activeRejected = !ContractRewardService.TrySettle(
                context, definition, first, ContractRating.Completed, currency, records, out int activeCoins);
            ContractCompletionRecord incompleteRecord = records.GetRecord(verificationId);
            bool incompleteRejected = collected == 2
                && activeRejected
                && activeCoins == 0
                && currency.CurrentCurrency == 0
                && incompleteRecord.CompletionCount == 0
                && incompleteRecord.BestRating == ContractRating.None;

            collected++;
            context.SetState(MissionLifecycleState.PrimaryObjectiveComplete);
            bool firstPaid = ContractRewardService.TrySettle(
                context, definition, first, ContractRating.Proficient, currency, records, out int firstCoins);
            bool duplicateRejected = !ContractRewardService.TrySettle(
                context, definition, first, ContractRating.Mastered, currency, records, out int duplicateCoins);
            ContractCompletionRecord firstRecord = records.GetRecord(verificationId);

            context.SetState(MissionLifecycleState.Success);
            context.EndContractAttempt();
            long second = context.BeginContract(definition, 202);
            context.SetState(MissionLifecycleState.Active);
            bool secondActiveRejected = !ContractRewardService.TrySettle(
                context, definition, second, ContractRating.Mastered, currency, records,
                out int secondActiveCoins);
            context.SetState(MissionLifecycleState.PrimaryObjectiveComplete);
            bool secondPaid = ContractRewardService.TrySettle(
                context, definition, second, ContractRating.Mastered, currency, records, out int secondCoins);
            ContractCompletionRecord repeatedRecord = records.GetRecord(verificationId);
            Object.DestroyImmediate(records);
            records = player.AddComponent<PlayerContractRecords>();
            ContractCompletionRecord persistedRecord = records.GetRecord(verificationId);

            context.EndContractAttempt();
            long failed = context.BeginContract(definition, 303);
            context.SetState(MissionLifecycleState.Active);
            context.SetState(MissionLifecycleState.Failure);
            bool failureRejected = !ContractRewardService.TrySettle(
                context, definition, failed, ContractRating.Completed, currency, records, out int failedCoins);

            context.EndContractAttempt();
            long abandoned = context.BeginContract(definition, 404);
            context.SetState(MissionLifecycleState.Active);
            context.EndContractAttempt();
            bool abandonmentRejected = !ContractRewardService.TrySettle(
                context, definition, abandoned, ContractRating.Completed, currency, records,
                out int abandonedCoins);

            long stale = context.BeginContract(definition, 505);
            context.SetState(MissionLifecycleState.Active);
            context.SetState(MissionLifecycleState.PrimaryObjectiveComplete);
            long current = context.BeginContract(definition, 606);
            context.SetState(MissionLifecycleState.Active);
            context.SetState(MissionLifecycleState.PrimaryObjectiveComplete);
            bool staleAttemptRejected = !ContractRewardService.TrySettle(
                context, definition, stale, ContractRating.Completed, currency, records, out int staleCoins);
            context.ClearRuntime(true);
            bool clearedTransitionRejected = !ContractRewardService.TrySettle(
                context, definition, current, ContractRating.Completed, currency, records,
                out int clearedCoins);

            PlayerContractRecords.ResetRecords(new[] { definition });
            ContractCompletionRecord resetRecord = records.GetRecord(verificationId);
            bool valid = setup
                && incompleteRejected
                && collected == definition.RequiredCount
                && firstPaid && firstCoins == 10
                && duplicateRejected && duplicateCoins == 0
                && firstRecord.CompletionCount == 1
                && firstRecord.BestRating == ContractRating.Proficient
                && second > first
                && secondActiveRejected && secondActiveCoins == 0
                && secondPaid && secondCoins == 10
                && currency.CurrentCurrency == 20
                && repeatedRecord.CompletionCount == 2
                && repeatedRecord.BestRating == ContractRating.Mastered
                && persistedRecord.CompletionCount == 2
                && persistedRecord.BestRating == ContractRating.Mastered
                && failureRejected && failedCoins == 0
                && abandonmentRejected && abandonedCoins == 0
                && staleAttemptRejected && staleCoins == 0
                && clearedTransitionRejected && clearedCoins == 0
                && resetRecord.CompletionCount == 0
                && resetRecord.BestRating == ContractRating.None;

            PlayerContractRecords.DeleteRecord(verificationId);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(contextObject);
            failure = valid ? null : "Active/incomplete authorization, exactly-once payout, repeat completion, failure, abandonment, stale transition, or reset verification failed.";
            return valid;
        }

        private static int Reward(ContractCatalog catalog, string id)
        {
            ContractDefinition definition = catalog.Find(id);
            return definition != null ? definition.CoinReward : -1;
        }

        private static int CountImplemented(ContractDefinition[] definitions)
        {
            int count = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null && definitions[i].IsImplemented) count++;
            }
            return count;
        }
    }
}
