using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Stable pure identity for a persistent phenomenon relationship group.</summary>
    public struct PhenomenonRelationshipGroupId : IEquatable<PhenomenonRelationshipGroupId>, IComparable<PhenomenonRelationshipGroupId>
    {
        private readonly string value;

        public PhenomenonRelationshipGroupId(string value)
        {
            this.value = value;
        }

        public string Value => value;
        public bool IsValid => !string.IsNullOrWhiteSpace(value);

        public bool Equals(PhenomenonRelationshipGroupId other)
        {
            return string.Equals(value, other.value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PhenomenonRelationshipGroupId && Equals((PhenomenonRelationshipGroupId)obj);
        }

        public override int GetHashCode()
        {
            return value != null ? StringComparer.Ordinal.GetHashCode(value) : 0;
        }

        public int CompareTo(PhenomenonRelationshipGroupId other)
        {
            return StringComparer.Ordinal.Compare(value, other.value);
        }

        public override string ToString()
        {
            return value ?? string.Empty;
        }
    }

    /// <summary>The single persistent relationship purpose currently established by Domain Law.</summary>
    public enum PhenomenonRelationshipKind
    {
        Synchronization = 1
    }

    /// <summary>Pure immutable persistent membership; it deliberately owns no carrier state or scene objects.</summary>
    public sealed class PhenomenonRelationshipGroup
    {
        private readonly IReadOnlyList<PhenomenonCarrierId> orderedMembers;

        internal PhenomenonRelationshipGroup(
            PhenomenonRelationshipGroupId groupId,
            LawPhenomenon phenomenon,
            IEnumerable<PhenomenonCarrierId> members)
        {
            GroupId = groupId;
            Phenomenon = phenomenon;
            List<PhenomenonCarrierId> copied = new List<PhenomenonCarrierId>(members);
            orderedMembers = copied.AsReadOnly();
        }

        public PhenomenonRelationshipGroupId GroupId { get; }
        public PhenomenonRelationshipKind RelationshipKind => PhenomenonRelationshipKind.Synchronization;
        public LawPhenomenon Phenomenon { get; }
        public IReadOnlyList<PhenomenonCarrierId> OrderedMembers => orderedMembers;
        public int MemberCount => orderedMembers.Count;

        public bool Contains(PhenomenonCarrierId carrierId)
        {
            int ignored;
            return TryGetMemberIndex(carrierId, out ignored);
        }

        public bool TryGetMemberIndex(PhenomenonCarrierId carrierId, out int index)
        {
            for (index = 0; index < orderedMembers.Count; index++)
            {
                if (orderedMembers[index].Equals(carrierId)) return true;
            }

            index = -1;
            return false;
        }
    }

    /// <summary>Traceable persistent relationship actions.</summary>
    public enum PhenomenonRelationshipGroupAction
    {
        Create = 1,
        Join = 2,
        Leave = 3,
        Dissolve = 4
    }

    /// <summary>Deterministic outcomes for relationship-group requests and transitions.</summary>
    public enum PhenomenonRelationshipGroupRejectionReason
    {
        None = 0,
        InvalidLaw = 1,
        ExpressionUnavailable = 2,
        InvalidExpressionContext = 3,
        ExpressionMismatch = 4,
        FrenzyInactive = 5,
        TerritoryBehaviorNotImplemented = 6,
        InvalidGroupId = 7,
        InvalidPhenomenon = 8,
        InvalidCarrierId = 9,
        PhenomenonMismatch = 10,
        InsufficientMembers = 11,
        MemberNotFound = 12,
        AlreadyMember = 13,
        GroupAlreadyDissolved = 14
    }

    /// <summary>Per-input membership outcome during group creation.</summary>
    public enum PhenomenonRelationshipGroupInitialMemberReason
    {
        Added = 0,
        InvalidCarrierId = 1,
        DuplicateMember = 2
    }

    /// <summary>Immutable record of one supplied initial member ID.</summary>
    public sealed class PhenomenonRelationshipGroupInitialMemberResult
    {
        internal PhenomenonRelationshipGroupInitialMemberResult(
            PhenomenonCarrierId carrierId,
            PhenomenonRelationshipGroupInitialMemberReason reason)
        {
            CarrierId = carrierId;
            Reason = reason;
        }

        public PhenomenonCarrierId CarrierId { get; }
        public PhenomenonRelationshipGroupInitialMemberReason Reason { get; }
        public bool Added => Reason == PhenomenonRelationshipGroupInitialMemberReason.Added;
    }

    /// <summary>Law-aware immutable request to establish a Synchronization relationship group.</summary>
    public sealed class SynchronizationRelationshipGroupCreationRequest
    {
        public SynchronizationRelationshipGroupCreationRequest(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonRelationshipGroupId groupId,
            IEnumerable<PhenomenonCarrierId> initialMembers)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            GroupId = groupId;
            InitialMembers = initialMembers != null
                ? new List<PhenomenonCarrierId>(initialMembers).AsReadOnly()
                : new List<PhenomenonCarrierId>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public PhenomenonRelationshipGroupId GroupId { get; }
        public IReadOnlyList<PhenomenonCarrierId> InitialMembers { get; }
    }

    /// <summary>Immutable result for create, join, leave, or explicit dissolution.</summary>
    public sealed class PhenomenonRelationshipGroupResult
    {
        internal PhenomenonRelationshipGroupResult(
            DomainLaw candidateLaw,
            PhenomenonRelationshipGroupAction action,
            PhenomenonCarrierId requestedCarrierId,
            PhenomenonRelationshipGroup groupBefore,
            PhenomenonRelationshipGroup groupAfter,
            bool succeeded,
            PhenomenonRelationshipGroupRejectionReason rejectionReason,
            bool membershipChanged,
            bool dissolved,
            IReadOnlyList<PhenomenonRelationshipGroupInitialMemberResult> initialMemberResults)
        {
            CandidateLaw = candidateLaw;
            Action = action;
            RequestedCarrierId = requestedCarrierId;
            GroupBefore = groupBefore;
            GroupAfter = groupAfter;
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            MembershipChanged = membershipChanged;
            Dissolved = dissolved;
            InitialMemberResults = initialMemberResults ?? new List<PhenomenonRelationshipGroupInitialMemberResult>().AsReadOnly();
        }

        public DomainLaw CandidateLaw { get; }
        public PhenomenonRelationshipGroupAction Action { get; }
        public PhenomenonCarrierId RequestedCarrierId { get; }
        public PhenomenonRelationshipGroup GroupBefore { get; }
        public PhenomenonRelationshipGroup GroupAfter { get; }
        public PhenomenonRelationshipGroupId GroupId => GroupAfter != null
            ? GroupAfter.GroupId
            : GroupBefore != null ? GroupBefore.GroupId : default(PhenomenonRelationshipGroupId);
        public LawPhenomenon Phenomenon => GroupAfter != null
            ? GroupAfter.Phenomenon
            : GroupBefore != null
                ? GroupBefore.Phenomenon
                : CandidateLaw != null ? CandidateLaw.Phenomenon : default(LawPhenomenon);
        public bool Succeeded { get; }
        public PhenomenonRelationshipGroupRejectionReason RejectionReason { get; }
        public int PreviousMemberCount => GroupBefore != null ? GroupBefore.MemberCount : 0;
        public int ResultingMemberCount => GroupAfter != null ? GroupAfter.MemberCount : 0;
        public bool MembershipChanged { get; }
        public bool Dissolved { get; }
        public IReadOnlyList<PhenomenonRelationshipGroupInitialMemberResult> InitialMemberResults { get; }
    }

    /// <summary>
    /// Pure creation and membership transitions for persistent Synchronization
    /// relationships. It establishes no synchronized scalar, delta, rate, or
    /// acceleration behavior.
    /// </summary>
    public static class SynchronizationRelationshipGroupEvaluator
    {
        public static PhenomenonRelationshipGroupResult Create(
            SynchronizationRelationshipGroupCreationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonRelationshipGroupId groupId = request != null
                ? request.GroupId
                : default(PhenomenonRelationshipGroupId);
            IReadOnlyList<PhenomenonCarrierId> initialMembers = request != null
                ? request.InitialMembers
                : null;

            PhenomenonRelationshipGroupRejectionReason rejection;
            if (!TryValidateLawAndExpression(law, context, out rejection))
            {
                return Rejected(law, PhenomenonRelationshipGroupAction.Create, default(PhenomenonCarrierId),
                    null, rejection, EmptyInitialResults());
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Synchronization)
            {
                return Rejected(law, PhenomenonRelationshipGroupAction.Create, default(PhenomenonCarrierId),
                    null, PhenomenonRelationshipGroupRejectionReason.TerritoryBehaviorNotImplemented, EmptyInitialResults());
            }

            if (!groupId.IsValid)
            {
                return Rejected(law, PhenomenonRelationshipGroupAction.Create, default(PhenomenonCarrierId),
                    null, PhenomenonRelationshipGroupRejectionReason.InvalidGroupId, EmptyInitialResults());
            }

            List<PhenomenonCarrierId> members = new List<PhenomenonCarrierId>();
            List<PhenomenonRelationshipGroupInitialMemberResult> memberResults =
                new List<PhenomenonRelationshipGroupInitialMemberResult>();
            HashSet<PhenomenonCarrierId> encountered = new HashSet<PhenomenonCarrierId>();
            if (initialMembers != null)
            {
                for (int index = 0; index < initialMembers.Count; index++)
                {
                    PhenomenonCarrierId carrierId = initialMembers[index];
                    if (!carrierId.IsValid)
                    {
                        memberResults.Add(new PhenomenonRelationshipGroupInitialMemberResult(
                            carrierId, PhenomenonRelationshipGroupInitialMemberReason.InvalidCarrierId));
                        continue;
                    }

                    if (!encountered.Add(carrierId))
                    {
                        memberResults.Add(new PhenomenonRelationshipGroupInitialMemberResult(
                            carrierId, PhenomenonRelationshipGroupInitialMemberReason.DuplicateMember));
                        continue;
                    }

                    members.Add(carrierId);
                    memberResults.Add(new PhenomenonRelationshipGroupInitialMemberResult(
                        carrierId, PhenomenonRelationshipGroupInitialMemberReason.Added));
                }
            }

            if (members.Count < 2)
            {
                return Rejected(law, PhenomenonRelationshipGroupAction.Create, default(PhenomenonCarrierId),
                    null, PhenomenonRelationshipGroupRejectionReason.InsufficientMembers, memberResults.AsReadOnly());
            }

            PhenomenonRelationshipGroup group = new PhenomenonRelationshipGroup(groupId, law.Phenomenon, members);
            return new PhenomenonRelationshipGroupResult(
                law, PhenomenonRelationshipGroupAction.Create, default(PhenomenonCarrierId), null, group,
                true, PhenomenonRelationshipGroupRejectionReason.None, true, false, memberResults.AsReadOnly());
        }

        public static PhenomenonRelationshipGroupResult Join(
            PhenomenonRelationshipGroup group,
            PhenomenonCarrierId carrierId,
            LawPhenomenon carrierPhenomenon)
        {
            if (group == null)
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Join, carrierId, null,
                    PhenomenonRelationshipGroupRejectionReason.GroupAlreadyDissolved, EmptyInitialResults());
            }

            if (!carrierId.IsValid)
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Join, carrierId, group,
                    PhenomenonRelationshipGroupRejectionReason.InvalidCarrierId, EmptyInitialResults());
            }

            PhenomenonSemanticProfile profile;
            if (!PhenomenonSemanticClassifier.TryGetProfile(carrierPhenomenon, out profile))
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Join, carrierId, group,
                    PhenomenonRelationshipGroupRejectionReason.InvalidPhenomenon, EmptyInitialResults());
            }

            if (carrierPhenomenon != group.Phenomenon)
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Join, carrierId, group,
                    PhenomenonRelationshipGroupRejectionReason.PhenomenonMismatch, EmptyInitialResults());
            }

            if (group.Contains(carrierId))
            {
                return new PhenomenonRelationshipGroupResult(
                    null, PhenomenonRelationshipGroupAction.Join, carrierId, group, group,
                    true, PhenomenonRelationshipGroupRejectionReason.AlreadyMember, false, false, EmptyInitialResults());
            }

            List<PhenomenonCarrierId> members = CopyMembers(group);
            members.Add(carrierId);
            PhenomenonRelationshipGroup updated = new PhenomenonRelationshipGroup(
                group.GroupId, group.Phenomenon, members);
            return new PhenomenonRelationshipGroupResult(
                null, PhenomenonRelationshipGroupAction.Join, carrierId, group, updated,
                true, PhenomenonRelationshipGroupRejectionReason.None, true, false, EmptyInitialResults());
        }

        public static PhenomenonRelationshipGroupResult Leave(
            PhenomenonRelationshipGroup group,
            PhenomenonCarrierId carrierId)
        {
            if (group == null)
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Leave, carrierId, null,
                    PhenomenonRelationshipGroupRejectionReason.GroupAlreadyDissolved, EmptyInitialResults());
            }

            if (!carrierId.IsValid)
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Leave, carrierId, group,
                    PhenomenonRelationshipGroupRejectionReason.InvalidCarrierId, EmptyInitialResults());
            }

            int memberIndex;
            if (!group.TryGetMemberIndex(carrierId, out memberIndex))
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Leave, carrierId, group,
                    PhenomenonRelationshipGroupRejectionReason.MemberNotFound, EmptyInitialResults());
            }

            List<PhenomenonCarrierId> members = CopyMembers(group);
            members.RemoveAt(memberIndex);
            if (members.Count < 2)
            {
                return new PhenomenonRelationshipGroupResult(
                    null, PhenomenonRelationshipGroupAction.Leave, carrierId, group, null,
                    true, PhenomenonRelationshipGroupRejectionReason.None, true, true, EmptyInitialResults());
            }

            PhenomenonRelationshipGroup updated = new PhenomenonRelationshipGroup(
                group.GroupId, group.Phenomenon, members);
            return new PhenomenonRelationshipGroupResult(
                null, PhenomenonRelationshipGroupAction.Leave, carrierId, group, updated,
                true, PhenomenonRelationshipGroupRejectionReason.None, true, false, EmptyInitialResults());
        }

        public static PhenomenonRelationshipGroupResult Dissolve(PhenomenonRelationshipGroup group)
        {
            if (group == null)
            {
                return Rejected(null, PhenomenonRelationshipGroupAction.Dissolve, default(PhenomenonCarrierId), null,
                    PhenomenonRelationshipGroupRejectionReason.GroupAlreadyDissolved, EmptyInitialResults());
            }

            return new PhenomenonRelationshipGroupResult(
                null, PhenomenonRelationshipGroupAction.Dissolve, default(PhenomenonCarrierId), group, null,
                true, PhenomenonRelationshipGroupRejectionReason.None, false, true, EmptyInitialResults());
        }

        private static bool TryValidateLawAndExpression(
            DomainLaw law,
            LawExpressionContext context,
            out PhenomenonRelationshipGroupRejectionReason rejection)
        {
            LawEvaluationRejectionReason lawRejection;
            if (DomainLawEvaluator.TryEvaluateEligibility(law, context, out lawRejection))
            {
                rejection = PhenomenonRelationshipGroupRejectionReason.None;
                return true;
            }

            rejection = lawRejection == LawEvaluationRejectionReason.InvalidLaw
                ? PhenomenonRelationshipGroupRejectionReason.InvalidLaw
                : lawRejection == LawEvaluationRejectionReason.ExpressionUnavailable
                    ? PhenomenonRelationshipGroupRejectionReason.ExpressionUnavailable
                    : lawRejection == LawEvaluationRejectionReason.InvalidExpressionContext
                        ? PhenomenonRelationshipGroupRejectionReason.InvalidExpressionContext
                        : lawRejection == LawEvaluationRejectionReason.ExpressionMismatch
                            ? PhenomenonRelationshipGroupRejectionReason.ExpressionMismatch
                            : lawRejection == LawEvaluationRejectionReason.FrenzyInactive
                                ? PhenomenonRelationshipGroupRejectionReason.FrenzyInactive
                                : PhenomenonRelationshipGroupRejectionReason.InvalidLaw;
            return false;
        }

        private static List<PhenomenonCarrierId> CopyMembers(PhenomenonRelationshipGroup group)
        {
            return new List<PhenomenonCarrierId>(group.OrderedMembers);
        }

        private static IReadOnlyList<PhenomenonRelationshipGroupInitialMemberResult> EmptyInitialResults()
        {
            return new List<PhenomenonRelationshipGroupInitialMemberResult>().AsReadOnly();
        }

        private static PhenomenonRelationshipGroupResult Rejected(
            DomainLaw law,
            PhenomenonRelationshipGroupAction action,
            PhenomenonCarrierId carrierId,
            PhenomenonRelationshipGroup groupBefore,
            PhenomenonRelationshipGroupRejectionReason rejection,
            IReadOnlyList<PhenomenonRelationshipGroupInitialMemberResult> initialMemberResults)
        {
            return new PhenomenonRelationshipGroupResult(
                law, action, carrierId, groupBefore, groupBefore, false, rejection,
                false, false, initialMemberResults);
        }
    }
}
