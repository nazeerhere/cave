using System;
using UnityEngine;

namespace Cave.Axioms.Mastery
{
    public enum MasteryDomain { Heat, Order, Flow, Mass, Phase, Resonance, StoneglassPrecision }
    public enum MasteryEvidenceKind { OrdinaryUse, Quality, Correction, Regulation, Counterphase, Convergence }
    public struct MasteryEvidence
    {
        public MasteryEvidence(MasteryDomain domain, MasteryEvidenceKind kind, float quality, float magnitude, float timestamp)
        { Domain=domain; Kind=kind; Quality=quality; Magnitude=magnitude; Timestamp=timestamp; }
        public MasteryDomain Domain { get; } public MasteryEvidenceKind Kind { get; }
        public float Quality { get; } public float Magnitude { get; } public float Timestamp { get; }
    }

    [DisallowMultipleComponent]
    public sealed class AxiomMasteryState : MonoBehaviour
    {
        [SerializeField, Range(.05f, .95f)] private float minimumCounterFactor = .45f;
        private readonly float[] values = new float[7];
        private readonly int[] repetition = new int[7];
        private readonly MasteryEvidenceKind[] lastKinds = new MasteryEvidenceKind[7];
        public event Action<MasteryDomain,float> MasteryChanged;
        public static AxiomMasteryState EnsureOn(GameObject owner) => owner == null ? null : owner.GetComponent<AxiomMasteryState>() ?? owner.AddComponent<AxiomMasteryState>();
        public float Get(MasteryDomain domain) => values[(int)domain];
        public float GetCounterFactor(MasteryDomain domain) => Mathf.Lerp(1f, minimumCounterFactor, Get(domain));
        public float GetCounterFactor(AxiomKind kind)
        { MasteryDomain domain; return TryDomain(kind,out domain) ? GetCounterFactor(domain) : 1f; }
        public void Record(MasteryEvidence evidence)
        {
            int i=(int)evidence.Domain; bool repeated=lastKinds[i]==evidence.Kind; repetition[i]=repeated?repetition[i]+1:0; lastKinds[i]=evidence.Kind;
            float baseWeight=evidence.Kind==MasteryEvidenceKind.OrdinaryUse?.01f:evidence.Kind==MasteryEvidenceKind.Correction?.08f:.04f;
            float diminish=repeated?Mathf.Max(.2f,1f/(1f+repetition[i]*.35f)):1f;
            float gain=baseWeight*Mathf.Clamp01(Mathf.Max(.2f,evidence.Quality))*Mathf.Max(.1f,evidence.Magnitude)*diminish;
            float before=values[i]; values[i]=Mathf.Clamp01(before+gain); if(values[i]!=before) MasteryChanged?.Invoke(evidence.Domain,values[i]);
        }
        public static bool TryDomain(AxiomKind kind,out MasteryDomain domain)
        { domain=MasteryDomain.Heat; if(kind==AxiomKind.Heat)return true; if(kind==AxiomKind.Order){domain=MasteryDomain.Order;return true;} if(kind==AxiomKind.Flow){domain=MasteryDomain.Flow;return true;} if(kind==AxiomKind.Mass){domain=MasteryDomain.Mass;return true;} if(kind==AxiomKind.Phase){domain=MasteryDomain.Phase;return true;} return false; }
    }
}
