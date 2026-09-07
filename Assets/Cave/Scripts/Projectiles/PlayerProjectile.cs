using System.Collections.Generic;
using Cave.Axioms.Elemental;
using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using Cave.Progression;
using UnityEngine;

namespace Cave.Projectiles
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerProjectile : MonoBehaviour, IEnemyParryableProjectile
    {
        [Header("Projectile")]
        [SerializeField, Min(0.01f)] private float speed = 15f;
        [SerializeField, Min(1)] private int baseDamage = 1;
        [SerializeField, Min(0.01f)] private float lifetime = 5f;
        [SerializeField] private LayerMask environmentLayers;
        [SerializeField] private LayerMask damageableLayers;

        [Header("Slow Shot")]
        [SerializeField, Range(0f, 1f)] private float slowMovementMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float slowDuration = 2f;

        [Header("Burn Shot")]
        [SerializeField, Min(1)] private int burnDamage = 1;
        [SerializeField, Min(0.01f)] private float burnTickInterval = 1f;
        [SerializeField, Min(0f)] private float burnDuration = 3f;

        private Rigidbody2D body;
        private Collider2D projectileCollider;
        private SpecialMode firedMode;
        private int resolvedDamage;
        private DamageContext damageContext;
        private bool hasLaunched;
        private bool hasImpacted;
        private int remainingEnemyHits = 1;
        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private readonly ElementalAxiomApplicationReceipt axiomApplicationReceipt = new ElementalAxiomApplicationReceipt();
        private SpecialModeTier2Settings tier3Settings;
        private int firedTier = 1;
        private SpriteRenderer[] visualRenderers;
        private Color[] baseVisualColors;
        private Vector3 baseScale;
        private float tierScale = 1f;
        private Transform[] scalableVisualTransforms;
        private Vector3[] baseVisualScales;
        private TrailRenderer tierTrail;
        private Material tierTrailMaterial;
        private bool reflectedByEnemy;
        private GameObject enemyParryOwner;
        private FrenzyBreakActivation frenzyActivation;

        public int BaseDamage => baseDamage;
        public int SkillTier => firedTier;
        public int RemainingEnemyHits => remainingEnemyHits;
        public bool IsPiercing => remainingEnemyHits > 1;
        public bool CanBeEnemyParried => hasLaunched && !hasImpacted && !reflectedByEnemy;

        public void SetFrenzyBreakActivation(FrenzyBreakActivation activation)
        {
            frenzyActivation = activation;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileCollider.isTrigger = true;
            visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseVisualColors = new Color[visualRenderers.Length];
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                baseVisualColors[index] = visualRenderers[index].color;
            }

            baseScale = transform.localScale;
            CacheScalableVisualTransforms();
        }

        public void Launch(Vector2 direction, SpecialMode mode, int damage)
        {
            Launch(direction, mode, damage, default);
        }

        public void Launch(Vector2 direction, SpecialMode mode, int damage, DamageContext context)
        {
            Launch(direction, mode, damage, context, 1);
        }

        public void Launch(
            Vector2 direction,
            SpecialMode mode,
            int damage,
            DamageContext context,
            int maximumEnemyHits)
        {
            Launch(direction, mode, damage, context, maximumEnemyHits, 1, null);
        }

        public void Launch(
            Vector2 direction,
            SpecialMode mode,
            int damage,
            DamageContext context,
            int maximumEnemyHits,
            int skillTier,
            SpecialModeTier2Settings specialModeSettings)
        {
            firedMode = mode;
            resolvedDamage = Mathf.Max(1, damage);
            damageContext = context.WithTraits(DamageTrait.Projectile);
            remainingEnemyHits = Mathf.Max(1, maximumEnemyHits);
            firedTier = Mathf.Clamp(skillTier, 1, 3);
            tier3Settings = specialModeSettings;
            hitTargets.Clear();
            axiomApplicationReceipt.Clear();
            reflectedByEnemy = false;
            enemyParryOwner = null;
            hasLaunched = true;
            ConfigureTierVisuals();
            body.velocity = direction.normalized * speed;
            Invoke(nameof(Expire), lifetime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!hasLaunched || hasImpacted)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                if (reflectedByEnemy)
                {
                    playerHealth.TryTakeDamage(
                        resolvedDamage,
                        new DamageContext(enemyParryOwner, DamageTrait.Direct | DamageTrait.Projectile));
                    BeginImpact();
                    Destroy(gameObject);
                }

                return;
            }

            if ((damageableLayers.value & (1 << other.gameObject.layer)) != 0)
            {
                Damageable damageable = other.GetComponentInParent<Damageable>();
                if (reflectedByEnemy)
                {
                    return;
                }

                if (damageable != null && !hitTargets.Contains(damageable))
                {
                    EnemyDefenseController defense = damageable.GetComponent<EnemyDefenseController>();
                    if (defense != null && defense.TryParryProjectile(this))
                    {
                        return;
                    }

                    hitTargets.Add(damageable);
                    int frenzyDamage = resolvedDamage;
                    bool isFrenzyCritical = frenzyActivation != null
                        && frenzyActivation.TryResolveDamage(
                            resolvedDamage,
                            damageable,
                            out frenzyDamage);
                    int impactDamage = isFrenzyCritical ? frenzyDamage : resolvedDamage;
                    DamageContext impactContext = isFrenzyCritical
                        ? damageContext.WithTraits(DamageTrait.FrenzyCritical)
                        : damageContext;
                    int appliedDamage = damageable.TakeDamageResolved(
                        impactDamage,
                        impactContext);
                    ElementalAxiomCombatBridge.TryApplyProjectileHit(
                        damageable,
                        firedMode,
                        appliedDamage,
                        isFrenzyCritical,
                        damageContext,
                        Time.time,
                        axiomApplicationReceipt);
                    if (isFrenzyCritical)
                    {
                        frenzyActivation.ApplyImpact(
                            damageable,
                            body.velocity,
                            appliedDamage);
                    }
                    ApplyStatusEffect(damageable, appliedDamage > 0);
                    remainingEnemyHits--;
                    if (remainingEnemyHits <= 0)
                    {
                        BeginImpact();
                        Destroy(gameObject);
                    }
                }

                return;
            }

            if ((environmentLayers.value & (1 << other.gameObject.layer)) != 0)
            {
                CaveSfx.Play(CaveSfxCue.Hit, 0.55f);
                BeginImpact();
                Destroy(gameObject);
            }
        }

        public bool TryEnemyParry(GameObject defender)
        {
            if (!CanBeEnemyParried)
            {
                return false;
            }

            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            Vector2 direction = player != null
                ? (Vector2)player.transform.position - (Vector2)transform.position
                : -body.velocity;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.left;
            }

            reflectedByEnemy = true;
            enemyParryOwner = defender;
            remainingEnemyHits = 1;
            hitTargets.Clear();
            body.velocity = direction.normalized * speed;
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                visualRenderers[index].color = new Color(0.9f, 0.45f, 1f, 1f);
            }

            return true;
        }

        private void ApplyStatusEffect(Damageable damageable, bool wasSuccessfulDirectHit)
        {
            if (!damageable.gameObject.activeInHierarchy)
            {
                return;
            }

            EnemyStatusEffects statusEffects = damageable.GetComponent<EnemyStatusEffects>();
            if (statusEffects == null)
            {
                return;
            }

            if (firedMode == SpecialMode.SlowShot)
            {
                statusEffects.ApplySlow(slowMovementMultiplier, slowDuration);
                if (frenzyActivation != null && frenzyActivation.UseFrostTierThreePin)
                {
                    statusEffects.ApplyImmobilize(tier3Settings != null
                        ? tier3Settings.FrostTier2PinDuration
                        : slowDuration);
                }
                else if (tier3Settings != null && firedTier >= 3)
                {
                    statusEffects.ApplyImmobilize(tier3Settings.FrostTier3FreezeDuration);
                }
                else if (tier3Settings != null && firedTier >= 2)
                {
                    statusEffects.ApplyImmobilize(tier3Settings.FrostTier2PinDuration);
                }
            }
            else if (firedMode == SpecialMode.BurnShot)
            {
                if (firedTier >= 3 && tier3Settings != null)
                {
                    if (wasSuccessfulDirectHit)
                    {
                        statusEffects.ApplyBurnWithHeatPersistence(
                            burnDamage,
                            burnTickInterval,
                            burnDuration,
                            damageContext,
                            tier3Settings.BurnSpreadRadius,
                            tier3Settings.MaximumBurnSpreadTargets,
                            damageableLayers);
                    }
                    else
                    {
                        statusEffects.ApplyBurn(
                            burnDamage,
                            burnTickInterval,
                            burnDuration,
                            damageContext,
                            tier3Settings.BurnSpreadRadius,
                            tier3Settings.MaximumBurnSpreadTargets,
                            damageableLayers);
                    }
                }
                else
                {
                    if (wasSuccessfulDirectHit)
                    {
                        statusEffects.ApplyBurnWithHeatPersistence(
                            burnDamage,
                            burnTickInterval,
                            burnDuration,
                            damageContext);
                    }
                    else
                    {
                        statusEffects.ApplyBurn(burnDamage, burnTickInterval, burnDuration, damageContext);
                    }
                }
            }
        }

        private void BeginImpact()
        {
            if (hasImpacted)
            {
                return;
            }

            hasImpacted = true;
            frenzyActivation?.Complete();
            if (firedTier >= 3
                && tier3Settings != null
                && firedMode == SpecialMode.SlowShot)
            {
                SlowFieldEffect.Create(
                    transform.position,
                    tier3Settings.SlowFieldRadius,
                    tier3Settings.SlowFieldDuration,
                    tier3Settings.SlowFieldMovementMultiplier,
                    damageableLayers,
                    tier3Settings.FrostFieldOutlineColor,
                    tier3Settings.FrostFieldFillColor);
            }

            body.velocity = Vector2.zero;
            body.simulated = false;
            projectileCollider.enabled = false;
        }

        private void Update()
        {
            frenzyActivation?.KeepAlive();
            if (hasLaunched && firedTier >= 3 && !hasImpacted)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.07f;
                ApplyVisualScale(tierScale * pulse);
            }
        }

        private void ConfigureTierVisuals()
        {
            if ((firedMode != SpecialMode.SlowShot && firedMode != SpecialMode.BurnShot)
                || tier3Settings == null)
            {
                return;
            }

            Color tierColor = tier3Settings.GetProjectileTierColor(firedMode, firedTier);
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                visualRenderers[index].color = Color.Lerp(baseVisualColors[index], tierColor, 0.78f);
            }

            tierScale = firedTier >= 3
                ? tier3Settings.Tier3ProjectileScale
                : firedTier >= 2
                    ? tier3Settings.Tier2ProjectileScale
                    : 1f;
            ApplyVisualScale(tierScale);

            if (firedTier < 2)
            {
                return;
            }

            tierTrail = gameObject.AddComponent<TrailRenderer>();
            tierTrailMaterial = new Material(Shader.Find("Sprites/Default"));
            tierTrail.material = tierTrailMaterial;
            tierTrail.time = firedTier >= 3
                ? tier3Settings.Tier3ProjectileTrailTime
                : tier3Settings.Tier2ProjectileTrailTime;
            tierTrail.startWidth = firedTier >= 3 ? 0.22f : 0.14f;
            tierTrail.endWidth = 0f;
            tierTrail.startColor = tierColor;
            Color transparent = tierColor;
            transparent.a = 0f;
            tierTrail.endColor = transparent;
            tierTrail.sortingOrder = ResolveVisualSortingOrder() - 1;
        }

        private void CacheScalableVisualTransforms()
        {
            List<Transform> transforms = new List<Transform>();

            foreach (SpriteRenderer spriteRenderer in visualRenderers)
            {
                if (spriteRenderer == null)
                {
                    continue;
                }

                Transform visualTransform = spriteRenderer.transform;

                // Never scale the projectile root. The Rigidbody2D and Collider2D live
                // on the root, so scaling it changes the physical collision volume.
                if (visualTransform == transform || transforms.Contains(visualTransform))
                {
                    continue;
                }

                transforms.Add(visualTransform);
            }

            scalableVisualTransforms = transforms.ToArray();
            baseVisualScales = new Vector3[scalableVisualTransforms.Length];

            for (int index = 0; index < scalableVisualTransforms.Length; index++)
            {
                baseVisualScales[index] = scalableVisualTransforms[index].localScale;
            }
        }

        private void ApplyVisualScale(float scaleMultiplier)
        {
            if (scalableVisualTransforms == null || baseVisualScales == null)
            {
                return;
            }

            for (int index = 0; index < scalableVisualTransforms.Length; index++)
            {
                Transform visualTransform = scalableVisualTransforms[index];
                if (visualTransform != null)
                {
                    visualTransform.localScale = baseVisualScales[index] * scaleMultiplier;
                }
            }
        }

        private int ResolveVisualSortingOrder()
        {
            int sortingOrder = 0;
            foreach (SpriteRenderer spriteRenderer in visualRenderers)
            {
                sortingOrder = Mathf.Max(sortingOrder, spriteRenderer.sortingOrder);
            }

            return sortingOrder;
        }

        private void Expire()
        {
            if (!hasLaunched || hasImpacted)
            {
                return;
            }

            BeginImpact();
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            frenzyActivation?.Complete();
            frenzyActivation = null;
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }

            hitTargets.Clear();
            ApplyVisualScale(1f);
            transform.localScale = baseScale;
            CancelInvoke();
        }

        private void OnDestroy()
        {
            if (tierTrailMaterial != null)
            {
                Destroy(tierTrailMaterial);
            }
        }
    }
}
