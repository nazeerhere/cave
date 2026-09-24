using System;
using System.Collections.Generic;
using Cave.Axioms;
using UnityEngine;

namespace Cave.Interactions
{
    /// <summary>Small semantic vocabulary for the interaction layer.</summary>
    [Flags]
    public enum InteractionTraits
    {
        None = 0,
        Projectile = 1 << 0,
        Moving = 1 << 1,
        ManaPowered = 1 << 2
    }

    public enum InteractionOwnership
    {
        Neutral,
        Player,
        Enemy,
        Claim
    }

    /// <summary>Only event kinds emitted by the initial interaction foundation.</summary>
    public enum InteractionEventKind
    {
        Spawned,
        Hit,
        DamageApplied,
        OwnershipChanged,
        Destroyed,
        ClaimAttempted,
        ClaimResolved,
        EnteredTerritory,
        ExitedTerritory
    }

    /// <summary>
    /// Immutable, strongly typed facts about a meaningful interaction seam.
    /// Axiom references point at existing Axiom state; they never duplicate it.
    /// </summary>
    public readonly struct InteractionEvent
    {
        public InteractionEvent(
            InteractionEventKind kind,
            InteractionIdentity source,
            GameObject target,
            InteractionTraits traits,
            int value = 0,
            AxiomKind? axiom = null,
            InteractionOwnership previousOwnership = InteractionOwnership.Neutral,
            InteractionOwnership currentOwnership = InteractionOwnership.Neutral,
            ClaimProvenance provenance = ClaimProvenance.None,
            ClaimRejectionReason rejection = ClaimRejectionReason.None)
        {
            Kind = kind;
            Source = source;
            Target = target;
            Traits = traits;
            Value = value;
            Axiom = axiom;
            PreviousOwnership = previousOwnership;
            CurrentOwnership = currentOwnership;
            Provenance = provenance;
            Rejection = rejection;
            Sequence = 0;
        }

        private InteractionEvent(InteractionEvent source, ulong sequence)
        {
            Kind = source.Kind;
            Source = source.Source;
            Target = source.Target;
            Traits = source.Traits;
            Value = source.Value;
            Axiom = source.Axiom;
            PreviousOwnership = source.PreviousOwnership;
            CurrentOwnership = source.CurrentOwnership;
            Provenance = source.Provenance;
            Rejection = source.Rejection;
            Sequence = sequence;
        }

        public ulong Sequence { get; }
        public InteractionEventKind Kind { get; }
        public InteractionIdentity Source { get; }
        public GameObject Target { get; }
        public InteractionTraits Traits { get; }
        public int Value { get; }
        public AxiomKind? Axiom { get; }
        public InteractionOwnership PreviousOwnership { get; }
        public InteractionOwnership CurrentOwnership { get; }
        public ClaimProvenance Provenance { get; }
        public ClaimRejectionReason Rejection { get; }

        internal InteractionEvent WithSequence(ulong sequence)
        {
            return new InteractionEvent(this, sequence);
        }
    }

    /// <summary>Explicit responders are registered directly; the bus never scans a scene.</summary>
    public interface IInteractionResponder
    {
        void Respond(InteractionEvent interactionEvent);
    }

    /// <summary>
    /// Event-only dispatcher with a bounded consequence chain. The resolver
    /// owns authority changes; responders may inspect facts and request another
    /// approved operation, but cannot directly set ownership.
    /// </summary>
    public static class InteractionEventBus
    {
        private const int MaximumDispatchDepth = 8;

        private static readonly List<IInteractionResponder> Responders = new List<IInteractionResponder>(8);
        private static ulong nextSequence;
        private static int dispatchDepth;

        public static event Action<InteractionEvent> EventEmitted;

        public static void Register(IInteractionResponder responder)
        {
            if (responder != null && !Responders.Contains(responder))
            {
                Responders.Add(responder);
            }
        }

        public static void Unregister(IInteractionResponder responder)
        {
            if (responder != null)
            {
                Responders.Remove(responder);
            }
        }

        public static bool Emit(InteractionEvent interactionEvent)
        {
            if (dispatchDepth >= MaximumDispatchDepth)
            {
                Debug.LogWarning(
                    "[Cave] Interaction event chain stopped at depth " + MaximumDispatchDepth
                    + " while emitting " + interactionEvent.Kind + ".");
                return false;
            }

            InteractionEvent resolvedEvent = interactionEvent.WithSequence(++nextSequence);
            dispatchDepth++;
            try
            {
                int responderCount = Responders.Count;
                for (int index = 0; index < responderCount; index++)
                {
                    Responders[index]?.Respond(resolvedEvent);
                }

                EventEmitted?.Invoke(resolvedEvent);
                return true;
            }
            finally
            {
                dispatchDepth--;
            }
        }
    }

    /// <summary>
    /// Current interaction state. Ownership is changed only by a resolver's
    /// approved authority operation, not by public field assignment.
    /// </summary>
    [Serializable]
    public sealed class InteractionState
    {
        [SerializeField] private InteractionOwnership ownership;
        [SerializeField] private GameObject authoritySource;
        [SerializeField, Min(0)] private int authorityStrength;
        // Kept deliberately small: Repossession needs truthful authority history,
        // not an unbounded event ledger.
        [SerializeField] private GameObject originatingAuthoritySource;
        [SerializeField] private GameObject previousAuthoritySource;

