using System;
using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Interactions
{
    /// <summary>
    /// Event-driven summary of the authority relationships maintained by one
    /// Claim source. It is intentionally a structural readout, not a resource
    /// bar or a balance system.
    /// </summary>
    public readonly struct ClaimLoadSnapshot
    {
        public ClaimLoadSnapshot(int activeAnchors, int activeTerritories, int claimedObjects)
        {
            ActiveAnchors = Mathf.Max(0, activeAnchors);
            ActiveTerritories = Mathf.Max(0, activeTerritories);
            ClaimedObjects = Mathf.Max(0, claimedObjects);
        }

        public int ActiveAnchors { get; }
        public int ActiveTerritories { get; }
        public int ClaimedObjects { get; }
        public int TotalRelationships => ActiveAnchors + ActiveTerritories + ClaimedObjects;
    }

    /// <summary>
    /// Small ownership grouping for one Claim authority source. It keeps direct
    /// references only; there is no global territory scan or graph database.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClaimAuthorityNetwork : MonoBehaviour, IInteractionResponder
    {
        [SerializeField] private GameObject authoritySource;

        private readonly HashSet<ClaimAnchor> anchors = new HashSet<ClaimAnchor>();
        private readonly HashSet<ClaimedTerritory> territories = new HashSet<ClaimedTerritory>();
        private readonly HashSet<InteractionIdentity> claimedObjects = new HashSet<InteractionIdentity>();
        private readonly List<ClaimAnchor> anchorScratch = new List<ClaimAnchor>();
        private readonly List<ClaimedTerritory> territoryScratch = new List<ClaimedTerritory>();
        private bool disposed;
        private bool isSubscribed;

        public event Action<ClaimLoadSnapshot> LoadChanged;

        public GameObject AuthoritySource => authoritySource != null ? authoritySource : gameObject;
        public bool CanMaintainAuthority => isActiveAndEnabled && !disposed;
        public ClaimLoadSnapshot Load { get; private set; }
        public int ActiveAnchorCount => Load.ActiveAnchors;
        public int ActiveTerritoryCount => Load.ActiveTerritories;
        public int ClaimedObjectCount => Load.ClaimedObjects;

        /// <summary>
        /// Copies this authority's currently active anchors into caller-owned
        /// storage. Combat abilities use this bounded relationship list rather
        /// than discovering anchors through a scene-wide query.
        /// </summary>
        public void CopyActiveAnchors(List<ClaimAnchor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            if (disposed)
            {
                return;
            }

            foreach (ClaimAnchor anchor in anchors)
            {
                if (anchor != null && anchor.IsActive)
                {
                    destination.Add(anchor);
                }
            }
        }

        /// <summary>
        /// Finds the deterministic active anchor whose territory currently
        /// supplies evidence for a target. This is an evidence lookup only;
        /// it never changes ownership or membership.
        /// </summary>
        public bool TryGetTerritorialAnchor(
            InteractionIdentity target,
            out ClaimAnchor territorialAnchor)
        {
            territorialAnchor = null;
            if (disposed || target == null)
            {
                return false;
            }

            int bestInstanceId = int.MaxValue;
            foreach (ClaimedTerritory territory in territories)
            {
                ClaimAnchor candidate = territory != null ? territory.Anchor : null;
                if (candidate == null
                    || !candidate.IsActive
                    || !territory.IsActive
                    || !territory.Contains(target))
                {
                    continue;
                }

                int candidateInstanceId = candidate.GetInstanceID();
                if (territorialAnchor == null || candidateInstanceId < bestInstanceId)
                {
                    territorialAnchor = candidate;
                    bestInstanceId = candidateInstanceId;
                }
            }

            return territorialAnchor != null;
        }

        private void Awake()
        {
            if (authoritySource == null)
            {
                authoritySource = gameObject;
            }
        }

        private void OnEnable()
        {
            Subscribe();
            RecalculateLoad();
        }

        private void OnDisable()
        {
            Unsubscribe();
            DeactivateAllAuthority();
        }

        private void OnDestroy()
        {
            DeactivateAllAuthority();
        }

        /// <summary>Used by a runtime authority owner before it manifests anchors.</summary>
        public void InitializeRuntime(GameObject source)
        {
            authoritySource = source != null ? source : gameObject;
            disposed = false;
            Subscribe();
            RecalculateLoad();
        }

        public void RegisterAnchor(ClaimAnchor anchor)
        {
            if (disposed || anchor == null || !anchors.Add(anchor))
            {
                return;
            }

            anchor.ActiveStateChanged += HandleAnchorStateChanged;
            anchor.AnchorDestroyed += HandleAnchorDestroyed;
            RecalculateLoad();
        }

        public void UnregisterAnchor(ClaimAnchor anchor)
        {
            if (anchor == null || !anchors.Remove(anchor))
            {
                return;
            }

            anchor.ActiveStateChanged -= HandleAnchorStateChanged;
            anchor.AnchorDestroyed -= HandleAnchorDestroyed;
            RecalculateLoad();
        }

        public void RegisterTerritory(ClaimedTerritory territory)
        {
            if (!disposed && territory != null && territories.Add(territory))
            {
                RecalculateLoad();
            }
        }

        public void UnregisterTerritory(ClaimedTerritory territory)
        {
            if (territory != null && territories.Remove(territory))
            {
                RecalculateLoad();
            }
        }

        /// <summary>
        /// Resolver-facing proof that the target is a currently registered
        /// occupant of territory belonging to this exact anchor/network.
        /// </summary>
        public bool HasTerritorialEvidence(ClaimAnchor anchor, InteractionIdentity target)
        {
            if (disposed || anchor == null || target == null || !anchor.IsActive || !anchors.Contains(anchor))
            {
                return false;
            }

            foreach (ClaimedTerritory territory in territories)
            {
                if (territory != null
                    && territory.IsActive
                    && territory.Anchor == anchor
                    && territory.Contains(target))
                {
                    return true;
                }
            }

            return false;
        }

        public void DeactivateAllAuthority()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            anchorScratch.Clear();
            foreach (ClaimAnchor anchor in anchors)
            {
                if (anchor != null)
                {
                    anchorScratch.Add(anchor);
                }
            }

            for (int index = 0; index < anchorScratch.Count; index++)
            {
                anchorScratch[index].Deactivate();
            }

            territoryScratch.Clear();
            foreach (ClaimedTerritory territory in territories)
            {
                if (territory != null)
                {
                    territoryScratch.Add(territory);
                }
            }

            for (int index = 0; index < territoryScratch.Count; index++)
            {
                territoryScratch[index].Deactivate();
            }

            claimedObjects.Clear();
            RecalculateLoad();
        }

        public void Respond(InteractionEvent interactionEvent)
        {
            if (interactionEvent.Kind == InteractionEventKind.Destroyed && interactionEvent.Source != null)
            {
                if (claimedObjects.Remove(interactionEvent.Source))
                {
                    RecalculateLoad();
                }
            }
        }

        private void HandleAnchorStateChanged(ClaimAnchor anchor, bool active)
        {
            RecalculateLoad();
        }

        private void HandleAnchorDestroyed(ClaimAnchor anchor)
        {
            UnregisterAnchor(anchor);
        }

        private void HandleClaimResolved(ClaimResult result)
        {
            if (!result.Succeeded || result.Attempt.Target == null)
            {
                return;
            }

            ClaimAnchor anchor = result.Attempt.Claimant != null
                ? result.Attempt.Claimant.GetComponent<ClaimAnchor>()
                : null;
            if (anchor != null && anchors.Contains(anchor) && claimedObjects.Add(result.Attempt.Target))
            {
                RecalculateLoad();
            }
        }

        private void Subscribe()
        {
            if (isSubscribed)
            {
                return;
            }

            isSubscribed = true;
            InteractionEventBus.Register(this);
            ClaimResolver.ClaimResolved += HandleClaimResolved;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            isSubscribed = false;
            InteractionEventBus.Unregister(this);
            ClaimResolver.ClaimResolved -= HandleClaimResolved;
        }

        private void RecalculateLoad()
        {
            int activeAnchorCount = 0;
            foreach (ClaimAnchor anchor in anchors)
            {
                if (anchor != null && anchor.IsActive)
                {
                    activeAnchorCount++;
                }
            }

            int activeTerritoryCount = 0;
            foreach (ClaimedTerritory territory in territories)
            {
                if (territory != null && territory.IsActive)
                {
                    activeTerritoryCount++;
                }
            }

            ClaimLoadSnapshot next = new ClaimLoadSnapshot(
                activeAnchorCount,
                activeTerritoryCount,
                claimedObjects.Count);
            if (next.ActiveAnchors == Load.ActiveAnchors
                && next.ActiveTerritories == Load.ActiveTerritories
                && next.ClaimedObjects == Load.ClaimedObjects)
            {
                return;
            }

            Load = next;
            LoadChanged?.Invoke(Load);
        }
    }

    /// <summary>
    /// Trigger-backed, bounded territory attached to a Claim anchor. Occupancy
    /// is evidence only: entering does not change ownership by itself.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class ClaimedTerritory : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 2f;

        private readonly HashSet<InteractionIdentity> occupants = new HashSet<InteractionIdentity>();
        private ClaimAnchor anchor;
        private ClaimAuthorityNetwork network;
        private CircleCollider2D territoryCollider;
        private bool configured;
        private bool isActive;

        public ClaimAnchor Anchor => anchor;
        public ClaimAuthorityNetwork Network => network;
        public float Radius => radius;
        public bool IsActive => configured && isActive && anchor != null && anchor.IsActive;
        public int OccupantCount => occupants.Count;

        public void Configure(ClaimAnchor ownerAnchor, ClaimAuthorityNetwork ownerNetwork, float territoryRadius)
        {
            if (ownerAnchor == null || ownerNetwork == null)
            {
                Debug.LogError("[Cave] Claimed Territory requires an active Claim Anchor and Claim Authority Network.", this);
                return;
            }

            UnsubscribeAnchor();
            anchor = ownerAnchor;
            network = ownerNetwork;
            radius = Mathf.Max(0.1f, territoryRadius);
            territoryCollider = GetComponent<CircleCollider2D>();
            territoryCollider.isTrigger = true;
            territoryCollider.radius = radius;
            configured = true;
            isActive = anchor.IsActive;
            territoryCollider.enabled = isActive;
            anchor.ActiveStateChanged += HandleAnchorStateChanged;
            anchor.AnchorDestroyed += HandleAnchorDestroyed;
            if (isActive)
            {
                network.RegisterTerritory(this);
            }
        }

        /// <summary>
        /// Shared membership operation for runtime trigger boundaries and
        /// deterministic editor verification. Entry is accepted only while the
        /// subject is inside this active bounded territory.
        /// </summary>
        private bool RegisterOccupant(InteractionIdentity subject)
        {
            if (subject == null)
            {
                return false;
            }

            if (IsActive && IsWithinBounds(subject.transform.position))
            {
                if (occupants.Add(subject))
                {
                    EmitBoundaryEvent(InteractionEventKind.EnteredTerritory, subject);
                }

                return true;
            }

            RemoveOccupant(subject);
            return false;
        }

        public bool Contains(InteractionIdentity subject)
        {
            return IsActive && subject != null && occupants.Contains(subject);
        }

        /// <summary>
        /// Editor verification seam. It uses the same validated membership
        /// operation as a real trigger enter and cannot grant eligibility by
        /// supplying a separate boolean or provenance flag.
        /// </summary>
        public bool RegisterOccupantForVerification(InteractionIdentity subject)
        {
            return RegisterOccupant(subject);
        }

        /// <summary>
        /// Editor verification seam corresponding to a real trigger exit.
        /// </summary>
        public bool UnregisterOccupantForVerification(InteractionIdentity subject)
        {
            return RemoveOccupant(subject);
        }

        public void Deactivate()
        {
            if (!configured || !isActive)
            {
                return;
            }

            isActive = false;
            if (territoryCollider != null)
            {
                territoryCollider.enabled = false;
            }

            if (occupants.Count > 0)
            {
                InteractionIdentity[] exiting = new InteractionIdentity[occupants.Count];
                occupants.CopyTo(exiting);
                for (int index = 0; index < exiting.Length; index++)
                {
                    RemoveOccupant(exiting[index]);
                }
            }

            network?.UnregisterTerritory(this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            RegisterOccupant(ResolveIdentity(other));
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            RemoveOccupant(ResolveIdentity(other));
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void OnDestroy()
        {
            Deactivate();
            UnsubscribeAnchor();
        }

        private void HandleAnchorStateChanged(ClaimAnchor changedAnchor, bool active)
        {
            if (changedAnchor == anchor && !active)
            {
                Deactivate();
            }
        }

        private void HandleAnchorDestroyed(ClaimAnchor destroyedAnchor)
        {
            if (destroyedAnchor == anchor)
            {
                Deactivate();
            }
        }

        private bool IsWithinBounds(Vector3 point)
        {
            Vector2 delta = (Vector2)(point - transform.position);
            return delta.sqrMagnitude <= radius * radius;
        }

        private static InteractionIdentity ResolveIdentity(Collider2D other)
        {
            return other != null ? other.GetComponentInParent<InteractionIdentity>() : null;
        }

        private bool RemoveOccupant(InteractionIdentity subject)
        {
            if (subject != null && occupants.Remove(subject))
            {
                EmitBoundaryEvent(InteractionEventKind.ExitedTerritory, subject);
                return true;
            }

            return false;
        }

        private void EmitBoundaryEvent(InteractionEventKind eventKind, InteractionIdentity subject)
        {
            if (anchor == null || anchor.Identity == null || subject == null)
            {
                return;
            }

            InteractionEventBus.Emit(new InteractionEvent(
                eventKind,
                anchor.Identity,
                subject.gameObject,
                subject.Traits,
                anchor.AuthorityStrength,
                previousOwnership: subject.Ownership,
                currentOwnership: subject.Ownership,
                provenance: ClaimProvenance.InsideClaimedTerritory));
        }

        private void UnsubscribeAnchor()
        {
            if (anchor != null)
            {
                anchor.ActiveStateChanged -= HandleAnchorStateChanged;
                anchor.AnchorDestroyed -= HandleAnchorDestroyed;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsActive ? new Color(0.8f, 0.2f, 1f, 0.8f) : new Color(0.35f, 0.35f, 0.35f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, radius);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (radius + 0.2f),
                "Claim Territory\n" + (IsActive ? "Active" : "Inactive")
                + " | Occupants " + occupants.Count);
#endif
        }
    }

    /// <summary>Explicit request for a reusable, temporary Claim Crystal.</summary>
    public readonly struct ClaimCrystalManifestationRequest
    {
        public ClaimCrystalManifestationRequest(
            ClaimAuthorityNetwork network,
            GameObject authoritySource,
            Vector2 position,
            int authorityStrength,
            float territoryRadius,
            float lifetime = 0f,
            int durability = 3)
        {
            Network = network;
            AuthoritySource = authoritySource;
            Position = position;
            AuthorityStrength = Mathf.Max(1, authorityStrength);
            TerritoryRadius = Mathf.Max(0.1f, territoryRadius);
            Lifetime = Mathf.Max(0f, lifetime);
            Durability = Mathf.Max(1, durability);
        }

        public ClaimAuthorityNetwork Network { get; }
        public GameObject AuthoritySource { get; }
        public Vector2 Position { get; }
        public int AuthorityStrength { get; }
        public float TerritoryRadius { get; }
        public float Lifetime { get; }
        public int Durability { get; }
    }

    /// <summary>
    /// Reusable manifestation service. It creates no boss AI, searches no
    /// scenes, and leaves existing crystal prefabs/assets untouched.
    /// </summary>
    public static class ClaimCrystalManifestation
    {
        public static ClaimCrystal Manifest(ClaimCrystalManifestationRequest request)
        {
            if (request.Network == null || !request.Network.CanMaintainAuthority)
            {
                Debug.LogError("[Cave] Claim Crystal Manifestation requires an active Claim Authority Network.");
                return null;
            }

            if (request.AuthoritySource != null
                && request.AuthoritySource != request.Network.AuthoritySource)
            {
                Debug.LogError("[Cave] Claim Crystal Manifestation source must match its Claim Authority Network.");
                return null;
            }

            GameObject crystalObject = new GameObject("Claim Crystal");
            crystalObject.transform.position = request.Position;
            crystalObject.AddComponent<InteractionIdentity>();
            ClaimAnchor anchor = crystalObject.AddComponent<ClaimAnchor>();
            anchor.InitializeRuntime(request.AuthorityStrength, request.Network);
            ClaimCrystalPresentation presentation = crystalObject.AddComponent<ClaimCrystalPresentation>();
            presentation.Configure(request.AuthorityStrength);

            Damageable damageable = crystalObject.AddComponent<Damageable>();
            damageable.SetRuntimeMaximumHealth(request.Durability, true);
            CircleCollider2D hurtCollider = crystalObject.AddComponent<CircleCollider2D>();
            hurtCollider.isTrigger = true;
            hurtCollider.radius = 0.28f;

            ClaimCrystal crystal = crystalObject.AddComponent<ClaimCrystal>();
            GameObject territoryObject = new GameObject("Claimed Territory");
            territoryObject.transform.SetParent(crystalObject.transform, false);
            ClaimedTerritory territory = territoryObject.AddComponent<ClaimedTerritory>();
            territory.Configure(anchor, request.Network, request.TerritoryRadius);
            crystal.Initialize(anchor, territory, damageable, request.Lifetime);
            return crystal;
        }
    }

    /// <summary>
    /// The first physical Claim Anchor. Its Damageable component makes it
    /// destructible, while its linked territory is retired on death, expiry, or
    /// owner cleanup. It does not alter any projectile or combat behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ClaimAnchor), typeof(Damageable))]
    public sealed class ClaimCrystal : MonoBehaviour
    {
        private ClaimAnchor anchor;
        private ClaimedTerritory territory;
        private Damageable damageable;
        private bool initialized;
        private bool retired;

        public ClaimAnchor Anchor => anchor;
        public ClaimedTerritory Territory => territory;
        public bool IsActive => initialized && !retired && anchor != null && anchor.IsActive;

        public void Initialize(
            ClaimAnchor ownerAnchor,
            ClaimedTerritory claimedTerritory,
            Damageable crystalDamageable,
            float lifetime)
        {
            anchor = ownerAnchor;
            territory = claimedTerritory;
            damageable = crystalDamageable;
            initialized = anchor != null && territory != null && damageable != null;
            if (!initialized)
            {
                Debug.LogError("[Cave] Claim Crystal initialization is missing a required authority component.", this);
                return;
            }

            damageable.Died -= HandleDestroyed;
            damageable.Died += HandleDestroyed;
            if (lifetime > 0f)
            {
                Invoke(nameof(Expire), lifetime);
            }
        }

        /// <summary>
        /// Retires this crystal's Claim authority while preserving normal
        /// runtime object lifetime ownership with its caller. The territory
        /// and anchor use the same cleanup path as destruction/expiry; callers
        /// may subsequently destroy the GameObject using the environment-
        /// appropriate Unity destruction API.
        /// </summary>
        public void RetireAuthority()
        {
            Retire(true);
        }

        private void OnDisable()
        {
            if (initialized && !retired)
            {
                Retire(false);
            }
        }

        private void OnDestroy()
        {
            if (damageable != null)
            {
                damageable.Died -= HandleDestroyed;
            }

            if (initialized && !retired)
            {
                Retire(true);
            }
        }

        private void HandleDestroyed()
        {
            Retire(true);
            Destroy(gameObject);
        }

        private void Expire()
        {
            Retire(true);
            Destroy(gameObject);
        }

        private void Retire(bool reportDestroyed)
        {
            if (retired)
            {
                return;
            }

            retired = true;
            CancelInvoke(nameof(Expire));
            territory?.Deactivate();
            if (reportDestroyed && anchor != null)
            {
                anchor.NotifyDestroyed();
            }
            else
            {
                anchor?.Deactivate();
            }
        }
    }

    /// <summary>
    /// Deliberately temporary runtime presentation for a manifested crystal.
    /// It owns no collider or authority state and can be replaced by final art
    /// without changing Claim behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ClaimCrystalPresentation : MonoBehaviour
    {
        private Material runtimeMaterial;

        public void Configure(int authorityStrength)
        {
            LineRenderer line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = 0.045f;
            line.endWidth = 0.045f;
            line.sortingOrder = 20;
            float scale = Mathf.Clamp(0.22f + authorityStrength * 0.025f, 0.22f, 0.4f);
            line.SetPosition(0, new Vector3(0f, scale, 0f));
            line.SetPosition(1, new Vector3(scale, 0f, 0f));
            line.SetPosition(2, new Vector3(0f, -scale, 0f));
            line.SetPosition(3, new Vector3(-scale, 0f, 0f));
            line.startColor = new Color(0.85f, 0.35f, 1f, 1f);
            line.endColor = new Color(0.45f, 0.8f, 1f, 0.9f);

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader);
                line.sharedMaterial = runtimeMaterial;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }
    }
}
