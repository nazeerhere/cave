using System;
using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyWorldStatusIndicators : MonoBehaviour
    {
        private const string RegistryResourceName = "MobStatusIconRegistry";
        private const int BodyRendererRecoveryFrameInterval = 120;
        private const int OptionalDependencyRefreshFrameInterval = 120;

        [Header("Icon Source")]
        [SerializeField] private MobStatusIconRegistry iconRegistry;

        [Header("Feet Placement")]
        [SerializeField, Min(0.01f)] private float iconSize = 0.16f;
        [SerializeField, Min(0.01f)] private float iconSpacing = 0.05f;
        [SerializeField, Min(0f)] private float feetPadding = 0.08f;
        [SerializeField, Min(0.01f)] private float minimumIconSize = 0.1f;
        [SerializeField, Min(0.01f)] private float maximumIconSize = 0.2f;
        [SerializeField] private int sortingOrderOffset = 18;

        private readonly List<MobStatusIconKind> activeKinds = new List<MobStatusIconKind>();
        private readonly List<SpriteRenderer> iconRenderers = new List<SpriteRenderer>();
        private Damageable damageable;
        private SpriteRenderer bodyRenderer;
        private GameObject root;
        private EnemyStatusEffects statusEffects;
        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private EnemyElementalEmpowerment elementalEmpowerment;
        private EnemyCorruptionLifecycle corruption;
        private EyeBrain eye;
        private EyePossessedHost possessedHost;
        private int appliedStatusMask = int.MinValue;
        private int nextBodyRendererRecoveryFrame;
        private int nextOptionalDependencyRefreshFrame;
        private bool rootVisible;

        public static void EnsureOn(GameObject owner)
        {
            if (owner != null && owner.GetComponent<EnemyWorldStatusIndicators>() == null)
            {
                owner.AddComponent<EnemyWorldStatusIndicators>();
            }
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            statusEffects = GetComponent<EnemyStatusEffects>();
            stagger = GetComponent<EnemyStagger>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            elementalEmpowerment = GetComponent<EnemyElementalEmpowerment>();
            corruption = GetComponent<EnemyCorruptionLifecycle>();
            eye = GetComponent<EyeBrain>();
            possessedHost = GetComponent<EyePossessedHost>();
            if (iconRegistry == null)
            {
                iconRegistry = Resources.Load<MobStatusIconRegistry>(RegistryResourceName);
            }

            CreateRoot();
            ResolveAndCacheBodyRenderer();
        }

        private void OnEnable()
        {
            appliedStatusMask = int.MinValue;
            rootVisible = false;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            RefreshOptionalDependenciesIfDue();
            int statusMask = BuildStatusMask();
            if (statusMask != appliedStatusMask)
            {
                appliedStatusMask = statusMask;
                RebuildIconRow(statusMask);
            }

            PositionAtFeet();
        }

        private int BuildStatusMask()
        {
            if (damageable == null || damageable.CurrentHealth <= 0)
            {
                return 0;
            }

            int mask = 0;
            if (statusEffects != null)
            {
                if (statusEffects.IsBurning) AddStatus(ref mask, MobStatusIconKind.Burn);
                if (statusEffects.IsImmobilized) AddStatus(ref mask, MobStatusIconKind.PinRoot);
                else if (statusEffects.IsSlowed) AddStatus(ref mask, MobStatusIconKind.Slow);
            }

            if (stagger != null && stagger.IsStaggered) AddStatus(ref mask, MobStatusIconKind.Stagger);
            if (damageModifiers != null
                && (damageModifiers.HasModifier(EnemyDamageModifierType.NecromancerBuff)
                    || damageModifiers.HasModifier(EnemyDamageModifierType.WizardBuff)))
            {
                AddStatus(ref mask, MobStatusIconKind.StrengthBuff);
            }

            if (elementalEmpowerment != null && elementalEmpowerment.IsEmpowered)
            {
                AddStatus(ref mask, MobStatusIconKind.ElementallyBuffed);
            }

            if (corruption != null)
            {
                if (corruption.IsRegenerating) AddStatus(ref mask, MobStatusIconKind.Regeneration);
                if (corruption.IsFrenzied) AddStatus(ref mask, MobStatusIconKind.Frenzied);
            }

            if (eye != null)
            {
                if (eye.IsGazeActive) AddStatus(ref mask, MobStatusIconKind.GazeLock);
                if (eye.IsFrenzied) AddStatus(ref mask, MobStatusIconKind.Frenzied);
            }

            if (possessedHost != null && possessedHost.IsPossessed)
            {
                AddStatus(ref mask, MobStatusIconKind.Possessed);
            }

            return mask;
        }

        private void RebuildIconRow(int statusMask)
        {
            RebuildActiveKinds(statusMask);
            int visibleCount = 0;
            foreach (MobStatusIconKind kind in activeKinds)
            {
                Sprite icon = iconRegistry != null ? iconRegistry.GetIcon(kind) : null;
                if (icon == null)
                {
                    continue;
                }

                SpriteRenderer renderer = GetOrCreateRenderer(visibleCount++);
                renderer.sprite = icon;
                renderer.enabled = true;
            }

            for (int index = visibleCount; index < iconRenderers.Count; index++)
            {
                iconRenderers[index].enabled = false;
            }

            rootVisible = visibleCount > 0;
            if (root != null) root.SetActive(rootVisible);

            float size = ResolveBoundedIconSize();
            float totalWidth = visibleCount * size + Mathf.Max(0, visibleCount - 1) * iconSpacing;
            for (int index = 0; index < visibleCount; index++)
            {
                SpriteRenderer renderer = iconRenderers[index];
                renderer.transform.localPosition = new Vector3(
                    -totalWidth * 0.5f + size * (index + 0.5f) + iconSpacing * index,
                    0f,
                    0f);
                renderer.transform.localScale = Vector3.one * ResolveSpriteScale(renderer.sprite, size);
            }
        }

        private SpriteRenderer GetOrCreateRenderer(int index)
        {
            while (iconRenderers.Count <= index)
            {
                GameObject icon = new GameObject("Status Icon") { hideFlags = HideFlags.DontSave };
                icon.transform.SetParent(root.transform, false);
                SpriteRenderer renderer = icon.AddComponent<SpriteRenderer>();
                if (bodyRenderer != null)
                {
                    renderer.sortingLayerID = bodyRenderer.sortingLayerID;
                    renderer.sortingOrder = bodyRenderer.sortingOrder + sortingOrderOffset;
                }

                iconRenderers.Add(renderer);
            }

            return iconRenderers[index];
        }

        private void CreateRoot()
        {
            root = new GameObject(name + " Status Indicators") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            root.SetActive(false);
        }

        private void PositionAtFeet()
        {
            if (root == null || !rootVisible)
            {
                return;
            }

            RecoverBodyRendererIfNeeded();
            if (bodyRenderer == null)
            {
                root.transform.position = transform.position + Vector3.down * feetPadding;
            }
            else
            {
                root.transform.position = new Vector3(
                    bodyRenderer.bounds.center.x,
                    bodyRenderer.bounds.min.y - feetPadding,
                    transform.position.z);
            }
        }

        private float ResolveBoundedIconSize()
        {
            float reference = bodyRenderer != null
                ? bodyRenderer.bounds.size.x * 0.16f
                : iconSize;
            return Mathf.Clamp(reference, minimumIconSize, maximumIconSize);
        }

        private float ResolveSpriteScale(Sprite sprite, float targetWorldSize)
        {
            if (sprite == null)
            {
                return 1f;
            }

            // Normalize from the sprite's rendered world bounds rather than its
            // texture resolution/PPU. Compensate for inherited enemy scaling so
            // a status icon cannot grow into a body-sized card.
            float spriteDimension = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            float inheritedScale = root != null
                ? Mathf.Max(Abs(root.transform.lossyScale.x), Abs(root.transform.lossyScale.y))
                : 1f;
            return targetWorldSize / Mathf.Max(0.0001f, spriteDimension * inheritedScale);
        }

        private void RebuildActiveKinds(int statusMask)
        {
            activeKinds.Clear();
            for (int index = 0; index <= (int)MobStatusIconKind.Frenzied; index++)
            {
                if ((statusMask & (1 << index)) != 0)
                {
                    activeKinds.Add((MobStatusIconKind)index);
                }
            }
        }

        private void RefreshOptionalDependenciesIfDue()
        {
            if (Time.frameCount < nextOptionalDependencyRefreshFrame
                || !HasMissingDependencies())
            {
                return;
            }

            nextOptionalDependencyRefreshFrame = Time.frameCount
                + OptionalDependencyRefreshFrameInterval;
            if (damageable == null) damageable = GetComponent<Damageable>();
            if (statusEffects == null) statusEffects = GetComponent<EnemyStatusEffects>();
            if (stagger == null) stagger = GetComponent<EnemyStagger>();
            if (damageModifiers == null) damageModifiers = GetComponent<EnemyDamageModifiers>();
            if (elementalEmpowerment == null) elementalEmpowerment = GetComponent<EnemyElementalEmpowerment>();
            if (corruption == null) corruption = GetComponent<EnemyCorruptionLifecycle>();
            if (eye == null) eye = GetComponent<EyeBrain>();
            if (possessedHost == null) possessedHost = GetComponent<EyePossessedHost>();
        }

        private bool HasMissingDependencies()
        {
            return damageable == null
                || statusEffects == null
                || stagger == null
                || damageModifiers == null
                || elementalEmpowerment == null
                || corruption == null
                || eye == null
                || possessedHost == null;
        }

        private void RecoverBodyRendererIfNeeded()
        {
            // A destroyed visual can be recovered, but never by scanning a healthy
            // enemy hierarchy from its recurring presentation path.
            if (bodyRenderer != null || Time.frameCount < nextBodyRendererRecoveryFrame)
            {
                return;
            }

            ResolveAndCacheBodyRenderer();
        }

        private void ResolveAndCacheBodyRenderer()
        {
            nextBodyRendererRecoveryFrame = Time.frameCount + BodyRendererRecoveryFrameInterval;
            SpriteRenderer primary = null;
            float primaryScore = float.NegativeInfinity;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate == null
                    || candidate.sprite == null
                    || candidate.transform.IsChildOf(root != null ? root.transform : transform)
                    || IsAuxiliaryRenderer(candidate))
                {
                    continue;
                }

                float sizeScore = candidate.bounds.size.x * candidate.bounds.size.y;
                if (candidate.transform == transform)
                {
                    sizeScore += 1000f;
                }

                if (sizeScore > primaryScore)
                {
                    primary = candidate;
                    primaryScore = sizeScore;
                }
            }

            bodyRenderer = primary;
        }

        private static bool IsAuxiliaryRenderer(SpriteRenderer candidate)
        {
            string rendererName = candidate.gameObject.name;
            return Contains(rendererName, "weapon")
                || Contains(rendererName, "sword")
                || Contains(rendererName, "axe")
                || Contains(rendererName, "pickaxe")
                || Contains(rendererName, "projectile")
                || Contains(rendererName, "hitbox")
                || Contains(rendererName, "helper")
                || Contains(rendererName, "effect")
                || Contains(rendererName, "vfx")
                || Contains(rendererName, "particle")
                || Contains(rendererName, "status")
                || Contains(rendererName, "icon");
        }

        private static bool Contains(string value, string fragment)
        {
            return value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddStatus(ref int mask, MobStatusIconKind kind)
        {
            mask |= 1 << (int)kind;
        }

        private static float Abs(float value) => value < 0f ? -value : value;

        private void OnDisable()
        {
            rootVisible = false;
            appliedStatusMask = int.MinValue;
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
        }
    }
}
