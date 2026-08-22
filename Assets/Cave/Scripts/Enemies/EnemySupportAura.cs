using System.Collections.Generic;
using Cave.Combat;
using Cave.Progression;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyArchetypeProfile))]
    public sealed class EnemySupportAura : MonoBehaviour
    {
        [SerializeField] private bool useSharedSettings = true;
        [SerializeField] private StrategicCombatSettings sharedSettings;
        [SerializeField] private LayerMask allyLayers = ~0;
        [SerializeField] private bool requireDifficultyEligibility;

        [Header("Support Overrides")]
        [SerializeField, Min(0.1f)] private float supportRadius = 5f;
        [SerializeField, Min(1f)] private float moveSpeedMultiplier = 1.2f;
        [SerializeField, Min(1)] private int maximumSupportedAllies = 6;
        [SerializeField, Min(0.1f)] private float healInterval = 3f;
        [SerializeField, Min(1)] private int healAmount = 1;
        [SerializeField] private Color auraColor = new Color(0.25f, 1f, 0.55f, 0.65f);

        private readonly HashSet<EnemyController> supportedControllers = new HashSet<EnemyController>();
        private readonly HashSet<EnemyController> currentControllers = new HashSet<EnemyController>();
        private readonly HashSet<Damageable> currentAllies = new HashSet<Damageable>();
        private Damageable ownDamageable;
        private WorldDifficultyManager difficultyManager;
        private LineRenderer auraRenderer;
        private Material auraMaterial;
        private float nextScanTime;
        private float nextHealTime;

        private float Radius => useSharedSettings && sharedSettings != null
            ? sharedSettings.SupportRadius
            : supportRadius;
        private float SpeedMultiplier => useSharedSettings && sharedSettings != null
            ? sharedSettings.SupportMoveSpeedMultiplier
            : moveSpeedMultiplier;
        private float HealInterval => useSharedSettings && sharedSettings != null
            ? sharedSettings.SupportHealInterval
            : healInterval;
        private int MaximumSupportedAllies => useSharedSettings && sharedSettings != null
            ? sharedSettings.MaximumSupportedAllies
            : maximumSupportedAllies;
        private int HealAmount => useSharedSettings && sharedSettings != null
            ? sharedSettings.SupportHealAmount
            : healAmount;

        private void Awake()
        {
            ownDamageable = GetComponent<Damageable>();
            GetComponent<EnemyArchetypeProfile>().AddRuntimeArchetype(EnemyArchetype.Support);
            CreateAuraVisual();
        }

        internal void ConfigureIfMissing(
            StrategicCombatSettings settings,
            WorldDifficultyManager worldDifficulty)
        {
            if (sharedSettings == null)
            {
                sharedSettings = settings;
            }

            difficultyManager = worldDifficulty;
            UpdateAuraVisual();
        }

        private void Update()
        {
            bool active = IsEligible();
            if (auraRenderer != null)
            {
                auraRenderer.enabled = active;
            }

            if (!active)
            {
                ClearSupport();
                return;
            }

            if (Time.time < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.time + 0.2f;
            bool shouldHeal = Time.time >= nextHealTime;
            if (shouldHeal)
            {
                nextHealTime = Time.time + HealInterval;
            }

            RefreshSupportedAllies(shouldHeal);
        }

        private bool IsEligible()
        {
            return !requireDifficultyEligibility
                || difficultyManager == null
                || sharedSettings == null
                || difficultyManager.DifficultyTier >= sharedSettings.SupportMinimumTier;
        }

        private void RefreshSupportedAllies(bool shouldHeal)
        {
            currentControllers.Clear();
            currentAllies.Clear();
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, Radius, allyLayers);
            foreach (Collider2D overlap in overlaps)
            {
                Damageable ally = overlap.GetComponentInParent<Damageable>();
                if (ally == null
                    || ally == ownDamageable
                    || ally.CurrentHealth <= 0
                    || ally.GetComponent<EnemyArchetypeProfile>() == null
                    || !currentAllies.Add(ally))
                {
                    continue;
                }

                if (currentAllies.Count > MaximumSupportedAllies)
                {
                    break;
                }

                EnemyController controller = ally.GetComponent<EnemyController>();
                if (controller != null)
                {
                    controller.SetSupportSpeedMultiplier(SpeedMultiplier);
                    currentControllers.Add(controller);
                }

                if (shouldHeal)
                {
                    ally.RestoreHealth(HealAmount);
                }
            }

            foreach (EnemyController previous in supportedControllers)
            {
                if (previous != null && !currentControllers.Contains(previous))
                {
                    previous.SetSupportSpeedMultiplier(1f);
                }
            }

            supportedControllers.Clear();
            foreach (EnemyController current in currentControllers)
            {
                supportedControllers.Add(current);
            }
        }

        private void ClearSupport()
        {
            foreach (EnemyController controller in supportedControllers)
            {
                controller?.SetSupportSpeedMultiplier(1f);
            }

            supportedControllers.Clear();
        }

        private void CreateAuraVisual()
        {
            GameObject visual = new GameObject("Support Aura Visual");
            visual.transform.SetParent(transform, false);
            auraRenderer = visual.AddComponent<LineRenderer>();
            auraMaterial = new Material(Shader.Find("Sprites/Default"));
            auraRenderer.material = auraMaterial;
            auraRenderer.useWorldSpace = false;
            auraRenderer.loop = true;
            auraRenderer.positionCount = 40;
            auraRenderer.startWidth = 0.06f;
            auraRenderer.endWidth = 0.06f;
            auraRenderer.startColor = auraColor;
            auraRenderer.endColor = auraColor;
            auraRenderer.sortingOrder = 5;
            UpdateAuraVisual();
        }

        private void UpdateAuraVisual()
        {
            if (auraRenderer == null)
            {
                return;
            }

            for (int index = 0; index < auraRenderer.positionCount; index++)
            {
                float angle = index / (float)auraRenderer.positionCount * Mathf.PI * 2f;
                auraRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius);
            }
        }

        private void OnDisable()
        {
            ClearSupport();
        }

        private void OnDestroy()
        {
            if (auraMaterial != null)
            {
                Destroy(auraMaterial);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = auraColor;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
