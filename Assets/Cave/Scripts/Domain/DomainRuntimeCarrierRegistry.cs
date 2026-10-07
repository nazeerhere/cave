using System;
using System.Collections.Generic;
using Cave.Axioms;
using UnityEngine;

namespace Cave.Domain
{
    public enum DomainRuntimeCarrierRegistrationResult
    {
        Registered = 0,
        AlreadyRegistered = 1,
        DuplicateCarrierId = 2,
        InvalidCarrier = 3
    }

    /// <summary>
    /// A session-stable, scene-local identity for one live Axiom authority.
    /// A serialized external ID may be supplied for spawned/persisted actors;
    /// otherwise an opaque session ID is generated once by this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AxiomRuntimeState))]
    public sealed class DomainRuntimeCarrierIdentity : MonoBehaviour
    {
        [SerializeField] private string carrierId;
        private AxiomRuntimeState runtime;

        public PhenomenonCarrierId CarrierId => new PhenomenonCarrierId(carrierId);
        public AxiomRuntimeState Runtime => runtime != null ? runtime : (runtime = GetComponent<AxiomRuntimeState>());

        public static DomainRuntimeCarrierIdentity EnsureOn(GameObject owner)
        {
            if (owner == null) return null;
            DomainRuntimeCarrierIdentity identity = owner.GetComponent<DomainRuntimeCarrierIdentity>();
            if (identity == null) identity = owner.AddComponent<DomainRuntimeCarrierIdentity>();
            identity.EnsureCarrierId();
            return identity;
        }

        private void Awake()
        {
            runtime = GetComponent<AxiomRuntimeState>();
            EnsureCarrierId();
        }

        private void OnEnable()
        {
            EnsureCarrierId();
            DomainRuntimeCarrierRegistry.Register(this);
        }

        private void OnDisable()
        {
            DomainRuntimeCarrierRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            DomainRuntimeCarrierRegistry.Unregister(this);
        }

        private void EnsureCarrierId()
        {
            if (!string.IsNullOrWhiteSpace(carrierId)) return;
            // Unity instance IDs are deliberately used only as an opaque ID for
            // this live registration session; no gameplay priority depends on it.
            carrierId = "runtime:" + gameObject.scene.handle + ":" + gameObject.GetInstanceID();
        }
    }

    /// <summary>
    /// Registry-backed Domain runtime accessor. It never discovers scene
    /// objects; callers see only explicitly registered live Axiom authorities.
    /// </summary>
    public static class DomainRuntimeCarrierRegistry
    {
        private static readonly Dictionary<PhenomenonCarrierId, DomainRuntimeCarrierIdentity> carriers =
            new Dictionary<PhenomenonCarrierId, DomainRuntimeCarrierIdentity>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntime()
        {
            carriers.Clear();
        }

        public static int Count
        {
            get { PruneDead(); return carriers.Count; }
        }

        public static DomainRuntimeCarrierRegistrationResult Register(DomainRuntimeCarrierIdentity identity)
        {
            if (!IsLive(identity) || !identity.CarrierId.IsValid)
                return DomainRuntimeCarrierRegistrationResult.InvalidCarrier;

            DomainRuntimeCarrierIdentity existing;
            if (carriers.TryGetValue(identity.CarrierId, out existing))
            {
                if (ReferenceEquals(existing, identity)) return DomainRuntimeCarrierRegistrationResult.AlreadyRegistered;
                if (!IsLive(existing))
                {
                    carriers[identity.CarrierId] = identity;
                    return DomainRuntimeCarrierRegistrationResult.Registered;
                }

                return DomainRuntimeCarrierRegistrationResult.DuplicateCarrierId;
            }

            carriers.Add(identity.CarrierId, identity);
            return DomainRuntimeCarrierRegistrationResult.Registered;
        }

        public static void Unregister(DomainRuntimeCarrierIdentity identity)
        {
            if (identity == null) return;
            DomainRuntimeCarrierIdentity existing;
            if (carriers.TryGetValue(identity.CarrierId, out existing) && ReferenceEquals(existing, identity))
                carriers.Remove(identity.CarrierId);
        }

        public static bool TryGet(PhenomenonCarrierId id, out DomainRuntimeCarrierIdentity identity)
        {
            PruneDead();
            if (carriers.TryGetValue(id, out identity) && IsLive(identity)) return true;
            identity = null;
            return false;
        }

        public static bool TryResolve(PhenomenonCarrierId id, LawPhenomenon phenomenon, float timestamp,
            out IDomainPhenomenonRuntimeTarget target)
        {
            DomainRuntimeCarrierIdentity identity;
            if (!TryGet(id, out identity) || identity.Runtime == null)
            {
                target = null;
                return false;
            }

            target = new AxiomRuntimeDomainTarget(identity.Runtime, phenomenon, timestamp);
            return true;
        }

        public static IDomainPhenomenonRuntimeAccessor CreateAccessor(float timestamp)
        {
            return new Accessor(timestamp);
        }