        public InteractionOwnership Ownership => ownership;
        public GameObject AuthoritySource => authoritySource;
        public int AuthorityStrength => authorityStrength;
        public GameObject OriginatingAuthoritySource => originatingAuthoritySource;
        public GameObject PreviousAuthoritySource => previousAuthoritySource;

        public bool HasAuthorityHistory(GameObject source)
        {
            return source != null
                && (authoritySource == source
                    || originatingAuthoritySource == source
                    || previousAuthoritySource == source);
        }

        internal void Establish(InteractionOwnership value, GameObject source, int strength)
        {
            if (originatingAuthoritySource == null && source != null)
            {
                originatingAuthoritySource = source;
            }

            if (authoritySource != null && authoritySource != source)
            {
                previousAuthoritySource = authoritySource;
            }

            ownership = value;
            authoritySource = source;
            authorityStrength = Mathf.Max(0, strength);
        }
    }

    /// <summary>
    /// Opt-in semantic identity for an entity or effect. Existing prefabs do not
    /// receive this component unless a specific interaction seam tracks them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionIdentity : MonoBehaviour
    {
        [SerializeField] private InteractionTraits traits;
        [SerializeField] private bool claimable;
        [SerializeField] private InteractionState state = new InteractionState();

        public InteractionTraits Traits => traits;
        public bool IsClaimable => claimable;
        public InteractionState State => state;
        public InteractionOwnership Ownership => state != null ? state.Ownership : InteractionOwnership.Neutral;
        public GameObject AuthoritySource => state != null ? state.AuthoritySource : null;
        public int AuthorityStrength => state != null ? state.AuthorityStrength : 0;
        public bool HasAuthorityHistory(GameObject source) => state != null && state.HasAuthorityHistory(source);

        internal void EstablishAtSpawn(
            InteractionTraits semanticTraits,
            bool canBeClaimed,
            InteractionOwnership owner,
            GameObject ownerSource,
            int strength)
        {
            traits = semanticTraits;
            claimable = canBeClaimed;
            if (state == null)
            {
                state = new InteractionState();
            }

            state.Establish(owner, ownerSource, strength);
        }

        internal void ApplyResolvedOwnership(
            InteractionOwnership owner,
            GameObject ownerSource,
            int strength)
        {
            if (state == null)
            {
                state = new InteractionState();
            }

            state.Establish(owner, ownerSource, strength);
        }
    }

    /// <summary>Approved spawn/hit/damage/destroy reporting helpers.</summary>
    public static class InteractionRuntime
    {
        public static InteractionIdentity TrackSpawn(
            GameObject subject,
            InteractionTraits traits,
            InteractionOwnership owner,
            GameObject authoritySource,
            bool claimable,
            int authorityStrength = 0,
            AxiomKind? axiom = null)
        {
            if (subject == null)
            {
                return null;
            }

            InteractionIdentity identity = subject.GetComponent<InteractionIdentity>();
            if (identity == null)
            {
                identity = subject.AddComponent<InteractionIdentity>();
            }

            identity.EstablishAtSpawn(traits, claimable, owner, authoritySource, authorityStrength);
            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.Spawned,
                identity,
                null,
                traits,
                axiom: axiom,
                currentOwnership: owner));
            return identity;
        }

        /// <summary>
        /// Explicit handoff for systems such as reflection or redirection which
        /// resolve ownership outside Claim. The handoff preserves the bounded
        /// authority history used by Repossession and emits the standard event.
        /// </summary>
        public static bool TransferResolvedOwnership(
            InteractionIdentity identity,
            InteractionOwnership ownership,
            GameObject authoritySource,
            int authorityStrength)
        {
            if (identity == null)
            {
                return false;
            }

            InteractionOwnership previous = identity.Ownership;
            identity.ApplyResolvedOwnership(ownership, authoritySource, authorityStrength);
            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.OwnershipChanged,
                identity,
                identity.gameObject,
                identity.Traits,
                Mathf.Max(0, authorityStrength),
                previousOwnership: previous,
                currentOwnership: ownership));
            return true;
        }

        public static void ReportHit(InteractionIdentity source, GameObject target, int value = 0)
        {
            if (source == null)
            {
                return;
            }

            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.Hit,
                source,
                target,
                source.Traits,
                value,
                currentOwnership: source.Ownership));
        }

        public static void ReportDamageApplied(InteractionIdentity source, GameObject target, int amount)
        {
            if (source == null)
            {
                return;
            }

            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.DamageApplied,
                source,
                target,
                source.Traits,
                amount,
                currentOwnership: source.Ownership));
        }

        public static void ReportDestroyed(InteractionIdentity source)
        {
            if (source == null)
            {
                return;
            }

            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.Destroyed,
                source,
                null,
                source.Traits,
                currentOwnership: source.Ownership));
        }
    }
}
