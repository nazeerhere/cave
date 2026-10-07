using System;
using UnityEngine;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Actor-local identity for a qualifying Pattern phenomenon. The identity is
    /// deliberately separate from S: it says what relationship exists, while
    /// the Axiom S/R/A channel remains the sole mutable strength authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomPatternContextState : MonoBehaviour
    {
        [SerializeField] private string resonanceIdentity;
        [SerializeField] private uint resonanceRevision;
        [SerializeField] private string phaseIdentity;
        [SerializeField] private uint phaseRevision;

        public static AxiomPatternContextState EnsureOn(GameObject owner)
        {
            return owner == null ? null : owner.GetComponent<AxiomPatternContextState>()
                ?? owner.AddComponent<AxiomPatternContextState>();
        }

        public bool TryRead(AxiomKind kind, out string identity, out uint revision)
        {
            switch (kind)
            {
                case AxiomKind.Resonance:
                    identity = resonanceIdentity;
                    revision = resonanceRevision;
                    return !string.IsNullOrWhiteSpace(identity);
                case AxiomKind.Phase:
                    identity = phaseIdentity;
                    revision = phaseRevision;
                    return !string.IsNullOrWhiteSpace(identity);
                default:
                    identity = null;
                    revision = 0;
                    return false;
            }
        }

        public void Establish(AxiomKind kind, string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) return;
            if (kind == AxiomKind.Resonance)
            {
                if (string.Equals(resonanceIdentity, identity, StringComparison.Ordinal)) return;
                resonanceIdentity = identity;
                resonanceRevision++;
            }
            else if (kind == AxiomKind.Phase)
            {
                if (string.Equals(phaseIdentity, identity, StringComparison.Ordinal)) return;
                phaseIdentity = identity;
                phaseRevision++;
            }
        }

        public void Clear(AxiomKind kind)
        {
            if (kind == AxiomKind.Resonance && !string.IsNullOrEmpty(resonanceIdentity))
            {
                resonanceIdentity = null;
                resonanceRevision++;
            }
            else if (kind == AxiomKind.Phase && !string.IsNullOrEmpty(phaseIdentity))
            {
                phaseIdentity = null;
                phaseRevision++;
            }
        }
    }
}
