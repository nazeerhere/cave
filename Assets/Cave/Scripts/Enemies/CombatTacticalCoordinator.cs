using System;
using System.Collections.Generic;

namespace Cave.Enemies
{
    /// <summary>
    /// High-level coordination context. An intent is never an ability command;
    /// a brain remains responsible for deciding whether any legal action fits it.
    /// </summary>
    public enum CombatTacticalIntent
    {
        None = 0,
        Pressure = 1,
        Flank = 2,
        Hold = 3,
        Protect = 4,
        Support = 5,
        Disrupt = 6,
        Reposition = 7,
        Retreat = 8
    }

    public enum CombatTacticalRole
    {
        None = 0,
        Frontline = 1,
        Skirmisher = 2,
        Support = 3,
        Controller = 4,
        Ranged = 5
    }

    /// <summary>
    /// Deliberately narrow input seam. Sprint 1 only uses explicit formation
    /// membership; later knowledge snapshots can populate this boundary without
    /// making the coordinator depend on arbitrary scene access.
    /// </summary>
    public readonly struct CombatTacticalMemberDescriptor
    {
        public CombatTacticalMemberDescriptor(string memberId, CombatTacticalRole role)
            : this(memberId, role, default)
        {
        }

        public CombatTacticalMemberDescriptor(
            string memberId,
            CombatTacticalRole role,
            KnowledgeSnapshot knowledge)
            : this(memberId, role, knowledge, default)
        {
        }

        public CombatTacticalMemberDescriptor(
            string memberId,
            CombatTacticalRole role,
            KnowledgeSnapshot knowledge,
            MobPsychologySnapshot psychology)
        {
            MemberId = memberId;
            Role = role;
            Knowledge = knowledge;
            Psychology = psychology;
        }

        public string MemberId { get; }
        public CombatTacticalRole Role { get; }
        /// <summary>Legitimate per-member input; never a world-state query.</summary>
        public KnowledgeSnapshot Knowledge { get; }
        /// <summary>Persistent per-mob interpretation state, not a CTC-owned value.</summary>
        public MobPsychologySnapshot Psychology { get; }
    }

    public readonly struct CombatTacticalAssignment
    {
        public CombatTacticalAssignment(string memberId, CombatTacticalIntent intent)
        {
            MemberId = memberId;
            Intent = intent;
        }

        public string MemberId { get; }
        public CombatTacticalIntent Intent { get; }
    }

    /// <summary>
    /// Pure deterministic policy for one explicit formation. It knows no Unity
    /// objects, abilities, world truth, or player position.
    /// </summary>
    public static class CombatTacticalCoordinator
    {
        public static IReadOnlyList<CombatTacticalAssignment> Evaluate(
            IList<CombatTacticalMemberDescriptor> members)
        {
            List<CombatTacticalMemberDescriptor> ordered =
                new List<CombatTacticalMemberDescriptor>();
            if (members != null)
            {
                for (int index = 0; index < members.Count; index++)
                {
                    CombatTacticalMemberDescriptor member = members[index];
                    if (!string.IsNullOrEmpty(member.MemberId))
                    {
                        ordered.Add(member);
                    }
                }
            }

            ordered.Sort(CompareMembers);
            bool hasFrontline = ContainsRole(ordered, CombatTacticalRole.Frontline);
            List<CombatTacticalAssignment> assignments =
                new List<CombatTacticalAssignment>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                CombatTacticalMemberDescriptor member = ordered[index];
                assignments.Add(new CombatTacticalAssignment(
                    member.MemberId,
                    ResolveIntent(member.Role, hasFrontline, member.Psychology)));
            }

            return assignments.AsReadOnly();
        }

        private static int CompareMembers(
            CombatTacticalMemberDescriptor left,
            CombatTacticalMemberDescriptor right)
        {
            return string.CompareOrdinal(left.MemberId, right.MemberId);
        }

        private static bool ContainsRole(
            IList<CombatTacticalMemberDescriptor> members,
            CombatTacticalRole role)
        {
            for (int index = 0; index < members.Count; index++)
            {
                if (members[index].Role == role)
                {
                    return true;
                }
            }

            return false;
        }