        public static IReadOnlyList<DomainRuntimeCarrierIdentity> EnumerateInRadius(Vector2 center, float radius)
        {
            PruneDead();
            float boundedRadius = Mathf.Max(0f, radius);
            float squared = boundedRadius * boundedRadius;
            List<DomainRuntimeCarrierIdentity> result = new List<DomainRuntimeCarrierIdentity>();
            foreach (KeyValuePair<PhenomenonCarrierId, DomainRuntimeCarrierIdentity> pair in carriers)
            {
                DomainRuntimeCarrierIdentity identity = pair.Value;
                if (!IsLive(identity)) continue;
                Vector2 delta = (Vector2)identity.transform.position - center;
                if (delta.sqrMagnitude <= squared) result.Add(identity);
            }
            result.Sort((left, right) => left.CarrierId.CompareTo(right.CarrierId));
            return result.AsReadOnly();
        }

        private static void PruneDead()
        {
            List<PhenomenonCarrierId> dead = null;
            foreach (KeyValuePair<PhenomenonCarrierId, DomainRuntimeCarrierIdentity> pair in carriers)
            {
                if (IsLive(pair.Value)) continue;
                if (dead == null) dead = new List<PhenomenonCarrierId>();
                dead.Add(pair.Key);
            }
            if (dead == null) return;
            for (int index = 0; index < dead.Count; index++) carriers.Remove(dead[index]);
        }

        private static bool IsLive(DomainRuntimeCarrierIdentity identity)
        {
            return identity != null && identity.isActiveAndEnabled && identity.Runtime != null
                && identity.Runtime.isActiveAndEnabled;
        }

        private sealed class Accessor : IDomainPhenomenonRuntimeAccessor
        {
            private readonly float timestamp;
            public Accessor(float timestamp) { this.timestamp = timestamp; }
            public bool TryResolve(PhenomenonCarrierId carrierId, LawPhenomenon phenomenon,
                out IDomainPhenomenonRuntimeTarget target)
            {
                return DomainRuntimeCarrierRegistry.TryResolve(carrierId, phenomenon, timestamp, out target);
            }
        }
    }

    public enum LocalizedDomainTargetRejectionReason
    {
        None = 0, CarrierInactive = 1, SemanticMappingUnsupported = 2, SnapshotUnavailable = 3
    }

    public sealed class LocalizedDomainRuntimeTarget
    {
        internal LocalizedDomainRuntimeTarget(DomainRuntimeCarrierIdentity carrier, PhenomenonSemanticSnapshot snapshot)
        { Carrier = carrier; Snapshot = snapshot; }
        public DomainRuntimeCarrierIdentity Carrier { get; }
        public PhenomenonCarrierId CarrierId => Carrier.CarrierId;
        public PhenomenonSemanticSnapshot Snapshot { get; }
    }

    public sealed class LocalizedDomainTargetResolution
    {
        internal LocalizedDomainTargetResolution(IReadOnlyList<LocalizedDomainRuntimeTarget> targets,
            LocalizedDomainTargetRejectionReason rejection)
        { Targets = targets; RejectionReason = rejection; }
        public IReadOnlyList<LocalizedDomainRuntimeTarget> Targets { get; }
        public LocalizedDomainTargetRejectionReason RejectionReason { get; }
        public bool Succeeded => RejectionReason == LocalizedDomainTargetRejectionReason.None;
    }

    /// <summary>Bounded registered-carrier discovery for a localized Domain. No global object lookup or unordered physics result is used.</summary>
    public static class LocalizedDomainTargetResolver
    {
        public static LocalizedDomainTargetResolution Resolve(LocalizedDomainCarrier carrier, float timestamp)
        {
            List<LocalizedDomainRuntimeTarget> targets = new List<LocalizedDomainRuntimeTarget>();
            if (carrier == null || !carrier.IsActive(timestamp))
                return new LocalizedDomainTargetResolution(targets.AsReadOnly(), LocalizedDomainTargetRejectionReason.CarrierInactive);
            if (carrier.BoundLaw == null || carrier.BoundLaw.Law == null
                || AxiomDomainSemanticCodec.GetCapability(carrier.BoundLaw.Law.Phenomenon) == AxiomDomainSemanticCapability.Unsupported)
                return new LocalizedDomainTargetResolution(targets.AsReadOnly(), LocalizedDomainTargetRejectionReason.SemanticMappingUnsupported);

            IReadOnlyList<DomainRuntimeCarrierIdentity> discovered = DomainRuntimeCarrierRegistry.EnumerateInRadius(carrier.Center, carrier.Radius);
            for (int index = 0; index < discovered.Count; index++)
            {
                DomainRuntimeCarrierIdentity identity = discovered[index];
                AxiomRuntimeDomainTarget target = new AxiomRuntimeDomainTarget(identity.Runtime, carrier.BoundLaw.Law.Phenomenon, timestamp);
                PhenomenonSemanticSnapshot snapshot;
                if (!target.TryRead(out snapshot) || snapshot == null) continue;
                targets.Add(new LocalizedDomainRuntimeTarget(identity, snapshot));
            }
            return new LocalizedDomainTargetResolution(targets.AsReadOnly(), LocalizedDomainTargetRejectionReason.None);
        }
    }
}
