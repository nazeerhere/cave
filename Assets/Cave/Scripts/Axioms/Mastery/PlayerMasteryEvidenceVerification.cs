using Cave.Domain;
namespace Cave.Axioms.Mastery
{
    public static class PlayerMasteryEvidenceVerification
    {
        public static bool TryRunAll(out string failure)
        {
            PlayerMasteryPolicy p = PlayerMasteryPolicy.Default; PlayerMasteryEvidenceState s = PlayerMasteryEvidenceState.Empty;
            s = s.Submit(new PhenomenonMasteryEvidenceSubmission(LawPhenomenon.Heat,MasteryEvidenceDimension.State,1f,true,LawExpression.Projectile),p);
            s = s.Submit(new PhenomenonMasteryEvidenceSubmission(LawPhenomenon.Heat,MasteryEvidenceDimension.Rate,1f,true,LawExpression.Projectile),p);
            s = s.Submit(new PhenomenonMasteryEvidenceSubmission(LawPhenomenon.Heat,MasteryEvidenceDimension.Acceleration,1f,true,LawExpression.Projectile),p);
            bool heat = s.GetPhenomenonReport(LawPhenomenon.Heat,p).IsMastered && s.GetProjectileReport(p).IsMastered && s.GetPhenomenonReport(LawPhenomenon.Potential,p).StateEvidence == 0f;
            PlayerMasteryEvidenceState failed = s.Submit(new PhenomenonMasteryEvidenceSubmission(LawPhenomenon.Heat,MasteryEvidenceDimension.Rate,1f,false,null),p);
            float pressure = failed.GetFailurePressure(LawPhenomenon.Heat,MasteryEvidenceDimension.Rate);
            PlayerMasteryEvidenceState reduced = failed.Submit(new PhenomenonMasteryEvidenceSubmission(LawPhenomenon.Heat,MasteryEvidenceDimension.Rate,1f,true,null),p);
            bool pressureBehavior = pressure > 0f && reduced.GetPhenomenonReport(LawPhenomenon.Heat,p).RateEvidence < 2f && failed.Decay(10f,p).GetFailurePressure(LawPhenomenon.Heat,MasteryEvidenceDimension.Rate) == 0f;
            PlayerMasteryEvidenceState frenzy = PlayerMasteryEvidenceState.Empty.SubmitFrenzy(new FrenzyMasterySample(5f,3,0),p).SubmitFrenzy(new FrenzyMasterySample(0f,0,2),p);
            DomainLaw law; LawValidationResult v; DomainLaw.TryCreate(LawExpression.Projectile,LawPhenomenon.Heat,LawTerritoryPrinciple.Propagation,out law,out v);
            DomainMasteryEligibilityResult eligible = DomainMasteryEligibility.Evaluate(law,s,p);
            bool prior = Prior(out failure);
            bool passed = heat && pressureBehavior && frenzy.FrenzyEvidence == 25f && eligible.IsEligible && AllTracks(s,p) && prior;
            if (passed) failure = null;
            return passed;
        }
        private static bool AllTracks(PlayerMasteryEvidenceState s, PlayerMasteryPolicy p)
        { foreach (LawPhenomenon n in (LawPhenomenon[])System.Enum.GetValues(typeof(LawPhenomenon))) if (s.GetPhenomenonReport(n,p)==null) return false; return true; }
        private static bool Prior(out string failure) { string f; bool ok = DomainCompositionVerification.TryRunAll(out f); failure=ok?null:f; return ok; }
    }
}
