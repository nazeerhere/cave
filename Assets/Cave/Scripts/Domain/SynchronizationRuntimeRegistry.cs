using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>
    /// Live lookup for the existing immutable Synchronization relationship
    /// groups.  It owns neither relationship semantics nor carrier state: a
    /// caller applies an already-authoritative immutable lifecycle result.
    /// </summary>
    public static class SynchronizationRuntimeRegistry
    {
        private static readonly Dictionary<PhenomenonRelationshipGroupId, PhenomenonRelationshipGroup> groups =
            new Dictionary<PhenomenonRelationshipGroupId, PhenomenonRelationshipGroup>();
        private static readonly Dictionary<PhenomenonCarrierId, PhenomenonRelationshipGroupId> membership =
            new Dictionary<PhenomenonCarrierId, PhenomenonRelationshipGroupId>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntime()
        {
            groups.Clear();
            membership.Clear();
        }

        /// <summary>Applies Create/Join/Leave/Dissolve output from the pure
        /// relationship evaluator. No proximity or ownership inference occurs.</summary>
        public static bool Apply(PhenomenonRelationshipGroupResult result)
        {
            if (result == null || !result.Succeeded) return false;
            if (result.Dissolved || result.GroupAfter == null)
            {
                Remove(result.GroupId);
                return true;
            }

            return Register(result.GroupAfter);
        }

        public static bool Register(PhenomenonRelationshipGroup group)
        {
            if (group == null || !group.GroupId.IsValid || group.MemberCount < 2) return false;
            // Membership is authoritative only while each member is a live
            // registered Domain carrier. Never retain dead object identities.
            List<PhenomenonCarrierId> live = LiveMembers(group);
            if (live.Count < 2) { Remove(group.GroupId); return false; }

            for (int index = 0; index < live.Count; index++)
            {
                PhenomenonRelationshipGroupId existing;
                if (membership.TryGetValue(live[index], out existing) && !existing.Equals(group.GroupId))
                    return false; // ambiguous live membership is fail-closed.
            }

            Remove(group.GroupId);
            groups[group.GroupId] = group;
            for (int index = 0; index < live.Count; index++) membership[live[index]] = group.GroupId;
            return true;
        }

        public static void Remove(PhenomenonRelationshipGroupId id)
        {
            PhenomenonRelationshipGroup previous;
            if (!groups.TryGetValue(id, out previous)) return;
            groups.Remove(id);
            for (int index = 0; index < previous.OrderedMembers.Count; index++)
            {
                PhenomenonRelationshipGroupId current;
                if (membership.TryGetValue(previous.OrderedMembers[index], out current) && current.Equals(id))
                    membership.Remove(previous.OrderedMembers[index]);
            }
        }

        public static bool TryGetGroup(PhenomenonCarrierId member, LawPhenomenon phenomenon,
            out PhenomenonRelationshipGroup group)
        {
            Prune();
            PhenomenonRelationshipGroupId id;
            if (!membership.TryGetValue(member, out id) || !groups.TryGetValue(id, out group)
                || group.Phenomenon != phenomenon || !group.Contains(member))
            { group = null; return false; }
            return true;
        }

        public static bool TryGetLiveMembers(PhenomenonCarrierId member, LawPhenomenon phenomenon,
            float timestamp, out PhenomenonRelationshipGroup group,
            out IReadOnlyList<PhenomenonCarrierSnapshot> snapshots)
        {
            snapshots = new List<PhenomenonCarrierSnapshot>().AsReadOnly();
            if (!TryGetGroup(member, phenomenon, out group)) return false;
            List<PhenomenonCarrierSnapshot> live = new List<PhenomenonCarrierSnapshot>();
            for (int index = 0; index < group.OrderedMembers.Count; index++)
            {
                IDomainPhenomenonRuntimeTarget target;
                PhenomenonSemanticSnapshot snapshot;
                PhenomenonCarrierId carrier = group.OrderedMembers[index];
                if (!DomainRuntimeCarrierRegistry.TryResolve(carrier, phenomenon, timestamp, out target)
                    || !target.TryRead(out snapshot) || snapshot == null) continue;
                live.Add(new PhenomenonCarrierSnapshot(carrier, phenomenon, snapshot));
            }
            if (live.Count < 2 || !Contains(live, member)) { Remove(group.GroupId); group = null; return false; }
            live.Sort((left, right) => left.CarrierId.CompareTo(right.CarrierId));
            snapshots = live.AsReadOnly();
            return true;
        }

        private static List<PhenomenonCarrierId> LiveMembers(PhenomenonRelationshipGroup group)
        {
            List<PhenomenonCarrierId> result = new List<PhenomenonCarrierId>();
            for (int index = 0; index < group.OrderedMembers.Count; index++)
            {
                DomainRuntimeCarrierIdentity identity;
                if (DomainRuntimeCarrierRegistry.TryGet(group.OrderedMembers[index], out identity)) result.Add(group.OrderedMembers[index]);
            }
            result.Sort((left, right) => left.CompareTo(right));
            return result;
        }

        private static void Prune()
        {
            List<PhenomenonRelationshipGroupId> dead = new List<PhenomenonRelationshipGroupId>();
            foreach (KeyValuePair<PhenomenonRelationshipGroupId, PhenomenonRelationshipGroup> pair in groups)
            {
                List<PhenomenonCarrierId> live = LiveMembers(pair.Value);
                if (live.Count < 2) { dead.Add(pair.Key); continue; }
                // The immutable group is retained, while the live index is
                // rebuilt so destroyed members cannot remain resolvable.
                for (int index = 0; index < pair.Value.OrderedMembers.Count; index++)
                {
                    PhenomenonRelationshipGroupId current;
                    if (membership.TryGetValue(pair.Value.OrderedMembers[index], out current) && current.Equals(pair.Key))
                        membership.Remove(pair.Value.OrderedMembers[index]);
                }
                for (int index = 0; index < live.Count; index++) membership[live[index]] = pair.Key;
            }
            for (int index = 0; index < dead.Count; index++) Remove(dead[index]);
        }

        private static bool Contains(IReadOnlyList<PhenomenonCarrierSnapshot> values, PhenomenonCarrierId id)
        { for (int index = 0; index < values.Count; index++) if (values[index].CarrierId.Equals(id)) return true; return false; }
    }
}
