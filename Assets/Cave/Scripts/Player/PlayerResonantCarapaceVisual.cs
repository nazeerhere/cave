using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// Presentation-only Carapace overlay.  Durability, suppression, and loss
    /// remain owned exclusively by <see cref="PlayerResonantCarapace"/>.
    ///
    /// The approved Wireframe Purple sheets are 3 x 2 storyboard atlases. This
    /// component normalizes their six equal cells at runtime while retaining
    /// the originals unchanged in Resources/Source.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerResonantCarapace))]
    public sealed class PlayerResonantCarapaceVisual : MonoBehaviour
    {
        private const int AtlasColumns = 3;
        private const int AtlasRows = 2;

        [Header("Source Sheets")]
        [SerializeField] private string auraResourcePath = "VFX/Carapace/Source/six_frame_spectral_crystal_knight_sprite_sheet";
        [SerializeField] private string impactResourcePath = "VFX/Carapace/Source/armored_warrior_impact_animation_sheet";
        [SerializeField] private string shatterResourcePath = "VFX/Carapace/Source/purple_crystal_knight_disintegration_sprite_sheet";

        [Header("Presentation")]
        [SerializeField, Range(0f, 1f)] private float idleOpacity = .33f;
        [SerializeField, Min(.01f)] private float idleFramesPerSecond = 5f;
        [SerializeField, Min(.01f)] private float impactFramesPerSecond = 12f;
        [SerializeField, Min(.01f)] private float shatterFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 340f;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, .12f, 0f);
        [SerializeField] private int sortingOrderOffset = 2;

        private PlayerResonantCarapace carapace;
        private SpriteRenderer playerRenderer;
        private SpriteRenderer idleRenderer;
        private SpriteRenderer transientRenderer;
        private Sprite[] idleFrames;
        private Sprite[] impactFrames;
        private Sprite[] shatterFrames;
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private float idleFrameClock;
        private float transientFrameClock;
        private int idleFrameIndex;
        private int transientFrameIndex;
        private bool transientLoops;
        private bool transientIsSuppression;

        public float IdleOpacity => idleOpacity;

        private void Awake()
        {
            carapace = GetComponent<PlayerResonantCarapace>();
            playerRenderer = FindPlayerRenderer();
            CreateRenderers();
            idleFrames = LoadAndNormalizeFrames(auraResourcePath);
            impactFrames = LoadAndNormalizeFrames(impactResourcePath);
            shatterFrames = LoadAndNormalizeFrames(shatterResourcePath);
        }

        private void OnEnable()
        {
            if (carapace == null) carapace = GetComponent<PlayerResonantCarapace>();
            if (carapace != null)
            {
                carapace.StateChanged += RefreshState;
                carapace.DamageAbsorbed += HandleDamageAbsorbed;
                carapace.RestorationBurst += HandleRestorationBurst;
                carapace.TerminalFractured += HandleTerminalFractured;
            }

            RefreshState();
        }

        private void OnDisable()
        {
            if (carapace != null)
            {
                carapace.StateChanged -= RefreshState;
                carapace.DamageAbsorbed -= HandleDamageAbsorbed;
                carapace.RestorationBurst -= HandleRestorationBurst;
                carapace.TerminalFractured -= HandleTerminalFractured;
            }

            HideAll();
        }

        private void OnDestroy()
        {
            for (int index = 0; index < generatedTextures.Count; index++)
            {
                if (generatedTextures[index] != null)
                {
                    Destroy(generatedTextures[index]);
                }
            }
        }

        private void Update()
        {
            SynchronizeRenderer();
            AdvanceIdle();
            AdvanceTransient();
        }

        private void RefreshState()
        {
            if (carapace == null || idleRenderer == null)
            {
                return;
            }

            ResonantCarapaceState state = carapace.State;
            if (carapace.IsOperational)
            {
                ShowIdle();
                return;
            }

            idleRenderer.enabled = false;
            if (state == ResonantCarapaceState.ResonanceDisabled)
            {
                // A single, dim response communicates suppression without
                // inventing another Resonance timer or retaining protection.
                PlayTransient(impactFrames, impactFramesPerSecond, false, .55f, true);
            }
            else if (state != ResonantCarapaceState.TerminallyFractured)
            {
                StopTransient();
            }
        }

        private void HandleDamageAbsorbed(int ignoredAmount)
        {
            if (carapace != null && carapace.IsOperational)
            {
                PlayTransient(impactFrames, impactFramesPerSecond, false, 1f, false);
            }
        }

        private void HandleTerminalFractured()
        {
            if (idleRenderer != null) idleRenderer.enabled = false;
            PlayTransient(shatterFrames, shatterFramesPerSecond, false, 1f, false);
        }

        private void HandleRestorationBurst(int ignoredAmount)
        {
            if (carapace == null || !carapace.IsOperational)
            {
                return;
            }

            // The approved package has no dedicated reconstruction sequence.
            // A short bright pass through the armor atlas communicates plate
            // restoration without reusing the offensive impact animation.
            PlayTransient(idleFrames, impactFramesPerSecond, false, .92f, false);
            ShowIdle();
        }

        private void CreateRenderers()
        {
            idleRenderer = CreateRenderer("Resonant Carapace Idle", sortingOrderOffset);
            transientRenderer = CreateRenderer("Resonant Carapace Response", sortingOrderOffset + 1);
            HideAll();
        }

        private SpriteRenderer CreateRenderer(string childName, int orderOffset)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localOffset;
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sortingOrder = (playerRenderer != null ? playerRenderer.sortingOrder : 0) + orderOffset;
            return renderer;
        }

        private SpriteRenderer FindPlayerRenderer()
        {
            MinerPlayerVisualAnimator animator = GetComponent<MinerPlayerVisualAnimator>();
            SpriteRenderer[] renderers = animator != null
                ? animator.GetComponentsInChildren<SpriteRenderer>(true)
                : GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer result = null;
            for (int index = 0; index < renderers.Length; index++)
            {
                SpriteRenderer candidate = renderers[index];
                if (candidate != null && candidate.gameObject != gameObject)
                {
                    result = candidate;
                    break;
                }
            }
            return result;
        }

        private void SynchronizeRenderer()
        {
            if (playerRenderer == null) playerRenderer = FindPlayerRenderer();
            SynchronizeRenderer(idleRenderer, sortingOrderOffset);
            SynchronizeRenderer(transientRenderer, sortingOrderOffset + 1);
        }

        private void SynchronizeRenderer(SpriteRenderer renderer, int orderOffset)
        {
            if (renderer == null) return;
            renderer.flipX = playerRenderer != null && playerRenderer.flipX;
            renderer.sortingLayerID = playerRenderer != null ? playerRenderer.sortingLayerID : renderer.sortingLayerID;
            renderer.sortingOrder = (playerRenderer != null ? playerRenderer.sortingOrder : 0) + orderOffset;
        }

        private void ShowIdle()
        {
            if (idleFrames == null || idleFrames.Length == 0) return;
            if (!idleRenderer.enabled)
            {
                idleFrameIndex = 0;
                idleFrameClock = 0f;
                idleRenderer.sprite = idleFrames[0];
                idleRenderer.enabled = true;
            }

            // Always write the authored runtime alpha.  No impact, repair, or
            // suppression path may leave this persistent layer intensified.
            idleRenderer.color = new Color(1f, 1f, 1f, idleOpacity);
        }

        private void AdvanceIdle()
        {
            if (idleRenderer == null || !idleRenderer.enabled || idleFrames == null || idleFrames.Length < 2)
            {
                return;
            }

            idleFrameClock += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(.01f, idleFramesPerSecond);
            if (idleFrameClock < frameDuration) return;
            idleFrameClock -= frameDuration;
            idleFrameIndex = (idleFrameIndex + 1) % idleFrames.Length;
            idleRenderer.sprite = idleFrames[idleFrameIndex];
        }

        private void PlayTransient(Sprite[] frames, float framesPerSecond, bool loop, float opacity, bool suppression)
        {
            if (transientRenderer == null || frames == null || frames.Length == 0) return;
            transientRenderer.sprite = frames[0];
            transientRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
            transientRenderer.enabled = true;
            transientRenderer.name = suppression ? "Resonant Carapace Suppression" : "Resonant Carapace Response";
            transientFrameIndex = 0;
            transientFrameClock = 0f;
            transientLoops = loop;
            transientIsSuppression = suppression;
            currentTransientFrames = frames;
            currentTransientFramesPerSecond = framesPerSecond;
        }

        private Sprite[] currentTransientFrames;
        private float currentTransientFramesPerSecond;

        private void AdvanceTransient()
        {
            if (transientRenderer == null || !transientRenderer.enabled || currentTransientFrames == null || currentTransientFrames.Length == 0)
            {
                return;
            }

            transientFrameClock += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(.01f, currentTransientFramesPerSecond);
            if (transientFrameClock < frameDuration) return;
            transientFrameClock -= frameDuration;
            transientFrameIndex++;
            if (transientFrameIndex >= currentTransientFrames.Length)
            {
                if (transientLoops)
                {
                    transientFrameIndex = 0;
                }
                else
                {
                    StopTransient();
                    // Suppression remains visually off until authority says it
                    // ended; ordinary idle will be restored by StateChanged.
                    return;
                }
            }
            transientRenderer.sprite = currentTransientFrames[transientFrameIndex];
        }

        private void StopTransient()
        {
            if (transientRenderer != null) transientRenderer.enabled = false;
            currentTransientFrames = null;
            transientLoops = false;
            transientIsSuppression = false;
        }

        private void HideAll()
        {
            if (idleRenderer != null) idleRenderer.enabled = false;
            StopTransient();
        }

        private Sprite[] LoadAndNormalizeFrames(string resourcePath)
        {
            Texture2D source = Resources.Load<Texture2D>(resourcePath);
            if (source == null || !source.isReadable || source.width < AtlasColumns || source.height < AtlasRows)
            {
                Debug.LogWarning("Resonant Carapace VFX source is unavailable or unreadable: " + resourcePath, this);
                return Array.Empty<Sprite>();
            }

            int cellWidth = source.width / AtlasColumns;
            int cellHeight = source.height / AtlasRows;
            Color32[] sourcePixels = source.GetPixels32();
            Sprite[] result = new Sprite[AtlasColumns * AtlasRows];
            int frame = 0;
            // Unity texture coordinates are bottom-left based.  Storyboard rows
            // are read visually from top to bottom, hence this conversion.
            for (int displayRow = 0; displayRow < AtlasRows; displayRow++)
            {
                int sourceRow = AtlasRows - 1 - displayRow;
                for (int column = 0; column < AtlasColumns; column++)
                {
                    Color32[] pixels = CopyAndCleanCell(sourcePixels, source.width, cellWidth, cellHeight, column, sourceRow);
                    Texture2D normalized = new Texture2D(cellWidth, cellHeight, TextureFormat.RGBA32, false, false);
                    normalized.name = "CarapaceVfxFrame" + frame;
                    normalized.filterMode = FilterMode.Bilinear;
                    normalized.wrapMode = TextureWrapMode.Clamp;
                    normalized.SetPixels32(pixels);
                    normalized.Apply(false, false);
                    generatedTextures.Add(normalized);
                    result[frame++] = Sprite.Create(
                        normalized,
                        new Rect(0f, 0f, cellWidth, cellHeight),
                        new Vector2(.5f, .5f),
                        pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);
                }
            }
            return result;
        }

        private static Color32[] CopyAndCleanCell(Color32[] source, int sourceWidth, int width, int height, int column, int row)
        {
            Color32[] pixels = new Color32[width * height];
            int sourceX = column * width;
            int sourceY = row * height;
            for (int y = 0; y < height; y++)
            {
                Array.Copy(source, (sourceY + y) * sourceWidth + sourceX, pixels, y * width, width);
            }

            // Conservative edge-connected chroma cleanup.  It only removes the
            // known synthetic blue/magenta exterior spill; enclosed dark and
            // low-saturation crystal detail stays intact.
            bool[] visited = new bool[pixels.Length];
            Queue<int> queue = new Queue<int>();
            for (int x = 0; x < width; x++) { EnqueueBorder(x, 0); EnqueueBorder(x, height - 1); }
            for (int y = 1; y < height - 1; y++) { EnqueueBorder(0, y); EnqueueBorder(width - 1, y); }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                pixels[index] = new Color32(0, 0, 0, 0);
                int x = index % width;
                int y = index / width;
                EnqueueBorder(x - 1, y);
                EnqueueBorder(x + 1, y);
                EnqueueBorder(x, y - 1);
                EnqueueBorder(x, y + 1);
            }
            return pixels;

            void EnqueueBorder(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                int index = y * width + x;
                if (visited[index] || !IsExteriorChromaSpill(pixels[index])) return;
                visited[index] = true;
                queue.Enqueue(index);
            }
        }

        private static bool IsExteriorChromaSpill(Color32 color)
        {
            if (color.a == 0) return false;
            bool electricBlue = color.b > 120 && color.b > color.r * 1.45f && color.b > color.g * 1.12f;
            bool magenta = color.r > 150 && color.b > 120 && color.g < 120;
            return electricBlue || magenta;
        }
    }
}
