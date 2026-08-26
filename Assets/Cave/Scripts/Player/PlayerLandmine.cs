using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerLandmine : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float armingDelay = 0.65f;
        [SerializeField, Min(0.1f)] private float triggerRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float blastRadius = 2.2f;
        [SerializeField, Min(0)] private int damage = 2;
        [SerializeField, Min(0f)] private float knockback = 11f;
        [SerializeField] private LayerMask enemyLayers = ~0;
        [SerializeField] private Color armedColor = new Color(1f, 0.55f, 0.08f, 0.9f);

        private readonly HashSet<Damageable> affected = new HashSet<Damageable>();
        private CircleCollider2D trigger;
        private LineRenderer ring;
        private Material ringMaterial;
        private DamageContext damageContext;
        private GameObject owner;
        private float armedAt;
        private bool isArmed;
        private bool hasDetonated;

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

        public void Arm(GameObject mineOwner, DamageContext context)
        {
            owner = mineOwner;
            damageContext = context.WithTraits(DamageTrait.AreaOfEffect | DamageTrait.StaggerHeavy);
            EnsureRuntimePresentation();
            armedAt = Time.time + armingDelay;
            isArmed = false;
        }

        private void Awake()
        {
            EnsureRuntimePresentation();
        }

        private void Update()
        {
            if (!isArmed && !hasDetonated && Time.time >= armedAt)
            {
                isArmed = true;
                if (ring != null)
                {
                    ring.startColor = armedColor;
                    ring.endColor = armedColor;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isArmed || hasDetonated)
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (IsValidEnemy(damageable))
            {
                Detonate();
            }
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
            affected.Clear();
            foreach (Collider2D overlap in Physics2D.OverlapCircleAll(
                transform.position,
                blastRadius,
                enemyLayers))
            {
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
            Destroy(gameObject, 0.05f);
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
            trigger.radius = triggerRadius;

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
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * triggerRadius);
            }
        }

        private void OnDestroy()
        {
            if (ringMaterial != null)
            {
                Destroy(ringMaterial);
            }
        }
    }

}
