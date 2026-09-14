using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.FieldControl;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerLandmine : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float armingDelay = 0.65f;
        [SerializeField, Min(0.1f)] private float triggerRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float localPulseRadius = 1.2f;
        [SerializeField, Min(0f)] private float localPulseForce = 7f;
        [SerializeField, Min(0.05f)] private float localPulseCooldown = 0.9f;
        [SerializeField, Min(0f)] private float localPulseEnergyCost = 0.8f;
        [SerializeField, Min(0.1f)] private float blastRadius = 2.2f;
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
        private readonly Collider2D[] pulseHits = new Collider2D[16];
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

        public event Action<PlayerLandmine> Removed;
        public bool IsActiveFieldNode => fieldNode != null && fieldNode.CanContribute;
        public float SurvivabilityFraction => fieldNode != null ? fieldNode.SurvivabilityFraction : 1f;
        public ulong CreationOrder => fieldNode != null ? fieldNode.CreationOrder : 0UL;

        public void ConfigureFallback(
            float delay,
            float triggerDistance,
            float explosionRadius,
            int explosionDamage,
            float explosionKnockback,
            LayerMask targets)
        {
            armingDelay = Mathf.Max(0f, delay);
            triggerRadius = Mathf.Max(0.1f, triggerDistance);
            blastRadius = Mathf.Max(triggerRadius, explosionRadius);
            damage = Mathf.Max(0, explosionDamage);
            knockback = Mathf.Max(0f, explosionKnockback);
            enemyLayers = targets;
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

        public void Arm(GameObject mineOwner, DamageContext context)
        {
            owner = mineOwner;
            damageContext = context.WithTraits(DamageTrait.AreaOfEffect | DamageTrait.StaggerHeavy);
            EnsureRuntimePresentation();
            diskVisual?.SetArming();
            armedAt = Time.time + armingDelay;
            isArmed = false;
        }

        public void ConfigureFieldNode(FieldNetwork network, FieldOwnerTeam team, int health, float energy, float lifetime, ulong placementOrder)
        {
            if (fieldNode == null) fieldNode = GetComponent<FieldNode>();
            if (fieldNode == null) fieldNode = gameObject.AddComponent<FieldNode>();
            fieldNode.Configure(network, team, health, energy, lifetime, placementOrder);
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
                PulseLocalField();
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isArmed || hasDetonated || sacrificePending || Time.time < nextLocalPulseAt) return;
            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (IsValidEnemy(damageable)) PulseLocalField();
        }

        private void PulseLocalField()
        {
            nextLocalPulseAt = Time.time + localPulseCooldown;
            affected.Clear();
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, localPulseRadius, pulseHits, enemyLayers);
            for (int index = 0; index < count; index++)
            {
                Damageable target = pulseHits[index] != null
                    ? pulseHits[index].GetComponentInParent<Damageable>()
                    : null;
                if (!IsValidEnemy(target) || !affected.Add(target)) continue;
                KnockbackReceiver receiver = target.GetComponent<KnockbackReceiver>();
                if (receiver == null) continue;
                Vector2 away = (Vector2)target.transform.position - (Vector2)transform.position;
                if (away.sqrMagnitude <= 0.001f) away = Vector2.right;
                receiver.ApplyKnockback((away.normalized + Vector2.up * 0.12f).normalized * localPulseForce);
            }
            fieldNode?.DrainEnergy(localPulseEnergyCost);
            SpawnPresentation(localPulseVisualPrefab, 0.35f);
            AreaPulseEffect.Create(transform.position, localPulseRadius, armedColor, 0.2f);
        }

        private void Detonate()
        {
            if (hasDetonated)
            {
                return;
            }

            hasDetonated = true;
            isArmed = false;
            trigger.enabled = false;
            diskVisual?.PlayExplosion();
            SpawnPresentation(sacrificeExplosionVisualPrefab, 0.5f);
            affected.Clear();
            int overlapCount = Physics2D.OverlapCircleNonAlloc(transform.position, blastRadius, pulseHits, enemyLayers);
            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D overlap = pulseHits[index];
                if (overlap == null) continue;
                Damageable target = overlap.GetComponentInParent<Damageable>();
                if (!IsValidEnemy(target) || !affected.Add(target))
                {
                    continue;
                }

                target.TakeDamage(damage, damageContext);
                if (!target.gameObject.activeInHierarchy)
                {
                    continue;
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

            AreaPulseEffect.Create(transform.position, blastRadius, armedColor, 0.32f);
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
            trigger.radius = Mathf.Max(triggerRadius, localPulseRadius);

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
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * localPulseRadius);
            }

            diskVisual = GetComponent<OblivionDiskVisual>();
            if (diskVisual == null) diskVisual = gameObject.AddComponent<OblivionDiskVisual>();
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
            Removed?.Invoke(this);
        }
    }

}
