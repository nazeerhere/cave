using System;
using UnityEngine;

namespace Cave.Interactions
{
    /// <summary>Every successful Claim must have one of these explicit reasons.</summary>
    public enum ClaimProvenance
    {
        None,
        CreatedByClaimOwner,
        TouchedOrCaptured,
        InsideClaimedTerritory,
        GrantedByConsent,
        ExistingClaimAnchor,
        /// <summary>Explicit reclamation of an object with retained Claim authority history.</summary>
        Repossession
    }

    public enum ClaimRejectionReason
    {
        None,
        MissingClaimant,
        MissingTarget,
        ClaimantLacksClaimAuthority,
        TargetIsNotClaimable,
        InvalidProvenance,
        ProvenanceUnavailable,
        ExistingAuthorityPrevails,
        ResistancePrevails
    }

    /// <summary>Immutable request for the sole initial authority-changing operation.</summary>
    public readonly struct ClaimAttempt
    {
        public ClaimAttempt(
            InteractionIdentity claimant,
            InteractionIdentity target,
            ClaimProvenance provenance,
            int authorityStrength = 0)
        {
            Claimant = claimant;
            Target = target;
            Provenance = provenance;
            AuthorityStrength = Mathf.Max(0, authorityStrength);
        }

        public InteractionIdentity Claimant { get; }
        public InteractionIdentity Target { get; }
        public ClaimProvenance Provenance { get; }
        public int AuthorityStrength { get; }
    }

    /// <summary>Inspectable result for UI, debugging, and future counterplay.</summary>
    public readonly struct ClaimResult
    {
        internal ClaimResult(
            ClaimAttempt attempt,
            bool succeeded,
            ClaimRejectionReason rejection,
            InteractionOwnership previousOwner,
            InteractionOwnership currentOwner)
        {
            Attempt = attempt;
            Succeeded = succeeded;
            Rejection = rejection;
            PreviousOwner = previousOwner;
            CurrentOwner = currentOwner;
        }

        public ClaimAttempt Attempt { get; }
        public bool Succeeded { get; }
        public ClaimRejectionReason Rejection { get; }
        public InteractionOwnership PreviousOwner { get; }
        public InteractionOwnership CurrentOwner { get; }

        public override string ToString()
        {
            return "Claim " + (Succeeded ? "succeeded" : "rejected")
                + ": " + PreviousOwner + " -> " + CurrentOwner
                + " via " + Attempt.Provenance
                + (Succeeded ? string.Empty : " (" + Rejection + ")");
        }
    }

    /// <summary>
    /// Future contest hook. It is intentionally inert until a claimant or
    /// Domain system supplies resistance through this explicit component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClaimResistance : MonoBehaviour
    {
        [SerializeField, Min(0)] private int resistanceStrength;

        public int ResistanceStrength => resistanceStrength;

        /// <summary>Explicit runtime/test setup without reflection or serialized-field mutation.</summary>
        public void ConfigureRuntimeResistance(int strength)
        {
            resistanceStrength = Mathf.Max(0, strength);
        }
    }

    /// <summary>
    /// Minimal authority source for future False God crystals and territory.
    /// It has no radius, terrain effect, or final art in this foundation pass.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionIdentity))]
    public sealed class ClaimAnchor : MonoBehaviour
    {
        [SerializeField] private bool isActive = true;
        [SerializeField, Min(1)] private int authorityStrength = 1;

        private InteractionIdentity identity;
        private ClaimAuthorityNetwork authorityNetwork;
        private bool initialized;
        private bool destroyedReported;

        public event Action<ClaimAnchor, bool> ActiveStateChanged;
        public event Action<ClaimAnchor> AnchorDestroyed;

        public bool IsActive => isActive;
        public int AuthorityStrength => authorityStrength;
        public InteractionIdentity Identity => identity;
        public ClaimAuthorityNetwork AuthorityNetwork => authorityNetwork;

        private void Awake()
        {
            identity = GetComponent<InteractionIdentity>();
        }

