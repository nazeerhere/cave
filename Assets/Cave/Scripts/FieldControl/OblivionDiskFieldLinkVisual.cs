using System;
using UnityEngine;

namespace Cave.FieldControl
{
    /// <summary>
    /// Appearance-only playback for the approved player-mine link frames.
    /// FieldLink remains authoritative for geometry, force, and collision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OblivionDiskFieldLinkVisual : MonoBehaviour
    {
        private const string ResourcePath = "VFX/OblivionDisk/Runtime/Approved/LinkCore";
        private const float FrameDuration = 0.1f;

        private static bool framesLoaded;
        private static Sprite[] frames;

        private SpriteRenderer spriteRenderer;
        private LineRenderer fallbackLine;
        private float linkLength;
        private float linkWidth;
        private Color linkColor;
        private int displayedFrame = -1;

        public void Configure(LineRenderer line)
        {
            fallbackLine = line;
            LoadFramesOnce();
            if (frames == null || frames.Length == 0) return;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = 7;
            }

            fallbackLine.enabled = false;
            spriteRenderer.enabled = true;
            displayedFrame = -1;
        }

        public void Refresh(float length, float width, Color color)
        {
            if (frames == null || frames.Length == 0 || spriteRenderer == null) return;
            linkLength = Mathf.Max(0.01f, length);
            linkWidth = Mathf.Max(0.01f, width);
            linkColor = color;
            ApplyFrame(Time.time);
        }

        private void Update()
        {
            if (spriteRenderer != null && spriteRenderer.enabled) ApplyFrame(Time.time);
        }

        private void ApplyFrame(float now)
        {
            int frame = Mathf.FloorToInt(now / FrameDuration) % frames.Length;
            if (frame != displayedFrame)
            {
                displayedFrame = frame;
                spriteRenderer.sprite = frames[frame];
            }

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null) return;
            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.size = new Vector2(linkLength, Mathf.Max(.12f, linkWidth * 2f));
            spriteRenderer.transform.localScale = Vector3.one;
            spriteRenderer.color = linkColor;
        }

        private static void LoadFramesOnce()
        {
            if (framesLoaded) return;
            framesLoaded = true;
            frames = Resources.LoadAll<Sprite>(ResourcePath);
            Array.Sort(frames, (left, right) => string.CompareOrdinal(
                left != null ? left.name : string.Empty,
                right != null ? right.name : string.Empty));
        }
    }
}
