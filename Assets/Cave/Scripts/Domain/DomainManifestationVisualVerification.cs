using System;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Deterministic coverage for the cosmetic-only tilted-orbit projection.</summary>
    public static class DomainManifestationVisualVerification
    {
        public static bool TryRunAll(out string failure)
        {
            try
            {
                DomainManifestationVisual.OrbitSample right = DomainManifestationVisual.ProjectOrbit(0f, 4f, 35f, 0f);
                DomainManifestationVisual.OrbitSample front = DomainManifestationVisual.ProjectOrbit(Mathf.PI * .5f, 4f, 35f, 0f);
                DomainManifestationVisual.OrbitSample back = DomainManifestationVisual.ProjectOrbit(-Mathf.PI * .5f, 4f, 35f, 0f);
                DomainManifestationVisual.OrbitSample turned = DomainManifestationVisual.ProjectOrbit(0f, 4f, 35f, 90f);

                Require(Near(right.Position.x, 4f) && Near(right.Position.y, 0f), "major axis projection");
                Require(Near(front.Position.x, 0f) && front.Position.y > 0f && front.Position.y < 4f,
                    "tilt projects the minor axis");
                Require(front.Depth > 0f && back.Depth < 0f && Near(front.Depth, -back.Depth),
                    "front/back depth separation");
                Require(Near(turned.Position.x, 0f) && Near(turned.Position.y, 4f), "plane orientation");
                Require(PlayerDomainManifestation.RequestedDrainForDelta(
                    PlayerDomainManifestation.DefaultChargeDrainPerSecond, .5f) == 1f,
                    "visual verification does not alter manifestation authority");

                failure = null;
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static bool Near(float left, float right) { return Mathf.Abs(left - right) < .001f; }
        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Domain manifestation visual verification failed: " + name);
        }
    }
}
