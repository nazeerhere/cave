using Cave.Interactions;

namespace Cave.Domain
{
    public enum ClaimDomainAuthorityAvailability
    {
        None = 0,
        MissingNetwork = 1,
        NetworkInactive = 2,
        NoDomainContestBinding = 3
    }

    /// <summary>
    /// Read-only bridge for a future explicit Claim-to-Domain/key binding. Current Claim data records ownership,
    /// provenance and local strengths, but has no such binding, so it deliberately cannot manufacture a rank.
    /// </summary>
    public static class ClaimDomainAuthorityAdapter
    {
        public static bool TryCreateEvidence(string participantId, DomainConflictContestKey contestKey,
            ClaimAuthorityNetwork network, out DomainConflictAuthorityEvidence evidence,
            out ClaimDomainAuthorityAvailability availability)
        {
            evidence = null;
            if (network == null) { availability = ClaimDomainAuthorityAvailability.MissingNetwork; return false; }
            if (!network.CanMaintainAuthority) { availability = ClaimDomainAuthorityAvailability.NetworkInactive; return false; }
            availability = ClaimDomainAuthorityAvailability.NoDomainContestBinding;
            return false;
        }
    }
}
