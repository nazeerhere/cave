using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Explicit, formation-local runtime wrapper for the Combat Tactical
    /// Coordinator. It never discovers scene enemies: only registered members
    /// can receive an intent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatTacticalFormation : MonoBehaviour
    {
        [Header("Formation Identity")]
        [SerializeField] private string formationId;
        [SerializeField] private bool coordinatorEnabled = true;
        [SerializeField, Min(0.05f)] private float evaluationInterval = 0.25f;

        [Header("Diagnostics (Read Only)")]
        [SerializeField] private int activeMemberCount;
        [SerializeField] private int evaluationCount;
        [SerializeField] private float lastEvaluationMilliseconds;

        private readonly List<CombatTacticalMember> members =
            new List<CombatTacticalMember>();
        private CombatTacticalFormationState state;
        private float nextEvaluationAt;

        /// <summary>Developer-wide switch; per-formation state remains intact.</summary>
        public static bool GloballyEnabled { get; set; } = true;

        public string FormationId => state != null ? state.FormationId : formationId;
        public bool IsCoordinatorEnabled => coordinatorEnabled && GloballyEnabled;
        public int ActiveMemberCount => activeMemberCount;
        public int EvaluationCount => evaluationCount;
        public float LastEvaluationMilliseconds => lastEvaluationMilliseconds;

        private void Awake()
        {
            EnsureState();
        }

        private void OnEnable()
        {
            EnsureState();
            SyncEnabledState();
            nextEvaluationAt = 0f;
        }

        private void Update()
        {
            PruneInactiveMembers();
            SyncEnabledState();
            if (!IsCoordinatorEnabled || state == null || !state.IsValid
                || Time.time < nextEvaluationAt)
            {
                return;
            }

            nextEvaluationAt = Time.time + Mathf.Max(0.05f, evaluationInterval);
            SyncMemberKnowledgeSnapshots();
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (state.Evaluate())
            {
                evaluationCount = state.EvaluationCount;
                RefreshMemberDiagnostics();
            }

            stopwatch.Stop();
            lastEvaluationMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
        }

        private void OnDisable()
        {
            if (state != null)
            {
                state.SetEnabled(false);
            }
        }

        public void SetCoordinatorEnabled(bool value)
        {
            coordinatorEnabled = value;
            SyncEnabledState();
        }

        internal bool Register(CombatTacticalMember member)
        {
            if (member == null)
            {
                return false;
            }

            EnsureState();
            for (int index = 0; index < members.Count; index++)
            {
                CombatTacticalMember existing = members[index];
                if (existing != null
                    && existing != member
                    && existing.MemberId == member.MemberId)
                {
                    return false;
                }
            }

            if (state == null || !state.Register(member.MemberId, member.ResolvedRole))
            {
                return false;
            }

            if (!members.Contains(member))
            {
                members.Add(member);
            }

            activeMemberCount = members.Count;
            return true;
        }

        internal void Unregister(CombatTacticalMember member)
        {
            if (member == null)
            {
                return;
            }

            members.Remove(member);
            state?.Unregister(member.MemberId);
            activeMemberCount = members.Count;
        }

        internal bool TryGetIntent(string memberId, out CombatTacticalIntent intent)
        {
            SyncEnabledState();
            if (!IsCoordinatorEnabled || state == null)
            {
                intent = CombatTacticalIntent.None;
                return false;
            }

            return state.TryGetIntent(memberId, out intent);
        }

        /// <summary>
        /// Explicit V1 formation-only sharing. A received SHARED fact cannot be
        /// relayed, and no member outside this authored formation is considered.
        /// </summary>
        internal int ShareKnowledge(CombatTacticalMember source, KnowledgeFact fact)
        {
            if (source == null || source.Formation != this || !fact.CanShareOnce)
            {
                return 0;
            }

            List<KnowledgeStore> recipients = new List<KnowledgeStore>();
            for (int index = 0; index < members.Count; index++)
            {
                CombatTacticalMember member = members[index];
                KnowledgeActor actor = member != null ? member.Knowledge : null;
                if (actor != null && actor.Store != null && actor != source.Knowledge)
                {
                    recipients.Add(actor.Store);
                }
            }

            return KnowledgeSharing.ShareOneHop(fact, recipients, Time.time);
        }

        private void EnsureState()
        {
            if (state == null || state.FormationId != formationId)
            {
                state = new CombatTacticalFormationState(formationId);
                for (int index = 0; index < members.Count; index++)
                {
                    CombatTacticalMember member = members[index];
                    if (member != null)
                    {
                        state.Register(member.MemberId, member.ResolvedRole);
                    }
                }
            }
        }

        private void SyncEnabledState()
        {
            if (state == null || state.IsEnabled == IsCoordinatorEnabled)
            {
                return;
            }

            state.SetEnabled(IsCoordinatorEnabled);
            if (!IsCoordinatorEnabled)
            {
                for (int index = 0; index < members.Count; index++)
                {
                    members[index]?.SetCurrentIntent(CombatTacticalIntent.None);
                }
            }
        }

        private void PruneInactiveMembers()
        {
            for (int index = members.Count - 1; index >= 0; index--)
            {
                CombatTacticalMember member = members[index];
                if (member != null && member.isActiveAndEnabled)
                {
                    continue;
                }

                if (member != null)
                {
                    state?.Unregister(member.MemberId);
                }

                members.RemoveAt(index);
            }

            activeMemberCount = members.Count;
        }

        private void RefreshMemberDiagnostics()
        {
            for (int index = 0; index < members.Count; index++)
            {
                CombatTacticalMember member = members[index];
                if (member == null)
                {
                    continue;
                }

                CombatTacticalIntent intent;
                member.SetCurrentIntent(state.TryGetIntent(member.MemberId, out intent)
                    ? intent
                    : CombatTacticalIntent.None);
            }
        }

        private void SyncMemberKnowledgeSnapshots()
        {
            if (state == null)
            {
                return;
            }

            for (int index = 0; index < members.Count; index++)
            {
                CombatTacticalMember member = members[index];
                if (member != null)
                {
                    state.SetKnowledgeSnapshot(member.MemberId, member.CreateKnowledgeSnapshot());
                }
            }
        }
    }

    /// <summary>
    /// Explicit opt-in membership. Missing formation, identity, or coordinator
    /// always means no tactical influence and normal brain behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatTacticalMember : MonoBehaviour
    {
        [SerializeField] private CombatTacticalFormation formation;
        [SerializeField] private string memberId;
        [SerializeField] private CombatTacticalRole role;

        [Header("Diagnostics (Read Only)")]
        [SerializeField] private CombatTacticalIntent currentIntent;

        private KnowledgeActor knowledge;

        public string MemberId => memberId;
        public CombatTacticalRole ResolvedRole => role;
        public CombatTacticalFormation Formation => formation;
        public CombatTacticalIntent CurrentIntent => currentIntent;
        internal KnowledgeActor Knowledge => knowledge;

        private void OnEnable()
        {
            knowledge = KnowledgeActor.EnsureOn(gameObject);
            Register();
        }

        private void Start()
        {
            Register();
        }

        private void OnDisable()
        {
            formation?.Unregister(this);
            currentIntent = CombatTacticalIntent.None;
        }

        private void OnDestroy()
        {
            formation?.Unregister(this);
        }

        public bool TryGetIntent(out CombatTacticalIntent intent)
        {
            if (formation != null && formation.TryGetIntent(memberId, out intent))
            {
                currentIntent = intent;
                return true;
            }

            intent = CombatTacticalIntent.None;
            currentIntent = CombatTacticalIntent.None;
            return false;
        }

        internal void SetCurrentIntent(CombatTacticalIntent intent)
        {
            currentIntent = intent;
            if (intent != CombatTacticalIntent.None)
            {
                knowledge?.ReceiveCommand(intent, formation != null ? formation.FormationId : string.Empty, Time.time);
            }
        }

        public KnowledgeSnapshot CreateKnowledgeSnapshot()
        {
            if (knowledge == null)
            {
                knowledge = GetComponent<KnowledgeActor>();
            }

            return knowledge != null ? knowledge.CreateSnapshot(Time.time) : default;
        }

        public int ShareKnowledge(KnowledgeFact fact)
        {
            return formation != null ? formation.ShareKnowledge(this, fact) : 0;
        }

        private void Register()
        {
            formation?.Register(this);
        }
    }
}
