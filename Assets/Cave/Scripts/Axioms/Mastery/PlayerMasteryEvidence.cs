using System;
using System.Collections.Generic;
using Cave.Domain;

namespace Cave.Axioms.Mastery
{
    public enum MasteryEvidenceDimension { State = 1, Rate = 2, Acceleration = 3 }
    public enum DomainMasteryEligibilityReason { Eligible = 0, InvalidLaw = 1, PhenomenonMasteryIncomplete = 2, ExpressionMasteryIncomplete = 3 }

    /// <summary>Pure configurable thresholds and bounded local failure-pressure behavior.</summary>
    public sealed class PlayerMasteryPolicy
    {
        public PlayerMasteryPolicy(float stateThreshold, float rateThreshold, float accelerationThreshold,
            float failurePressureGain, float maximumFailurePressure, float pressureRecoveryPerSecond,
            float minimumEvidenceEfficiency, float frenzyCriticalCoefficient, float frenzyKillWeight, float frenzyThreshold)
        {
            StateThreshold = stateThreshold; RateThreshold = rateThreshold; AccelerationThreshold = accelerationThreshold;
            FailurePressureGain = failurePressureGain; MaximumFailurePressure = maximumFailurePressure;
            PressureRecoveryPerSecond = pressureRecoveryPerSecond; MinimumEvidenceEfficiency = minimumEvidenceEfficiency;
            FrenzyCriticalCoefficient = frenzyCriticalCoefficient; FrenzyKillWeight = frenzyKillWeight; FrenzyThreshold = frenzyThreshold;
        }
        public static PlayerMasteryPolicy Default { get; } = new PlayerMasteryPolicy(1f, 1f, 1f, .25f, 1f, .25f, .5f, 1f, 5f, 10f);
        public float StateThreshold { get; } public float RateThreshold { get; } public float AccelerationThreshold { get; }
        public float FailurePressureGain { get; } public float MaximumFailurePressure { get; } public float PressureRecoveryPerSecond { get; }
        public float MinimumEvidenceEfficiency { get; } public float FrenzyCriticalCoefficient { get; } public float FrenzyKillWeight { get; } public float FrenzyThreshold { get; }
    }

    public sealed class MasteryChannelReport
    {
        internal MasteryChannelReport(float state, float rate, float acceleration, PlayerMasteryPolicy policy)
        { StateEvidence = state; RateEvidence = rate; AccelerationEvidence = acceleration; StateLock = state >= policy.StateThreshold; RateLock = StateLock && rate >= policy.RateThreshold; AccelerationLock = RateLock && acceleration >= policy.AccelerationThreshold; }
        public float StateEvidence { get; } public float RateEvidence { get; } public float AccelerationEvidence { get; }
        public bool StateLock { get; } public bool RateLock { get; } public bool AccelerationLock { get; } public bool IsMastered => StateLock && RateLock && AccelerationLock;
    }

    /// <summary>Pure causal evidence supplied by gameplay only after it has determined intervention correctness.</summary>
    public sealed class PhenomenonMasteryEvidenceSubmission
    {
        public PhenomenonMasteryEvidenceSubmission(LawPhenomenon phenomenon, MasteryEvidenceDimension dimension, float amount, bool succeeded, LawExpression? expression)
        { Phenomenon = phenomenon; Dimension = dimension; Amount = amount; Succeeded = succeeded; Expression = expression; }
        public LawPhenomenon Phenomenon { get; } public MasteryEvidenceDimension Dimension { get; } public float Amount { get; } public bool Succeeded { get; } public LawExpression? Expression { get; }
    }
    public sealed class FrenzyMasterySample
    { public FrenzyMasterySample(float activeDuration, int criticalHits, int kills) { ActiveDuration = activeDuration; CriticalHits = criticalHits; Kills = kills; } public float ActiveDuration { get; } public int CriticalHits { get; } public int Kills { get; } }

    /// <summary>Immutable run-local evidence store for ordinary phenomenon and expression vocabulary.</summary>
    public sealed class PlayerMasteryEvidenceState
    {
        private readonly Dictionary<string, float> evidence;
        private readonly Dictionary<string, float> pressure;
        private readonly float frenzyEvidence;
        public static PlayerMasteryEvidenceState Empty { get; } = new PlayerMasteryEvidenceState(new Dictionary<string,float>(), new Dictionary<string,float>(), 0f);
        private PlayerMasteryEvidenceState(Dictionary<string,float> evidence, Dictionary<string,float> pressure, float frenzyEvidence) { this.evidence = evidence; this.pressure = pressure; this.frenzyEvidence = frenzyEvidence; }

        public MasteryChannelReport GetPhenomenonReport(LawPhenomenon phenomenon, PlayerMasteryPolicy policy) => Report("p:" + (int)phenomenon, policy);
        public MasteryChannelReport GetProjectileReport(PlayerMasteryPolicy policy) => Report("x:projectile", policy);
        public float FrenzyEvidence => frenzyEvidence;
        public bool IsFrenzyMastered(PlayerMasteryPolicy policy) => frenzyEvidence >= policy.FrenzyThreshold;
        public float GetFailurePressure(LawPhenomenon phenomenon, MasteryEvidenceDimension dimension) => Get(pressure, Key("p:" + (int)phenomenon, dimension));