        public void InitializeRuntime(int strength, ClaimAuthorityNetwork network = null)
        {
            authorityStrength = Mathf.Max(1, strength);
            isActive = true;
            destroyedReported = false;
            authorityNetwork = network;
            identity = InteractionRuntime.TrackSpawn(
                gameObject,
                InteractionTraits.None,
                InteractionOwnership.Claim,
                authorityNetwork != null ? authorityNetwork.AuthoritySource : gameObject,
                false,
                authorityStrength);
            initialized = true;
            authorityNetwork?.RegisterAnchor(this);
            ActiveStateChanged?.Invoke(this, true);
        }

        public void Deactivate()
        {
            if (!isActive)
            {
                return;
            }

            isActive = false;
            ActiveStateChanged?.Invoke(this, false);
        }

        /// <summary>
        /// Retires a runtime anchor that was destroyed or expired while still
        /// allowing ordinary deactivation to remain a non-destruction state.
        /// </summary>
        public void NotifyDestroyed()
        {
            Deactivate();
            if (initialized && !destroyedReported && identity != null)
            {
                destroyedReported = true;
                InteractionRuntime.ReportDestroyed(identity);
            }
        }

        private void OnDestroy()
        {
            NotifyDestroyed();
            authorityNetwork?.UnregisterAnchor(this);
            AnchorDestroyed?.Invoke(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isActive ? new Color(0.85f, 0.25f, 1f, 0.9f) : new Color(0.35f, 0.35f, 0.35f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, 0.32f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.45f,
                "Claim Anchor\n" + (isActive ? "Active" : "Inactive")
                + " | Strength " + authorityStrength
                + " | Owner " + (identity != null ? identity.Ownership.ToString() : "Uninitialized"));
#endif
        }

        public static ClaimAnchor CreateRuntime(
            Vector2 position,
            int strength = 1,
            ClaimAuthorityNetwork network = null)
        {
            GameObject anchorObject = new GameObject("Claim Anchor");
            anchorObject.transform.position = position;
            anchorObject.AddComponent<InteractionIdentity>();
            ClaimAnchor anchor = anchorObject.AddComponent<ClaimAnchor>();
            anchor.InitializeRuntime(strength, network);
            return anchor;
        }
    }

    /// <summary>
    /// Deterministic, narrow resolver for Claim primitives. It is the only
    /// initial path that changes runtime Claim ownership.
    /// </summary>
    public static class ClaimResolver
    {
        public static event Action<ClaimResult> ClaimResolved;

        public static ClaimResult Resolve(ClaimAttempt attempt)
        {
            InteractionOwnership previousOwner = attempt.Target != null
                ? attempt.Target.Ownership
                : InteractionOwnership.Neutral;
            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.ClaimAttempted,
                attempt.Claimant,
                attempt.Target != null ? attempt.Target.gameObject : null,
                attempt.Claimant != null ? attempt.Claimant.Traits : InteractionTraits.None,
                attempt.AuthorityStrength,
                previousOwnership: previousOwner,
                currentOwnership: previousOwner,
                provenance: attempt.Provenance));

            ClaimRejectionReason rejection = Validate(attempt);
            if (rejection != ClaimRejectionReason.None)
            {
                return Complete(attempt, false, rejection, previousOwner, previousOwner);
            }

            int requestedStrength = Mathf.Max(attempt.AuthorityStrength, attempt.Claimant.AuthorityStrength);
            ClaimResistance resistance = attempt.Target.GetComponent<ClaimResistance>();
            if (resistance != null && requestedStrength <= resistance.ResistanceStrength)
            {
                return Complete(
                    attempt,
                    false,
                    ClaimRejectionReason.ResistancePrevails,
                    previousOwner,
                    previousOwner);
            }

            if (previousOwner != InteractionOwnership.Neutral
                && requestedStrength <= attempt.Target.AuthorityStrength)
            {
                return Complete(
                    attempt,
                    false,
                    ClaimRejectionReason.ExistingAuthorityPrevails,
                    previousOwner,
                    previousOwner);
            }

