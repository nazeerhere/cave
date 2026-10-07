using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Small evidence vocabulary; these are interpretations of legitimate facts, never world truth.</summary>
    public enum PsychologyEvidenceKind
    {
        None = 0, AllyDeath = 1, SuccessfulPressure = 2, PlayerVulnerability = 3,
        PlayerRetreat = 4, RepeatedBehavior = 5, FormationBreak = 6, PlayerSurrounded = 7
    }

    public readonly struct MobPsychologySnapshot
    {
        public MobPsychologySnapshot(string mobId, float aggression, float confidence, float fear, float cooperation)
        { MobId=mobId; Aggression=Mathf.Clamp01(aggression); Confidence=Mathf.Clamp01(confidence); Fear=Mathf.Clamp01(fear); Cooperation=Mathf.Clamp01(cooperation); }
        public string MobId { get; } public float Aggression { get; } public float Confidence { get; } public float Fear { get; } public float Cooperation { get; }
    }

    /// <summary>Validated delta contract for either a deterministic or optional Jev interpreter.</summary>
    public readonly struct MobPsychologyDelta
    {
        public MobPsychologyDelta(string mobId, float aggression, float confidence, float fear, float cooperation)
        { MobId=mobId; Aggression=aggression; Confidence=confidence; Fear=fear; Cooperation=cooperation; }
        public string MobId { get; } public float Aggression { get; } public float Confidence { get; } public float Fear { get; } public float Cooperation { get; }
        public bool IsFinite => IsFiniteValue(Aggression)&&IsFiniteValue(Confidence)&&IsFiniteValue(Fear)&&IsFiniteValue(Cooperation);
        private static bool IsFiniteValue(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public readonly struct PsychologyEvidence
    {
        public PsychologyEvidence(PsychologyEvidenceKind kind, string observerId, long provenanceId, float occurredAt)
        { Kind=kind; ObserverId=observerId; ProvenanceId=provenanceId; OccurredAt=occurredAt; }
        public PsychologyEvidenceKind Kind { get; } public string ObserverId { get; } public long ProvenanceId { get; } public float OccurredAt { get; }
    }

    public readonly struct PsychologyInterpretationRequest
    {
        public PsychologyInterpretationRequest(string groupId, IReadOnlyList<MobPsychologySnapshot> members, IReadOnlyList<PsychologyEvidence> evidence)
        { GroupId=groupId; Members=Copy(members); Evidence=Copy(evidence); }
        public string GroupId { get; } public IReadOnlyList<MobPsychologySnapshot> Members { get; } public IReadOnlyList<PsychologyEvidence> Evidence { get; }
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source) { return new List<T>(source ?? Array.Empty<T>()).AsReadOnly(); }
    }

    /// <summary>Provider boundary. A provider interprets only supplied evidence and returns deltas.</summary>
    public interface IPsychologyInterpreter
    {
        bool TryInterpret(PsychologyInterpretationRequest request, out IReadOnlyList<MobPsychologyDelta> deltas);
    }

    /// <summary>Conservative playable fallback when Jev is disabled, absent, slow, or unavailable.</summary>
    public sealed class DeterministicPsychologyInterpreter : IPsychologyInterpreter
    {
        public bool TryInterpret(PsychologyInterpretationRequest request, out IReadOnlyList<MobPsychologyDelta> deltas)
        {
            List<MobPsychologyDelta> result = new List<MobPsychologyDelta>();
            if (request.Members == null) { deltas=result.AsReadOnly(); return false; }
            for (int memberIndex=0; memberIndex<request.Members.Count; memberIndex++)
            {
                MobPsychologySnapshot member=request.Members[memberIndex]; float aggression=0f, confidence=0f, fear=0f, cooperation=0f;
                for (int evidenceIndex=0; evidenceIndex<request.Evidence.Count; evidenceIndex++)
                {
                    switch (request.Evidence[evidenceIndex].Kind)
                    {
                        case PsychologyEvidenceKind.AllyDeath: fear+=0.12f; confidence-=0.10f; cooperation+=0.05f; break;
                        case PsychologyEvidenceKind.SuccessfulPressure: confidence+=0.05f; aggression+=0.04f; break;
                        case PsychologyEvidenceKind.PlayerVulnerability: confidence+=0.04f; aggression+=0.08f; break;
                        case PsychologyEvidenceKind.PlayerRetreat: confidence+=0.04f; aggression+=0.06f; break;
                        case PsychologyEvidenceKind.RepeatedBehavior: aggression+=0.03f; break;
                        case PsychologyEvidenceKind.FormationBreak: fear+=0.08f; cooperation-=0.06f; break;
                        case PsychologyEvidenceKind.PlayerSurrounded: confidence+=0.03f; cooperation+=0.04f; break;
                    }
                }
                result.Add(new MobPsychologyDelta(member.MobId,
                    Mathf.Clamp(aggression, -MobPsychologyState.MaximumInterpreterDelta, MobPsychologyState.MaximumInterpreterDelta),
                    Mathf.Clamp(confidence, -MobPsychologyState.MaximumInterpreterDelta, MobPsychologyState.MaximumInterpreterDelta),
                    Mathf.Clamp(fear, -MobPsychologyState.MaximumInterpreterDelta, MobPsychologyState.MaximumInterpreterDelta),
                    Mathf.Clamp(cooperation, -MobPsychologyState.MaximumInterpreterDelta, MobPsychologyState.MaximumInterpreterDelta)));
            }
            deltas=result.AsReadOnly(); return true;
        }
    }

    /// <summary>One bounded group scheduler. The optional provider is synchronous by design in V1; unavailable work falls back safely.</summary>
    public sealed class PsychologyGroupScheduler
    {
        private const int MaximumPendingEvidence = 16;
        private readonly List<PsychologyEvidence> pending = new List<PsychologyEvidence>(MaximumPendingEvidence);
        private float lastInterpretedAt = float.NegativeInfinity;
        public bool JevEnabled { get; set; }
        public bool RequestInFlight { get; private set; }
        public bool IsDirty => pending.Count > 0;
        public float LastInterpretedAt => lastInterpretedAt;
        public int PendingEvidenceCount => pending.Count;
        public void MarkDirty(PsychologyEvidence evidence) { if(evidence.Kind==PsychologyEvidenceKind.None)return; if(pending.Count==MaximumPendingEvidence)pending.RemoveAt(0); pending.Add(evidence); }
        public bool TryInterpret(float now, string groupId, IReadOnlyList<MobPsychologySnapshot> members, IPsychologyInterpreter jev, IPsychologyInterpreter fallback, out IReadOnlyList<MobPsychologyDelta> deltas, out bool usedJev)
        {
            deltas=Array.Empty<MobPsychologyDelta>(); usedJev=false;
            if(!IsDirty||RequestInFlight||now-lastInterpretedAt<5f)return false;
            RequestInFlight=true;
            try
            {
                PsychologyInterpretationRequest request=new PsychologyInterpretationRequest(groupId,members,pending.AsReadOnly());
                bool interpreted=JevEnabled&&jev!=null&&jev.TryInterpret(request,out deltas);
                usedJev=interpreted;
                if(!interpreted) interpreted=fallback!=null&&fallback.TryInterpret(request,out deltas);
                if(!interpreted) return false;
                pending.Clear(); lastInterpretedAt=now; return true;
            }
            finally { RequestInFlight=false; }
        }
    }

    /// <summary>Per-mob authoritative bounded psychology. No interpreter owns this state.</summary>
    [DisallowMultipleComponent]
    public sealed class MobPsychologyState : MonoBehaviour
    {
        public const float MaximumInterpreterDelta = 0.35f;
        [SerializeField, Range(0f,1f)] private float aggression=0.5f;
        [SerializeField, Range(0f,1f)] private float confidence=0.5f;
        [SerializeField, Range(0f,1f)] private float fear=0.5f;
        [SerializeField, Range(0f,1f)] private float cooperation=0.5f;
        public MobPsychologySnapshot Snapshot(string mobId) { return new MobPsychologySnapshot(mobId,aggression,confidence,fear,cooperation); }
        public bool TryApply(MobPsychologyDelta delta, string expectedMobId)
        {
            if(string.IsNullOrEmpty(expectedMobId)||delta.MobId!=expectedMobId||!delta.IsFinite||Mathf.Abs(delta.Aggression)>MaximumInterpreterDelta||Mathf.Abs(delta.Confidence)>MaximumInterpreterDelta||Mathf.Abs(delta.Fear)>MaximumInterpreterDelta||Mathf.Abs(delta.Cooperation)>MaximumInterpreterDelta)return false;
            aggression=Mathf.Clamp01(aggression+delta.Aggression); confidence=Mathf.Clamp01(confidence+delta.Confidence); fear=Mathf.Clamp01(fear+delta.Fear); cooperation=Mathf.Clamp01(cooperation+delta.Cooperation); return true;
        }
    }
}
