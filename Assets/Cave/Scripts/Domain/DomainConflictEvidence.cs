using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>One state key which can be governed independently of every other Domain key.</summary>
    public struct DomainConflictContestKey : IEquatable<DomainConflictContestKey>, IComparable<DomainConflictContestKey>
    {
        public DomainConflictContestKey(PhenomenonCarrierId carrierId, LawPhenomenon phenomenon)
        { CarrierId = carrierId; Phenomenon = phenomenon; }
        public PhenomenonCarrierId CarrierId { get; }
        public LawPhenomenon Phenomenon { get; }
        public bool Equals(DomainConflictContestKey other) { return CarrierId.Equals(other.CarrierId) && Phenomenon == other.Phenomenon; }
        public override bool Equals(object obj) { return obj is DomainConflictContestKey && Equals((DomainConflictContestKey)obj); }
        public override int GetHashCode() { return CarrierId.GetHashCode() ^ (int)Phenomenon; }
        public int CompareTo(DomainConflictContestKey other) { int carrier = CarrierId.CompareTo(other.CarrierId); return carrier != 0 ? carrier : ((int)Phenomenon).CompareTo((int)other.Phenomenon); }
        public override string ToString() { return CarrierId + " / " + Phenomenon; }
    }

    public sealed class DomainJurisdictionEvidence
    {
        public DomainJurisdictionEvidence(string participantId, DomainConflictContestKey contestKey, bool hasJurisdiction, string provenance)
        { ParticipantId = participantId; ContestKey = contestKey; HasJurisdiction = hasJurisdiction; Provenance = provenance; }
        public string ParticipantId { get; }
        public DomainConflictContestKey ContestKey { get; }
        public bool HasJurisdiction { get; }
        public string Provenance { get; }
        public bool IsValid => !string.IsNullOrEmpty(ParticipantId) && !string.IsNullOrEmpty(Provenance);
    }

    public sealed class DomainSpecificityEvidence
    {
        public DomainSpecificityEvidence(string participantId, DomainConflictContestKey contestKey, int rank, string provenance)
        { ParticipantId = participantId; ContestKey = contestKey; Rank = rank; Provenance = provenance; }
        public string ParticipantId { get; }
        public DomainConflictContestKey ContestKey { get; }
        public int Rank { get; }
        public string Provenance { get; }
        public bool IsValid => !string.IsNullOrEmpty(ParticipantId) && Rank >= 0 && !string.IsNullOrEmpty(Provenance);
    }

    public sealed class DomainConflictAuthorityEvidence
    {
        public DomainConflictAuthorityEvidence(string participantId, DomainConflictContestKey contestKey, int rank, string provenance)
        { ParticipantId = participantId; ContestKey = contestKey; Rank = rank; Provenance = provenance; }
        public string ParticipantId { get; }
        public DomainConflictContestKey ContestKey { get; }
        public int Rank { get; }
        public string Provenance { get; }
        public bool IsValid => !string.IsNullOrEmpty(ParticipantId) && Rank >= 0 && !string.IsNullOrEmpty(Provenance);
    }

    /// <summary>Explicit upstream evidence. Absence is never converted into a default rank.</summary>
    public sealed class DomainConflictEvidenceSet
    {
        private readonly IReadOnlyList<DomainJurisdictionEvidence> jurisdiction;
        private readonly IReadOnlyList<DomainSpecificityEvidence> specificity;
        private readonly IReadOnlyList<DomainConflictAuthorityEvidence> authority;
        public DomainConflictEvidenceSet(IEnumerable<DomainJurisdictionEvidence> jurisdiction, IEnumerable<DomainSpecificityEvidence> specificity, IEnumerable<DomainConflictAuthorityEvidence> authority)
        {
            this.jurisdiction = Copy(jurisdiction); this.specificity = Copy(specificity); this.authority = Copy(authority);
        }
        public static DomainConflictEvidenceSet Empty { get; } = new DomainConflictEvidenceSet(null, null, null);
        public IReadOnlyList<DomainJurisdictionEvidence> Jurisdiction => jurisdiction;
        public IReadOnlyList<DomainSpecificityEvidence> Specificity => specificity;
        public IReadOnlyList<DomainConflictAuthorityEvidence> Authority => authority;
        public DomainJurisdictionEvidence FindJurisdiction(DomainConflictContestKey key) { for (int index=0;index<jurisdiction.Count;index++) if(jurisdiction[index].ContestKey.Equals(key)) return jurisdiction[index]; return null; }
        public DomainSpecificityEvidence FindSpecificity(DomainConflictContestKey key) { for (int index=0;index<specificity.Count;index++) if(specificity[index].ContestKey.Equals(key)) return specificity[index]; return null; }
        public DomainConflictAuthorityEvidence FindAuthority(DomainConflictContestKey key) { for (int index=0;index<authority.Count;index++) if(authority[index].ContestKey.Equals(key)) return authority[index]; return null; }
        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) { return new List<T>(values ?? new T[0]).AsReadOnly(); }
    }

    /// <summary>Immutable participant; its committed complexity is always calculated by the existing Domain calculator.</summary>
    public sealed class DomainConflictParticipant
    {
        public DomainConflictParticipant(string participantId, DomainCommitPlan commitPlan, DomainComposition composition, DomainComplexityPolicy complexityPolicy, DomainConflictEvidenceSet evidence, string provenance)
        {
            ParticipantId = participantId; CommitPlan = commitPlan; Composition = composition;
            ComplexityReport = composition != null ? DomainComplexityCalculator.Calculate(composition, complexityPolicy) : null;
            Evidence = evidence ?? DomainConflictEvidenceSet.Empty; Provenance = provenance;
        }
        public string ParticipantId { get; }
        public DomainCommitPlan CommitPlan { get; }
        public DomainComposition Composition { get; }
        public DomainComplexityReport ComplexityReport { get; }
        public DomainConflictEvidenceSet Evidence { get; }
        public string Provenance { get; }
        public float CommittedComplexity => ComplexityReport != null ? ComplexityReport.TotalComplexity : float.NaN;
    }
}
