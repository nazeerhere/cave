using UnityEngine;

namespace Cave.Audio
{
    public enum CaveMusicState
    {
        Starting,
        Exploration,
        Combat,
        IntenseCombat,
        Heart
    }

    [CreateAssetMenu(menuName = "Cave/Audio/Music Library", fileName = "CaveMusicLibrary")]
    public sealed class CaveMusicLibrary : ScriptableObject
    {
        [SerializeField] private AudioClip starting;
        [SerializeField] private AudioClip exploration;
        [SerializeField] private AudioClip combat;
        [SerializeField] private AudioClip intenseCombat;
        [SerializeField] private AudioClip heart;

        public AudioClip GetClip(CaveMusicState state)
        {
            switch (state)
            {
                case CaveMusicState.Starting: return starting;
                case CaveMusicState.Exploration: return exploration;
                case CaveMusicState.Combat: return combat;
                case CaveMusicState.IntenseCombat: return intenseCombat;
                case CaveMusicState.Heart: return heart;
                default: return exploration;
            }
        }
    }
}
