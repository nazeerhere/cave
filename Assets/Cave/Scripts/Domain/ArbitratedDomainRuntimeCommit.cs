using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    public enum ArbitratedDomainCommitPlanRejectionReason
    {
        None = 0, ArbitrationResultRequired = 1, ArbitrationUnsuccessful = 2,
        InvalidConflict = 3, InvalidParticipant = 4, InvalidAuthoritativeProjection = 5,
        DuplicateSelectedKey = 6, MissingSelectedLineage = 7, SnapshotMismatch = 8,
        BaselineDeferredIncluded = 9, InvalidConflictIncluded = 10
    }
    public enum ArbitratedRuntimeCommitRejectionReason { None = 0, ArbitratedPlanNotCommittable = 1 }

    /// <summary>
    /// The sole adapter from cross-Domain authority into the existing atomic runtime commit contract.
    /// It has no public constructor: entries can only be derived from a validated arbitration projection.
    /// </summary>
    public sealed class ArbitratedDomainCommitPlan
    {
        internal ArbitratedDomainCommitPlan(CrossDomainArbitrationResult arbitration, IReadOnlyList<CrossDomainAuthoritativeWrite> writes, DomainCommitPlan runtimePlan, ArbitratedDomainCommitPlanRejectionReason rejection)
        { Arbitration = arbitration; AuthoritativeWrites = writes; RuntimePlan = runtimePlan; RejectionReason = rejection; }
        public CrossDomainArbitrationResult Arbitration { get; }
        public IReadOnlyList<CrossDomainAuthoritativeWrite> AuthoritativeWrites { get; }
        /// <summary>Internal commit representation; retained for inspection but never rebuilt from raw candidate proposals.</summary>
        public DomainCommitPlan RuntimePlan { get; }
        public ArbitratedDomainCommitPlanRejectionReason RejectionReason { get; }
        public bool IsCommittable => RejectionReason == ArbitratedDomainCommitPlanRejectionReason.None;
        public int WriteCount => RuntimePlan != null ? RuntimePlan.WriteCount : 0;
        public int NoOpCount => RuntimePlan != null ? RuntimePlan.NoOpCount : 0;
    }

    public static class ArbitratedDomainCommitPlanBuilder
    {
        public static ArbitratedDomainCommitPlan Build(CrossDomainArbitrationResult arbitration)
        {
            if (arbitration == null) return Rejected(null, ArbitratedDomainCommitPlanRejectionReason.ArbitrationResultRequired);
            if (!arbitration.Succeeded) return Rejected(arbitration, arbitration.HasInvalidConflicts ? ArbitratedDomainCommitPlanRejectionReason.InvalidConflict : ArbitratedDomainCommitPlanRejectionReason.ArbitrationUnsuccessful);
            if (arbitration.HasInvalidConflicts) return Rejected(arbitration, ArbitratedDomainCommitPlanRejectionReason.InvalidConflict);
            if (!ParticipantsValid(arbitration.Participants)) return Rejected(arbitration, ArbitratedDomainCommitPlanRejectionReason.InvalidParticipant);
            if (arbitration.Outcomes == null || arbitration.AuthoritativeWrites == null) return Rejected(arbitration, ArbitratedDomainCommitPlanRejectionReason.InvalidAuthoritativeProjection);

            List<CrossDomainAuthoritativeWrite> expected = new List<CrossDomainAuthoritativeWrite>();
            for (int index = 0; index < arbitration.Outcomes.Count; index++)
            {
                DomainConflictOutcome outcome = arbitration.Outcomes[index];
                ArbitratedDomainCommitPlanRejectionReason rejection;
                CrossDomainAuthoritativeWrite projected;
                if (!TryProject(outcome, out projected, out rejection)) return Rejected(arbitration, rejection);
                if (projected != null) expected.Add(projected);
            }
            expected.Sort((left, right) => Key(left.Entry).CompareTo(Key(right.Entry)));
            Dictionary<DomainConflictContestKey, CrossDomainAuthoritativeWrite> provided = new Dictionary<DomainConflictContestKey, CrossDomainAuthoritativeWrite>();
            for (int index=0;index<arbitration.AuthoritativeWrites.Count;index++)
            {
                CrossDomainAuthoritativeWrite write=arbitration.AuthoritativeWrites[index];
                if(write==null||write.Entry==null)return Rejected(arbitration,ArbitratedDomainCommitPlanRejectionReason.InvalidAuthoritativeProjection);
                if(write.Outcome!=null&&write.Outcome.IsBaselineDeferred)return Rejected(arbitration,ArbitratedDomainCommitPlanRejectionReason.BaselineDeferredIncluded);
                if(write.Outcome!=null&&write.Outcome.IsInvalid)return Rejected(arbitration,ArbitratedDomainCommitPlanRejectionReason.InvalidConflictIncluded);
                DomainConflictContestKey key=Key(write.Entry);if(provided.ContainsKey(key))return Rejected(arbitration,ArbitratedDomainCommitPlanRejectionReason.DuplicateSelectedKey);provided.Add(key,write);
            }
            if (expected.Count != provided.Count) return Rejected(arbitration, ArbitratedDomainCommitPlanRejectionReason.InvalidAuthoritativeProjection);
            List<DomainCommitEntry> entries = new List<DomainCommitEntry>();
            for (int index = 0; index < expected.Count; index++)
            {
                CrossDomainAuthoritativeWrite actual;if(!provided.TryGetValue(Key(expected[index].Entry),out actual)||!Matches(expected[index], actual)) return Rejected(arbitration, ArbitratedDomainCommitPlanRejectionReason.InvalidAuthoritativeProjection);
                entries.Add(expected[index].Entry);
            }
            DomainCommitPlan runtimePlan = new DomainCommitPlan(null, entries.AsReadOnly(), DomainCommitPlanRejectionReason.None);
            return new ArbitratedDomainCommitPlan(arbitration, expected.AsReadOnly(), runtimePlan, ArbitratedDomainCommitPlanRejectionReason.None);
        }

        private static bool TryProject(DomainConflictOutcome outcome, out CrossDomainAuthoritativeWrite projection, out ArbitratedDomainCommitPlanRejectionReason rejection)
        {
            projection = null; rejection = ArbitratedDomainCommitPlanRejectionReason.None;
            if (outcome == null) { rejection = ArbitratedDomainCommitPlanRejectionReason.InvalidAuthoritativeProjection; return false; }
            if (outcome.IsInvalid) { if (outcome.SelectedCandidate != null) { rejection = ArbitratedDomainCommitPlanRejectionReason.InvalidConflictIncluded; return false; } return true; }
            if (outcome.IsBaselineDeferred) { if (outcome.SelectedCandidate != null) { rejection = ArbitratedDomainCommitPlanRejectionReason.BaselineDeferredIncluded; return false; } return true; }
            if (!IsSelectedKind(outcome.Kind) || outcome.SelectedCandidate == null || !CandidateMatchesOutcome(outcome, outcome.SelectedCandidate)) { rejection = ArbitratedDomainCommitPlanRejectionReason.MissingSelectedLineage; return false; }
            DomainCommitEntry entry = outcome.SelectedCandidate.Entry;
            if (!EntryValidForOutcome(entry, outcome)) { rejection = ArbitratedDomainCommitPlanRejectionReason.SnapshotMismatch; return false; }
            IReadOnlyList<string> supporters = Supporters(outcome);
            if (supporters == null) { rejection = ArbitratedDomainCommitPlanRejectionReason.MissingSelectedLineage; return false; }
            projection = new CrossDomainAuthoritativeWrite(outcome, entry, supporters);
            return true;
        }
        private static bool Matches(CrossDomainAuthoritativeWrite expected, CrossDomainAuthoritativeWrite actual)
        {
            if (expected == null || actual == null || !object.ReferenceEquals(expected.Outcome, actual.Outcome) || !object.ReferenceEquals(expected.Entry, actual.Entry)) return false;
            if (!EntryEqual(expected.Entry, actual.Entry) || expected.SupportingParticipantIds == null || actual.SupportingParticipantIds == null || expected.SupportingParticipantIds.Count != actual.SupportingParticipantIds.Count) return false;
            for (int index=0; index<expected.SupportingParticipantIds.Count; index++) if (expected.SupportingParticipantIds[index] != actual.SupportingParticipantIds[index]) return false;
            return true;
        }
        private static bool CandidateMatchesOutcome(DomainConflictOutcome outcome, DomainConflictCandidate candidate)
        {
            if (candidate == null || candidate.Entry == null || outcome.Candidates == null) return false;
            bool included=false;for(int index=0;index<outcome.Candidates.Count;index++)if(object.ReferenceEquals(outcome.Candidates[index],candidate)){included=true;break;}if(!included)return false;
            DomainConflictParticipant participant=candidate.Participant;
            return participant != null && PlanValid(participant.CommitPlan) && ContainsReference(participant.CommitPlan.Entries,candidate.Entry);
        }
        private static bool EntryValidForOutcome(DomainCommitEntry entry, DomainConflictOutcome outcome)
        {
            if(entry==null||entry.ExpectedBefore==null||entry.FinalAfter==null||entry.ExpectedBefore.Phenomenon!=entry.Phenomenon||entry.FinalAfter.Phenomenon!=entry.Phenomenon||!Key(entry).Equals(outcome.ContestKey)||entry.CausalTransitionIds==null||entry.CausalTransitionIds.Count==0)return false;
            if(outcome.Trace!=null&&outcome.Trace.CommonBefore!=null&&!DomainCommitSnapshot.Equal(entry.ExpectedBefore,outcome.Trace.CommonBefore))return false;
            return true;
        }
        private static IReadOnlyList<string> Supporters(DomainConflictOutcome outcome)
        {
            List<string> values=new List<string>();
            if(outcome.Kind==DomainConflictOutcomeKind.Consensus){for(int index=0;index<outcome.Candidates.Count;index++){DomainConflictCandidate candidate=outcome.Candidates[index];if(!CandidateMatchesOutcome(outcome,candidate)||!DomainCommitSnapshot.Equal(candidate.ExpectedBefore,outcome.SelectedCandidate.ExpectedBefore)||!DomainCommitSnapshot.Equal(candidate.FinalAfter,outcome.SelectedCandidate.FinalAfter))return null;values.Add(candidate.ParticipantId);}}
            else values.Add(outcome.SelectedCandidate.ParticipantId);
            return values.AsReadOnly();
        }
        private static bool IsSelectedKind(DomainConflictOutcomeKind kind){return kind==DomainConflictOutcomeKind.Uncontested||kind==DomainConflictOutcomeKind.Consensus||kind==DomainConflictOutcomeKind.SelectedByJurisdiction||kind==DomainConflictOutcomeKind.SelectedBySpecificity||kind==DomainConflictOutcomeKind.SelectedByAuthority||kind==DomainConflictOutcomeKind.SelectedByCommittedComplexity;}
        private static bool ParticipantsValid(IReadOnlyList<DomainConflictParticipant> participants){if(participants==null)return false;HashSet<string> ids=new HashSet<string>();for(int index=0;index<participants.Count;index++){DomainConflictParticipant participant=participants[index];if(participant==null||string.IsNullOrEmpty(participant.ParticipantId)||!ids.Add(participant.ParticipantId)||!PlanValid(participant.CommitPlan))return false;}return true;}
        private static bool PlanValid(DomainCommitPlan plan){DomainBoundedOrchestrationResult source=plan!=null?plan.Source:null;return plan!=null&&plan.IsCommittable&&source!=null&&source.Succeeded&&source.IsCausallyTerminal&&!source.HasUnresolvedProposalCollisions&&source.FinalUnresolvedKeys!=null&&source.FinalUnresolvedKeys.Count==0;}
        private static bool ContainsReference(IReadOnlyList<DomainCommitEntry> entries,DomainCommitEntry expected){if(entries==null)return false;for(int index=0;index<entries.Count;index++)if(object.ReferenceEquals(entries[index],expected))return true;return false;}
        private static bool EntryEqual(DomainCommitEntry left,DomainCommitEntry right){return left!=null&&right!=null&&left.CarrierId.Equals(right.CarrierId)&&left.Phenomenon==right.Phenomenon&&DomainCommitSnapshot.Equal(left.ExpectedBefore,right.ExpectedBefore)&&DomainCommitSnapshot.Equal(left.FinalAfter,right.FinalAfter);}
        private static DomainConflictContestKey Key(DomainCommitEntry entry){return new DomainConflictContestKey(entry.CarrierId,entry.Phenomenon);}
        private static ArbitratedDomainCommitPlan Rejected(CrossDomainArbitrationResult arbitration,ArbitratedDomainCommitPlanRejectionReason reason){return new ArbitratedDomainCommitPlan(arbitration,new List<CrossDomainAuthoritativeWrite>().AsReadOnly(),null,reason);}
    }

    public sealed class ArbitratedDomainRuntimeCommitResult
    {
        internal ArbitratedDomainRuntimeCommitResult(ArbitratedDomainCommitPlan plan,DomainCommitResult runtimeResult,ArbitratedRuntimeCommitRejectionReason rejection)
        { Plan=plan;RuntimeResult=runtimeResult;RejectionReason=rejection; }
        public ArbitratedDomainCommitPlan Plan { get; } public DomainCommitResult RuntimeResult { get; }
        public ArbitratedRuntimeCommitRejectionReason RejectionReason { get; } public bool Succeeded => RejectionReason==ArbitratedRuntimeCommitRejectionReason.None&&RuntimeResult!=null&&RuntimeResult.Succeeded;
    }
    public sealed class ArbitratedDomainRuntimeCommitter
    {
        private readonly DomainRuntimeCommitter committer;
        public ArbitratedDomainRuntimeCommitter(DomainRuntimeCommitter committer=null){this.committer=committer??new DomainRuntimeCommitter();}
        public ArbitratedDomainRuntimeCommitResult Commit(ArbitratedDomainCommitPlan plan,IDomainPhenomenonRuntimeAccessor accessor)
        {
            if(plan==null||!plan.IsCommittable||plan.RuntimePlan==null)return new ArbitratedDomainRuntimeCommitResult(plan,null,ArbitratedRuntimeCommitRejectionReason.ArbitratedPlanNotCommittable);
            return new ArbitratedDomainRuntimeCommitResult(plan,committer.Commit(plan.RuntimePlan,accessor),ArbitratedRuntimeCommitRejectionReason.None);
        }
    }
}
