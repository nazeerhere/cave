using Cave.FieldControl;
using System;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// Actor-local presentation for a player FieldNode. It loads the approved
    /// disk frames once per play session and remains invisible when the editor
    /// extractor has not yet produced runtime sprites.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OblivionDiskVisual : MonoBehaviour
    {
        private enum DisplayState { Idle, Arming, Active, LowEnergy, Overload, Explosion }

        private const float IdleFrameDuration = 0.12f;
        private const float ArmingFrameDuration = 0.075f;
        private const float ExplosionFrameDuration = 0.028f;

        private static bool spritesLoaded;
        private static Sprite[] idleFrames;
        private static Sprite[] armingFrames;
        private static Sprite[] explosionFrames;
        private static Sprite[] shutdownFrames;

        private FieldNode node;
        private SpriteRenderer spriteRenderer;
        private DisplayState state = DisplayState.Idle;
        private float stateStartedAt;
        private int displayedFrame = -1;

        public void Configure(FieldNode fieldNode)
        {
            node = fieldNode;
        }

        public void UseRenderer(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            if (spriteRenderer != null && spriteRenderer != renderer)
                spriteRenderer.gameObject.SetActive(false);
            spriteRenderer = renderer;
            spriteRenderer.sortingOrder = 9;
            displayedFrame = -1;
            RefreshSprite(true);
        }

        public void SetArming() { SetState(DisplayState.Arming); }
        public void SetActive() { SetState(DisplayState.Active); }
        public void SetOverload() { SetState(DisplayState.Overload); }
        public void PlayExplosion() { SetState(DisplayState.Explosion); }
        public void SetShutdown() { SetState(DisplayState.LowEnergy); }

        private void Awake()
        {
            LoadSpritesOnce();
            EnsureRenderer();
            RefreshSprite(true);
        }

        private void Update()
        {
            if (state == DisplayState.Active && node != null
                && (node.EnergyFraction <= 0.3f || node.HealthFraction <= 0.3f))
            {
                SetState(DisplayState.LowEnergy);
            }
            else if (state == DisplayState.LowEnergy && node != null
                && node.EnergyFraction > 0.3f && node.HealthFraction > 0.3f)
            {
                SetState(DisplayState.Active);
            }

            RefreshSprite(false);
        }

        private void EnsureRenderer()
        {
            Transform existing = transform.Find("Oblivion Disk Sprite");
            GameObject visualObject;
            if (existing != null)
            {
                visualObject = existing.gameObject;
                spriteRenderer = visualObject.GetComponent<SpriteRenderer>();
            }
            else
            {
                visualObject = new GameObject("Oblivion Disk Sprite");
                visualObject.transform.SetParent(transform, false);
                spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null) spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 9;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
        }

        private void SetState(DisplayState next)
        {
            if (state == next) return;
            state = next;
            stateStartedAt = Time.time;
            displayedFrame = -1;
        }

        private void RefreshSprite(bool force)
        {
            if (spriteRenderer == null) return;
            Sprite[] frames = GetFrames(state);
            if (frames == null || frames.Length == 0)
            {
                spriteRenderer.enabled = false;
                return;
            }

            spriteRenderer.enabled = true;
            float duration = GetFrameDuration(state);
            int frame = Mathf.Min(frames.Length - 1, Mathf.FloorToInt((Time.time - stateStartedAt) / duration));
            if (state == DisplayState.Idle || state == DisplayState.Active || state == DisplayState.LowEnergy)
            {
                frame %= frames.Length;
            }

            if (force || frame != displayedFrame)
            {
                displayedFrame = frame;
                spriteRenderer.sprite = frames[frame];
            }

            switch (state)
            {
                case DisplayState.Idle:
                    spriteRenderer.color = new Color(0.78f, 0.78f, 0.88f, 0.9f);
                    break;
                case DisplayState.LowEnergy:
                    spriteRenderer.color = new Color(0.8f, 0.36f, 1f, 0.55f);
                    break;
                case DisplayState.Overload:
                case DisplayState.Explosion:
                    spriteRenderer.color = Color.white;
                    break;
                default:
                    spriteRenderer.color = new Color(0.82f, 0.68f, 1f, 1f);
                    break;
            }
        }

        private static Sprite[] GetFrames(DisplayState displayState)
        {
            switch (displayState)
            {
                case DisplayState.Arming:
                case DisplayState.Overload:
                    return armingFrames;
                case DisplayState.Explosion:
                    return explosionFrames;
                case DisplayState.LowEnergy:
                    return shutdownFrames;
                default:
                    return idleFrames;
            }
        }

        private static float GetFrameDuration(DisplayState displayState)
        {
            if (displayState == DisplayState.Explosion) return ExplosionFrameDuration;
            if (displayState == DisplayState.Arming || displayState == DisplayState.Overload) return ArmingFrameDuration;
            return IdleFrameDuration;
        }

        private static void LoadSpritesOnce()
        {
            if (spritesLoaded) return;
            spritesLoaded = true;
            idleFrames = Resources.LoadAll<Sprite>("VFX/OblivionDisk/Runtime/Approved/Idle");
            armingFrames = Resources.LoadAll<Sprite>("VFX/OblivionDisk/Runtime/Approved/Arming");
            explosionFrames = Resources.LoadAll<Sprite>("VFX/OblivionDisk/Runtime/Approved/Explosion");
            shutdownFrames = Resources.LoadAll<Sprite>("VFX/OblivionDisk/Runtime/Approved/Shutdown");
            Array.Sort(idleFrames, CompareSprites);
            Array.Sort(armingFrames, CompareSprites);
            Array.Sort(explosionFrames, CompareSprites);
            Array.Sort(shutdownFrames, CompareSprites);
        }

        private static int CompareSprites(Sprite left, Sprite right)
        {
            return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
        }
    }
}
