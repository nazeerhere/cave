using System;
using System.Collections.Generic;
using Cave.Axioms.Phase;
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
        private readonly List<int> activeStackCounts = new List<int>();
        private readonly List<SpriteRenderer> iconRenderers = new List<SpriteRenderer>();
        private readonly List<TextMesh> stackCountRenderers = new List<TextMesh>();
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
        private PhaseCombatState phase;
        private EnemyTeamBuffState teamBuffState;
        private int appliedStatusMask = int.MinValue;
        private int appliedImaginaryStacks = -1;
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
            phase = GetComponent<PhaseCombatState>();
            teamBuffState = GetComponent<EnemyTeamBuffState>();
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
            appliedImaginaryStacks = -1;
            rootVisible = false;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            RefreshOptionalDependenciesIfDue();
            int statusMask = BuildStatusMask(out int imaginaryStacks);
            if (statusMask != appliedStatusMask || imaginaryStacks != appliedImaginaryStacks)
            {
                appliedStatusMask = statusMask;
                appliedImaginaryStacks = imaginaryStacks;
                RebuildIconRow(statusMask, imaginaryStacks);
            }

            PositionAtFeet();
        }

        private int BuildStatusMask(out int imaginaryStacks)
        {
            imaginaryStacks = 0;
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

            if (phase != null && phase.LatentStacks > 0)
            {
                imaginaryStacks = phase.LatentStacks;
                AddStatus(ref mask, MobStatusIconKind.Imaginary);
            }


            if (teamBuffState != null)
            {
                AddTeamBuffIcon(ref mask, MobStatusIconKind.Stagger);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.Frenzied);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.StrengthBuff);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.ElementallyBuffed);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.Regeneration);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.GazeLock);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.TowerSuppression);
                AddTeamBuffIcon(ref mask, MobStatusIconKind.PinRoot);
            }

            return mask;
        }

        private void AddTeamBuffIcon(ref int mask, MobStatusIconKind kind)
        {
            if (teamBuffState.HasIcon(kind)) AddStatus(ref mask, kind);
        }

        private void RebuildIconRow(int statusMask, int imaginaryStacks)
        {
            RebuildActiveKinds(statusMask, imaginaryStacks);
            int visibleCount = 0;
            for (int index = 0; index < activeKinds.Count; index++)
            {
                MobStatusIconKind kind = activeKinds[index];
                Sprite icon = iconRegistry != null ? iconRegistry.GetIcon(kind) : null;
                if (icon == null)
                {
                    continue;
                }

                SpriteRenderer renderer = GetOrCreateRenderer(visibleCount++);
                renderer.sprite = icon;
                renderer.enabled = true;
                ConfigureStackCount(visibleCount - 1, activeStackCounts[index]);
            }

            for (int index = visibleCount; index < iconRenderers.Count; index++)
            {
                iconRenderers[index].enabled = false;
            }

            for (int index = visibleCount; index < stackCountRenderers.Count; index++)
            {
                stackCountRenderers[index].gameObject.SetActive(false);
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
                if (index < stackCountRenderers.Count && stackCountRenderers[index].gameObject.activeSelf)
                {
                    stackCountRenderers[index].transform.localPosition = renderer.transform.localPosition
                        + new Vector3(size * 0.32f, -size * 0.28f, -0.01f);
                    stackCountRenderers[index].characterSize = size * 0.15f;
                }
            }
        }

        private void ConfigureStackCount(int index, int stackCount)
        {
            if (stackCount <= 0)
            {
                if (index < stackCountRenderers.Count)
                {
                    stackCountRenderers[index].gameObject.SetActive(false);
                }

                return;
            }

            TextMesh text = GetOrCreateStackCountRenderer(index);
            text.text = stackCount.ToString();
            text.gameObject.SetActive(true);
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

        private TextMesh GetOrCreateStackCountRenderer(int index)
        {
            while (stackCountRenderers.Count <= index)
            {
                GameObject count = new GameObject("Status Stack Count") { hideFlags = HideFlags.DontSave };
                count.transform.SetParent(root.transform, false);
                TextMesh text = count.AddComponent<TextMesh>();
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 24;
                text.fontStyle = FontStyle.Bold;
                text.color = new Color(0.94f, 0.98f, 1f, 1f);
                MeshRenderer renderer = count.GetComponent<MeshRenderer>();
                if (bodyRenderer != null)
                {
                    renderer.sortingLayerID = bodyRenderer.sortingLayerID;
                    renderer.sortingOrder = bodyRenderer.sortingOrder + sortingOrderOffset + 1;
                }

                stackCountRenderers.Add(text);
            }

            return stackCountRenderers[index];
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

        private void RebuildActiveKinds(int statusMask, int imaginaryStacks)
        {
            activeKinds.Clear();
            activeStackCounts.Clear();
            for (int index = 0; index <= (int)MobStatusIconKind.Stoneglass; index++)
            {
                if ((statusMask & (1 << index)) != 0)
                {
                    MobStatusIconKind kind = (MobStatusIconKind)index;
                    activeKinds.Add(kind);
                    activeStackCounts.Add(kind == MobStatusIconKind.Imaginary ? imaginaryStacks : 0);
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
            if (teamBuffState == null) teamBuffState = GetComponent<EnemyTeamBuffState>();
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
                || possessedHost == null
                || teamBuffState == null;
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
            appliedImaginaryStacks = -1;
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
        }
    }
}
