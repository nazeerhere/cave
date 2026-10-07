using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cave.Diagnostics;
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
        [SerializeField] private bool jevEnabled;
        [SerializeField, Min(0.05f)] private float evaluationInterval = 0.25f;

        [Header("Diagnostics (Read Only)")]
        [SerializeField] private int activeMemberCount;
        [SerializeField] private int evaluationCount;
        [SerializeField] private float lastEvaluationMilliseconds;
        [SerializeField] private bool psychologyDirty;
        [SerializeField] private float lastPsychologyInterpretationTime;
        [SerializeField] private bool psychologyRequestInFlight;
        [SerializeField] private int pendingPsychologyEvidence;

        private readonly List<CombatTacticalMember> members =
            new List<CombatTacticalMember>();
        private CombatTacticalFormationState state;
        private readonly PsychologyGroupScheduler psychologyScheduler = new PsychologyGroupScheduler();
        private readonly DeterministicPsychologyInterpreter deterministicPsychology = new DeterministicPsychologyInterpreter();
        private IPsychologyInterpreter jevInterpreter;
        private float nextEvaluationAt;

        /// <summary>Developer-wide switch; per-formation state remains intact.</summary>
        public static bool GloballyEnabled { get; set; } = true;
        public static bool GloballyJevEnabled { get; set; } = true;

        public string FormationId => state != null ? state.FormationId : formationId;
        public bool IsCoordinatorEnabled => coordinatorEnabled && GloballyEnabled;
        public int ActiveMemberCount => activeMemberCount;
        public int EvaluationCount => evaluationCount;
        public float LastEvaluationMilliseconds => lastEvaluationMilliseconds;
        public bool IsJevEnabled => jevEnabled && GloballyJevEnabled;
        public bool IsPsychologyDirty => psychologyScheduler.IsDirty;
        public bool IsPsychologyRequestInFlight => psychologyScheduler.RequestInFlight;
        public float LastPsychologyInterpretationTime => psychologyScheduler.LastInterpretedAt;
        public int PendingPsychologyEvidenceCount => psychologyScheduler.PendingEvidenceCount;

        private void Awake()
        {
            EnsureState();
        }

        private void OnEnable()
        {
            RuntimeTelemetry.RegisterFormation(true);
            EnsureState();
            SyncEnabledState();
            nextEvaluationAt = 0f;
        }

        private void Update()
        {
            PruneInactiveMembers();
            SyncEnabledState();
            if (state == null || !state.IsValid || Time.time < nextEvaluationAt)
            {
                return;
            }

            nextEvaluationAt = Time.time + Mathf.Max(0.05f, evaluationInterval * ResourceGovernor.CurrentNonUrgentCtcIntervalMultiplier);
            SyncMemberKnowledgeSnapshots();
            ProcessPsychology();
            if (!IsCoordinatorEnabled)
            {
                return;
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (state.Evaluate())
            {
                evaluationCount = state.EvaluationCount;
                RefreshMemberDiagnostics();
            }

            stopwatch.Stop();
            lastEvaluationMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
            RuntimeTelemetry.RecordCtcEvaluation(lastEvaluationMilliseconds);
        }

        private void OnDisable()
        {
            RuntimeTelemetry.RegisterFormation(false);
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

        /// <summary>Optional Jev seam. Null/unavailable providers always fall back to deterministic C# interpretation.</summary>
        public void SetJevInterpreter(IPsychologyInterpreter value)
        {
            jevInterpreter = value;
        }

        public void SetJevEnabled(bool value)
        {
            jevEnabled = value;
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
                    KnowledgeSnapshot knowledge = member.CreateKnowledgeSnapshot();
                    state.SetKnowledgeSnapshot(member.MemberId, knowledge);
                    member.CollectPsychologyEvidence(knowledge, psychologyScheduler);
                    state.SetPsychologySnapshot(member.MemberId, member.CreatePsychologySnapshot());
                }
            }
        }

        private void ProcessPsychology()
        {
            psychologyScheduler.JevEnabled = IsJevEnabled;
            List<MobPsychologySnapshot> snapshots = new List<MobPsychologySnapshot>(members.Count);
            for (int index=0;index<members.Count;index++) if(members[index]!=null) snapshots.Add(members[index].CreatePsychologySnapshot());
            IReadOnlyList<MobPsychologyDelta> deltas; bool usedJev;
            if (psychologyScheduler.TryInterpret(Time.time, FormationId, snapshots.AsReadOnly(), jevInterpreter, deterministicPsychology, out deltas, out usedJev))
            {
                if (ValidatePsychologyDeltas(deltas))
                {
                    for(int deltaIndex=0;deltaIndex<deltas.Count;deltaIndex++)
                        for(int memberIndex=0;memberIndex<members.Count;memberIndex++)
                            if(members[memberIndex]!=null&&members[memberIndex].MemberId==deltas[deltaIndex].MobId)
                                members[memberIndex].ApplyPsychology(deltas[deltaIndex]);
                }
            }
            psychologyDirty=psychologyScheduler.IsDirty;
            lastPsychologyInterpretationTime=psychologyScheduler.LastInterpretedAt;
            psychologyRequestInFlight=psychologyScheduler.RequestInFlight;
            pendingPsychologyEvidence=psychologyScheduler.PendingEvidenceCount;
        }

        private bool ValidatePsychologyDeltas(IReadOnlyList<MobPsychologyDelta> deltas)
        {
            if(deltas==null)return false; HashSet<string> ids=new HashSet<string>();
            for(int index=0;index<deltas.Count;index++)
            {
                MobPsychologyDelta delta=deltas[index]; bool known=false;
                for(int memberIndex=0;memberIndex<members.Count;memberIndex++) if(members[memberIndex]!=null&&members[memberIndex].MemberId==delta.MobId){known=true;break;}
                if(!known||!ids.Add(delta.MobId)||!delta.IsFinite||Mathf.Abs(delta.Aggression)>MobPsychologyState.MaximumInterpreterDelta||Mathf.Abs(delta.Confidence)>MobPsychologyState.MaximumInterpreterDelta||Mathf.Abs(delta.Fear)>MobPsychologyState.MaximumInterpreterDelta||Mathf.Abs(delta.Cooperation)>MobPsychologyState.MaximumInterpreterDelta)return false;
            }
            return true;
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
        private MobPsychologyState psychology;
        private readonly Dictionary<KnowledgeFactType,long> psychologyProvenanceByType = new Dictionary<KnowledgeFactType,long>();

        public string MemberId => memberId;
        public CombatTacticalRole ResolvedRole => role;
        public CombatTacticalFormation Formation => formation;
        public CombatTacticalIntent CurrentIntent => currentIntent;
        public KnowledgeSnapshot KnowledgeSnapshot => CreateKnowledgeSnapshot();
        public MobPsychologySnapshot PsychologySnapshot => CreatePsychologySnapshot();
        internal KnowledgeActor Knowledge => knowledge;
        internal MobPsychologyState Psychology => psychology;

        private void OnEnable()
        {
            RuntimeTelemetry.RegisterMob(true);
            knowledge = KnowledgeActor.EnsureOn(gameObject);
            psychology = GetComponent<MobPsychologyState>();
            if (psychology == null) psychology = gameObject.AddComponent<MobPsychologyState>();
            Register();
        }

        private void Start()
        {
            Register();
        }

        private void OnDisable()
        {
            RuntimeTelemetry.RegisterMob(false);
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

        internal MobPsychologySnapshot CreatePsychologySnapshot()
        {
            if (psychology == null) psychology=GetComponent<MobPsychologyState>();
            return psychology!=null?psychology.Snapshot(memberId):default;
        }

        internal void ApplyPsychology(MobPsychologyDelta delta)
        {
            if(psychology==null) psychology=GetComponent<MobPsychologyState>();
            psychology?.TryApply(delta,memberId);
        }

        internal void CollectPsychologyEvidence(KnowledgeSnapshot snapshot, PsychologyGroupScheduler scheduler)
        {
            if(scheduler==null)return;
            for(int index=0;index<snapshot.Facts.Count;index++)
            {
                KnowledgeFact fact=snapshot.Facts[index]; long seen;
                if(psychologyProvenanceByType.TryGetValue(fact.Type,out seen)&&seen==fact.ProvenanceId)continue;
                psychologyProvenanceByType[fact.Type]=fact.ProvenanceId;
                PsychologyEvidenceKind kind=PsychologyEvidenceKind.None;
                if(fact.Type==KnowledgeFactType.PlayerLowStamina)kind=PsychologyEvidenceKind.PlayerVulnerability;
                else if(fact.Type==KnowledgeFactType.PlayerRetreating)kind=PsychologyEvidenceKind.PlayerRetreat;
                else if(fact.Type==KnowledgeFactType.PlayerPressuringAlly)kind=PsychologyEvidenceKind.SuccessfulPressure;
                if(kind!=PsychologyEvidenceKind.None)scheduler.MarkDirty(new PsychologyEvidence(kind,fact.OriginalObserverId,fact.ProvenanceId,Time.time));
            }
        }

        private void Register()
        {
            formation?.Register(this);
        }
    }
}
