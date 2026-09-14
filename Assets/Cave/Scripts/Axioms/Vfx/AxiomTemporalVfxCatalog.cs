using System;
using UnityEngine;

namespace Cave.Axioms.Vfx
{
    [CreateAssetMenu(menuName = "Cave/Axioms/Temporal VFX Catalog", fileName = "AxiomTemporalVfxCatalog")]
    public sealed class AxiomTemporalVfxCatalog : ScriptableObject
    {
        [SerializeField] private GameObject persistentPrefab;
        [SerializeField] private AxiomSpriteAnimation pulseRing;
        [SerializeField] private AxiomSpriteAnimation lockStar;
        [SerializeField] private AxiomSpriteAnimation orbitalLock;
        [SerializeField] private AxiomSpriteAnimation coherenceRing;
        [SerializeField] private AxiomSpriteAnimation lockFlash;
        [SerializeField] private AxiomSpriteAnimation errorGlitch;
        [SerializeField] private AxiomSpriteAnimation fractureCollapse;
        [SerializeField] private AxiomSpriteAnimation unstableBreak;
        [SerializeField] private AxiomSpriteAnimation groundPulse;
        [SerializeField] private AxiomSpriteAnimation resonanceBurst;

        public GameObject PersistentPrefab => persistentPrefab;
        public AxiomSpriteAnimation PulseRing => pulseRing;
        public AxiomSpriteAnimation LockStar => lockStar;
        public AxiomSpriteAnimation OrbitalLock => orbitalLock;
        public AxiomSpriteAnimation CoherenceRing => coherenceRing;
        public AxiomSpriteAnimation LockFlash => lockFlash;
        public AxiomSpriteAnimation ErrorGlitch => errorGlitch;
        public AxiomSpriteAnimation FractureCollapse => fractureCollapse;
        public AxiomSpriteAnimation UnstableBreak => unstableBreak;
        public AxiomSpriteAnimation GroundPulse => groundPulse;
        public AxiomSpriteAnimation ResonanceBurst => resonanceBurst;

        public void Configure(GameObject prefab, AxiomSpriteAnimation pulse, AxiomSpriteAnimation state, AxiomSpriteAnimation rate, AxiomSpriteAnimation acceleration, AxiomSpriteAnimation flash, AxiomSpriteAnimation error, AxiomSpriteAnimation fracture, AxiomSpriteAnimation unstable, AxiomSpriteAnimation ground, AxiomSpriteAnimation resonance)
        {
            persistentPrefab = prefab; pulseRing = pulse; lockStar = state; orbitalLock = rate; coherenceRing = acceleration;
            lockFlash = flash; errorGlitch = error; fractureCollapse = fracture; unstableBreak = unstable;
            groundPulse = ground; resonanceBurst = resonance;
        }
    }

    [Serializable]
    public struct AxiomSpriteAnimation
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(.01f)] private float framesPerSecond;
        [SerializeField, Min(.01f)] private float scale;
        public AxiomSpriteAnimation(Sprite[] framesValue, float fps, float scaleValue) { frames = framesValue; framesPerSecond = fps; scale = scaleValue; }
        public Sprite[] Frames => frames;
        public float FramesPerSecond => Mathf.Max(.01f, framesPerSecond);
        public float Scale => Mathf.Max(.01f, scale);
        public bool IsValid => frames != null && frames.Length > 0;
    }
}
