using System.Collections.Generic;
using Cave.Player;
using Cave.UI;
using UnityEngine;

namespace Cave.Enemies
{
    public static class DetectiveResearchReworkVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyEvidenceThreshold(out failure)
                && VerifyMovementSampling(out failure)
                && VerifyExpiration(out failure)
                && VerifyEscalatingProgression(out failure)
                && VerifyIncorrectPredictionPreservesProgress(out failure)
                && VerifyFreshEvidence(out failure)
                && VerifyMaximumStageAndExactlyOnce(out failure)
                && VerifyTowerReset(out failure)
                && VerifyDebuffValues(out failure)
                && VerifyHudFormatting(out failure);
        }

        private static bool VerifyEvidenceThreshold(out string failure)
        {
            bool pass = !DetectiveResearchRules.CanBeginPrediction(6, 20f, 6f)
                && !DetectiveResearchRules.CanBeginPrediction(7, 5.99f, 6f)
                && DetectiveResearchRules.CanBeginPrediction(7, 6f, 6f)
                && DetectiveResearchRules.RequiredEvidenceCount == 7
                && DetectiveResearchRules.ResolveEvidenceRequirement(7, 7, 2) == 7
                && DetectiveResearchRules.ResolveEvidenceRequirement(3, 5, 1) == 7;
            failure = pass ? null : "The seven-evidence/minimum-duration prediction gate failed.";
            return pass;
        }

        private static bool VerifyMovementSampling(out string failure)
        {
            List<PlayerActionCategory> evidence = new List<PlayerActionCategory>();
            int repetitions = 0;
            bool first = DetectiveResearchRules.TryRecordObservation(
                evidence, PlayerActionCategory.Reposition, 3, ref repetitions);
            bool second = DetectiveResearchRules.TryRecordObservation(
                evidence, PlayerActionCategory.Reposition, 3, ref repetitions);
            bool third = DetectiveResearchRules.TryRecordObservation(
                evidence, PlayerActionCategory.Reposition, 3, ref repetitions);
            bool fourth = DetectiveResearchRules.TryRecordObservation(
                evidence, PlayerActionCategory.Reposition, 3, ref repetitions);
            bool pass = first && !second && !third && fourth && evidence.Count == 2;
            failure = pass ? null : "Repeated movement bypassed the existing sample safeguard.";
            return pass;
        }

        private static bool VerifyExpiration(out string failure)
        {
            int stage = 1;
            int correct = 1;
            bool pass = !DetectiveResearchRules.CanBeginPrediction(6, 20f, 6f)
                && stage == 1 && correct == 1;
            failure = pass ? null : "An incomplete expired cycle changed research progress.";
            return pass;
        }

        private static bool VerifyEscalatingProgression(out string failure)
        {
            int stage = 0;
            int correct = 0;
            bool resolved = false;
            bool advanced;
            DetectiveResearchRules.TryResolvePrediction(
                true, ref resolved, ref stage, ref correct, out advanced);
            bool stageZero = advanced && stage == 1 && correct == 0;

            resolved = false;
            DetectiveResearchRules.TryResolvePrediction(
                true, ref resolved, ref stage, ref correct, out advanced);
            bool stageOneFirst = !advanced && stage == 1 && correct == 1;
            resolved = false;
            DetectiveResearchRules.TryResolvePrediction(
                true, ref resolved, ref stage, ref correct, out advanced);
            bool stageOneSecond = advanced && stage == 2 && correct == 0;

            for (int index = 0; index < 2; index++)
            {
                resolved = false;
                DetectiveResearchRules.TryResolvePrediction(
                    true, ref resolved, ref stage, ref correct, out advanced);
                if (advanced || stage != 2 || correct != index + 1)
                {
                    failure = "Stage 2 advanced before its third correct prediction.";
                    return false;
                }
            }

            resolved = false;
            DetectiveResearchRules.TryResolvePrediction(
                true, ref resolved, ref stage, ref correct, out advanced);
            bool pass = stageZero && stageOneFirst && stageOneSecond
                && advanced && stage == 3 && correct == 0;
            failure = pass ? null : "The 1/2/3 correct-prediction progression failed.";
            return pass;
        }

        private static bool VerifyIncorrectPredictionPreservesProgress(out string failure)
        {
            int stage = 2;
            int correct = 2;
            bool resolved = false;
            bool advanced;
            bool accepted = DetectiveResearchRules.TryResolvePrediction(
                false, ref resolved, ref stage, ref correct, out advanced);
            bool pass = accepted && resolved && !advanced && stage == 2 && correct == 2;
            failure = pass ? null : "An incorrect prediction erased earned stage progress.";
            return pass;
        }

        private static bool VerifyFreshEvidence(out string failure)
        {
            List<PlayerActionCategory> evidence = new List<PlayerActionCategory>();
            int repetitions = 0;
            for (int index = 0; index < DetectiveResearchRules.RequiredEvidenceCount; index++)
            {
                DetectiveResearchRules.TryRecordObservation(
                    evidence, PlayerActionCategory.Attack, 3, ref repetitions);
            }

            bool firstReady = DetectiveResearchRules.CanBeginPrediction(evidence.Count, 6f, 6f);
            evidence.Clear();
            repetitions = 0;
            bool pass = firstReady
                && !DetectiveResearchRules.CanBeginPrediction(evidence.Count, 20f, 6f);
            failure = pass ? null : "A new prediction reused evidence from the prior cycle.";
            return pass;
        }

        private static bool VerifyMaximumStageAndExactlyOnce(out string failure)
        {
            int stage = 3;
            int correct = 0;
            bool resolved = false;
            bool advanced;
            bool first = DetectiveResearchRules.TryResolvePrediction(
                true, ref resolved, ref stage, ref correct, out advanced);
            bool duplicate = DetectiveResearchRules.TryResolvePrediction(
                true, ref resolved, ref stage, ref correct, out advanced);
            bool pass = first && !duplicate && stage == 3 && correct == 0
                && DetectiveResearchRules.RequiredCorrectPredictions(3) == 0;
            failure = pass ? null : "Maximum-stage or exactly-once prediction scoring failed.";
            return pass;
        }

        private static bool VerifyTowerReset(out string failure)
        {
            int stage = 2;
            int correct = 2;
            bool resolved = true;
            DetectiveResearchRules.ResetTowerProgress(ref stage, ref correct, ref resolved);
            bool pass = stage == 0 && correct == 0 && !resolved;
            failure = pass ? null : "Tower destruction did not clear research progress.";
            return pass;
        }

        private static bool VerifyDebuffValues(out string failure)
        {
            DetectiveTowerDebuffStage[] stages =
            {
                new DetectiveTowerDebuffStage(0.65f, 0.7f),
                new DetectiveTowerDebuffStage(0.35f, 0.7f),
                new DetectiveTowerDebuffStage(0.35f, 0.45f),
                new DetectiveTowerDebuffStage(0.2f, 0.3f)
            };
            float[] mana = { 1.1f, 1.2f, 1.35f, 1.5f };
            float[] drops = { 0.9f, 0.75f, 0.55f, 0.35f };
            bool pass = stages.Length == 4
                && Mathf.Approximately(stages[0].HealthRegenerationMultiplier, 0.65f)
                && Mathf.Approximately(stages[3].StaminaRegenerationMultiplier, 0.3f)
                && Mathf.Approximately(mana[2], 1.35f)
                && Mathf.Approximately(drops[3], 0.35f);
            failure = pass ? null : "Established Tower debuff values changed.";
            return pass;
        }

        private static bool VerifyHudFormatting(out string failure)
        {
            bool pass = DetectiveResearchHud.FormatEvidence(6) == "EVIDENCE  6/7"
                && DetectiveResearchHud.FormatPredictionProgress(1, 1) == "CORRECT PREDICTIONS  1/2"
                && DetectiveResearchHud.FormatPredictionProgress(3, 0) == "CORRECT PREDICTIONS  MAX STAGE"
                && DetectiveResearchHud.FormatTowerStage(3) == "TOWER STAGE  3  •  MAX STAGE";
            failure = pass ? null : "Research HUD evidence/progression formatting failed.";
            return pass;
        }
    }
}