        private static CombatTacticalIntent ResolveIntent(
            CombatTacticalRole role,
            bool hasFrontline,
            MobPsychologySnapshot psychology)
        {
            CombatTacticalIntent baseline;
            switch (role)
            {
                case CombatTacticalRole.Frontline:
                    baseline = CombatTacticalIntent.Pressure; break;
                case CombatTacticalRole.Skirmisher:
                    baseline = hasFrontline
                        ? CombatTacticalIntent.Flank
                        : CombatTacticalIntent.Pressure; break;
                case CombatTacticalRole.Support:
                    baseline = CombatTacticalIntent.Support; break;
                case CombatTacticalRole.Controller:
                    baseline = CombatTacticalIntent.Protect; break;
                case CombatTacticalRole.Ranged:
                    baseline = CombatTacticalIntent.Hold; break;
                default:
                    return CombatTacticalIntent.None;
            }

            // Psychology is influence, never an ability instruction or a global
            // override. A default/missing state preserves the authored role plan.
            if (psychology.MobId == null || psychology.Aggression == 0f && psychology.Confidence == 0f
                && psychology.Fear == 0f && psychology.Cooperation == 0f)
            {
                return baseline;
            }

            float pressureReadiness = psychology.Aggression * 0.45f
                + psychology.Confidence * 0.30f - psychology.Fear * 0.45f;
            if (psychology.Fear - psychology.Confidence > 0.45f && baseline != CombatTacticalIntent.Support)
            {
                return CombatTacticalIntent.Reposition;
            }
            if (psychology.Cooperation - psychology.Aggression > 0.38f
                && (role == CombatTacticalRole.Frontline || role == CombatTacticalRole.Controller))
            {
                return CombatTacticalIntent.Protect;
            }
            if (pressureReadiness > 0.38f && baseline == CombatTacticalIntent.Hold)
            {
                return CombatTacticalIntent.Pressure;
            }
            return baseline;
        }
    }

    /// <summary>
    /// Owns membership and assignments for one formation without any Unity or
    /// scene-search dependency. The MonoBehaviour wrapper supplies cadence and
    /// lifecycle; this state object keeps those concerns testable and fail-soft.
    /// </summary>
    public sealed class CombatTacticalFormationState
    {
        private readonly string formationId;
        private readonly Dictionary<string, CombatTacticalRole> members =
            new Dictionary<string, CombatTacticalRole>(StringComparer.Ordinal);
        private readonly Dictionary<string, CombatTacticalIntent> assignments =
            new Dictionary<string, CombatTacticalIntent>(StringComparer.Ordinal);
        private readonly Dictionary<string, KnowledgeSnapshot> knowledgeByMember =
            new Dictionary<string, KnowledgeSnapshot>(StringComparer.Ordinal);
        private readonly Dictionary<string, MobPsychologySnapshot> psychologyByMember =
            new Dictionary<string, MobPsychologySnapshot>(StringComparer.Ordinal);
        private bool enabled;

        public CombatTacticalFormationState(string formationId)
        {
            this.formationId = formationId;
        }

        public string FormationId => formationId;
        public bool IsValid => !string.IsNullOrEmpty(formationId);
        public bool IsEnabled => enabled && IsValid;
        public int MemberCount => members.Count;
        public int EvaluationCount { get; private set; }
        public int LastKnowledgeFactCount { get; private set; }

        public bool Register(string memberId, CombatTacticalRole role)
        {
            if (!IsValid || string.IsNullOrEmpty(memberId) || role == CombatTacticalRole.None)
            {
                return false;
            }

            members[memberId] = role;
            return true;
        }

        public bool Unregister(string memberId)
        {
            if (string.IsNullOrEmpty(memberId))
            {
                return false;
            }

            assignments.Remove(memberId);
            knowledgeByMember.Remove(memberId);
            psychologyByMember.Remove(memberId);
            return members.Remove(memberId);
        }

        /// <summary>
        /// Snapshot ownership remains with the member. CTC receives only this
        /// bounded value during its next deterministic evaluation.
        /// </summary>
        public void SetKnowledgeSnapshot(string memberId, KnowledgeSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(memberId) && members.ContainsKey(memberId))
            {
                knowledgeByMember[memberId] = snapshot;
            }
        }

        public void SetPsychologySnapshot(string memberId, MobPsychologySnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(memberId) && members.ContainsKey(memberId)) psychologyByMember[memberId] = snapshot;
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!IsEnabled)
            {
                assignments.Clear();
            }
        }

        public bool Evaluate()
        {
            if (!IsEnabled)
            {
                return false;
            }

            List<CombatTacticalMemberDescriptor> input =
                new List<CombatTacticalMemberDescriptor>(members.Count);
            LastKnowledgeFactCount = 0;
            foreach (KeyValuePair<string, CombatTacticalRole> member in members)
            {
                KnowledgeSnapshot knowledge;
                knowledgeByMember.TryGetValue(member.Key, out knowledge);
                MobPsychologySnapshot psychology;
                psychologyByMember.TryGetValue(member.Key, out psychology);
                LastKnowledgeFactCount += knowledge.Count;
                input.Add(new CombatTacticalMemberDescriptor(member.Key, member.Value, knowledge, psychology));
            }

            IReadOnlyList<CombatTacticalAssignment> result = CombatTacticalCoordinator.Evaluate(input);
            assignments.Clear();
            for (int index = 0; index < result.Count; index++)
            {
                CombatTacticalAssignment assignment = result[index];
                if (assignment.Intent != CombatTacticalIntent.None)
                {
                    assignments[assignment.MemberId] = assignment.Intent;
                }
            }

            EvaluationCount++;
            return true;
        }

        public bool TryGetIntent(string memberId, out CombatTacticalIntent intent)
        {
            if (!IsEnabled || string.IsNullOrEmpty(memberId))
            {
                intent = CombatTacticalIntent.None;
                return false;
            }

            return assignments.TryGetValue(memberId, out intent)
                && intent != CombatTacticalIntent.None;
        }
    }
}
