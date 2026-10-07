using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Fixed-size presentation cell for one mob status. Reactive Axiom art is
    /// child-only and never participates in row geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobStatusIconCell : MonoBehaviour
    {
        private SpriteRenderer artworkRenderer;
        private TextMesh stackCountRenderer;
        private Transform futureOverlayAnchor;
        private SpriteRenderer dimensionFrameRenderer;
        private SpriteRenderer dimensionBadgeRenderer;
        private SpriteRenderer reactiveMarkerRenderer;

        public Transform FutureOverlayAnchor => futureOverlayAnchor;
        public Vector2 CellSize { get; private set; }
        public int PresentedStackCount { get; private set; }

        public void Configure(
            Sprite artwork,
            int stackCount,
            float cellWidth,
            float cellHeight,
            float artworkPadding,
            Vector2 stackCountAnchor,
            float stackCountScale,
            int stackCountFontSize,
            FontStyle stackCountFontStyle,
            Color stackCountColor,
            int sortingLayerId,
            int sortingOrder)
        {
            EnsureChildren();

            CellSize = new Vector2(Mathf.Max(0.0001f, cellWidth), Mathf.Max(0.0001f, cellHeight));
            PresentedStackCount = Mathf.Max(0, stackCount);
            artworkRenderer.sprite = artwork;
            artworkRenderer.enabled = artwork != null;
            artworkRenderer.sortingLayerID = sortingLayerId;
            artworkRenderer.sortingOrder = sortingOrder;
            artworkRenderer.transform.localPosition = Vector3.zero;

            if (artwork != null)
            {
                float inheritedScale = Mathf.Max(Abs(transform.lossyScale.x), Abs(transform.lossyScale.y));
                float extent = MobStatusPresentationLayout.ResolveArtworkExtent(
                    CellSize.x,
                    CellSize.y,
                    artworkPadding);
                float scale = MobStatusPresentationLayout.ResolveArtworkScale(
                    artwork.bounds.size.x,
                    artwork.bounds.size.y,
                    extent,
                    inheritedScale);
                artworkRenderer.transform.localScale = Vector3.one * scale;
            }

            stackCountRenderer.text = PresentedStackCount.ToString();
            stackCountRenderer.gameObject.SetActive(PresentedStackCount > 0);
            stackCountRenderer.transform.localPosition = new Vector3(
                CellSize.x * stackCountAnchor.x,
                CellSize.y * stackCountAnchor.y,
                -0.01f);
            stackCountRenderer.characterSize = Mathf.Min(CellSize.x, CellSize.y) * Mathf.Max(0f, stackCountScale);
            stackCountRenderer.fontSize = stackCountFontSize;
            stackCountRenderer.fontStyle = stackCountFontStyle;
            stackCountRenderer.color = stackCountColor;
            MeshRenderer countMeshRenderer = stackCountRenderer.GetComponent<MeshRenderer>();
            countMeshRenderer.sortingLayerID = sortingLayerId;
            countMeshRenderer.sortingOrder = sortingOrder + 3;

            futureOverlayAnchor.localPosition = Vector3.zero;
            futureOverlayAnchor.localScale = Vector3.one;
            futureOverlayAnchor.gameObject.SetActive(false);
            dimensionFrameRenderer.sprite = null;
            dimensionFrameRenderer.enabled = false;
            dimensionBadgeRenderer.sprite = null;
            dimensionBadgeRenderer.enabled = false;
            reactiveMarkerRenderer.sprite = null;
            reactiveMarkerRenderer.enabled = false;
        }

        public void ConfigureReactive(
            AxiomReactiveStatusVisual visual,
            AxiomControlOverlayRegistry registry,
            int sortingLayerId,
            int sortingOrder)
        {
            EnsureChildren();
            if (!visual.HasOverlay || registry == null)
            {
                return;
            }

            Sprite frame = registry.GetFrame(visual.Dimension);
            Sprite badge = registry.GetBadge(visual.Dimension);
            Sprite marker = registry.GetMarker(visual.Marker);
            if (frame == null || badge == null || marker == null)
            {
                return;
            }

            futureOverlayAnchor.gameObject.SetActive(true);
            ConfigureOverlayRenderer(
                dimensionFrameRenderer,
                frame,
                Vector3.zero,
                CellSize.x,
                CellSize.y,
                sortingLayerId,
                sortingOrder + 1);
            ConfigureOverlayRenderer(
                dimensionBadgeRenderer,
                badge,
                new Vector3(CellSize.x * .27f, CellSize.y * .24f, -0.02f),
                CellSize.x * .32f,
                CellSize.y * .32f,
                sortingLayerId,
                sortingOrder + 2);
            ConfigureOverlayRenderer(
                reactiveMarkerRenderer,
                marker,
                new Vector3(-CellSize.x * .17f, CellSize.y * .04f, -0.01f),
                CellSize.x * .48f,
                CellSize.y * .48f,
                sortingLayerId,
                sortingOrder + 3);
        }

        private void EnsureChildren()
        {
            if (artworkRenderer == null)
            {
                GameObject artwork = new GameObject("Artwork") { hideFlags = HideFlags.DontSave };
                artwork.transform.SetParent(transform, false);
                artworkRenderer = artwork.AddComponent<SpriteRenderer>();
            }

            if (stackCountRenderer == null)
            {
                GameObject count = new GameObject("Stack Count") { hideFlags = HideFlags.DontSave };
                count.transform.SetParent(transform, false);
                stackCountRenderer = count.AddComponent<TextMesh>();
                stackCountRenderer.anchor = TextAnchor.MiddleCenter;
                stackCountRenderer.alignment = TextAlignment.Center;
                stackCountRenderer.fontStyle = FontStyle.Bold;
                stackCountRenderer.color = new Color(0.94f, 0.98f, 1f, 1f);
            }

            if (futureOverlayAnchor == null)
            {
                GameObject overlay = new GameObject("Future Reactive Overlay") { hideFlags = HideFlags.DontSave };
                overlay.transform.SetParent(transform, false);
                futureOverlayAnchor = overlay.transform;
            }

            if (dimensionFrameRenderer == null)
            {
                GameObject frame = new GameObject("Dimension Frame") { hideFlags = HideFlags.DontSave };
                frame.transform.SetParent(futureOverlayAnchor, false);
                dimensionFrameRenderer = frame.AddComponent<SpriteRenderer>();
            }

            if (reactiveMarkerRenderer == null)
            {
                GameObject marker = new GameObject("Direction Or Result") { hideFlags = HideFlags.DontSave };
                marker.transform.SetParent(futureOverlayAnchor, false);
                reactiveMarkerRenderer = marker.AddComponent<SpriteRenderer>();
            }

            if (dimensionBadgeRenderer == null)
            {
                GameObject badge = new GameObject("Dimension Badge") { hideFlags = HideFlags.DontSave };
                badge.transform.SetParent(futureOverlayAnchor, false);
                dimensionBadgeRenderer = badge.AddComponent<SpriteRenderer>();
            }
        }

        private void ConfigureOverlayRenderer(
            SpriteRenderer renderer,
            Sprite sprite,
            Vector3 localPosition,
            float availableWidth,
            float availableHeight,
            int sortingLayerId,
            int sortingOrder)
        {
            float inheritedScale = Mathf.Max(Abs(transform.lossyScale.x), Abs(transform.lossyScale.y));
            float scale = MobStatusPresentationLayout.ResolveArtworkScale(
                sprite.bounds.size.x,
                sprite.bounds.size.y,
                Mathf.Min(availableWidth, availableHeight),
                inheritedScale);
            renderer.sprite = sprite;
            renderer.enabled = true;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.localPosition = localPosition;
            renderer.transform.localScale = Vector3.one * scale;
        }

        private static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }
    }
}
