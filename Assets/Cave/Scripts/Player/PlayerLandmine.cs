using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Axioms;
using Cave.Axioms.Elemental;
using Cave.Enemies;
using Cave.FieldControl;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>Immutable observation of one ordinary disk pulse; it does not alter pulse targeting or damage.</summary>
    public sealed class PlayerLandminePulseContext
    {
        public PlayerLandminePulseContext(GameObject triggeringTarget, IReadOnlyList<GameObject> affectedTargets)
        {
            TriggeringTarget = triggeringTarget;
            AffectedTargets = affectedTargets;
        }
        public GameObject TriggeringTarget { get; }
        public IReadOnlyList<GameObject> AffectedTargets { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerLandmine : MonoBehaviour
    {
        internal const int OverlapQueryCapacity = 64;

        [SerializeField, Min(0f)] private float armingDelay = 0.65f;
        [SerializeField, Min(0.1f)] private float triggerRadius = 1.95f;
        [SerializeField, Min(0.1f)] private float localPulseRadius = 3.6f;
        [SerializeField, Min(0f)] private float localPulseForce = 7f;
        [SerializeField, Min(0.05f)] private float localPulseCooldown = 0.9f;
        [SerializeField, Min(0f)] private float localPulseEnergyCost = 0.8f;
        [Header("Axiom Potential (provisional tuning)")]
        [SerializeField, Min(0f)] private float localPulsePotentialInput = 1f;
        [SerializeField, Min(0f)] private float detonationPotentialInput = 2f;
        [Header("Player-to-Disk Pull Corridor (provisional tuning)")]
        [SerializeField, Min(0f)] private float corridorHalfWidth = 0.55f;
        [SerializeField, Min(0f)] private float corridorPullRange = 1.2f;
        [SerializeField, Min(0f)] private float corridorPullForce = 10.5f;
        [SerializeField, Min(0.1f)] private float blastRadius = 6.6f;
        [SerializeField, Min(0)] private int damage = 2;
        [SerializeField, Min(0f)] private float knockback = 11f;
        [SerializeField] private LayerMask enemyLayers = ~0;
        [SerializeField] private Color armedColor = new Color(1f, 0.55f, 0.08f, 0.9f);
        [Header("Editable Presentation")]
        [SerializeField] private GameObject diskVisualPrefab;
        [SerializeField] private GameObject localPulseVisualPrefab;
        [SerializeField] private GameObject overloadVisualPrefab;
        [SerializeField] private GameObject sacrificeExplosionVisualPrefab;

        private readonly HashSet<Damageable> affected = new HashSet<Damageable>();
        // The balanced radii cover nine times the former area. Keep the query
        // non-allocating while raising its headroom so dense encounters do not
        // silently truncate pulse or detonation targets.
        private readonly Collider2D[] pulseHits = new Collider2D[OverlapQueryCapacity];
        private CircleCollider2D trigger;
        private LineRenderer ring;
        private Material ringMaterial;
        private OblivionDiskVisual diskVisual;
        private DamageContext damageContext;
        private GameObject owner;
        private FieldNode fieldNode;
        private float armedAt;
        private bool isArmed;
        private bool hasDetonated;
        private bool sacrificePending;
        private bool removalNotified;
        private float nextLocalPulseAt;
        private int progressionDamageBonus;
        private float progressionAreaMultiplier = 1f;
        private float progressionPullStrengthMultiplier = 1f;
        private float tacticalGuardUntil;
        private float tacticalSurgeUntil;
        private bool tacticalSelected;

        public event Action<PlayerLandmine> Removed;
        /// <summary>Raised once an armed ordinary disk has affected at least one valid enemy through its existing pulse path.</summary>
        public event Action<PlayerLandmine> MeaningfulPulse;
        /// <summary>Optional typed observation of the actual collider-triggered focal target and affected set.</summary>
        public event Action<PlayerLandmine, PlayerLandminePulseContext> MeaningfulPulseResolved;
        /// <summary>Lifecycle observation only; consumers must not alter ordinary disk behavior.</summary>
        public event Action<PlayerLandmine> StateChanged;
        public bool IsActiveFieldNode => fieldNode != null && fieldNode.CanContribute;
        public bool IsArmed => isArmed;
        public bool HasDetonated => hasDetonated;
        public bool IsOperational => isArmed && !hasDetonated && !sacrificePending && IsActiveFieldNode;
        public float LocalPulseRadius => localPulseRadius * progressionAreaMultiplier;
        public float TriggerRadius => triggerRadius;
        public float BlastRadius => blastRadius * progressionAreaMultiplier;
        public FieldNode FieldNode => fieldNode;
        public float SurvivabilityFraction => fieldNode != null ? fieldNode.SurvivabilityFraction : 1f;
        public ulong CreationOrder => fieldNode != null ? fieldNode.CreationOrder : 0UL;
        public bool TacticalGuardActive => Time.time < tacticalGuardUntil;
        public bool TacticalSurgeActive => Time.time < tacticalSurgeUntil;
        public float EffectiveLocalPulseForce => ResolveTacticalPulseForce(
            localPulseForce, TacticalGuardActive, TacticalSurgeActive);
        public float EffectiveLocalPulseCooldown => ResolveTacticalPulseCooldown(
            localPulseCooldown, TacticalSurgeActive);
        /// <summary>Observation seam for additive runtime state; it never changes disk combat behavior.</summary>
        public void NotifyObservedStateChanged() { StateChanged?.Invoke(this); }

        public void ApplyTacticalGuard(float duration)
        {
            tacticalGuardUntil = Mathf.Max(tacticalGuardUntil, Time.time + Mathf.Max(0f, duration));
            fieldNode?.ApplyTacticalGuard(duration, 0.6f);
            StateChanged?.Invoke(this);
        }

        public void ApplyTacticalSurge(float duration)
        {
            tacticalSurgeUntil = Mathf.Max(tacticalSurgeUntil, Time.time + Mathf.Max(0f, duration));
            StateChanged?.Invoke(this);
        }

        public void ClearTacticalModifiers()
        {
            tacticalGuardUntil = 0f;
            tacticalSurgeUntil = 0f;
            fieldNode?.ClearTacticalGuard();
            SetTacticalSelected(false);
            StateChanged?.Invoke(this);
        }

        public void SetTacticalSelected(bool selected)
        {
            tacticalSelected = selected;
            RefreshRingColor();
        }

        internal static float ResolveTacticalPulseForce(float baseline, bool guard, bool surge)
        {
            float multiplier = (guard ? 1.4f : 1f) * (surge ? 1.5f : 1f);
            return Mathf.Max(0f, baseline) * multiplier;
        }

        internal static float ResolveTacticalPulseCooldown(float baseline, bool surge)
        {
            return Mathf.Max(0.05f, baseline * (surge ? 0.5f : 1f));
        }

        public void ConfigureFallback(
            float delay,
            float triggerDistance,
            float pulseRadius,
            float explosionRadius,
            int explosionDamage,
            float explosionKnockback,
            LayerMask targets)
        {
            armingDelay = Mathf.Max(0f, delay);
            ConfigureEffectiveRanges(triggerDistance, pulseRadius, explosionRadius, targets);
            damage = Mathf.Max(0, explosionDamage);
            knockback = Mathf.Max(0f, explosionKnockback);
        }

        /// <summary>
        /// Applies the authoritative authored radii as absolute values. Calling
        /// this more than once cannot compound the balance multipliers, and it
        /// keeps prefab-backed and fallback disks on the same effective values.
        /// </summary>
        public void ConfigureEffectiveRanges(
            float triggerDistance,
            float pulseRadius,
            float explosionRadius,
            LayerMask targets)
        {
            triggerRadius = Mathf.Max(0.1f, triggerDistance);
            localPulseRadius = Mathf.Max(0.1f, pulseRadius);
            blastRadius = Mathf.Max(triggerRadius, explosionRadius);
            enemyLayers = targets;
            EnsureRuntimePresentation();
        }

        public void ConfigurePresentation(GameObject disk, GameObject pulse, GameObject overload, GameObject explosion)
        {
            diskVisualPrefab = disk;
            localPulseVisualPrefab = pulse;
            overloadVisualPrefab = overload;
            sacrificeExplosionVisualPrefab = explosion;
            if (diskVisualPrefab != null && transform.Find("Oblivion Disk Visual") == null)
            {
                GameObject instance = Instantiate(diskVisualPrefab, transform);
                instance.name = "Oblivion Disk Visual";
                if (diskVisual == null) diskVisual = GetComponent<OblivionDiskVisual>();
                diskVisual?.UseRenderer(instance.GetComponentInChildren<SpriteRenderer>(true));
            }
        }

        /// <summary>
        /// Snapshot permanent disk upgrades when the disk is placed. Existing
        /// nodes retain their authored runtime shape; new ranks affect later
        /// placements without changing the field-network authority.
        /// </summary>
        public void ConfigureProgressionUpgrades(int damageBonus, float areaMultiplier, float pullStrengthMultiplier)
        {
            progressionDamageBonus = Mathf.Max(0, damageBonus);
            progressionAreaMultiplier = Mathf.Max(1f, areaMultiplier);
            progressionPullStrengthMultiplier = Mathf.Max(1f, pullStrengthMultiplier);
            EnsureRuntimePresentation();
        }

        public void Arm(GameObject mineOwner, DamageContext context)
        {
            owner = mineOwner;
            damageContext = context.WithTraits(DamageTrait.AreaOfEffect | DamageTrait.StaggerHeavy);
            EnsureRuntimePresentation();
            diskVisual?.SetArming();
            armedAt = Time.time + armingDelay;
            isArmed = false;
            StateChanged?.Invoke(this);
        }

        public void ConfigureFieldNode(FieldNetwork network, FieldOwnerTeam team, int health, float energy, float lifetime, ulong placementOrder)
        {
            if (fieldNode != null) fieldNode.StateChanged -= HandleFieldNodeStateChanged;
            if (fieldNode == null) fieldNode = GetComponent<FieldNode>();
            if (fieldNode == null) fieldNode = gameObject.AddComponent<FieldNode>();
            fieldNode.Configure(network, team, health, energy, lifetime, placementOrder);
            fieldNode.StateChanged -= HandleFieldNodeStateChanged;
            fieldNode.StateChanged += HandleFieldNodeStateChanged;
            diskVisual?.Configure(fieldNode);
        }

        public void Sacrifice()
        {
            if (hasDetonated || sacrificePending) return;
            fieldNode?.Sacrifice();
            sacrificePending = true;
            diskVisual?.SetOverload();
            SpawnPresentation(overloadVisualPrefab, 0.2f);
            StartCoroutine(DetonateAfterOverload());
        }

        private void Awake()
        {
            EnsureRuntimePresentation();
        }

        private void Update()
        {
            if (!isArmed && !hasDetonated && !sacrificePending && Time.time >= armedAt)
            {
                isArmed = true;
                if (ring != null)
                {
                    ring.startColor = armedColor;
                    ring.endColor = armedColor;
                }
                diskVisual?.SetActive();
                RefreshRingColor();
                StateChanged?.Invoke(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isArmed || hasDetonated || sacrificePending)
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (IsValidEnemy(damageable) && Time.time >= nextLocalPulseAt)
            {
                PulseLocalField(damageable.gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isArmed || hasDetonated || sacrificePending || Time.time < nextLocalPulseAt) return;
            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (IsValidEnemy(damageable)) PulseLocalField(damageable.gameObject);
        }

        private void PulseLocalField(GameObject triggeringTarget)
        {
            nextLocalPulseAt = Time.time + EffectiveLocalPulseCooldown;
            affected.Clear();
            float pulseRadius = LocalPulseRadius;
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, pulseRadius, pulseHits, enemyLayers);
            for (int index = 0; index < count; index++)
            {
                Damageable target = pulseHits[index] != null
                    ? pulseHits[index].GetComponentInParent<Damageable>()
                    : null;
                if (!IsValidEnemy(target) || !affected.Add(target)) continue;
                AxiomPhenomenonApplicationBridge.Apply(
                    target.gameObject,
                    AxiomKind.Potential,
                    localPulsePotentialInput,
                    owner != null ? owner : gameObject,
                    target.gameObject,
                    Time.time);
                KnockbackReceiver receiver = target.GetComponent<KnockbackReceiver>();
                if (receiver == null) continue;
                Vector2 away = (Vector2)target.transform.position - (Vector2)transform.position;
                if (away.sqrMagnitude <= 0.001f) away = Vector2.right;
                bool inPullCorridor = owner != null && OblivionTrapPullGeometry.IsWithinCorridor(
                    owner.transform.position,
                    target.transform.position,
                    transform.position,
                    corridorHalfWidth,
                    corridorPullRange);
                Vector2 force = inPullCorridor
                    ? ((Vector2)transform.position - (Vector2)target.transform.position).normalized
                        * corridorPullForce * progressionPullStrengthMultiplier
                    : (away.normalized + Vector2.up * 0.12f).normalized * EffectiveLocalPulseForce;
                receiver.ApplyKnockback(force);
            }
            fieldNode?.DrainEnergy(localPulseEnergyCost);
            if (affected.Count > 0)
            {
                MeaningfulPulse?.Invoke(this);
                List<GameObject> targets = new List<GameObject>(affected.Count);
                foreach (Damageable target in affected) if (target != null) targets.Add(target.gameObject);
                targets.Sort((left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
                MeaningfulPulseResolved?.Invoke(this, new PlayerLandminePulseContext(
                    triggeringTarget, targets.AsReadOnly()));
            }
            SpawnPresentation(localPulseVisualPrefab, 0.35f);
            AreaPulseEffect.Create(transform.position, pulseRadius, armedColor, 0.2f);
        }

        private void Detonate()
        {
            if (hasDetonated)
            {
                return;
            }

            hasDetonated = true;
            isArmed = false;
            StateChanged?.Invoke(this);
            trigger.enabled = false;
            diskVisual?.PlayExplosion();
            SpawnPresentation(sacrificeExplosionVisualPrefab, 0.5f);
            affected.Clear();
            float resolvedBlastRadius = BlastRadius;
            int overlapCount = Physics2D.OverlapCircleNonAlloc(transform.position, resolvedBlastRadius, pulseHits, enemyLayers);
            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D overlap = pulseHits[index];
                if (overlap == null) continue;
                Damageable target = overlap.GetComponentInParent<Damageable>();
                if (!IsValidEnemy(target) || !affected.Add(target))
                {
                    continue;
                }

                int appliedDamage = target.TakeDamageResolved(damage + progressionDamageBonus, damageContext);
                if (!target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (appliedDamage > 0)
                {
                    AxiomPhenomenonApplicationBridge.Apply(
                        target.gameObject,
                        AxiomKind.Potential,
                        detonationPotentialInput,
                        owner != null ? owner : gameObject,
                        target.gameObject,
                        Time.time);
                }

                target.GetComponent<EnemyStagger>()?.TryStagger(StaggerStrength.Heavy);
                KnockbackReceiver receiver = target.GetComponent<KnockbackReceiver>();
                if (receiver != null)
                {
                    Vector2 away = (Vector2)target.transform.position - (Vector2)transform.position;
                    if (away.sqrMagnitude <= 0.001f)
                    {
                        away = Vector2.right;
                    }

                    receiver.ApplyKnockback((away.normalized + Vector2.up * 0.25f).normalized * knockback);
                }
            }

            AreaPulseEffect.Create(transform.position, resolvedBlastRadius, armedColor, 0.32f);
            Destroy(gameObject, 0.35f);
        }

        private System.Collections.IEnumerator DetonateAfterOverload()
        {
            yield return new WaitForSeconds(0.12f);
            sacrificePending = false;
            Detonate();
        }

        private bool IsValidEnemy(Damageable damageable)
        {
            return damageable != null
                && damageable.CurrentHealth > 0
                && damageable.gameObject != owner
                && damageable.GetComponent<EnemyArchetypeProfile>() != null;
        }

        private void EnsureRuntimePresentation()
        {
            trigger = GetComponent<CircleCollider2D>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<CircleCollider2D>();
            }

            trigger.isTrigger = true;
            // Enter/Stay is the arming trigger. The wider local pulse is an
            // explicit overlap query and must not silently broaden this collider.
            trigger.radius = TriggerRadius;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            ring = GetComponent<LineRenderer>();
            if (ring == null)
            {
                ring = gameObject.AddComponent<LineRenderer>();
                ringMaterial = new Material(Shader.Find("Sprites/Default"));
                ring.material = ringMaterial;
                ring.loop = true;
                ring.useWorldSpace = false;
                ring.positionCount = 20;
                ring.startWidth = 0.08f;
                ring.endWidth = 0.08f;
                ring.sortingOrder = 8;
            }

            Color unarmed = new Color(0.5f, 0.5f, 0.55f, 0.75f);
            ring.startColor = unarmed;
            ring.endColor = unarmed;
            for (int index = 0; index < ring.positionCount; index++)
            {
                float angle = index / (float)ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * LocalPulseRadius);
            }

            diskVisual = GetComponent<OblivionDiskVisual>();
            if (diskVisual == null) diskVisual = gameObject.AddComponent<OblivionDiskVisual>();
            RefreshRingColor();
        }

        private void RefreshRingColor()
        {
            if (ring == null) return;
            Color color = tacticalSelected
                ? new Color(0.2f, 0.95f, 1f, 1f)
                : isArmed ? armedColor : new Color(0.5f, 0.5f, 0.55f, 0.75f);
            ring.startColor = color;
            ring.endColor = color;
        }

        private void SpawnPresentation(GameObject prefab, float lifetime)
        {
            if (prefab == null) return;
            GameObject instance = Instantiate(prefab, transform.position, Quaternion.identity);
            Destroy(instance, Mathf.Max(0.05f, lifetime));
        }

        private void OnDestroy()
        {
            NotifyRemoved();
            if (fieldNode != null) fieldNode.StateChanged -= HandleFieldNodeStateChanged;
            if (ringMaterial != null)
            {
                Destroy(ringMaterial);
            }
        }

        private void OnDisable() { NotifyRemoved(); }

        private void NotifyRemoved()
        {
            if (removalNotified) return;
            removalNotified = true;
            StateChanged?.Invoke(this);
            Removed?.Invoke(this);
        }

        private void HandleFieldNodeStateChanged(FieldNode _)
        {
            StateChanged?.Invoke(this);
        }
    }

}
