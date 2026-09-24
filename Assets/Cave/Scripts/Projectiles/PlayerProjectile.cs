using System.Collections.Generic;
using Cave.Axioms.Elemental;
using Cave.Axioms.Phase;
using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.Interactions;
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
        private Sprite[] baseVisualSprites;
        private SpriteDrawMode[] baseVisualDrawModes;
        private Vector2[] baseVisualSizes;
        private Vector3 baseScale;
        private float tierScale = 1f;
        private Transform[] scalableVisualTransforms;
        private Vector3[] baseVisualScales;
        private TrailRenderer tierTrail;
        private Material tierTrailMaterial;
        private bool reflectedByEnemy;
        private GameObject enemyParryOwner;
        private FrenzyBreakActivation frenzyActivation;
        private InteractionIdentity interactionIdentity;
        private bool interactionDestroyedReported;
        private bool claimSuspended;
        private bool runtimeReferencesCached;
        private bool isHeavyProjectile;
        private float heavyExplosionRadius;
        private GameObject launchOwner;
        private GameObject ownerOverride;
        private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
        private bool usesProvidedPresentation;
        private bool usesImaginaryPresentation;
        private float launchedAt;
        private Vector3 launchPosition;
        private bool firstContactTraced;
        private bool firstTerminalTraced;
        private bool firstMovementTraced;

        public int BaseDamage => baseDamage;
        public int SkillTier => firedTier;
        public int RemainingEnemyHits => remainingEnemyHits;
        public bool IsPiercing => remainingEnemyHits > 1;
        public bool IsHeavyProjectile => isHeavyProjectile;
        public bool UsesProvidedPresentation => usesProvidedPresentation;
        public bool UsesImaginaryPresentation => usesImaginaryPresentation;
        public bool CanBeEnemyParried => hasLaunched && !hasImpacted && !reflectedByEnemy;
        public InteractionIdentity InteractionIdentity => interactionIdentity;
        public bool IsClaimSuspended => claimSuspended;
        public bool CanBeClaimSuspended => hasLaunched
            && !hasImpacted
            && !claimSuspended
            && body != null
            && projectileCollider != null
            && interactionIdentity != null;

        public void SetFrenzyBreakActivation(FrenzyBreakActivation activation)
        {
            frenzyActivation = activation;
        }

        /// <summary>Explicit launcher ownership fallback when damage context is intentionally absent.</summary>
        public void SetOwner(GameObject owner)
        {
            ownerOverride = owner;
        }

        /// <summary>
        /// Configures the launched instance as the terminal Heavy expression of
        /// the current projectile path. Heavy never inherits rapid pierce.
        /// Root scale is intentional here: it defines both the visible and
        /// collision footprint, unlike cosmetic tier scaling on child visuals.
        /// </summary>
        public void ConfigureHeavyProjectile(float scaleMultiplier, float explosionRadius)
        {
            isHeavyProjectile = true;
            heavyExplosionRadius = Mathf.Max(0.1f, explosionRadius);
            remainingEnemyHits = ChargedProjectilePolicy.HeavyMaximumEnemyHits;
            damageContext = damageContext.WithTraits(DamageTrait.Heavy | DamageTrait.AreaOfEffect);
            transform.localScale = baseScale * Mathf.Max(1f, scaleMultiplier);
        }

        private void Awake()
        {
            CacheRuntimeReferences();
        }

        private bool CacheRuntimeReferences()
        {
            if (runtimeReferencesCached)
            {
                return body != null && projectileCollider != null;
            }

            body = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            if (body == null || projectileCollider == null)
            {
                Debug.LogError("[Cave] PlayerProjectile requires Rigidbody2D and Collider2D before launch.", this);
                return false;
            }

            projectileCollider.isTrigger = true;
            visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseVisualColors = new Color[visualRenderers.Length];
            baseVisualSprites = new Sprite[visualRenderers.Length];
            baseVisualDrawModes = new SpriteDrawMode[visualRenderers.Length];
            baseVisualSizes = new Vector2[visualRenderers.Length];
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                baseVisualColors[index] = visualRenderers[index].color;
                baseVisualSprites[index] = visualRenderers[index].sprite;
                baseVisualDrawModes[index] = visualRenderers[index].drawMode;
                baseVisualSizes[index] = visualRenderers[index].size;
            }

            baseScale = transform.localScale;
            CacheScalableVisualTransforms();
            runtimeReferencesCached = true;
            return true;
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
            if (!CacheRuntimeReferences())
            {
                return;
            }

            firedMode = mode;
            resolvedDamage = Mathf.Max(1, damage);
            damageContext = context.WithTraits(DamageTrait.Projectile);
            remainingEnemyHits = Mathf.Max(1, maximumEnemyHits);
            firedTier = Mathf.Clamp(skillTier, 1, 3);
            tier3Settings = specialModeSettings;
            isHeavyProjectile = false;
            heavyExplosionRadius = 0f;
            transform.localScale = baseScale;
            hitTargets.Clear();
            axiomApplicationReceipt.Clear();
            reflectedByEnemy = false;
            enemyParryOwner = null;
            hasLaunched = true;
            claimSuspended = false;
            interactionDestroyedReported = false;
            launchedAt = Time.time;
            launchPosition = transform.position;
            firstContactTraced = false;
            firstTerminalTraced = false;
            firstMovementTraced = false;
            ConfigureOwnerCollisionFiltering(damageContext.Source != null ? damageContext.Source : ownerOverride);
            ownerOverride = null;
            interactionIdentity = InteractionRuntime.TrackSpawn(
                gameObject,
                InteractionTraits.Projectile | InteractionTraits.Moving | InteractionTraits.ManaPowered,
                InteractionOwnership.Player,
                damageContext.Source,
                true,
                1);
            ConfigureTierVisuals();
            body.velocity = direction.normalized * speed;
            TraceSpawn();
            Invoke(nameof(Expire), lifetime);
        }

        /// <summary>
        /// Safely parks a live player projectile after a successful explicit
        /// Claim. Ownership remains entirely with ClaimResolver; this method
        /// only pauses projectile behaviour so future Repossession can reuse
        /// the same object and interaction identity.
        /// </summary>
        public bool TrySuspendForClaim(Transform holder)
        {
            if (!CacheRuntimeReferences() || !CanBeClaimSuspended)
            {
                return false;
            }

            claimSuspended = true;
            hasImpacted = true;
            frenzyActivation?.Complete();
            frenzyActivation = null;
            CancelInvoke();
            body.velocity = Vector2.zero;
            body.simulated = false;
            projectileCollider.enabled = false;
            if (holder != null)
            {
                transform.SetParent(holder, true);
            }

            return true;
        }

        /// <summary>
        /// Terminal cleanup for a captured projectile whose authority owner is
        /// retiring. Captures never resume normal hit processing implicitly.
        /// </summary>
        public void RetireClaimCapture()
        {
            if (!claimSuspended)
            {
                return;
            }

#if UNITY_EDITOR
            // Editor verification can exercise a real boss-death cleanup
            // chain. Unity forbids deferred Destroy while not playing, so use
            // immediate cleanup only in that editor-only execution context.
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }
#endif
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!hasLaunched || hasImpacted)
            {
                return;
            }

            TraceFirstContact(other);

            // The player's body, weapon, and transient attack colliders share
            // one owner hierarchy. They are never valid targets for this shot,
            // including on the spawn frame before Physics2D has applied ignores.
            if (!reflectedByEnemy && IsOwnerCollider(other))
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                if (reflectedByEnemy)
                {
                    TraceFirstTerminal("reflected-player-hit", other);
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

                    if (isHeavyProjectile)
                    {
                        TraceFirstTerminal("heavy-damageable-hit", other);
                        BeginHeavyImpact();
                        Destroy(gameObject);
                        return;
                    }

                    ResolveProjectileHit(damageable);
                    remainingEnemyHits--;
                    if (remainingEnemyHits <= 0)
                    {
                        TraceFirstTerminal("damageable-hit", other);
                        BeginImpact();
                        Destroy(gameObject);
                    }
                }

                return;
            }

            if ((environmentLayers.value & (1 << other.gameObject.layer)) != 0)
            {
                TraceFirstTerminal(isHeavyProjectile ? "heavy-environment-hit" : "environment-hit", other);
                CaveSfx.Play(CaveSfxCue.Hit, 0.55f);
                if (isHeavyProjectile)
                {
                    BeginHeavyImpact();
                }
                else
                {
                    BeginImpact();
                }
                Destroy(gameObject);
            }
        }

        private void ResolveProjectileHit(Damageable damageable)
        {
            if (damageable == null || !hitTargets.Add(damageable))
            {
                return;
            }

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
            InteractionRuntime.ReportHit(interactionIdentity, damageable.gameObject, impactDamage);
            int appliedDamage = damageable.TakeDamageResolved(impactDamage, impactContext);
            InteractionRuntime.ReportDamageApplied(
                interactionIdentity,
                damageable.gameObject,
                appliedDamage);
            ElementalAxiomCombatBridge.TryApplyProjectileHit(
                damageable,
                firedMode,
                appliedDamage,
                isFrenzyCritical,
                damageContext,
                Time.time,
                axiomApplicationReceipt);
            if (frenzyActivation != null
                && (frenzyActivation.ManaInfused || isFrenzyCritical))
            {
                frenzyActivation.ApplyImpact(
                    damageable,
                    body.velocity,
                    appliedDamage,
                    isFrenzyCritical);
            }

            ApplyStatusEffect(damageable, appliedDamage > 0);
        }

        private void BeginHeavyImpact()
        {
            if (hasImpacted)
            {
                return;
            }

            // The explosion is the one authoritative Heavy hit. The collision
            // target is included by the radius query, so it cannot receive an
            // accidental direct-hit plus AoE double application.
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                transform.position,
                heavyExplosionRadius,
                damageableLayers);
            for (int index = 0; index < overlaps.Length; index++)
            {
                Damageable damageable = overlaps[index] != null
                    ? overlaps[index].GetComponentInParent<Damageable>()
                    : null;
                ResolveProjectileHit(damageable);
            }

            CaveSfx.Play(CaveSfxCue.Explosion, 0.75f);
            BeginImpact();
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
            ResetOwnerCollisionFiltering();
            launchOwner = null;
            ownerOverride = null;
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
                    tier3Settings.FrostFieldFillColor,
                    tier3Settings.FrostTier2PinDuration,
                    tier3Settings.SlowFieldMergeGrowth,
                    tier3Settings.SlowFieldMaximumRadius);
            }

            body.velocity = Vector2.zero;
            body.simulated = false;
            projectileCollider.enabled = false;
        }

        private void Update()
        {
            TraceFirstMovement();
            frenzyActivation?.KeepAlive();
            if (hasLaunched && firedTier >= 3 && !hasImpacted)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.07f;
                ApplyVisualScale(tierScale * pulse);
            }
        }

        private void ConfigureTierVisuals()
        {
            usesImaginaryPresentation = launchOwner != null
                && launchOwner.GetComponent<PhaseCombatState>()?.HasOpening == true;
            usesProvidedPresentation = ApplyProvidedPresentation();

            if (tier3Settings == null)
            {
                return;
            }

            Color tierColor = tier3Settings.GetProjectileTierColor(firedMode, firedTier);
            if (!usesProvidedPresentation
                && (firedMode == SpecialMode.SlowShot || firedMode == SpecialMode.BurnShot))
            {
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
            }
            else
            {
                // The supplied art already carries the intended Stage 1/2/3
                // size progression. Root scale stays untouched so hitboxes do
                // not inherit source-pixel dimensions.
                tierScale = 1f;
            }

            if (firedTier < 2) return;

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

        private bool ApplyProvidedPresentation()
        {
            Sprite presentation = PlayerProjectilePresentation.Resolve(
                firedMode,
                firedTier,
                usesImaginaryPresentation);
            if (presentation == null)
            {
                return false;
            }

            for (int index = 0; index < visualRenderers.Length; index++)
            {
                SpriteRenderer renderer = visualRenderers[index];
                if (renderer == null) continue;
                renderer.sprite = presentation;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.color = Color.white;
            }

            return true;
        }

        private void RestoreBasePresentation()
        {
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                SpriteRenderer renderer = visualRenderers[index];
                if (renderer == null) continue;
                renderer.sprite = baseVisualSprites[index];
                renderer.drawMode = baseVisualDrawModes[index];
                renderer.size = baseVisualSizes[index];
                renderer.color = baseVisualColors[index];
            }

            usesProvidedPresentation = false;
            usesImaginaryPresentation = false;
        }

        private void ConfigureOwnerCollisionFiltering(GameObject owner)
        {
            ResetOwnerCollisionFiltering();
            launchOwner = owner;
            if (launchOwner == null || projectileCollider == null) return;

            Collider2D[] ownerColliders = launchOwner.GetComponentsInChildren<Collider2D>(true);
            for (int index = 0; index < ownerColliders.Length; index++)
            {
                Collider2D ownerCollider = ownerColliders[index];
                if (ownerCollider == null || ownerCollider == projectileCollider) continue;
                Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
                ignoredOwnerColliders.Add(ownerCollider);
            }
        }

        private void ResetOwnerCollisionFiltering()
        {
            if (projectileCollider != null)
            {
                for (int index = 0; index < ignoredOwnerColliders.Count; index++)
                {
                    Collider2D ignored = ignoredOwnerColliders[index];
                    if (ignored != null)
                    {
                        Physics2D.IgnoreCollision(projectileCollider, ignored, false);
                    }
                }
            }

            ignoredOwnerColliders.Clear();
        }

        private bool IsOwnerCollider(Collider2D collider)
        {
            return collider != null
                && launchOwner != null
                && (collider.gameObject == launchOwner || collider.transform.IsChildOf(launchOwner.transform));
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

            TraceFirstTerminal("lifetime-expired", null);
            BeginImpact();
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (hasLaunched && !hasImpacted)
            {
                TraceFirstTerminal("disabled-without-impact", null);
            }

            frenzyActivation?.Complete();
            frenzyActivation = null;
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }

            hitTargets.Clear();
            ApplyVisualScale(1f);
            transform.localScale = baseScale;
            RestoreBasePresentation();
            ResetOwnerCollisionFiltering();
            launchOwner = null;
            CancelInvoke();
        }

        private void OnDestroy()
        {
            if (hasLaunched && !hasImpacted)
            {
                TraceFirstTerminal("destroyed-without-impact", null);
            }

            if (!interactionDestroyedReported && interactionIdentity != null)
            {
                interactionDestroyedReported = true;
                InteractionRuntime.ReportDestroyed(interactionIdentity);
            }

            if (tierTrailMaterial != null)
            {
                Destroy(tierTrailMaterial);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceSpawn()
        {
            Debug.Log(
                "[Cave][ProjectileTrace] SPAWN"
                + " id=" + GetInstanceID()
                + " kind=" + (isHeavyProjectile ? "charged" : "regular")
                + " element=" + firedMode
                + " stage=" + firedTier
                + " imaginary=" + usesImaginaryPresentation
                + " position=" + launchPosition
                + " projectileCollider=" + (projectileCollider != null ? projectileCollider.GetType().Name : "<missing>")
                + " bounds=" + (projectileCollider != null ? projectileCollider.bounds.ToString() : "<missing>")
                + " layer=" + gameObject.layer + "(" + LayerMask.LayerToName(gameObject.layer) + ")"
                + " tag=" + gameObject.tag
                + " velocity=" + (body != null ? body.velocity.ToString() : "<missing>")
                + " lifetime=" + lifetime.ToString("0.##")
                + " owner=" + (launchOwner != null ? HierarchyPath(launchOwner.transform) : "<none>"),
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceFirstMovement()
        {
            if (!hasLaunched || hasImpacted || firstMovementTraced)
            {
                return;
            }

            firstMovementTraced = true;
            Debug.Log(
                "[Cave][ProjectileTrace] FIRST MOVEMENT"
                + " id=" + GetInstanceID()
                + " kind=" + (isHeavyProjectile ? "charged" : "regular")
                + " age=" + Mathf.Max(0f, Time.time - launchedAt).ToString("0.000")
                + " position=" + transform.position
                + " velocity=" + (body != null ? body.velocity.ToString() : "<missing>")
                + " simulated=" + (body != null && body.simulated),
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceFirstContact(Collider2D other)
        {
            if (firstContactTraced)
            {
                return;
            }

            firstContactTraced = true;
            TraceProjectileInteraction("FIRST CONTACT", "non-terminal-observation", other);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceFirstTerminal(string branch, Collider2D other)
        {
            if (firstTerminalTraced)
            {
                return;
            }

            firstTerminalTraced = true;
            TraceProjectileInteraction("FIRST TERMINAL", branch, other);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TraceProjectileInteraction(string eventName, string branch, Collider2D other)
        {
            bool ownerCollider = IsOwnerCollider(other);
            bool weaponOrAttackCollider = other != null
                && (other.GetComponentInParent<SpinSwordAttack>() != null
                    || other.GetComponentInParent<ChargedAttack>() != null
                    || other.GetComponentInParent<SidewaysParryAttack>() != null);
            Damageable damageable = other != null ? other.GetComponentInParent<Damageable>() : null;
            bool worldCollision = other != null
                && (environmentLayers.value & (1 << other.gameObject.layer)) != 0;
            string otherName = other != null ? other.gameObject.name : "<none>";
            string otherPath = other != null ? HierarchyPath(other.transform) : "<none>";
            string otherType = other != null ? other.GetType().Name : "<none>";
            int otherLayer = other != null ? other.gameObject.layer : -1;
            string layerName = otherLayer >= 0 ? LayerMask.LayerToName(otherLayer) : "<none>";
            string projectileName = projectileCollider != null ? projectileCollider.GetType().Name : "<missing>";
            Debug.Log(
                "[Cave][ProjectileTrace] " + eventName
                + " branch=" + branch
                + " id=" + GetInstanceID()
                + " kind=" + (isHeavyProjectile ? "charged" : "regular")
                + " element=" + firedMode
                + " stage=" + firedTier
                + " imaginary=" + usesImaginaryPresentation
                + " age=" + Mathf.Max(0f, Time.time - launchedAt).ToString("0.000")
                + " spawn=" + launchPosition
                + " position=" + transform.position
                + " projectileCollider=" + projectileName
                + " projectileBounds=" + (projectileCollider != null ? projectileCollider.bounds.ToString() : "<missing>")
                + " projectileLayer=" + gameObject.layer + "(" + LayerMask.LayerToName(gameObject.layer) + ")"
                + " other=" + otherName
                + " path=" + otherPath
                + " otherCollider=" + otherType
                + " layer=" + otherLayer + "(" + layerName + ")"
                + " tag=" + (other != null ? other.tag : "<none>")
                + " trigger=" + (other != null && other.isTrigger)
                + " owner=" + ownerCollider
                + " weaponOrAttack=" + weaponOrAttackCollider
                + " damageable=" + (damageable != null)
                + " world=" + worldCollision
                + " faction=" + FactionFor(other, damageable)
                + " callback=OnTriggerEnter2D"
                + " ownerFilter=" + ownerCollider,
                this);
        }

        private static string FactionFor(Collider2D collider, Damageable damageable)
        {
            if (collider == null)
            {
                return "none";
            }

            if (collider.GetComponentInParent<PlayerHealth>() != null)
            {
                return "player";
            }

            if (collider.GetComponentInParent<MobBrainBase>() != null)
            {
                return "enemy";
            }

            return damageable != null ? "damageable-unclassified" : "unclassified";
        }

        private static string HierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<none>";
            }

            string result = target.name;
            for (Transform current = target.parent; current != null; current = current.parent)
            {
                result = current.name + "/" + result;
            }

            return result;
        }
    }

    /// <summary>Loads approved projectile art without coupling sprite pixels to gameplay colliders.</summary>
    internal static class PlayerProjectilePresentation
    {
        private const float PixelsPerUnit = 1200f;
        private static readonly Dictionary<string, Sprite> CachedSprites = new Dictionary<string, Sprite>();

        internal static Sprite Resolve(SpecialMode mode, int tier, bool imaginaryOverride)
        {
            string resourcePath = ResourcePathFor(mode, tier, imaginaryOverride);
            Sprite cached;
            if (CachedSprites.TryGetValue(resourcePath, out cached)) return cached;

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            CachedSprites[resourcePath] = sprite;
            return sprite;
        }

        private static string FamilyFor(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.BurnShot: return "Fire";
                case SpecialMode.SlowShot: return "Ice";
                case SpecialMode.Flight: return "Wind";
                case SpecialMode.DamageBoost: return "Earth";
                default: return "Fire";
            }
        }

        internal static string ResourcePathFor(SpecialMode mode, int tier, bool imaginaryOverride)
        {
            string family = imaginaryOverride ? "Imaginary_Axiom" : FamilyFor(mode);
            return "Projectiles/Player/" + family + "/" + FileNameFor(family, tier);
        }

        private static string FileNameFor(string family, int tier)
        {
            int stage = Mathf.Clamp(tier, 1, 3);
            return family == "Imaginary_Axiom"
                ? "imaginary_axiom_stage_" + stage
                : family.ToLowerInvariant() + "_stage_" + stage;
        }
    }
}
