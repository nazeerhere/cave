using Cave.Axioms;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Focused coverage for explicit carrier registration and bounded localized target discovery.</summary>
    public static class DomainRuntimeWorldBridgeVerification
    {
        public static bool TryRunAll(out string failure)
        {
            GameObject inside = new GameObject("DomainRuntimeBridgeInside");
            GameObject outside = new GameObject("DomainRuntimeBridgeOutside");
            try
            {
                inside.transform.position = Vector3.zero;
                outside.transform.position = new Vector3(10f, 0f, 0f);
                AxiomRuntimeState insideRuntime = inside.AddComponent<AxiomRuntimeState>();
                outside.AddComponent<AxiomRuntimeState>();
                // Batch verification runs outside Play Mode, where Awake does
                // not auto-run for freshly added components. Production Axiom
                // runtime performs this same EnsureOn call from Awake/OnEnable.
                DomainRuntimeCarrierIdentity.EnsureOn(inside);
                DomainRuntimeCarrierIdentity.EnsureOn(outside);
                DomainRuntimeCarrierIdentity insideIdentity = inside.GetComponent<DomainRuntimeCarrierIdentity>();
                DomainRuntimeCarrierIdentity outsideIdentity = outside.GetComponent<DomainRuntimeCarrierIdentity>();
                if (insideIdentity != null) DomainRuntimeCarrierRegistry.Register(insideIdentity);
                if (outsideIdentity != null) DomainRuntimeCarrierRegistry.Register(outsideIdentity);
                DomainRuntimeCarrierIdentity lookedUp;
                bool found = insideIdentity != null
                    && DomainRuntimeCarrierRegistry.TryGet(insideIdentity.CarrierId, out lookedUp);
                DomainRuntimeCarrierRegistrationResult registration = insideIdentity != null
                    ? DomainRuntimeCarrierRegistry.Register(insideIdentity)
                    : DomainRuntimeCarrierRegistrationResult.InvalidCarrier;
                if (insideIdentity == null || outsideIdentity == null
                    || !found || registration != DomainRuntimeCarrierRegistrationResult.AlreadyRegistered)
                {
                    failure = "A live Axiom authority did not register exactly once (found=" + found
                        + ", registration=" + registration + ", runtime=" + (insideRuntime != null)
                        + ", runtimeActive=" + (insideRuntime != null && insideRuntime.isActiveAndEnabled)
                        + ", identityActive=" + (insideIdentity != null && insideIdentity.isActiveAndEnabled)
                        + ", objectActive=" + inside.activeInHierarchy
                        + ", id=" + (insideIdentity != null ? insideIdentity.CarrierId.ToString() : "none")
                        + ", count=" + DomainRuntimeCarrierRegistry.Count + ").";
                    return false;
                }

                DomainLaw law;
                LawValidationResult validation;
                if (!DomainLaw.TryCreate(LawExpression.Trap, LawPhenomenon.Heat,
                    LawTerritoryPrinciple.Reversal, out law, out validation))
                {
                    failure = "Could not create a valid Trap law for the world bridge verification.";
                    return false;
                }
                LocalizedDomainCarrier carrier = new LocalizedDomainCarrier("child", "parent", "owner",
                    new BoundTrapLaw("law", law), Vector2.zero, 2f, 10f);
                LocalizedDomainTargetResolution resolved = LocalizedDomainTargetResolver.Resolve(carrier, 1f);
                bool insideOnly = resolved.Succeeded && resolved.Targets.Count == 1
                    && resolved.Targets[0].CarrierId.Equals(insideIdentity.CarrierId)
                    && resolved.Targets[0].Snapshot != null;
                if (!insideOnly)
                {
                    failure = "Localized target resolution was not radius-bounded and deterministically ordered.";
                    return false;
                }

                inside.SetActive(false);
                bool unregistered = !DomainRuntimeCarrierRegistry.TryGet(insideIdentity.CarrierId, out _)
                    && DomainRuntimeCarrierRegistry.Count >= 1;
                failure = unregistered ? null : "Disabled carrier remained registered as a live runtime target.";
                return unregistered;
            }
            finally
            {
                Object.DestroyImmediate(inside);
                Object.DestroyImmediate(outside);
            }
        }
    }
}
