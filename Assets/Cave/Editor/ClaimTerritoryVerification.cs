using Cave.Interactions;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>
    /// Editor-only proof that territorial provenance is based on real, bounded
    /// occupancy rather than a caller-provided flag. It creates no assets,
    /// prefabs, or scenes and destroys all temporary objects before returning.
    /// </summary>
    public static class ClaimTerritoryVerification
    {
        [MenuItem("Tools/Cave/Verification/Run Claim Territory Verification")]
        private static void RunFromMenu()
        {
            if (TryVerify(out string report))
            {
                Debug.Log("[Cave] Claim territory verification passed: " + report);
                return;
            }

            Debug.LogError("[Cave] Claim territory verification failed: " + report);
        }

        public static bool TryVerify(out string report)
        {
            GameObject authorityObject = null;
            GameObject targetObject = null;
            GameObject postExitTargetObject = null;
            ClaimCrystal crystal = null;
            int enteredEvents = 0;
            int exitedEvents = 0;
            int ownershipChangedEvents = 0;
            System.Action<InteractionEvent> listener = interactionEvent =>
            {
                switch (interactionEvent.Kind)
                {
                    case InteractionEventKind.EnteredTerritory:
                        enteredEvents++;
                        break;
                    case InteractionEventKind.ExitedTerritory:
                        exitedEvents++;
                        break;
                    case InteractionEventKind.OwnershipChanged:
                        ownershipChangedEvents++;
                        break;
                }
            };

            InteractionEventBus.EventEmitted += listener;
            try
            {
                authorityObject = new GameObject("Claim Territory Verification Authority");
                InteractionRuntime.TrackSpawn(
                    authorityObject,
                    InteractionTraits.None,
                    InteractionOwnership.Claim,
                    authorityObject,
                    false,
                    3);
                ClaimAuthorityNetwork network = authorityObject.AddComponent<ClaimAuthorityNetwork>();
                network.InitializeRuntime(authorityObject);
                crystal = ClaimCrystalManifestation.Manifest(new ClaimCrystalManifestationRequest(
                    network,
                    authorityObject,
                    Vector2.zero,
                    3,
                    2f));

                targetObject = new GameObject("Claim Territory Verification Projectile");
                targetObject.transform.position = new Vector2(4f, 0f);
                InteractionIdentity target = InteractionRuntime.TrackSpawn(
                    targetObject,
                    InteractionTraits.Projectile | InteractionTraits.Moving | InteractionTraits.ManaPowered,
                    InteractionOwnership.Player,
                    authorityObject,
                    true,
                    1);

                ClaimResult outside = ClaimResolver.Resolve(new ClaimAttempt(
                    crystal.Anchor.Identity,
                    target,
                    ClaimProvenance.InsideClaimedTerritory,
                    3));

                targetObject.transform.position = Vector2.zero;
                bool gainedEvidence = crystal.Territory.RegisterOccupantForVerification(target);
                bool cachedEvidence = crystal.Territory.Network.HasTerritorialEvidence(
                    crystal.Anchor,
                    target);
                ClaimResult inside = ClaimResolver.Resolve(new ClaimAttempt(
                    crystal.Anchor.Identity,
                    target,
                    ClaimProvenance.InsideClaimedTerritory,
                    3));

                bool originalTargetExited = crystal.Territory.UnregisterOccupantForVerification(target);
                postExitTargetObject = new GameObject("Claim Territory Verification Post-Exit Projectile");
                postExitTargetObject.transform.position = Vector2.zero;
                InteractionIdentity postExitTarget = InteractionRuntime.TrackSpawn(
                    postExitTargetObject,
                    InteractionTraits.Projectile | InteractionTraits.Moving | InteractionTraits.ManaPowered,
                    InteractionOwnership.Player,
                    authorityObject,
                    true,
                    1);
                bool postExitTargetEntered = crystal.Territory.RegisterOccupantForVerification(postExitTarget);
                bool postExitTargetExited = crystal.Territory.UnregisterOccupantForVerification(postExitTarget);
                bool stillEligibleAfterExit = crystal.Territory.Network.HasTerritorialEvidence(
                    crystal.Anchor,
                    postExitTarget);
                ClaimResult afterExit = ClaimResolver.Resolve(new ClaimAttempt(
                    crystal.Anchor.Identity,
                    postExitTarget,
                    ClaimProvenance.InsideClaimedTerritory,
                    4));

                crystal.Anchor.Deactivate();
                ClaimResult inactiveAnchor = ClaimResolver.Resolve(new ClaimAttempt(
                    crystal.Anchor.Identity,
                    postExitTarget,
                    ClaimProvenance.InsideClaimedTerritory,
                    5));
                bool territoryRemovedOnDeactivate = !crystal.Territory.IsActive;
                Object.DestroyImmediate(crystal.gameObject);
                crystal = null;
                bool anchorRemovedOnDestroy = network.ActiveAnchorCount == 0
                    && network.ActiveTerritoryCount == 0;

                bool passed = outside.Rejection == ClaimRejectionReason.ProvenanceUnavailable
                    && gainedEvidence
                    && cachedEvidence
                    && inside.Succeeded
                    && target.Ownership == InteractionOwnership.Claim
                    && originalTargetExited
                    && postExitTargetEntered
                    && postExitTargetExited
                    && !stillEligibleAfterExit
                    && afterExit.Rejection == ClaimRejectionReason.ProvenanceUnavailable
                    && afterExit.PreviousOwner == InteractionOwnership.Player
                    && territoryRemovedOnDeactivate
                    && anchorRemovedOnDestroy
                    && inactiveAnchor.Rejection == ClaimRejectionReason.ProvenanceUnavailable
                    && enteredEvents == 2
                    && exitedEvents == 2
                    && ownershipChangedEvents == 1
                    && network.ClaimedObjectCount == 1;
                report = passed
                    ? "anchor/territory created; outside projectile was rejected; entry created territorial evidence; explicit Claim changed Player -> Claim once; exit, deactivation, and destruction removed territory authority."
                    : "outside=" + outside + "; inside=" + inside + "; afterExit=" + afterExit
                        + "; inactive=" + inactiveAnchor + "; entered=" + enteredEvents
                        + "; exited=" + exitedEvents + "; ownershipChanged=" + ownershipChangedEvents
                        + "; claimedObjects=" + network.ClaimedObjectCount
                        + "; gainedEvidence=" + gainedEvidence
                        + "; cachedEvidence=" + cachedEvidence
                        + "; originalTargetExited=" + originalTargetExited
                        + "; postExitTargetEntered=" + postExitTargetEntered
                        + "; postExitTargetExited=" + postExitTargetExited + ".";
                return passed;
            }
            finally
            {
                InteractionEventBus.EventEmitted -= listener;
                if (targetObject != null)
                {
                    Object.DestroyImmediate(targetObject);
                }

                if (postExitTargetObject != null)
                {
                    Object.DestroyImmediate(postExitTargetObject);
                }

                if (crystal != null)
                {
                    Object.DestroyImmediate(crystal.gameObject);
                }

                if (authorityObject != null)
                {
                    Object.DestroyImmediate(authorityObject);
                }
            }
        }
    }
}
