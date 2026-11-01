using Cave.InputSystem;
using UnityEngine;

namespace Cave.Projectiles
{
    public static class ManifestationOriginVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!Mathf.Approximately(ManifestationOriginPolicy.ResolveUpkeep(
                    ManifestationOrigin.Body, 3, 8f, 2f), 0f)
                || !Mathf.Approximately(ManifestationOriginPolicy.ResolveUpkeep(
                    ManifestationOrigin.Anchor, 1, 8f, 2f), 2f)
                || !Mathf.Approximately(ManifestationOriginPolicy.ResolveUpkeep(
                    ManifestationOrigin.Anchor, 2, 8f, 2f), 2.5f)
                || !Mathf.Approximately(ManifestationOriginPolicy.ResolveUpkeep(
                    ManifestationOrigin.Anchor, 3, 8f, 2f), 3f)
                || !Mathf.Approximately(ManifestationOriginPolicy.ResolveUpkeep(
                    ManifestationOrigin.Orbital, 1, 8f, 2f), 2f)
                || !Mathf.Approximately(ManifestationOriginPolicy.ResolveUpkeep(
                    ManifestationOrigin.Orbital, 2, 8f, 2f), 2.5f)
                || !Mathf.Approximately(ManifestationOriginPolicy.ResolveUpkeep(
                    ManifestationOrigin.Orbital, 3, 8f, 2f), 3f))
            {
                failure = "Manifestation upkeep must keep Body free and resolve Anchor/Orbital to 2/2.5/3 mana per second.";
                return false;
            }

            Vector2 direction = ManifestationOriginPolicy.ResolveLaunchDirection(
                new Vector2(-4f, 3f), new Vector2(2f, 3f));
            if ((direction - Vector2.right).sqrMagnitude > .0001f
                || ManifestationOriginPolicy.ResolveLaunchDirection(Vector2.one, Vector2.one) != Vector2.zero)
            {
                failure = "Manifestation launch direction is not origin-to-player with a closed zero-length case.";
                return false;
            }

            float start = ManifestationOriginPolicy.ResolveOrbitalHorizontalOffset(0f, 6f, 10f);
            float rightEndpoint = ManifestationOriginPolicy.ResolveOrbitalHorizontalOffset(1.5f, 6f, 10f);
            float centerReturn = ManifestationOriginPolicy.ResolveOrbitalHorizontalOffset(3f, 6f, 10f);
            float leftEndpoint = ManifestationOriginPolicy.ResolveOrbitalHorizontalOffset(4.5f, 6f, 10f);
            if (Mathf.Abs(start) > .0001f || Mathf.Abs(rightEndpoint - 10f) > .0001f
                || Mathf.Abs(centerReturn) > .0001f || Mathf.Abs(leftEndpoint + 10f) > .0001f)
            {
                failure = "Orbital does not preserve its approved six-second sinusoidal ±10 path.";
                return false;
            }

            InputBindings defaults = InputBindings.CreateDefault();
            if (defaults.GetBinding(GameAction.RecallManifestation).Primary != KeyCode.G)
            {
                failure = "Recall Manifestation default binding must remain G.";
                return false;
            }
            foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
            {
                if (action == GameAction.RecallManifestation) continue;
                KeyBinding binding = defaults.GetBinding(action);
                if (binding.Primary == KeyCode.G || binding.Secondary == KeyCode.G)
                {
                    failure = "Recall Manifestation default G conflicts with " + action + ".";
                    return false;
                }
            }

            failure = null;
            return true;
        }
    }
}