            attempt.Target.ApplyResolvedOwnership(
                InteractionOwnership.Claim,
                attempt.Claimant.gameObject,
                requestedStrength);
            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.OwnershipChanged,
                attempt.Claimant,
                attempt.Target.gameObject,
                attempt.Target.Traits,
                requestedStrength,
                previousOwnership: previousOwner,
                currentOwnership: InteractionOwnership.Claim,
                provenance: attempt.Provenance));
            return Complete(
                attempt,
                true,
                ClaimRejectionReason.None,
                previousOwner,
                InteractionOwnership.Claim);
        }

        private static ClaimRejectionReason Validate(ClaimAttempt attempt)
        {
            if (attempt.Claimant == null)
            {
                return ClaimRejectionReason.MissingClaimant;
            }

            if (attempt.Target == null)
            {
                return ClaimRejectionReason.MissingTarget;
            }

            if (attempt.Claimant.Ownership != InteractionOwnership.Claim)
            {
                return ClaimRejectionReason.ClaimantLacksClaimAuthority;
            }

            if (!attempt.Target.IsClaimable)
            {
                return ClaimRejectionReason.TargetIsNotClaimable;
            }

            if (attempt.Provenance == ClaimProvenance.None)
            {
                return ClaimRejectionReason.InvalidProvenance;
            }

            if (!HasAvailableProvenance(attempt))
            {
                return ClaimRejectionReason.ProvenanceUnavailable;
            }

            return ClaimRejectionReason.None;
        }

        private static bool HasAvailableProvenance(ClaimAttempt attempt)
        {
            switch (attempt.Provenance)
            {
                case ClaimProvenance.ExistingClaimAnchor:
                    ClaimAnchor anchor = attempt.Claimant.GetComponent<ClaimAnchor>();
                    return anchor != null && anchor.IsActive;
                case ClaimProvenance.CreatedByClaimOwner:
                    ClaimAnchor creationAnchor = attempt.Claimant.GetComponent<ClaimAnchor>();
                    return attempt.Target.AuthoritySource == attempt.Claimant.gameObject
                        || (creationAnchor != null
                            && creationAnchor.AuthorityNetwork != null
                            && attempt.Target.AuthoritySource
                                == creationAnchor.AuthorityNetwork.AuthoritySource);
                case ClaimProvenance.InsideClaimedTerritory:
                    ClaimAnchor territorialAnchor = attempt.Claimant.GetComponent<ClaimAnchor>();
                    return territorialAnchor != null
                        && territorialAnchor.IsActive
                        && territorialAnchor.AuthorityNetwork != null
                        && territorialAnchor.AuthorityNetwork.HasTerritorialEvidence(
                            territorialAnchor,
                            attempt.Target);
                case ClaimProvenance.Repossession:
                    ClaimAuthorityNetwork claimantNetwork = attempt.Claimant.GetComponent<ClaimAuthorityNetwork>();
                    return claimantNetwork != null
                        && claimantNetwork.CanMaintainAuthority
                        && attempt.Target.Ownership != InteractionOwnership.Claim
                        && attempt.Target.HasAuthorityHistory(claimantNetwork.AuthoritySource);
                default:
                    // Capture and consent require future explicit evidence;
                    // accepting them before that evidence exists would be arbitrary.
                    return false;
            }
        }

        private static ClaimResult Complete(
            ClaimAttempt attempt,
            bool succeeded,
            ClaimRejectionReason rejection,
            InteractionOwnership previousOwner,
            InteractionOwnership currentOwner)
        {
            ClaimResult result = new ClaimResult(
                attempt,
                succeeded,
                rejection,
                previousOwner,
                currentOwner);
            InteractionEventBus.Emit(new InteractionEvent(
                InteractionEventKind.ClaimResolved,
                attempt.Claimant,
                attempt.Target != null ? attempt.Target.gameObject : null,
                attempt.Target != null ? attempt.Target.Traits : InteractionTraits.None,
                attempt.AuthorityStrength,
                previousOwnership: previousOwner,
                currentOwnership: currentOwner,
                provenance: attempt.Provenance,
                rejection: rejection));
            ClaimResolved?.Invoke(result);
            return result;
        }
    }
}
