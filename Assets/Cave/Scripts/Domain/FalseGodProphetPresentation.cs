using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Presentation-only bridge between the False God combat state contract and
    /// the authored Form 1 frame sequences.  It never changes combat state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodCombatController))]
    public sealed class FalseGodProphetPresentation : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float pixelsPerUnit = 128f;
        [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;
        [SerializeField] private int sortingOrder = 20;

        private FalseGodCombatController combat;
        private SpriteRenderer bodyRenderer;
        private Sprite[] currentFrames;
        private float elapsed;
        private int frameIndex = -1;
        private bool holding;
        private int holdFrame = -1;

        private void Awake()
        {
            combat = GetComponent<FalseGodCombatController>();
            EnsureRenderer();
        }

        private void OnEnable()
        {
            if (combat == null)
            {
                combat = GetComponent<FalseGodCombatController>();
            }

            if (combat != null)
            {
                combat.PresentationStateChanged -= HandlePresentationState;
                combat.PresentationStateChanged += HandlePresentationState;
                HandlePresentationState(combat.PresentationState);
            }
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.PresentationStateChanged -= HandlePresentationState;
            }
        }

        private void Update()
        {
            if (currentFrames == null || currentFrames.Length <= 1)
            {
                return;
            }

            elapsed += Time.deltaTime;
            int nextFrame = Mathf.FloorToInt(elapsed * framesPerSecond) % currentFrames.Length;
            if (holding)
            {
                nextFrame = holdFrame;
            }
            else if (holdFrame >= 0 && nextFrame >= holdFrame)
            {
                holding = true;
                nextFrame = holdFrame;
            }
            ApplyFrame(nextFrame);
        }

        /// <summary>Called by combat only after its authoritative release point occurs.</summary>
        public void ReleaseGameplayHold()
        {
            holding = false;
            holdFrame = -1;
            elapsed = Mathf.Max(elapsed, (frameIndex + 1) / framesPerSecond);
        }

        private void HandlePresentationState(FalseGodProphetPresentationState state)
        {
            currentFrames = FalseGodProphetArt.LoadBodyFrames(BodySequenceFor(state));
            elapsed = 0f;
            frameIndex = -1;
            holding = false;
            holdFrame = HoldFrameFor(state);
            ApplyFrame(0);
        }

        private void ApplyFrame(int requestedFrame)
        {
            EnsureRenderer();
            if (bodyRenderer == null || currentFrames == null || currentFrames.Length == 0)
            {
                return;
            }

            int safeFrame = Mathf.Clamp(requestedFrame, 0, currentFrames.Length - 1);
            if (frameIndex == safeFrame)
            {
                return;
            }

            frameIndex = safeFrame;
            bodyRenderer.sprite = currentFrames[safeFrame];
        }

        private void EnsureRenderer()
        {
            if (bodyRenderer != null)
            {
                return;
            }

            Transform child = transform.Find("Prophet Visual");
            if (child == null)
            {
                GameObject visual = new GameObject("Prophet Visual");
                visual.transform.SetParent(transform, false);
                child = visual.transform;
            }

            bodyRenderer = child.GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
            {
                bodyRenderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            bodyRenderer.sortingOrder = sortingOrder;
            child.localScale = Vector3.one * (64f / pixelsPerUnit);
        }

        private static string BodySequenceFor(FalseGodProphetPresentationState state)
        {
            switch (state)
            {
                case FalseGodProphetPresentationState.Move:
                case FalseGodProphetPresentationState.MoveFloat: return "Move_Float";
                case FalseGodProphetPresentationState.Hurt: return "Hurt";
                case FalseGodProphetPresentationState.Block: return "Block";
                case FalseGodProphetPresentationState.BasicCast: return "Basic_Cast";
                case FalseGodProphetPresentationState.ShadowCast: return "Shadow_Cast";
                case FalseGodProphetPresentationState.ClaimCommand: return "Claim_Command";
                case FalseGodProphetPresentationState.CausalBeam:
                case FalseGodProphetPresentationState.HighAuthorityCast: return "causal_beam";
                case FalseGodProphetPresentationState.Ascension:
                case FalseGodProphetPresentationState.FinalAscension: return "Ascension";
                case FalseGodProphetPresentationState.TitheReconfigurationCast: return "Claim_Command";
                default: return "Idle";
            }
        }

        // Centralized authored holds. Idle, Hurt and movement intentionally do not hold.
        private static int HoldFrameFor(FalseGodProphetPresentationState state)
        {
            switch (state)
            {
                case FalseGodProphetPresentationState.ShadowCast: return 3;
                case FalseGodProphetPresentationState.CausalBeam:
                case FalseGodProphetPresentationState.HighAuthorityCast: return 3;
                case FalseGodProphetPresentationState.ClaimCommand:
                case FalseGodProphetPresentationState.TitheReconfigurationCast: return 4;
                default: return -1;
            }
        }
    }

    public enum FalseGodProphetVfxKind
    {
        EchoProjectile, SlowBolt, DashShadow, CrystalManifestation, CrystalRain,
        CrystalChainHook, Appropriation, TriuneAnchors, CausalBeam
    }

    /// <summary>One-shot or attached authored VFX frame playback; isolated from ability logic.</summary>
    public sealed class FalseGodProphetVfxPlayback : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float framesPerSecond = 12f;
        [SerializeField] private bool destroyAtEnd;
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float elapsed;
        private int frameIndex = -1;

        public static void Attach(GameObject target, FalseGodProphetVfxKind kind, bool destroyWithAnimation)
        {
            if (target == null)
            {
                return;
            }

            SpriteRenderer fallbackRenderer = target.GetComponent<SpriteRenderer>();
            if (fallbackRenderer != null)
            {
                fallbackRenderer.enabled = false;
            }

            GameObject visual = new GameObject("Authored " + kind + " Visual");
            visual.transform.SetParent(target.transform, false);
            // Projectile roots retain their small gameplay scale. This inverse local
            // scale keeps supplied art readable without changing collider geometry.
            visual.transform.localScale = Vector3.one * 3f;
            FalseGodProphetVfxPlayback playback = visual.AddComponent<FalseGodProphetVfxPlayback>();
            playback.Initialize(kind, destroyWithAnimation);
        }

        public static void PlayOneShot(FalseGodProphetVfxKind kind, Vector2 position, float scale = 1f)
        {
            GameObject visual = new GameObject("False God VFX " + kind);
            visual.transform.position = position;
            visual.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale * 0.5f);
            FalseGodProphetVfxPlayback playback = visual.AddComponent<FalseGodProphetVfxPlayback>();
            playback.Initialize(kind, true);
        }

        public void Initialize(FalseGodProphetVfxKind kind, bool shouldDestroyAtEnd)
        {
            frames = FalseGodProphetArt.LoadVfxFrames(VfxSequenceFor(kind));
            destroyAtEnd = shouldDestroyAtEnd;
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingOrder = 26;
            ApplyFrame(0);
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
            {
                if (destroyAtEnd)
                {
                    Destroy(gameObject);
                }

                return;
            }

            elapsed += Time.deltaTime;
            int nextFrame = Mathf.FloorToInt(elapsed * framesPerSecond);
            if (nextFrame >= frames.Length)
            {
                if (destroyAtEnd)
                {
                    Destroy(gameObject);
                }

                return;
            }

            ApplyFrame(nextFrame);
        }

        private void ApplyFrame(int requestedFrame)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            int safeFrame = Mathf.Clamp(requestedFrame, 0, frames.Length - 1);
            if (safeFrame == frameIndex)
            {
                return;
            }

            frameIndex = safeFrame;
            spriteRenderer.sprite = frames[safeFrame];
        }

        private static string VfxSequenceFor(FalseGodProphetVfxKind kind)
        {
            switch (kind)
            {
                case FalseGodProphetVfxKind.EchoProjectile: return "Echo_Projectile";
                case FalseGodProphetVfxKind.SlowBolt: return "Slow_Bolt";
                case FalseGodProphetVfxKind.DashShadow: return "Dash_Shadow";
                case FalseGodProphetVfxKind.CrystalManifestation: return "Crystal_Manifestation";
                case FalseGodProphetVfxKind.CrystalRain: return "Crystal_Rain";
                case FalseGodProphetVfxKind.CrystalChainHook: return "Crystal_Chain_Hook";
                case FalseGodProphetVfxKind.Appropriation: return "Appropriation";
                case FalseGodProphetVfxKind.TriuneAnchors: return "Triune_Anchors";
                default: return "Causal_Beam_VFX";
            }
        }
    }

    internal static class FalseGodProphetArt
    {
        private static readonly Dictionary<string, Sprite[]> CachedFrames = new Dictionary<string, Sprite[]>();
        private const string BodyRoot = "FalseGod/Prophet/false_god_all_cropped_animations_final/";
        private const string VfxRoot = "FalseGod/Prophet/false_god_form1_vfx_cropped/";

        public static Sprite[] LoadBodyFrames(string sequence) => Load(BodyRoot + sequence);
        public static Sprite[] LoadVfxFrames(string sequence) => Load(VfxRoot + sequence);

        private static Sprite[] Load(string resourcePath)
        {
            if (CachedFrames.TryGetValue(resourcePath, out Sprite[] cached))
            {
                return cached;
            }

            Sprite[] loaded = Resources.LoadAll<Sprite>(resourcePath);
            Array.Sort(loaded, CompareByName);
            CachedFrames.Add(resourcePath, loaded);
            return loaded;
        }

        private static int CompareByName(Sprite left, Sprite right)
        {
            return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
        }
    }
}