        public PlayerMasteryEvidenceState Submit(PhenomenonMasteryEvidenceSubmission submission, PlayerMasteryPolicy policy)
        {
            Dictionary<string,float> nextEvidence = new Dictionary<string,float>(evidence); Dictionary<string,float> nextPressure = new Dictionary<string,float>(pressure);
            string scope = "p:" + (int)submission.Phenomenon; string key = Key(scope, submission.Dimension);
            if (!submission.Succeeded) { nextPressure[key] = Math.Min(policy.MaximumFailurePressure, Get(nextPressure,key) + policy.FailurePressureGain); return new PlayerMasteryEvidenceState(nextEvidence,nextPressure,frenzyEvidence); }
            if (submission.Amount <= 0f || float.IsNaN(submission.Amount) || float.IsInfinity(submission.Amount)) return new PlayerMasteryEvidenceState(nextEvidence,nextPressure,frenzyEvidence);
            float efficiency = Math.Max(policy.MinimumEvidenceEfficiency, 1f - Get(nextPressure,key)); float gained = submission.Amount * efficiency;
            nextEvidence[key] = Get(nextEvidence,key) + gained;
            if (submission.Expression.HasValue && submission.Expression.Value == LawExpression.Projectile) { string projectileKey = Key("x:projectile", submission.Dimension); nextEvidence[projectileKey] = Get(nextEvidence,projectileKey) + gained; }
            return new PlayerMasteryEvidenceState(nextEvidence,nextPressure,frenzyEvidence);
        }

        public PlayerMasteryEvidenceState SubmitFrenzy(FrenzyMasterySample sample, PlayerMasteryPolicy policy)
        { if (sample == null || sample.ActiveDuration < 0f || sample.CriticalHits < 0 || sample.Kills < 0) return this; float timeContribution = sample.CriticalHits == 0 ? 0f : sample.ActiveDuration * sample.CriticalHits * policy.FrenzyCriticalCoefficient; return new PlayerMasteryEvidenceState(new Dictionary<string,float>(evidence),new Dictionary<string,float>(pressure),frenzyEvidence + timeContribution + sample.Kills * policy.FrenzyKillWeight); }
        public PlayerMasteryEvidenceState Decay(float deltaTime, PlayerMasteryPolicy policy)
        { Dictionary<string,float> next = new Dictionary<string,float>(pressure); foreach (string key in new List<string>(next.Keys)) next[key] = Math.Max(0f,next[key] - Math.Max(0f,deltaTime) * policy.PressureRecoveryPerSecond); return new PlayerMasteryEvidenceState(new Dictionary<string,float>(evidence),next,frenzyEvidence); }
        private MasteryChannelReport Report(string scope, PlayerMasteryPolicy policy) => new MasteryChannelReport(Get(evidence,Key(scope,MasteryEvidenceDimension.State)),Get(evidence,Key(scope,MasteryEvidenceDimension.Rate)),Get(evidence,Key(scope,MasteryEvidenceDimension.Acceleration)),policy);
        private static string Key(string scope, MasteryEvidenceDimension dim) => scope + ":" + (int)dim;
        private static float Get(Dictionary<string,float> values, string key) { float value; return values.TryGetValue(key,out value) ? value : 0f; }
    }

    public sealed class DomainMasteryEligibilityResult
    { internal DomainMasteryEligibilityResult(bool eligible, DomainMasteryEligibilityReason reason) { IsEligible=eligible; Reason=reason; } public bool IsEligible { get; } public DomainMasteryEligibilityReason Reason { get; } }
    public static class DomainMasteryEligibility
    {
        public static DomainMasteryEligibilityResult Evaluate(DomainLaw law, PlayerMasteryEvidenceState state, PlayerMasteryPolicy policy)
        {
            if (law == null || !DomainLaw.Validate(law.Expression,law.Phenomenon,law.TerritoryPrinciple).IsValid) return new DomainMasteryEligibilityResult(false,DomainMasteryEligibilityReason.InvalidLaw);
            PlayerMasteryEvidenceState mastery = state ?? PlayerMasteryEvidenceState.Empty; if (!mastery.GetPhenomenonReport(law.Phenomenon,policy).IsMastered) return new DomainMasteryEligibilityResult(false,DomainMasteryEligibilityReason.PhenomenonMasteryIncomplete);
            bool expression = law.Expression == LawExpression.Projectile ? mastery.GetProjectileReport(policy).IsMastered : mastery.IsFrenzyMastered(policy);
            return new DomainMasteryEligibilityResult(expression, expression ? DomainMasteryEligibilityReason.Eligible : DomainMasteryEligibilityReason.ExpressionMasteryIncomplete);
        }
    }
}
