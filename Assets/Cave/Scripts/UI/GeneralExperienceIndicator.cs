using Cave.Enemies;
using UnityEngine;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonInheritance))]
    public sealed class GeneralExperienceIndicator : MonoBehaviour
    {
        private const string ProjectSkullAssetPath =
            "Assets/Brackeys/2D Mega Pack/Items & Icons/Gothic/Skull.png";

        [Header("Visibility / Placement")]
        [SerializeField] private bool showIndicator = true;
        [SerializeField, Min(0f)] private float headClearance = 0.08f;
        [SerializeField, Min(0.1f)] private float fallbackHeight = 0.65f;

        [Header("Compact Badge")]
        [SerializeField, Min(0.4f)] private float baseBadgeWidth = 1.12f;
        [SerializeField, Min(0.1f)] private float badgeHeight = 0.34f;
        [SerializeField, Min(0f)] private float widthPerExtraDigit = 0.12f;
        [SerializeField] private Sprite skullIcon;
        [SerializeField, Min(0.01f)] private float characterSize = 0.038f;
        [SerializeField, Min(8)] private int fontSize = 30;

        [Header("Dark-Fantasy Colors")]
        [SerializeField] private Color ironColor = new Color(0.12f, 0.10f, 0.11f, 1f);
        [SerializeField] private Color bronzeColor = new Color(0.43f, 0.28f, 0.16f, 1f);
        [SerializeField] private Color bronzeHighlight = new Color(0.72f, 0.48f, 0.22f, 1f);
        [SerializeField] private Color stoneColor = new Color(0.035f, 0.032f, 0.045f, 0.98f);
        [SerializeField] private Color accentColor = new Color(0.12f, 0.75f, 1f, 1f);
        [SerializeField] private Color textColor = new Color(1f, 0.86f, 0.55f, 1f);

        private SkeletonInheritance inheritance;
        private SpriteRenderer bodyRenderer;
        private GameObject badgeRoot;
        private TextMesh label;
        private SpriteRenderer frame;
        private SpriteRenderer shadow;
        private SpriteRenderer innerPlate;
        private SpriteRenderer topHighlight;
        private SpriteRenderer bottomEdge;
        private Transform leftCap;
        private Transform rightCap;
        private Transform skullMedallion;
        private Transform divider;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private int sortingLayerId;
        private int baseSortingOrder;

        private void Awake()
        {
            inheritance = GetComponent<SkeletonInheritance>();
            bodyRenderer = FindPrimaryBodyRenderer();
            ResolveSkullIconForEditorPreview();
            BuildBadge();
            UpdateBadgeTransform();
            Refresh();
        }

        private void OnEnable()
        {
            inheritance.InheritanceChanged += Refresh;
            inheritance.RankChanged += HandleRankChanged;
            Refresh();
        }

        private void OnDisable()
        {
            inheritance.InheritanceChanged -= Refresh;
            inheritance.RankChanged -= HandleRankChanged;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (badgeRoot != null)
            {
                UpdateBadgeTransform();
            }
        }

        private void HandleRankChanged(SkeletonRank rank)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (badgeRoot == null || label == null || inheritance == null)
            {
                return;
            }

            bool visible = showIndicator && inheritance.Rank == SkeletonRank.General;
            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            label.text = "GEN " + inheritance.WitnessedDeaths;
            ApplyBadgeWidth(CalculateBadgeWidth(inheritance.WitnessedDeaths));
        }

        private void BuildBadge()
        {
            CreateRuntimeSprite();
            ResolveSorting();

            badgeRoot = new GameObject("General Rank Badge")
            {
                hideFlags = HideFlags.DontSave
            };

            shadow = CreatePart(
                "Iron Shadow", ironColor, Vector2.one, new Vector2(0f, -0.025f),
                baseSortingOrder);
            frame = CreatePart(
                "Bronze Frame", bronzeColor, Vector2.one, Vector2.zero,
                baseSortingOrder + 1);
            innerPlate = CreatePart(
                "Dark Stone Face", stoneColor, Vector2.one, Vector2.zero,
                baseSortingOrder + 2);
            topHighlight = CreatePart(
                "Bronze Top Highlight", bronzeHighlight, Vector2.one, Vector2.zero,
                baseSortingOrder + 3);
            bottomEdge = CreatePart(
                "Iron Bottom Edge", ironColor, Vector2.one, Vector2.zero,
                baseSortingOrder + 3);

            leftCap = CreatePart(
                "Left Iron Cap", bronzeColor, new Vector2(0.16f, 0.16f), Vector2.zero,
                baseSortingOrder + 1, 45f).transform;
            rightCap = CreatePart(
                "Right Iron Cap", bronzeColor, new Vector2(0.16f, 0.16f), Vector2.zero,
                baseSortingOrder + 1, 45f).transform;

            CreatePart(
                "Blue Crest Backing", ironColor, new Vector2(0.15f, 0.15f),
                new Vector2(0f, badgeHeight * 0.5f), baseSortingOrder + 4, 45f);
            CreatePart(
                "Blue Crest", accentColor, new Vector2(0.085f, 0.085f),
                new Vector2(0f, badgeHeight * 0.5f), baseSortingOrder + 5, 45f);

            skullMedallion = new GameObject("Skull Medallion").transform;
            skullMedallion.SetParent(badgeRoot.transform, false);
            CreatePart(
                "Skull Medallion Frame", bronzeHighlight, new Vector2(0.26f, 0.26f),
                Vector2.zero, baseSortingOrder + 4, 45f, skullMedallion);
            CreatePart(
                "Skull Medallion Face", ironColor, new Vector2(0.205f, 0.205f),
                Vector2.zero, baseSortingOrder + 5, 45f, skullMedallion);
            BuildSkullIcon();

            divider = CreatePart(
                "Skull Divider", bronzeColor, new Vector2(0.018f, badgeHeight * 0.60f),
                Vector2.zero, baseSortingOrder + 5).transform;

            GameObject labelObject = new GameObject("General Level Text");
            labelObject.transform.SetParent(badgeRoot.transform, false);
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = fontSize;
            label.characterSize = characterSize;
            label.color = textColor;
            label.text = "GEN 0";
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = baseSortingOrder + 7;

            ApplyBadgeWidth(baseBadgeWidth);
        }

        private void BuildSkullIcon()
        {
            if (skullIcon != null)
            {
                GameObject iconObject = new GameObject("Skull Icon");
                iconObject.transform.SetParent(skullMedallion, false);
                SpriteRenderer icon = iconObject.AddComponent<SpriteRenderer>();
                icon.sprite = skullIcon;
                icon.color = textColor;
                icon.sortingLayerID = sortingLayerId;
                icon.sortingOrder = baseSortingOrder + 6;

                Bounds spriteBounds = skullIcon.bounds;
                float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
                float iconScale = largestDimension > 0f ? 0.145f / largestDimension : 1f;
                iconObject.transform.localScale = Vector3.one * iconScale;
                return;
            }

            // Runtime-promoted Generals still need a complete badge when no prefab icon is assigned.
            CreatePart(
                "Skull Head", textColor, new Vector2(0.12f, 0.095f),
                new Vector2(0f, 0.025f), baseSortingOrder + 6, 0f, skullMedallion);
            CreatePart(
                "Skull Jaw", textColor, new Vector2(0.075f, 0.05f),
                new Vector2(0f, -0.045f), baseSortingOrder + 6, 0f, skullMedallion);
            CreatePart(
                "Skull Left Eye", stoneColor, new Vector2(0.026f, 0.026f),
                new Vector2(-0.028f, 0.028f), baseSortingOrder + 7, 0f, skullMedallion);
            CreatePart(
                "Skull Right Eye", stoneColor, new Vector2(0.026f, 0.026f),
                new Vector2(0.028f, 0.028f), baseSortingOrder + 7, 0f, skullMedallion);
            CreatePart(
                "Skull Nose", stoneColor, new Vector2(0.018f, 0.023f),
                new Vector2(0f, -0.005f), baseSortingOrder + 7, 45f, skullMedallion);
        }

        private void ApplyBadgeWidth(float width)
        {
            SetPartSize(shadow, new Vector2(width + 0.14f, badgeHeight + 0.13f));
            SetPartSize(frame, new Vector2(width + 0.08f, badgeHeight + 0.08f));
            SetPartSize(innerPlate, new Vector2(width, badgeHeight));
            SetPartSize(topHighlight, new Vector2(width - 0.06f, 0.022f));
            topHighlight.transform.localPosition =
                new Vector3(0f, badgeHeight * 0.5f - 0.02f, 0f);
            SetPartSize(bottomEdge, new Vector2(width - 0.04f, 0.035f));
            bottomEdge.transform.localPosition =
                new Vector3(0f, -badgeHeight * 0.5f + 0.025f, 0f);

            leftCap.localPosition = new Vector3(-width * 0.5f - 0.045f, 0f, 0f);
            rightCap.localPosition = new Vector3(width * 0.5f + 0.045f, 0f, 0f);

            float leftEdge = -width * 0.5f;
            skullMedallion.localPosition = new Vector3(leftEdge + 0.18f, 0f, 0f);
            divider.localPosition = new Vector3(leftEdge + 0.36f, 0f, 0f);
            label.transform.localPosition = new Vector3(0.18f, 0.005f, 0f);
        }

        private float CalculateBadgeWidth(int witnessedDeaths)
        {
            int digitCount = Mathf.Max(1, witnessedDeaths.ToString().Length);
            return baseBadgeWidth + Mathf.Max(0, digitCount - 1) * widthPerExtraDigit;
        }

        private void UpdateBadgeTransform()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = FindPrimaryBodyRenderer();
            }

            Vector3 position = transform.position + Vector3.up * fallbackHeight;
            if (bodyRenderer != null && bodyRenderer.enabled)
            {
                Bounds bounds = bodyRenderer.bounds;
                position = new Vector3(
                    bounds.center.x,
                    bounds.max.y + headClearance + badgeHeight * 0.5f,
                    transform.position.z);
            }

            badgeRoot.transform.position = position;
            badgeRoot.transform.rotation = Quaternion.identity;
            badgeRoot.transform.localScale = Vector3.one;
        }

        private SpriteRenderer FindPrimaryBodyRenderer()
        {
            SpriteRenderer selected = null;
            float selectedArea = 0f;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate == null || candidate.sprite == null)
                {
                    continue;
                }

                Vector3 size = candidate.bounds.size;
                float area = Mathf.Abs(size.x * size.y);
                if (selected == null || area > selectedArea)
                {
                    selected = candidate;
                    selectedArea = area;
                }
            }

            return selected;
        }

        private void ResolveSorting()
        {
            sortingLayerId = bodyRenderer != null ? bodyRenderer.sortingLayerID : 0;
            int bodyOrder = bodyRenderer != null ? bodyRenderer.sortingOrder : 0;
            baseSortingOrder = Mathf.Max(200, bodyOrder + 20);
        }

        private void CreateRuntimeSprite()
        {
            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "General Badge Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            runtimeTexture.SetPixel(0, 0, Color.white);
            runtimeTexture.Apply();

            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            runtimeSprite.name = "General Badge Pixel";
            runtimeSprite.hideFlags = HideFlags.DontSave;
        }

        private SpriteRenderer CreatePart(
            string partName,
            Color color,
            Vector2 size,
            Vector2 localPosition,
            int sortingOrder,
            float rotation = 0f,
            Transform parent = null)
        {
            GameObject part = new GameObject(partName);
            part.transform.SetParent(parent != null ? parent : badgeRoot.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            part.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = runtimeSprite;
            renderer.color = color;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void SetPartSize(SpriteRenderer renderer, Vector2 size)
        {
            if (renderer != null)
            {
                renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            }
        }

        private void SetVisible(bool visible)
        {
            if (badgeRoot != null && badgeRoot.activeSelf != visible)
            {
                badgeRoot.SetActive(visible);
            }
        }

        private void ResolveSkullIconForEditorPreview()
        {
#if UNITY_EDITOR
            if (skullIcon == null)
            {
                skullIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSkullAssetPath);
            }
#endif
        }

        private void OnDestroy()
        {
            if (badgeRoot != null)
            {
                Destroy(badgeRoot);
            }

            if (runtimeSprite != null)
            {
                Destroy(runtimeSprite);
            }

            if (runtimeTexture != null)
            {
                Destroy(runtimeTexture);
            }
        }
    }
}
