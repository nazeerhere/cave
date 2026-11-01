using System;
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
        Whoosh,

        // Batch 1 semantic identities. These are appended so the nine legacy
        // serialized cue values retain their original numeric contract.
        PlayerJump,
        PlayerLanding,
        PlayerDash,
        PlayerHurt,
        PlayerDeath,
        PlayerRespawn,
        SwordSwing,
        SpinSwing,
        HeavyCharge,
        HeavyChargeStage,
        HeavyRelease,
        HeavyImpact,
        BlockImpact,
        Parry,
        PerfectParry,
        GuardBreak,
        GroundSmashDescent,
        GroundSmashImpact,

        // Batch 2 shared enemy semantics. Appended only.
        EnemyMeleeSwing,
        EnemyBodyImpact,
        EnemyHeavyImpact,
        EnemyBlockImpact,
        EnemyGuardBreak,
        EnemyStagger,
        EnemyDeath,
        EnemyProjectileLaunch,
        StatusBurnStart,
        StatusSlowStart,
        StatusImmobilizeStart,
        StatusReleased,

        // Batch 3 player-power semantics. Appended only: existing serialized
        // cue values remain stable while profiles can be authored later.
        PlayerProjectileLaunch,
        PlayerProjectileImpact,
        SkillPathHeat,
        SkillPathOrder,
        SkillPathFlow,
        SkillPathMass,
        BraceQuick,
        BraceFull,
        BraceDeep,
        BraceExit,
        StrengthShieldReady,
        StrengthShieldBreak,
        StrengthShieldRecharge,
        FrenzyActivated,
        FrenzySustain,
        FrenzyBreak,
        FrenzyExpired,
        ResonanceStep,
        ResonanceBreak,
        DestabilizedStart,
        DestabilizedEnd,
        Counterphase,
        FrequencyFiltering,
        PhaseOpening,
        LatentThreshold,
        PhaseCollapse,
        TemporalState,
        TemporalRate,
        TemporalAcceleration,
        StateLock,
        RateLock,
        AccelerationLock,
        ControlledEquilibrium,
        Convergence,
        ManifestationThreshold,
        MasteryMilestone
    }

    /// <summary>
    /// Optional metadata for an existing semantic cue. The legacy single-clip
    /// slots below remain the compatibility fallback, so current assets do not
    /// need migration before future batches add variants or policies.
    /// </summary>
    [Serializable]
    public sealed class CaveSfxCueProfile
    {
        [SerializeField] private CaveSfxCue cue;
        [SerializeField] private AudioClip[] variants;
        [SerializeField, Range(0.5f, 1.5f)] private float minimumPitch = 1f;
        [SerializeField, Range(0.5f, 1.5f)] private float maximumPitch = 1f;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(0)] private int maximumConcurrentVoices;

        public CaveSfxCue Cue => cue;
        public AudioClip[] Variants => variants;
        public float MinimumPitch => Mathf.Min(minimumPitch, maximumPitch);
        public float MaximumPitch => Mathf.Max(minimumPitch, maximumPitch);
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public int MaximumConcurrentVoices => Mathf.Max(0, maximumConcurrentVoices);

        public bool HasVariants
        {
            get
            {
                if (variants == null) return false;
                for (int i = 0; i < variants.Length; i++)
                    if (variants[i] != null) return true;
                return false;
            }
        }
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
        [Header("Optional Per-Cue Playback Profiles")]
        [SerializeField] private CaveSfxCueProfile[] cueProfiles;

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

        public CaveSfxCueProfile GetProfile(CaveSfxCue cue)
        {
            if (cueProfiles == null) return null;
            for (int i = 0; i < cueProfiles.Length; i++)
            {
                CaveSfxCueProfile profile = cueProfiles[i];
                if (profile != null && profile.Cue == cue)
                    return profile;
            }

            return null;
        }
    }
}
