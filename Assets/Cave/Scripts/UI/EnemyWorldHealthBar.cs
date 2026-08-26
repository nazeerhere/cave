using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyWorldHealthBar : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Vector2 worldOffset = new Vector2(0f, 2.15f);
        [SerializeField, Min(0.5f)] private float width = 2.35f;
        [SerializeField, Min(0.05f)] private float height = 0.16f;

        [Header("Visibility")]
        [SerializeField, Min(0f)] private float disengageHideDelay = 2.5f;

        [Header("Damage Trail")]
        [SerializeField, Min(0f)] private float trailDelay = 0.36f;
        [SerializeField, Min(0.01f)] private float trailCatchupDuration = 0.18f;

        [Header("Colors")]
        [SerializeField] private Color frameColor = new Color(0.08f, 0.065f, 0.075f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0.17f, 0.13f, 0.14f, 1f);
        [SerializeField] private Color trailingColor = new Color(1f, 0.48f, 0.08f, 1f);
        [SerializeField] private Color fillColor = new Color(0.78f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color segmentColor = new Color(0.09f, 0.06f, 0.07f, 0.9f);

        private Damageable damageable;
        private MobBrainBase brain;
        private GameObject barRoot;
        private Transform trailingFill;
        private Transform currentFill;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private float currentFraction = 1f;
        private float trailingFraction = 1f;
        private float trailReleaseAt;
        private float lastCombatVisibilityTime = float.NegativeInfinity;
        private bool subscribed;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            brain = GetComponent<MobBrainBase>();
            BuildVisuals();
            RefreshHealth(true);
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshHealth(true);
            SetVisible(false);
        }

        private void Update()
        {
            RefreshHealth(false);

            bool alive = damageable != null && damageable.CurrentHealth > 0;
            bool engaged = alive && IsEngagedState();
            if (engaged)
            {
                lastCombatVisibilityTime = Time.time;
            }

            bool visible = alive
                && (engaged || Time.time <= lastCombatVisibilityTime + disengageHideDelay);
            SetVisible(visible);

            if (Time.time >= trailReleaseAt && trailingFraction > currentFraction)
            {
                trailingFraction = Mathf.MoveTowards(
                    trailingFraction,
                    currentFraction,
                    Time.deltaTime / trailCatchupDuration);
                ApplyFill(trailingFill, trailingFraction);
            }
        }

        private void LateUpdate()
        {
            if (barRoot != null)
            {
                barRoot.transform.position = (Vector2)transform.position + worldOffset;
                barRoot.transform.rotation = Quaternion.identity;
                barRoot.transform.localScale = Vector3.one;
            }
        }

        private void RefreshHealth(bool initialize)
        {
            if (damageable == null || damageable.MaximumHealth <= 0)
            {
                return;
            }

            float nextFraction = Mathf.Clamp01(
                damageable.CurrentHealth / (float)damageable.MaximumHealth);
            if (initialize)
            {
                currentFraction = nextFraction;
                trailingFraction = nextFraction;
            }
            else if (nextFraction < currentFraction)
            {
                currentFraction = nextFraction;
                trailReleaseAt = Time.time + trailDelay;
            }
            else if (nextFraction > currentFraction)
            {
                currentFraction = nextFraction;
                trailingFraction = Mathf.Max(trailingFraction, currentFraction);
            }

            ApplyFill(currentFill, currentFraction);
            ApplyFill(trailingFill, trailingFraction);
        }

        private bool IsEngagedState()
        {
            if (brain == null)
            {
                return false;
            }

            return brain.CurrentState != MobBrainState.Patrol
                && brain.CurrentState != MobBrainState.ReturnToPatrol;
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (!blocked && appliedDamage > 0)
            {
                lastCombatVisibilityTime = Time.time;
                RefreshHealth(false);
            }
        }

        private void HandleDied()
        {
            SetVisible(false);
        }

        private void Subscribe()
        {
            if (subscribed || damageable == null)
            {
                return;
            }

            damageable.DamageResolved += HandleDamageResolved;
            damageable.Died += HandleDied;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || damageable == null)
            {
                return;
            }

            damageable.DamageResolved -= HandleDamageResolved;
            damageable.Died -= HandleDied;
            subscribed = false;
        }

        private void BuildVisuals()
        {
            if (barRoot != null)
            {
                return;
            }

            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Enemy Health Bar Pixel",
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
            runtimeSprite.name = "Enemy Health Bar Pixel";
            runtimeSprite.hideFlags = HideFlags.DontSave;

            barRoot = new GameObject($"{name} Elite Health Bar")
            {
                hideFlags = HideFlags.DontSave
            };
            barRoot.transform.position = (Vector2)transform.position + worldOffset;

            int sortingLayerId = 0;
            int sortingOrder = 200;
            SpriteRenderer sourceRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (sourceRenderer != null)
            {
                sortingLayerId = sourceRenderer.sortingLayerID;
                sortingOrder = Mathf.Max(sortingOrder, sourceRenderer.sortingOrder + 20);
            }

            CreatePart(
                "Frame",
                frameColor,
                new Vector2(width + 0.18f, height + 0.16f),
                Vector2.zero,
                sortingLayerId,
                sortingOrder);
            CreatePart(
                "Background",
                backgroundColor,
                new Vector2(width, height),
                Vector2.zero,
                sortingLayerId,
                sortingOrder + 1);
            trailingFill = CreatePart(
                "Damage Trail",
                trailingColor,
                new Vector2(width, height),
                Vector2.zero,
                sortingLayerId,
                sortingOrder + 2).transform;
            currentFill = CreatePart(
                "Health Fill",
                fillColor,
                new Vector2(width, height),
                Vector2.zero,
                sortingLayerId,
                sortingOrder + 3).transform;

            for (int index = 1; index < 5; index++)
            {
                float x = -width * 0.5f + width * index / 5f;
                CreatePart(
                    $"Segment {index}",
                    segmentColor,
                    new Vector2(0.025f, height),
                    new Vector2(x, 0f),
                    sortingLayerId,
                    sortingOrder + 4);
            }
        }

        private GameObject CreatePart(
            string partName,
            Color color,
            Vector2 size,
            Vector2 localPosition,
            int sortingLayerId,
            int sortingOrder)
        {
            GameObject part = new GameObject(partName)
            {
                hideFlags = HideFlags.DontSave
            };
            part.transform.SetParent(barRoot.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = runtimeSprite;
            renderer.color = color;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            return part;
        }

        private void ApplyFill(Transform fill, float fraction)
        {
            if (fill == null)
            {
                return;
            }

            float fillWidth = width * Mathf.Clamp01(fraction);
            fill.localScale = new Vector3(fillWidth, height, 1f);
            fill.localPosition = new Vector3(
                -width * 0.5f + fillWidth * 0.5f,
                0f,
                0f);
        }

        private void SetVisible(bool visible)
        {
            if (barRoot != null && barRoot.activeSelf != visible)
            {
                barRoot.SetActive(visible);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (barRoot != null)
            {
                Destroy(barRoot);
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

        private void OnValidate()
        {
            width = Mathf.Max(0.5f, width);
            height = Mathf.Max(0.05f, height);
            trailCatchupDuration = Mathf.Max(0.01f, trailCatchupDuration);
        }
    }
}
