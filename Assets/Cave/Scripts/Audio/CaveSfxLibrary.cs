using UnityEngine;

namespace Cave.Audio
{
    public enum CaveSfxCue
    {
        Bonus,
        ButtonHover,
        ButtonPress,
        Explosion,
        Hit,
        Landing,
        Shot,
        Spawn,
        Whoosh
    }

    [CreateAssetMenu(menuName = "Cave/Audio/SFX Library", fileName = "CaveSfxLibrary")]
    public sealed class CaveSfxLibrary : ScriptableObject
    {
        [SerializeField] private AudioClip bonus;
        [SerializeField] private AudioClip buttonHover;
        [SerializeField] private AudioClip buttonPress;
        [SerializeField] private AudioClip explosion;
        [SerializeField] private AudioClip hit;
        [SerializeField] private AudioClip landing;
        [SerializeField] private AudioClip shot;
        [SerializeField] private AudioClip spawn;
        [SerializeField] private AudioClip whoosh;

        public AudioClip GetClip(CaveSfxCue cue)
        {
            switch (cue)
            {
                case CaveSfxCue.Bonus:
                    return bonus;
                case CaveSfxCue.ButtonHover:
                    return buttonHover;
                case CaveSfxCue.ButtonPress:
                    return buttonPress;
                case CaveSfxCue.Explosion:
                    return explosion;
                case CaveSfxCue.Hit:
                    return hit;
                case CaveSfxCue.Landing:
                    return landing;
                case CaveSfxCue.Shot:
                    return shot;
                case CaveSfxCue.Spawn:
                    return spawn;
                case CaveSfxCue.Whoosh:
                    return whoosh;
                default:
                    return null;
            }
        }
    }
}
