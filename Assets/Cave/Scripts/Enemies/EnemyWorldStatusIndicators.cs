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

        [Header("Icon Source")]
        [SerializeField] private MobStatusIconRegistry iconRegistry;

        [Header("Feet Placement")]
        [SerializeField, Min(0.01f)] private float iconSize = 0.28f;
        [SerializeField, Min(0.01f)] private float iconSpacing = 0.05f;
        [SerializeField, Min(0f)] private float feetPadding = 0.08f;
        [SerializeField, Min(0.01f)] private float minimumIconSize = 0.18f;
        [SerializeField, Min(0.01f)] private float maximumIconSize = 0.38f;
        [SerializeField] private int sortingOrderOffset = 18;

        private readonly List<MobStatusIconKind> activeKinds = new List<MobStatusIconKind>();
        private readonly List<SpriteRenderer> iconRenderers = new List<SpriteRenderer>();
        private Damageable damageable;
        private GameObject root;
        private EnemyStatusEffects statusEffects;
        private EnemyStagger stagger;
        private EnemyDamageModifiers damageModifiers;
        private EnemyElementalEmpowerment elementalEmpowerment;
        private EnemyCorruptionLifecycle corruption;
        private EyeBrain eye;
        private EyePossessedHost possessedHost;

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
        }

        private void LateUpdate()
        {
            RefreshActiveKinds();
            RefreshIcons();
            PositionAtFeet();
        }

        private void RefreshActiveKinds()
        {
            activeKinds.Clear();
            if (statusEffects == null) statusEffects = GetComponent<EnemyStatusEffects>();
            if (stagger == null) stagger = GetComponent<EnemyStagger>();
            if (damageModifiers == null) damageModifiers = GetComponent<EnemyDamageModifiers>();
            if (elementalEmpowerment == null) elementalEmpowerment = GetComponent<EnemyElementalEmpowerment>();
            if (corruption == null) corruption = GetComponent<EnemyCorruptionLifecycle>();
            if (eye == null) eye = GetComponent<EyeBrain>();
            if (possessedHost == null) possessedHost = GetComponent<EyePossessedHost>();
            if (damageable == null || damageable.CurrentHealth <= 0)
            {
                return;
            }

            if (statusEffects != null)
            {
                if (statusEffects.IsBurning) activeKinds.Add(MobStatusIconKind.Burn);
                if (statusEffects.IsImmobilized) activeKinds.Add(MobStatusIconKind.PinRoot);
                else if (statusEffects.IsSlowed) activeKinds.Add(MobStatusIconKind.Slow);
            }

            if (stagger != null && stagger.IsStaggered) activeKinds.Add(MobStatusIconKind.Stagger);
            if (damageModifiers != null
                && (damageModifiers.HasModifier(EnemyDamageModifierType.NecromancerBuff)
                    || damageModifiers.HasModifier(EnemyDamageModifierType.WizardBuff)))
            {
                activeKinds.Add(MobStatusIconKind.StrengthBuff);
            }

            if (elementalEmpowerment != null && elementalEmpowerment.IsEmpowered)
            {
                activeKinds.Add(MobStatusIconKind.ElementallyBuffed);
            }

            if (corruption != null)
            {
                if (corruption.IsRegenerating) activeKinds.Add(MobStatusIconKind.Regeneration);
                if (corruption.IsFrenzied) activeKinds.Add(MobStatusIconKind.Frenzied);
            }

            if (eye != null)
            {
                if (eye.IsGazeActive) activeKinds.Add(MobStatusIconKind.GazeLock);
                if (eye.IsFrenzied) activeKinds.Add(MobStatusIconKind.Frenzied);
            }

            if (possessedHost != null && possessedHost.IsPossessed)
            {
                activeKinds.Add(MobStatusIconKind.Possessed);
            }
        }

        private void RefreshIcons()
        {
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

            if (root != null)
            {
                root.SetActive(visibleCount > 0);
            }

            float size = ResolveBoundedIconSize();
            float totalWidth = visibleCount * size + Mathf.Max(0, visibleCount - 1) * iconSpacing;
            for (int index = 0; index < visibleCount; index++)
            {
                SpriteRenderer renderer = iconRenderers[index];
                renderer.transform.localPosition = new Vector3(
                    -totalWidth * 0.5f + size * (index + 0.5f) + iconSpacing * index,
                    0f,
                    0f);
                renderer.transform.localScale = Vector3.one * size;
            }
        }

        private SpriteRenderer GetOrCreateRenderer(int index)
        {
            while (iconRenderers.Count <= index)
            {
                GameObject icon = new GameObject("Status Icon") { hideFlags = HideFlags.DontSave };
                icon.transform.SetParent(root.transform, false);
                SpriteRenderer renderer = icon.AddComponent<SpriteRenderer>();
                SpriteRenderer primary = FindPrimaryRenderer();
                if (primary != null)
                {
                    renderer.sortingLayerID = primary.sortingLayerID;
                    renderer.sortingOrder = primary.sortingOrder + sortingOrderOffset;
                }

                iconRenderers.Add(renderer);
            }

            return iconRenderers[index];
        }

        private void CreateRoot()
        {
            root = new GameObject(name + " Status Indicators") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
        }

        private void PositionAtFeet()
        {
            if (root == null)
            {
                return;
            }

            SpriteRenderer primary = FindPrimaryRenderer();
            if (primary == null)
            {
                root.transform.position = transform.position + Vector3.down * feetPadding;
            }
            else
            {
                root.transform.position = new Vector3(
                    primary.bounds.center.x,
                    primary.bounds.min.y - feetPadding,
                    transform.position.z);
            }

            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        private float ResolveBoundedIconSize()
        {
            SpriteRenderer primary = FindPrimaryRenderer();
            float reference = primary != null
                ? Mathf.Max(primary.bounds.size.x, primary.bounds.size.y) * 0.16f
                : iconSize;
            return Mathf.Clamp(Mathf.Max(iconSize, reference), minimumIconSize, maximumIconSize);
        }

        private SpriteRenderer FindPrimaryRenderer()
        {
            SpriteRenderer primary = null;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate == null || candidate.transform.IsChildOf(root != null ? root.transform : transform))
                {
                    continue;
                }

                if (primary == null || candidate.bounds.size.y > primary.bounds.size.y)
                {
                    primary = candidate;
                }
            }

            return primary;
        }

        private void OnDisable()
        {
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
        }
    }
}
