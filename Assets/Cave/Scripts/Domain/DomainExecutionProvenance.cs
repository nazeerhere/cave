using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>
    /// Immutable lineage carried with a Domain participant. Root participants
    /// intentionally have no child carrier; child identity is never invented.
    /// </summary>
    public sealed class DomainExecutionProvenance
    {
        public DomainExecutionProvenance(string ownerAuthorityId, string parentDomainAuthorityId,
            string childCarrierId, PhenomenonCarrierId sourceCarrierId, string lawId,
            LawExpression expression, LawPhenomenon phenomenon, LawTerritoryPrinciple territory,
            string executionGenerationId, PhenomenonOperationKind effectiveOperation)
        {
            OwnerAuthorityId = ownerAuthorityId;
            ParentDomainAuthorityId = parentDomainAuthorityId;
            ChildCarrierId = childCarrierId;
            SourceCarrierId = sourceCarrierId;
            LawId = lawId;
            Expression = expression;
            Phenomenon = phenomenon;
            Territory = territory;
            ExecutionGenerationId = executionGenerationId;
            EffectiveOperation = effectiveOperation;
        }

        public string OwnerAuthorityId { get; }
        public string ParentDomainAuthorityId { get; }
        public string ChildCarrierId { get; }
        public PhenomenonCarrierId SourceCarrierId { get; }
        public string LawId { get; }
        public LawExpression Expression { get; }
        public LawPhenomenon Phenomenon { get; }
        public LawTerritoryPrinciple Territory { get; }
        public string ExecutionGenerationId { get; }
        public PhenomenonOperationKind EffectiveOperation { get; }
        public bool IsRootParticipant => string.IsNullOrEmpty(ChildCarrierId);
        public bool HasParent => !string.IsNullOrEmpty(ParentDomainAuthorityId);
        public bool IsValid => !string.IsNullOrEmpty(OwnerAuthorityId)
            && HasParent && !string.IsNullOrEmpty(LawId)
            && !string.IsNullOrEmpty(ExecutionGenerationId)
            && SourceCarrierId.IsValid;
    }

    /// <summary>Deterministic same-parent equivalence filtering performed before the existing cross-parent arbitrator.</summary>
    internal static class DomainSameParentSiblingDedupe
    {
        internal static void Filter(List<DomainConflictCandidate> candidates)
        {
            if (candidates == null || candidates.Count < 2) return;
            candidates.Sort(Compare);
            List<DomainConflictCandidate> kept = new List<DomainConflictCandidate>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++)
            {
                DomainConflictCandidate candidate = candidates[index];
                bool duplicate = false;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                    if (Equivalent(kept[keptIndex], candidate)) { duplicate = true; break; }
                if (!duplicate) kept.Add(candidate);
            }
            candidates.Clear();
            candidates.AddRange(kept);
        }

        private static bool Equivalent(DomainConflictCandidate left, DomainConflictCandidate right)
        {
            DomainExecutionProvenance a = left.Participant.ExecutionProvenance;
            DomainExecutionProvenance b = right.Participant.ExecutionProvenance;
            return a != null && b != null && a.IsValid && b.IsValid
                && string.Equals(a.ParentDomainAuthorityId, b.ParentDomainAuthorityId, StringComparison.Ordinal)
                && string.Equals(a.ExecutionGenerationId, b.ExecutionGenerationId, StringComparison.Ordinal)
                && a.Phenomenon == b.Phenomenon && a.EffectiveOperation == b.EffectiveOperation
                && a.Expression == b.Expression && a.Territory == b.Territory
                && string.Equals(a.LawId, b.LawId, StringComparison.Ordinal)
                && left.Entry.CarrierId.Equals(right.Entry.CarrierId)
                && DomainCommitSnapshot.Equal(left.FinalAfter, right.FinalAfter);
        }

        private static int Compare(DomainConflictCandidate left, DomainConflictCandidate right)
        {
            DomainExecutionProvenance a = left.Participant.ExecutionProvenance;
            DomainExecutionProvenance b = right.Participant.ExecutionProvenance;
            int parent = string.CompareOrdinal(a != null ? a.ParentDomainAuthorityId : null, b != null ? b.ParentDomainAuthorityId : null);
            if (parent != 0) return parent;
            int target = left.Entry.CarrierId.CompareTo(right.Entry.CarrierId);
            if (target != 0) return target;
            int law = string.CompareOrdinal(a != null ? a.LawId : null, b != null ? b.LawId : null);
            if (law != 0) return law;
            int child = string.CompareOrdinal(a != null ? a.ChildCarrierId : null, b != null ? b.ChildCarrierId : null);
            return child != 0 ? child : string.CompareOrdinal(left.ParticipantId, right.ParticipantId);
        }
    }
}
