using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    public enum DomainConflictOutcomeKind
    {
        Uncontested = 0, Consensus = 1, SelectedByJurisdiction = 2, SelectedBySpecificity = 3,
        SelectedByAuthority = 4, SelectedByCommittedComplexity = 5,
        BaselineDeferredNoJurisdiction = 6, BaselineDeferredStalemate = 7,
        InvalidBaselineMismatch = 8, InvalidEvidence = 9
    }
    public enum CrossDomainArbitrationRejectionReason { None = 0, ParticipantRequired = 1, DuplicateParticipantId = 2, InvalidParticipant = 3, InvalidConflict = 4 }

    public sealed class DomainConflictCandidate
    {
        internal DomainConflictCandidate(DomainConflictParticipant participant, DomainCommitEntry entry)
        { Participant = participant; Entry = entry; }
        public DomainConflictParticipant Participant { get; }
        public string ParticipantId => Participant.ParticipantId;
        public DomainCommitEntry Entry { get; }
        public PhenomenonSemanticSnapshot ExpectedBefore => Entry.ExpectedBefore;
        public PhenomenonSemanticSnapshot FinalAfter => Entry.FinalAfter;
    }

    public sealed class DomainConflictTrace
    {
        internal DomainConflictTrace(DomainConflictContestKey key, PhenomenonSemanticSnapshot before, IReadOnlyList<DomainConflictCandidate> original)
        { ContestKey = key; CommonBefore = before; OriginalCandidates = original; JurisdictionSurvivors = Empty<string>(); SpecificitySurvivors = Empty<string>(); AuthoritySurvivors = Empty<string>(); ComplexitySurvivors = Empty<string>(); JurisdictionEvidence = Empty<DomainJurisdictionEvidence>(); SpecificityEvidence = Empty<DomainSpecificityEvidence>(); AuthorityEvidence = Empty<DomainConflictAuthorityEvidence>(); Complexities = Empty<DomainCommittedComplexityEvidence>(); }
        public DomainConflictContestKey ContestKey { get; } public PhenomenonSemanticSnapshot CommonBefore { get; }
        public IReadOnlyList<DomainConflictCandidate> OriginalCandidates { get; }
        public IReadOnlyList<DomainJurisdictionEvidence> JurisdictionEvidence { get; internal set; }
        public IReadOnlyList<string> JurisdictionSurvivors { get; internal set; }
        public IReadOnlyList<DomainSpecificityEvidence> SpecificityEvidence { get; internal set; }
        public IReadOnlyList<string> SpecificitySurvivors { get; internal set; }
        public IReadOnlyList<DomainConflictAuthorityEvidence> AuthorityEvidence { get; internal set; }
        public IReadOnlyList<string> AuthoritySurvivors { get; internal set; }
        public IReadOnlyList<DomainCommittedComplexityEvidence> Complexities { get; internal set; }
        public IReadOnlyList<string> ComplexitySurvivors { get; internal set; }
        public bool SpecificityRequired { get; internal set; } public bool AuthorityRequired { get; internal set; } public bool ComplexityRequired { get; internal set; }
        public DomainConflictOutcomeKind Outcome { get; internal set; } public string SelectedParticipantId { get; internal set; }
        private static IReadOnlyList<T> Empty<T>() { return new List<T>().AsReadOnly(); }
    }
    public sealed class DomainCommittedComplexityEvidence
    { internal DomainCommittedComplexityEvidence(string participantId, float value) { ParticipantId = participantId; Value = value; } public string ParticipantId { get; } public float Value { get; } }

    public sealed class DomainConflictOutcome
    {
        internal DomainConflictOutcome(DomainConflictContestKey key, DomainConflictOutcomeKind kind, IReadOnlyList<DomainConflictCandidate> candidates, DomainConflictCandidate selected, DomainConflictTrace trace)
        { ContestKey = key; Kind = kind; Candidates = candidates; SelectedCandidate = selected; Trace = trace; }
        public DomainConflictContestKey ContestKey { get; } public DomainConflictOutcomeKind Kind { get; }
        public IReadOnlyList<DomainConflictCandidate> Candidates { get; } public DomainConflictCandidate SelectedCandidate { get; }
        public DomainConflictTrace Trace { get; }
        public bool IsBaselineDeferred => Kind == DomainConflictOutcomeKind.BaselineDeferredNoJurisdiction || Kind == DomainConflictOutcomeKind.BaselineDeferredStalemate;
        public bool IsInvalid => Kind == DomainConflictOutcomeKind.InvalidBaselineMismatch || Kind == DomainConflictOutcomeKind.InvalidEvidence;
    }
    public sealed class CrossDomainAuthoritativeWrite
    {
        internal CrossDomainAuthoritativeWrite(DomainConflictOutcome outcome, DomainCommitEntry entry, IReadOnlyList<string> support)
        { Outcome = outcome; Entry = entry; SupportingParticipantIds = support; }
        public DomainConflictOutcome Outcome { get; } public DomainCommitEntry Entry { get; } public IReadOnlyList<string> SupportingParticipantIds { get; }
    }
    public sealed class CrossDomainArbitrationResult
    {
        internal CrossDomainArbitrationResult(IReadOnlyList<DomainConflictParticipant> participants, IReadOnlyList<DomainConflictOutcome> outcomes, IReadOnlyList<CrossDomainAuthoritativeWrite> writes, CrossDomainArbitrationRejectionReason rejection)
        { Participants = participants; Outcomes = outcomes; AuthoritativeWrites = writes; RejectionReason = rejection; }
        public IReadOnlyList<DomainConflictParticipant> Participants { get; } public IReadOnlyList<DomainConflictOutcome> Outcomes { get; } public IReadOnlyList<CrossDomainAuthoritativeWrite> AuthoritativeWrites { get; }
        public CrossDomainArbitrationRejectionReason RejectionReason { get; } public bool Succeeded => RejectionReason == CrossDomainArbitrationRejectionReason.None;
        public bool HasActualConflicts { get { for (int i=0;i<Outcomes.Count;i++) if (Outcomes[i].Candidates.Count > 1 && Outcomes[i].Kind != DomainConflictOutcomeKind.Consensus) return true; return false; } }
        public bool HasBaselineDeferrals { get { for (int i=0;i<Outcomes.Count;i++) if (Outcomes[i].IsBaselineDeferred) return true; return false; } }
        public bool HasInvalidConflicts { get { for (int i=0;i<Outcomes.Count;i++) if (Outcomes[i].IsInvalid) return true; return false; } }
    }

    /// <summary>Pure per-key arbitration. Stable IDs order output only; they are never a priority rule.</summary>
    public static class DomainConflictArbitrator
    {
        public static CrossDomainArbitrationResult Arbitrate(IEnumerable<DomainConflictParticipant> source)
        {
            List<DomainConflictParticipant> participants = new List<DomainConflictParticipant>(source ?? new DomainConflictParticipant[0]);
            participants.Sort((left,right) => string.CompareOrdinal(left != null ? left.ParticipantId : null, right != null ? right.ParticipantId : null));
            CrossDomainArbitrationRejectionReason participantRejection = ValidateParticipants(participants);
            if (participantRejection != CrossDomainArbitrationRejectionReason.None) return new CrossDomainArbitrationResult(participants.AsReadOnly(), Empty<DomainConflictOutcome>(), Empty<CrossDomainAuthoritativeWrite>(), participantRejection);
            List<Group> groups = GroupEntries(participants); List<DomainConflictOutcome> outcomes = new List<DomainConflictOutcome>(); List<CrossDomainAuthoritativeWrite> writes = new List<CrossDomainAuthoritativeWrite>(); bool invalid = false;
            for (int index=0; index<groups.Count; index++) { DomainConflictOutcome outcome = Resolve(groups[index]); outcomes.Add(outcome); if (outcome.IsInvalid) invalid = true; CrossDomainAuthoritativeWrite write = ToWrite(outcome); if (write != null) writes.Add(write); }
            return new CrossDomainArbitrationResult(participants.AsReadOnly(), outcomes.AsReadOnly(), writes.AsReadOnly(), invalid ? CrossDomainArbitrationRejectionReason.InvalidConflict : CrossDomainArbitrationRejectionReason.None);
        }
        private static DomainConflictOutcome Resolve(Group group)
        {
            List<DomainConflictCandidate> candidates = group.Candidates; PhenomenonSemanticSnapshot before = candidates[0].ExpectedBefore;
            if (!SameBefore(candidates, before)) { DomainConflictTrace mismatch=new DomainConflictTrace(group.Key,before,candidates.AsReadOnly());mismatch.Outcome=DomainConflictOutcomeKind.InvalidBaselineMismatch;return new DomainConflictOutcome(group.Key, DomainConflictOutcomeKind.InvalidBaselineMismatch, candidates.AsReadOnly(), null, mismatch); }
            if (candidates.Count == 1) return new DomainConflictOutcome(group.Key, DomainConflictOutcomeKind.Uncontested, candidates.AsReadOnly(), candidates[0], null);
            if (SameAfter(candidates, candidates[0].FinalAfter)) return new DomainConflictOutcome(group.Key, DomainConflictOutcomeKind.Consensus, candidates.AsReadOnly(), candidates[0], null);
            DomainConflictTrace trace = new DomainConflictTrace(group.Key, before, candidates.AsReadOnly());
            List<DomainJurisdictionEvidence> jurisdiction = new List<DomainJurisdictionEvidence>(); List<DomainConflictCandidate> survivors = new List<DomainConflictCandidate>();
            for (int i=0;i<candidates.Count;i++) { DomainJurisdictionEvidence value=candidates[i].Participant.Evidence.FindJurisdiction(group.Key); if(value==null||!value.IsValid||value.ParticipantId!=candidates[i].ParticipantId) return Invalid(group,trace); jurisdiction.Add(value);if(value.HasJurisdiction)survivors.Add(candidates[i]); }
            trace.JurisdictionEvidence=jurisdiction.AsReadOnly();trace.JurisdictionSurvivors=Ids(survivors);
            if(survivors.Count==0) return Outcome(group,DomainConflictOutcomeKind.BaselineDeferredNoJurisdiction,null,trace);
            if(survivors.Count==1) return Outcome(group,DomainConflictOutcomeKind.SelectedByJurisdiction,survivors[0],trace);
            trace.SpecificityRequired=true; List<DomainSpecificityEvidence> specificity=new List<DomainSpecificityEvidence>(); if(!TryBestSpecificity(group.Key,survivors,out survivors,out specificity))return Invalid(group,trace); trace.SpecificityEvidence=specificity.AsReadOnly();trace.SpecificitySurvivors=Ids(survivors);
            if(survivors.Count==1)return Outcome(group,DomainConflictOutcomeKind.SelectedBySpecificity,survivors[0],trace);
            trace.AuthorityRequired=true; List<DomainConflictAuthorityEvidence> authority=new List<DomainConflictAuthorityEvidence>(); if(!TryBestAuthority(group.Key,survivors,out survivors,out authority))return Invalid(group,trace);trace.AuthorityEvidence=authority.AsReadOnly();trace.AuthoritySurvivors=Ids(survivors);
            if(survivors.Count==1)return Outcome(group,DomainConflictOutcomeKind.SelectedByAuthority,survivors[0],trace);
            trace.ComplexityRequired=true; List<DomainCommittedComplexityEvidence> complexities=new List<DomainCommittedComplexityEvidence>(); if(!TryBestComplexity(survivors,out survivors,out complexities))return Invalid(group,trace);trace.Complexities=complexities.AsReadOnly();trace.ComplexitySurvivors=Ids(survivors);
            if(survivors.Count==1)return Outcome(group,DomainConflictOutcomeKind.SelectedByCommittedComplexity,survivors[0],trace);
            return Outcome(group,DomainConflictOutcomeKind.BaselineDeferredStalemate,null,trace);
        }
        private static bool TryBestSpecificity(DomainConflictContestKey key,List<DomainConflictCandidate> input,out List<DomainConflictCandidate> output,out List<DomainSpecificityEvidence> evidence)
        { evidence=new List<DomainSpecificityEvidence>();int best=int.MinValue;for(int i=0;i<input.Count;i++){DomainSpecificityEvidence value=input[i].Participant.Evidence.FindSpecificity(key);if(value==null||!value.IsValid||value.ParticipantId!=input[i].ParticipantId){output=null;return false;}evidence.Add(value);if(value.Rank>best)best=value.Rank;}output=Filter(input,c=>c.Participant.Evidence.FindSpecificity(key).Rank==best);return true; }
        private static bool TryBestAuthority(DomainConflictContestKey key,List<DomainConflictCandidate> input,out List<DomainConflictCandidate> output,out List<DomainConflictAuthorityEvidence> evidence)
        { evidence=new List<DomainConflictAuthorityEvidence>();int best=int.MinValue;for(int i=0;i<input.Count;i++){DomainConflictAuthorityEvidence value=input[i].Participant.Evidence.FindAuthority(key);if(value==null||!value.IsValid||value.ParticipantId!=input[i].ParticipantId){output=null;return false;}evidence.Add(value);if(value.Rank>best)best=value.Rank;}output=Filter(input,c=>c.Participant.Evidence.FindAuthority(key).Rank==best);return true; }
        private static bool TryBestComplexity(List<DomainConflictCandidate> input,out List<DomainConflictCandidate> output,out List<DomainCommittedComplexityEvidence> evidence)
        { evidence=new List<DomainCommittedComplexityEvidence>();float best=float.NegativeInfinity;for(int i=0;i<input.Count;i++){float value=input[i].Participant.CommittedComplexity;if(float.IsNaN(value)||float.IsInfinity(value)||value<0f){output=null;return false;}evidence.Add(new DomainCommittedComplexityEvidence(input[i].ParticipantId,value));if(value>best)best=value;}output=Filter(input,c=>c.Participant.CommittedComplexity==best);return true; }
        private static DomainConflictOutcome Invalid(Group group,DomainConflictTrace trace){trace.Outcome=DomainConflictOutcomeKind.InvalidEvidence;return new DomainConflictOutcome(group.Key,DomainConflictOutcomeKind.InvalidEvidence,group.Candidates.AsReadOnly(),null,trace);}
        private static DomainConflictOutcome Outcome(Group group,DomainConflictOutcomeKind kind,DomainConflictCandidate selected,DomainConflictTrace trace){trace.Outcome=kind;trace.SelectedParticipantId=selected!=null?selected.ParticipantId:null;return new DomainConflictOutcome(group.Key,kind,group.Candidates.AsReadOnly(),selected,trace);}
        private static CrossDomainAuthoritativeWrite ToWrite(DomainConflictOutcome outcome){if(outcome.Kind!=DomainConflictOutcomeKind.Uncontested&&outcome.Kind!=DomainConflictOutcomeKind.Consensus&&outcome.SelectedCandidate==null)return null;List<string> ids=new List<string>();if(outcome.Kind==DomainConflictOutcomeKind.Consensus)for(int i=0;i<outcome.Candidates.Count;i++)ids.Add(outcome.Candidates[i].ParticipantId);else ids.Add(outcome.SelectedCandidate.ParticipantId);return new CrossDomainAuthoritativeWrite(outcome,outcome.SelectedCandidate.Entry,ids.AsReadOnly());}
        private static bool SameBefore(List<DomainConflictCandidate> values,PhenomenonSemanticSnapshot before){for(int i=1;i<values.Count;i++)if(!DomainCommitSnapshot.Equal(before,values[i].ExpectedBefore))return false;return true;}
        private static bool SameAfter(List<DomainConflictCandidate> values,PhenomenonSemanticSnapshot after){for(int i=1;i<values.Count;i++)if(!DomainCommitSnapshot.Equal(after,values[i].FinalAfter))return false;return true;}
        private static List<DomainConflictCandidate> Filter(List<DomainConflictCandidate> input,Func<DomainConflictCandidate,bool> predicate){List<DomainConflictCandidate> result=new List<DomainConflictCandidate>();for(int i=0;i<input.Count;i++)if(predicate(input[i]))result.Add(input[i]);return result;}
        private static IReadOnlyList<string> Ids(List<DomainConflictCandidate> values){List<string> ids=new List<string>();for(int i=0;i<values.Count;i++)ids.Add(values[i].ParticipantId);return ids.AsReadOnly();}
        private static List<Group> GroupEntries(List<DomainConflictParticipant> participants){List<Group> groups=new List<Group>();for(int i=0;i<participants.Count;i++)for(int j=0;j<participants[i].CommitPlan.Entries.Count;j++){DomainCommitEntry entry=participants[i].CommitPlan.Entries[j];DomainConflictContestKey key=new DomainConflictContestKey(entry.CarrierId,entry.Phenomenon);Group group=null;for(int k=0;k<groups.Count;k++)if(groups[k].Key.Equals(key)){group=groups[k];break;}if(group==null){group=new Group(key);groups.Add(group);}group.Candidates.Add(new DomainConflictCandidate(participants[i],entry));}groups.Sort((left,right)=>left.Key.CompareTo(right.Key));for(int i=0;i<groups.Count;i++)groups[i].Candidates.Sort((left,right)=>string.CompareOrdinal(left.ParticipantId,right.ParticipantId));return groups;}
        private static CrossDomainArbitrationRejectionReason ValidateParticipants(List<DomainConflictParticipant> participants){if(participants.Count==0)return CrossDomainArbitrationRejectionReason.ParticipantRequired;for(int i=0;i<participants.Count;i++){DomainConflictParticipant p=participants[i];if(p==null||string.IsNullOrEmpty(p.ParticipantId)||p.Composition==null||p.CommitPlan==null||!p.CommitPlan.IsCommittable||!IsValidPlan(p.CommitPlan))return CrossDomainArbitrationRejectionReason.InvalidParticipant;if(i>0&&participants[i-1].ParticipantId==p.ParticipantId)return CrossDomainArbitrationRejectionReason.DuplicateParticipantId;}return CrossDomainArbitrationRejectionReason.None;}
        private static bool IsValidPlan(DomainCommitPlan plan){DomainBoundedOrchestrationResult source=plan.Source;if(source==null||!source.Succeeded||!source.IsCausallyTerminal||source.HasUnresolvedProposalCollisions||source.FinalUnresolvedKeys==null||source.FinalUnresolvedKeys.Count!=0)return false;HashSet<DomainConflictContestKey> keys=new HashSet<DomainConflictContestKey>();for(int i=0;i<plan.Entries.Count;i++){DomainCommitEntry entry=plan.Entries[i];if(entry==null||entry.ExpectedBefore==null||entry.FinalAfter==null||entry.ExpectedBefore.Phenomenon!=entry.Phenomenon||entry.FinalAfter.Phenomenon!=entry.Phenomenon||entry.CausalTransitionIds==null||entry.CausalTransitionIds.Count==0||!keys.Add(new DomainConflictContestKey(entry.CarrierId,entry.Phenomenon)))return false;}return true;}
        private static IReadOnlyList<T> Empty<T>(){return new List<T>().AsReadOnly();}
        private sealed class Group{public Group(DomainConflictContestKey key){Key=key;Candidates=new List<DomainConflictCandidate>();}public DomainConflictContestKey Key;public List<DomainConflictCandidate> Candidates;}
    }
}
