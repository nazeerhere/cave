using UnityEngine;

namespace Cave.EnvironmentFx
{
    /// <summary>
    /// Lightweight, self-contained environmental motion for the Heart Chamber's upward water stream.
    /// It caches renderer state once and only moves its assigned visual layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReverseStreamAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] streamLayers;
        [SerializeField] private SpriteRenderer[] bubbleLayers;
        [SerializeField, Min(0.01f)] private float streamSpeed = 0.28f;
        [SerializeField, Min(0.01f)] private float streamTravel = 0.42f;
        [SerializeField, Min(0.01f)] private float bubbleSpeed = 0.55f;
        [SerializeField, Min(0.01f)] private float bubbleRiseDistance = 2.4f;

        private Vector3[] streamBasePositions;
        private Vector3[] bubbleBasePositions;
        private Color[] bubbleBaseColors;
        private float elapsed;

        public void Configure(
            SpriteRenderer[] configuredStreamLayers,
            SpriteRenderer[] configuredBubbleLayers,
            float configuredStreamSpeed,
            float configuredStreamTravel,
            float configuredBubbleSpeed,
            float configuredBubbleRise)
        {
            streamLayers = configuredStreamLayers;
            bubbleLayers = configuredBubbleLayers;
            streamSpeed = configuredStreamSpeed;
            streamTravel = configuredStreamTravel;
            bubbleSpeed = configuredBubbleSpeed;
            bubbleRiseDistance = configuredBubbleRise;
            CacheState();
        }

        private void Awake()
        {
            CacheState();
        }

        private void OnEnable()
        {
            if (streamBasePositions == null || bubbleBasePositions == null)
            {
                CacheState();
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            AnimateStreamLayers();
            AnimateBubbles();
        }

        private void CacheState()
        {
            int streamCount = streamLayers == null ? 0 : streamLayers.Length;
            streamBasePositions = new Vector3[streamCount];
            for (int index = 0; index < streamCount; index++)
            {
                if (streamLayers[index] != null)
                {
                    streamBasePositions[index] = streamLayers[index].transform.localPosition;
                }
            }

            int bubbleCount = bubbleLayers == null ? 0 : bubbleLayers.Length;
            bubbleBasePositions = new Vector3[bubbleCount];
            bubbleBaseColors = new Color[bubbleCount];
            for (int index = 0; index < bubbleCount; index++)
            {
                if (bubbleLayers[index] != null)
                {
                    bubbleBasePositions[index] = bubbleLayers[index].transform.localPosition;
                    bubbleBaseColors[index] = bubbleLayers[index].color;
                }
            }
        }

        private void AnimateStreamLayers()
        {
            for (int index = 0; index < streamLayers.Length; index++)
            {
                SpriteRenderer layer = streamLayers[index];
                if (layer == null)
                {
                    continue;
                }

                float phase = elapsed * streamSpeed + index * 0.43f;
                Vector3 position = streamBasePositions[index];
                position.y += Mathf.Sin(phase) * streamTravel;
                position.x += Mathf.Sin(phase * 0.67f) * streamTravel * 0.14f;
                layer.transform.localPosition = position;
            }
        }

        private void AnimateBubbles()
        {
            for (int index = 0; index < bubbleLayers.Length; index++)
            {
                SpriteRenderer bubble = bubbleLayers[index];
                if (bubble == null)
                {
                    continue;
                }

                float progress = Mathf.Repeat(elapsed * bubbleSpeed + index * 0.29f, 1f);
                Vector3 position = bubbleBasePositions[index];
                position.y += progress * bubbleRiseDistance;
                bubble.transform.localPosition = position;
                Color color = bubbleBaseColors[index];
                color.a *= Mathf.Sin(progress * Mathf.PI);
                bubble.color = color;
            }
        }
    }
}
