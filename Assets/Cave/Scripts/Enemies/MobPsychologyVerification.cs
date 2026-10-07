using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Deterministic coverage for the optional psychology/Jev seam.</summary>
    public static class MobPsychologyVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if(!VerifyFallbackAndCooldown(out failure)||!VerifyMalformedRejection(out failure)||!VerifyClamp(out failure)||!VerifyCtcInfluence(out failure)) return false;
            failure=null; return true;
        }

        private static bool VerifyFallbackAndCooldown(out string failure)
        {
            PsychologyGroupScheduler scheduler=new PsychologyGroupScheduler();
            scheduler.MarkDirty(new PsychologyEvidence(PsychologyEvidenceKind.PlayerRetreat,"eye",1,0f));
            IReadOnlyList<MobPsychologyDelta> first; bool usedJev;
            bool interpreted=scheduler.TryInterpret(0f,"group",Members(),null,new DeterministicPsychologyInterpreter(),out first,out usedJev);
            scheduler.MarkDirty(new PsychologyEvidence(PsychologyEvidenceKind.PlayerVulnerability,"eye",2,1f));
            IReadOnlyList<MobPsychologyDelta> beforeCooldown; bool ignored;
            bool throttled=!scheduler.TryInterpret(4.9f,"group",Members(),null,new DeterministicPsychologyInterpreter(),out beforeCooldown,out ignored);
            IReadOnlyList<MobPsychologyDelta> afterCooldown;
            bool refreshed=scheduler.TryInterpret(5f,"group",Members(),null,new DeterministicPsychologyInterpreter(),out afterCooldown,out ignored);
            bool valid=interpreted&&!usedJev&&first.Count==1&&first[0].Aggression>0f&&throttled&&scheduler.IsDirty==false&&refreshed&&afterCooldown.Count==1;
            failure=valid?null:"Psychology fallback or cooldown failed."; return valid;
        }

        private static bool VerifyMalformedRejection(out string failure)
        {
            PsychologyGroupScheduler scheduler=new PsychologyGroupScheduler { JevEnabled=true };
            scheduler.MarkDirty(new PsychologyEvidence(PsychologyEvidenceKind.AllyDeath,"eye",3,0f));
            IReadOnlyList<MobPsychologyDelta> deltas; bool usedJev;
            bool interpreted=scheduler.TryInterpret(0f,"group",Members(),new InvalidInterpreter(),new DeterministicPsychologyInterpreter(),out deltas,out usedJev);
            GameObject subject=new GameObject("Psychology Verification");
            try
            {
                MobPsychologyState state=subject.AddComponent<MobPsychologyState>();
                MobPsychologySnapshot before=state.Snapshot("mob");
                bool applied=deltas.Count==1&&state.TryApply(deltas[0],"mob");
                MobPsychologySnapshot after=state.Snapshot("mob");
                bool valid=interpreted&&usedJev&&!applied&&Mathf.Approximately(before.Aggression,after.Aggression);
                failure=valid?null:"Malformed Jev output was accepted."; return valid;
            }
            finally { Object.DestroyImmediate(subject); }
        }

        private static bool VerifyClamp(out string failure)
        {
            GameObject subject=new GameObject("Psychology Clamp Verification");
            try
            {
                MobPsychologyState state=subject.AddComponent<MobPsychologyState>();
                bool accepted=state.TryApply(new MobPsychologyDelta("mob",0.35f,-0.35f,0.35f,-0.35f),"mob");
                bool rejected=!state.TryApply(new MobPsychologyDelta("mob",float.NaN,0f,0f,0f),"mob");
                MobPsychologySnapshot snapshot=state.Snapshot("mob");
                bool valid=accepted&&rejected&&snapshot.Aggression>=0f&&snapshot.Aggression<=1f&&snapshot.Fear>=0f&&snapshot.Fear<=1f;
                failure=valid?null:"Psychology bounds were not enforced."; return valid;
            }
            finally { Object.DestroyImmediate(subject); }
        }

        private static bool VerifyCtcInfluence(out string failure)
        {
            IReadOnlyList<CombatTacticalAssignment> baseline=CombatTacticalCoordinator.Evaluate(new List<CombatTacticalMemberDescriptor>{new CombatTacticalMemberDescriptor("mob",CombatTacticalRole.Frontline)});
            IReadOnlyList<CombatTacticalAssignment> fearful=CombatTacticalCoordinator.Evaluate(new List<CombatTacticalMemberDescriptor>{new CombatTacticalMemberDescriptor("mob",CombatTacticalRole.Frontline,default,new MobPsychologySnapshot("mob",0.2f,0.1f,0.9f,0.5f))});
            bool valid=baseline[0].Intent==CombatTacticalIntent.Pressure&&fearful[0].Intent==CombatTacticalIntent.Reposition;
            failure=valid?null:"Psychology did not remain a bounded CTC preference input."; return valid;
        }

        private static IReadOnlyList<MobPsychologySnapshot> Members(){return new List<MobPsychologySnapshot>{new MobPsychologySnapshot("mob",0.5f,0.5f,0.5f,0.5f)}.AsReadOnly();}
        private sealed class InvalidInterpreter:IPsychologyInterpreter { public bool TryInterpret(PsychologyInterpretationRequest request,out IReadOnlyList<MobPsychologyDelta> deltas){deltas=new List<MobPsychologyDelta>{new MobPsychologyDelta("unknown",0.1f,0f,0f,0f)}.AsReadOnly();return true;} }
    }
}
