using UnityEngine;

namespace Cave.Player
{
    /// <summary>Deterministic coverage for the Brace-only resource contribution.</summary>
    public static class BraceResourceRecoveryVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyBraceOneRates(out failure)
                || !VerifyBraceTwoRatesAndNoStacking(out failure)
                || !VerifyInactiveStagesContributeNothing(out failure)
                || !VerifyClampExpectations(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyBraceOneRates(out string failure)
        {
            BraceResourceRecovery atOneHundred = BraceResourceRecoveryPolicy.Resolve(BraceStage.Full, 100f, 100f, 1f);
            BraceResourceRecovery atTwoHundred = BraceResourceRecoveryPolicy.Resolve(BraceStage.Full, 200f, 100f, 1f);
            bool valid = Approximately(atOneHundred.Stamina, 5f)
                && Approximately(atOneHundred.Mana, 0f)
                && Approximately(atTwoHundred.Stamina, 10f);
            failure = valid ? null : "Brace I did not restore exactly five percent of maximum Stamina per second.";
            return valid;
        }

        private static bool VerifyBraceTwoRatesAndNoStacking(out string failure)
        {
            BraceResourceRecovery recovery = BraceResourceRecoveryPolicy.Resolve(BraceStage.Deep, 100f, 100f, 1f);
            bool valid = Approximately(recovery.Stamina, 3f)
                && Approximately(recovery.Mana, 5f)
                && !Approximately(recovery.Stamina, 8f);
            failure = valid ? null : "Brace II Stamina/Mana rates were incorrect or stacked Brace I recovery.";
            return valid;
        }

        private static bool VerifyInactiveStagesContributeNothing(out string failure)
        {
            BraceResourceRecovery none = BraceResourceRecoveryPolicy.Resolve(BraceStage.None, 100f, 100f, 1f);
            BraceResourceRecovery quick = BraceResourceRecoveryPolicy.Resolve(BraceStage.Quick, 100f, 100f, 1f);
            bool valid = Approximately(none.Stamina, 0f) && Approximately(none.Mana, 0f)
                && Approximately(quick.Stamina, 0f) && Approximately(quick.Mana, 0f);
            failure = valid ? null : "Brace recovery persisted outside an active Brace I/II stage.";
            return valid;
        }

        private static bool VerifyClampExpectations(out string failure)
        {
            BraceResourceRecovery recovery = BraceResourceRecoveryPolicy.Resolve(BraceStage.Deep, 100f, 100f, 1f);
            bool valid = Approximately(Mathf.Min(100f, 99f + recovery.Stamina), 100f)
                && Approximately(Mathf.Min(100f, 99f + recovery.Mana), 100f)
                && Approximately(BraceResourceRecoveryPolicy.Resolve(BraceStage.Full, -1f, -1f, -1f).Stamina, 0f);
            failure = valid ? null : "Brace recovery did not preserve resource cap or invalid-time expectations.";
            return valid;
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) < 0.0001f;
        }
    }
}
