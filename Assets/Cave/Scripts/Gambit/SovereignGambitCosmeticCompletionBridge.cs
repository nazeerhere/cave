using Cave.Player;
using UnityEngine;

namespace Cave.Gambit
{
    /// <summary>
    /// Optional presentation-progression bridge for a real Gambit host. It listens to the
    /// authoritative runtime PhaseChanged event and never decides a match outcome itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SovereignGambitCosmeticCompletionBridge : MonoBehaviour
    {
        private SovereignGambitRuntime runtime;
        private PlayerSkinCosmetics skins;

        public void Bind(SovereignGambitRuntime match, PlayerSkinCosmetics playerSkins)
        {
            if (runtime != null)
            {
                runtime.PhaseChanged -= HandlePhaseChanged;
            }
            runtime = match;
            skins = playerSkins;
            if (runtime != null)
            {
                runtime.PhaseChanged += HandlePhaseChanged;
                if (runtime.Phase == GambitMatchPhase.Victory)
                {
                    skins?.NotifySovereignGambitCompleted();
                }
            }
        }

        private void HandlePhaseChanged(GambitMatchPhase phase)
        {
            if (phase == GambitMatchPhase.Victory)
            {
                skins?.NotifySovereignGambitCompleted();
            }
        }

        private void OnDestroy()
        {
            if (runtime != null)
            {
                runtime.PhaseChanged -= HandlePhaseChanged;
            }
        }
    }
}
