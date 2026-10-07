using Cave.Interactions;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Proves the Claim adapter is observational while a Domain/key binding is unavailable.</summary>
    public static class ClaimDomainAuthorityAdapterVerification
    {
        public static bool TryRunAll(out string failure)
        {
            DomainConflictAuthorityEvidence missingEvidence;
            ClaimDomainAuthorityAvailability missingAvailability;
            bool missingNetwork = !ClaimDomainAuthorityAdapter.TryCreateEvidence(
                "domain-a", new DomainConflictContestKey(new PhenomenonCarrierId("carrier"), LawPhenomenon.Heat),
                null, out missingEvidence, out missingAvailability)
                && missingEvidence == null
                && missingAvailability == ClaimDomainAuthorityAvailability.MissingNetwork;

            GameObject authority = null;
            try
            {
                authority = new GameObject("Claim Domain Authority Adapter Verification");
                InteractionIdentity identity = InteractionRuntime.TrackSpawn(
                    authority, InteractionTraits.None, InteractionOwnership.Claim, authority, false, 7);
                ClaimAuthorityNetwork network = authority.AddComponent<ClaimAuthorityNetwork>();
                network.InitializeRuntime(authority);

                InteractionOwnership ownershipBefore = identity.Ownership;
                int strengthBefore = identity.AuthorityStrength;
                ClaimLoadSnapshot loadBefore = network.Load;
                DomainConflictAuthorityEvidence evidence;
                ClaimDomainAuthorityAvailability availability;
                bool produced = ClaimDomainAuthorityAdapter.TryCreateEvidence(
                    "domain-a", new DomainConflictContestKey(new PhenomenonCarrierId("carrier"), LawPhenomenon.Heat),
                    network, out evidence, out availability);

                bool unchanged = identity.Ownership == ownershipBefore
                    && identity.AuthorityStrength == strengthBefore
                    && network.Load.ActiveAnchors == loadBefore.ActiveAnchors
                    && network.Load.ActiveTerritories == loadBefore.ActiveTerritories
                    && network.Load.ClaimedObjects == loadBefore.ClaimedObjects;
                bool pass = missingNetwork && !produced && evidence == null
                    && availability == ClaimDomainAuthorityAvailability.NoDomainContestBinding && unchanged;
                failure = pass ? null : "Claim Domain authority adapter verification failed.";
                return pass;
            }
            finally
            {
                if (authority != null) Object.DestroyImmediate(authority);
            }
        }
    }
}
