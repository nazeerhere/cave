using UnityEngine;

namespace Cave.Audio
{
    /// <summary>Persistent host used only for bounded owner-loop cleanup.</summary>
    public sealed class CaveSfxRuntime : MonoBehaviour
    {
        private void Update()
        {
            CaveSfx.UpdateOwnedLoops();
        }
    }
}
