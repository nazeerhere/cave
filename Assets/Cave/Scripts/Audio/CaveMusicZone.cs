using UnityEngine;

namespace Cave.Audio
{
    /// <summary>Optional authored scene hook for boss/Heart music; no collider required.</summary>
    public sealed class CaveMusicZone : MonoBehaviour
    {
        [SerializeField] private CaveMusicState requestedState = CaveMusicState.Exploration;

        private void OnEnable()
        {
            CaveMusicController.Instance?.SetForcedState(requestedState);
        }

        private void OnDisable()
        {
            CaveMusicController.Instance?.SetForcedState(null);
        }
    }
}
