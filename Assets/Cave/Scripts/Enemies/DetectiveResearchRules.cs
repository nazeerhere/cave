using System;
using System.Collections.Generic;
using Cave.Player;

namespace Cave.Enemies
{
    /// <summary>
    /// Deterministic rules shared by the live Detective coordinator and focused verification.
    /// This class owns no runtime state.
    /// </summary>
    public static class DetectiveResearchRules
    {
        public const int RequiredEvidenceCount = 7;
        public const int MaximumTowerStage = 3;
        public const float ProvisionalMaximumObservationDuration = 20f;

        public static int RequiredCorrectPredictions(int currentStage)
        {
            int stage = Math.Max(0, Math.Min(currentStage, MaximumTowerStage));
            return stage >= MaximumTowerStage ? 0 : stage + 1;
        }

        public static int ResolveEvidenceRequirement(
            int configuredNormalLength,
            int configuredMaximumLength,
            int curseBonus)
        {
            // Read the serialized values so older assets migrate deterministically,
            // then enforce the approved fixed contract. The curse cannot push it to eight.
            int requested = Math.Min(
                Math.Max(0, configuredNormalLength) + Math.Max(0, curseBonus),
                Math.Max(0, configuredMaximumLength));
            return Math.Max(RequiredEvidenceCount, Math.Min(requested, RequiredEvidenceCount));
        }

        public static bool CanBeginPrediction(
            int evidenceCount,
            float observationTime,
            float minimumObservationDuration)
        {
            return evidenceCount >= RequiredEvidenceCount
                && observationTime >= Math.Max(0f, minimumObservationDuration);
        }

        public static bool TryRecordObservation(
            IList<PlayerActionCategory> evidence,
            PlayerActionCategory category,
            int repeatedMoveEventsPerSample,
            ref int consecutiveMoveRepetitions)
        {
            if (evidence == null || evidence.Count >= RequiredEvidenceCount)
            {
                return false;
            }

            bool isMove = category == PlayerActionCategory.Reposition;
            bool previousWasMove = evidence.Count > 0
                && evidence[evidence.Count - 1] == PlayerActionCategory.Reposition;
            if (isMove && previousWasMove)
            {
                consecutiveMoveRepetitions++;
                if (consecutiveMoveRepetitions < Math.Max(1, repeatedMoveEventsPerSample))
                {
                    return false;
                }

                consecutiveMoveRepetitions = 0;
            }
            else
            {
                consecutiveMoveRepetitions = 0;
            }

            evidence.Add(category);
            return true;
        }

        public static bool TryResolvePrediction(
            bool correct,
            ref bool attemptResolved,
            ref int currentStage,
            ref int currentStageCorrectPredictions,
            out bool stageAdvanced)
        {
            stageAdvanced = false;
            if (attemptResolved)
            {
                return false;
            }

            attemptResolved = true;
            currentStage = Math.Max(0, Math.Min(currentStage, MaximumTowerStage));
            currentStageCorrectPredictions = Math.Max(0, currentStageCorrectPredictions);
            if (!correct || currentStage >= MaximumTowerStage)
            {
                return true;
            }

            int required = RequiredCorrectPredictions(currentStage);
            currentStageCorrectPredictions++;
            if (currentStageCorrectPredictions < required)
            {
                return true;
            }

            currentStage++;
            currentStageCorrectPredictions = 0;
            stageAdvanced = true;
            return true;
        }

        public static void ResetTowerProgress(
            ref int currentStage,
            ref int currentStageCorrectPredictions,
            ref bool attemptResolved)
        {
            currentStage = 0;
            currentStageCorrectPredictions = 0;
            attemptResolved = false;
        }
    }
}
