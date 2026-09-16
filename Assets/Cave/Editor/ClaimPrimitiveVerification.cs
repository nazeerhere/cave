using Cave.Interactions;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor-only, non-production proof for the Claim primitives.</summary>
    public static class ClaimPrimitiveVerification
    {
        [MenuItem("Tools/Cave/Verification/Run Claim Primitive Verification")]
        private static void RunFromMenu()
        {
            if (TryVerify(out string report))
            {
                Debug.Log("[Cave] Claim primitive verification passed: " + report);
                return;
            }

            Debug.LogError("[Cave] Claim primitive verification failed: " + report);
        }

        public static bool TryVerify(out string report)
        {
            GameObject targetObject = null;
            ClaimAnchor anchor = null;
            int ownershipChangedEvents = 0;
            int resolvedEvents = 0;
            System.Action<InteractionEvent> listener = interactionEvent =>
            {
                if (interactionEvent.Kind == InteractionEventKind.OwnershipChanged)
                {
                    ownershipChangedEvents++;
                }
                else if (interactionEvent.Kind == InteractionEventKind.ClaimResolved)
                {
                    resolvedEvents++;
                }
            };

            InteractionEventBus.EventEmitted += listener;
            try
            {
                anchor = ClaimAnchor.CreateRuntime(Vector2.zero, 2);
                targetObject = new GameObject("Claim Verification Target");
                InteractionIdentity target = InteractionRuntime.TrackSpawn(
                    targetObject,
                    InteractionTraits.Projectile | InteractionTraits.Moving,
                    InteractionOwnership.Neutral,
                    null,
                    true);

                ClaimResult valid = ClaimResolver.Resolve(new ClaimAttempt(
                    anchor.Identity,
                    target,
                    ClaimProvenance.ExistingClaimAnchor,
                    2));
                ClaimResult invalid = ClaimResolver.Resolve(new ClaimAttempt(
                    anchor.Identity,
                    target,
                    ClaimProvenance.None,
                    3));

                bool passed = valid.Succeeded
                    && target.Ownership == InteractionOwnership.Claim
                    && invalid.Rejection == ClaimRejectionReason.InvalidProvenance
                    && !invalid.Succeeded
                    && ownershipChangedEvents == 1
                    && resolvedEvents == 2;
                report = passed
                    ? "valid ExistingClaimAnchor claim changed Neutral -> Claim; invalid None provenance was rejected; ownership and resolution events emitted."
                    : "valid=" + valid + "; invalid=" + invalid
                        + "; ownershipEvents=" + ownershipChangedEvents
                        + "; resolvedEvents=" + resolvedEvents + ".";
                return passed;
            }
            finally
            {
                InteractionEventBus.EventEmitted -= listener;
                if (targetObject != null)
                {
                    Object.DestroyImmediate(targetObject);
                }

                if (anchor != null)
                {
                    Object.DestroyImmediate(anchor.gameObject);
                }
            }
        }
    }
}
