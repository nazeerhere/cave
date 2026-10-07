using Cave.Player;

namespace Cave.Domain
{
    /// <summary>Focused pure coverage for the shared rebindable altar/Domain action.</summary>
    public static class DomainActivationInputVerification
    {
        public static bool TryRunAll(out string failure)
        {
            bool valid = !SharedDomainActivationPolicy.IsHoldReached(.59f, .6f)
                && SharedDomainActivationPolicy.IsHoldReached(.6f, .6f)
                && SharedDomainActivationPolicy.ResolveRelease(false)
                    == SharedDomainActivationAction.SummonCurseAltar
                && SharedDomainActivationPolicy.ResolveRelease(true)
                    == SharedDomainActivationAction.None
                && SharedDomainActivationPolicy.ResolveHold(false)
                    == SharedDomainActivationAction.RequestManifestation
                && SharedDomainActivationPolicy.ResolveHold(true)
                    == SharedDomainActivationAction.DismissManifestation;
            failure = valid ? null : "Shared Curse Altar / Domain tap-hold routing was incorrect.";
            return valid;
        }
    }
}
