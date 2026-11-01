using System.Collections.Generic;
using Cave.Axioms.Elemental;
using Cave.Axioms.Phase;
using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.Interactions;
using Cave.Player;
using Cave.Progression;
using Cave.Diagnostics;
using Cave.Environmental;
using UnityEngine;

namespace Cave.Projectiles
{
    public enum PlayerProjectileLifecycleEvent
    {
        Launched,
        Impacted,
        Expired
    }

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

        [Header("Heavy Presentation")]
        [Tooltip("Presentation-only terminal effect. Heavy collision and damage remain in BeginHeavyImpact.")]
        [SerializeField] private GameObject heavyExplosionVisualPrefab;
        [SerializeField, Min(.01f)] private float heavyExplosionVisualLifetime = .8f;

        private Rigidbody2D body;
        private Collider2D projectileCollider;
        private SpecialMode firedMode;
        private int resolvedDamage;
        private DamageContext damageContext;
        private bool hasLaunched;
        private bool hasImpacted;
        private bool lifecycleTerminalRaised;
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
        private float heavyVisualScale = 1f;
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
        private float heavyKnockback;
        private bool heavyImpactVisualSpawned;
        private int structuralImpactSequence;
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
        private bool telemetryRegistered;
        private float configuredStandardSpeed;
        private float intrinsicImpulse;
        private float launchedSpeed;
        private int standardImpactSequence;

        public int BaseDamage => baseDamage;
        public float AuthoredSpeed => speed;
        public int SkillTier => firedTier;
        public int RemainingEnemyHits => remainingEnemyHits;
        public bool IsPiercing => remainingEnemyHits > 1;
        public bool IsHeavyProjectile => isHeavyProjectile;
        /// <summary>Projectile-owned lifecycle seam for future travel loops.</summary>
        public event System.Action<PlayerProjectile, PlayerProjectileLifecycleEvent> LifecycleChanged;
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
        /// Pattern-owned launch inputs. Zero speed preserves the prefab's
        /// authored speed; Heavy never reads this configuration.
        /// </summary>
        public void ConfigureStandardPattern(float requestedSpeed, float requestedIntrinsicImpulse)
        {
            configuredStandardSpeed = Mathf.Max(0f, requestedSpeed);
            intrinsicImpulse = Mathf.Max(0f, requestedIntrinsicImpulse);
        }

        /// <summary>
        /// Configures the launched instance as the terminal Heavy expression of
        /// the current projectile path. Heavy never inherits rapid pierce.
        /// The heavy silhouette scales only a visual child. The root owns the
        /// Rigidbody2D and Collider2D, so its physical footprint stays stable.
        /// </summary>
        public void ConfigureHeavyProjectile(
            float scaleMultiplier,
            float explosionRadius,
            float knockback,
            float committedCharge)
        {
            isHeavyProjectile = true;
            heavyExplosionRadius = Mathf.Max(0.1f, explosionRadius);
            heavyKnockback = Mathf.Max(0f, knockback);
            remainingEnemyHits = ChargedProjectilePolicy.HeavyMaximumEnemyHits;
            damageContext = damageContext.WithTraits(DamageTrait.Heavy | DamageTrait.AreaOfEffect);
            heavyVisualScale = Mathf.Max(1f, scaleMultiplier);
            ApplyVisualScale(tierScale * heavyVisualScale);
            ApplyHeavyTravelPresentation(committedCharge);
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
            EnsureVisualChildForRootRenderer();
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
            heavyKnockback = 0f;
            heavyImpactVisualSpawned = false;
            heavyVisualScale = 1f;
            configuredStandardSpeed = isHeavyProjectile ? 0f : configuredStandardSpeed;
            launchedSpeed = isHeavyProjectile
                ? speed
                : configuredStandardSpeed > 0f ? configuredStandardSpeed : speed;
            standardImpactSequence = 0;
            transform.localScale = baseScale;
            hitTargets.Clear();
            axiomApplicationReceipt.Clear();
            reflectedByEnemy = false;
            enemyParryOwner = null;
            hasLaunched = true;
            lifecycleTerminalRaised = false;
            if (!telemetryRegistered) { telemetryRegistered = true; RuntimeTelemetry.Acquired(RuntimeWorkCategory.Projectile); }
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
            body.velocity = direction.normalized * launchedSpeed;
            LifecycleChanged?.Invoke(this, PlayerProjectileLifecycleEvent.Launched);
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
            RaiseTerminalLifecycle(PlayerProjectileLifecycleEvent.Expired);
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
                if (isHeavyProjectile)
                {
                    BeginHeavyImpact();
                }
                else
                {
                    ApplyStandardStructuralImpulse(other);
                    BeginImpact();
                }
                Destroy(gameObject);
            }
        }

        private bool ResolveProjectileHit(Damageable damageable)
        {
            if (damageable == null || !hitTargets.Add(damageable))
            {
                return false;
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
            bool frenzyAxiomVariant = frenzyActivation != null;
            ElementalAxiomCombatBridge.TryApplyProjectileHit(
                damageable,
                firedMode,
                appliedDamage,
                frenzyAxiomVariant,
                damageContext,
                Time.time,
                axiomApplicationReceipt);
            if (frenzyActivation != null)
            {
                frenzyActivation.ApplyImpact(
                    damageable,
                    body.velocity,
                    appliedDamage,
                    isFrenzyCritical);
            }

            ApplyStatusEffect(damageable, appliedDamage > 0);
            ApplyStandardProjectileImpulse(damageable);
            return true;
        }

        private void ApplyStandardProjectileImpulse(Damageable damageable)
        {
            if (isHeavyProjectile || damageable == null || intrinsicImpulse <= 0f)
            {
                return;
            }

            KnockbackReceiver receiver = damageable.GetComponent<KnockbackReceiver>();
            if (receiver == null)
            {
                return;
            }

            Vector2 velocity = body != null ? body.velocity : Vector2.right;
            Vector2 direction = velocity.sqrMagnitude > .0001f ? velocity.normalized : Vector2.right;
            receiver.ApplyKnockback(direction * StandardProjectileImpulsePolicy.Resolve(
                intrinsicImpulse,
                velocity.magnitude,
                speed));
        }

        private void ApplyStandardStructuralImpulse(Collider2D collider)
        {
            if (isHeavyProjectile || collider == null || intrinsicImpulse <= 0f)
            {
                return;
            }

            Vector2 velocity = body != null ? body.velocity : Vector2.right;
            float impulse = StandardProjectileImpulsePolicy.Resolve(
                intrinsicImpulse,
                velocity.magnitude,
                speed);
            StructuralImpactRouter.Apply(collider, new StructuralImpactContext(
                launchOwner != null ? launchOwner : gameObject,
                transform.position,
                velocity,
                impulse * StandardProjectileImpulsePolicy.DestructibleResistanceMultiplier,
                damageContext,
                true,
                false,
                unchecked(GetInstanceID() * 397 ^ ++standardImpactSequence)));
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
                if (ResolveProjectileHit(damageable))
                {
                    ApplyHeavyKnockback(damageable);
                }
            }

            // The terminal Heavy explosion is also the sole structural event;
            // its projectile instance id deduplicates every covered object.
            StructuralImpactRouter.ApplyArea(new StructuralImpactContext(
                launchOwner != null ? launchOwner : gameObject,
                transform.position,
                body != null ? body.velocity : Vector2.right,
                Mathf.Max(1f, resolvedDamage),
                damageContext,
                true,
                true,
                unchecked(GetInstanceID() * 397 ^ ++structuralImpactSequence)), heavyExplosionRadius);

            SpawnHeavyImpactVisual();
            BeginImpact();
        }

        /// <summary>
        /// Heavy applies force only as part of its deduplicated terminal AoE.
        /// ResolveProjectileHit gates this call, so collider overlap order and
        /// multi-collider enemies cannot receive duplicate force.
        /// </summary>
        private void ApplyHeavyKnockback(Damageable damageable)
        {
            if (damageable == null || heavyKnockback <= 0f)
            {
                return;
            }

            KnockbackReceiver receiver = damageable.GetComponent<KnockbackReceiver>();
            if (receiver == null)
            {
                return;
            }

            Vector2 travelDirection = body != null ? body.velocity : Vector2.right;
            Vector2 direction = ChargedProjectilePolicy.HeavyExplosionDirection(
                transform.position,
                damageable.transform.position,
                travelDirection);
            receiver.ApplyKnockback(direction * heavyKnockback);
        }

        private void SpawnHeavyImpactVisual()
        {
            if (heavyImpactVisualSpawned) return;
            heavyImpactVisualSpawned = true;
            if (HeavyProjectilePresentation.TrySpawnFallout(
                    firedMode,
                    usesImaginaryPresentation,
                    transform.position,
                    heavyExplosionRadius,
                    ResolveVisualSortingOrder() + 1))
            {
                return;
            }

            // The older CFX prefab remains an intentional fallback only for a
            // missing approved sheet; it never layers beneath the approved
            // Fallout sequence.
            if (heavyExplosionVisualPrefab == null) return;
            GameObject visual = Instantiate(heavyExplosionVisualPrefab, transform.position, Quaternion.identity);
            // The source effect is authored around the 3.5-unit terminal
            // radius. This only follows the already-resolved radius; it has no
            // collider, target query, or damage authority.
            float scale = Mathf.Max(.01f, heavyExplosionRadius / ChargedProjectilePolicy.DefaultHeavyExplosionRadius);
            visual.transform.localScale *= scale;
            Destroy(visual, Mathf.Max(.01f, heavyExplosionVisualLifetime));
        }

        private void ApplyHeavyTravelPresentation(float committedCharge)
        {
            Sprite presentation;
            if (!HeavyProjectilePresentation.TryResolveTravel(
                    firedMode,
                    usesImaginaryPresentation,
                    committedCharge,
                    out presentation))
            {
                return;
            }

            for (int index = 0; index < visualRenderers.Length; index++)
            {
                SpriteRenderer renderer = visualRenderers[index];
                if (renderer == null) continue;
                renderer.sprite = presentation;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.color = Color.white;
            }

            usesProvidedPresentation = true;
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

        private void BeginImpact(bool audibleImpact = true)
        {
            if (hasImpacted)
            {
                return;
            }

            hasImpacted = true;
            RaiseTerminalLifecycle(PlayerProjectileLifecycleEvent.Impacted);
            if (audibleImpact && !reflectedByEnemy)
            {
                // The projectile owns this terminal sound. Damageable suppresses
                // generic projectile impacts so one ordinary collision is one cue.
                CaveSfx.PlayAt(CaveSfxCue.PlayerProjectileImpact, transform.position,
                    isHeavyProjectile ? .85f : .65f);
            }
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
                ApplyVisualScale(tierScale * heavyVisualScale * pulse);
            }
        }

        private void ConfigureTierVisuals()
        {
            usesImaginaryPresentation = launchOwner != null
                && launchOwner.GetComponent<PhaseCombatState>()?.HasImaginaryActive == true;
            // The dedicated Heavy prefab carries its own authored projectile
            // silhouette. Do not replace it with the ordinary mode-stage art
            // during Launch; ConfigureHeavyProjectile still owns only its
            // presentation scale after launch commits.
            bool usesDedicatedHeavyPresentation = heavyExplosionVisualPrefab != null;
            usesProvidedPresentation = !usesDedicatedHeavyPresentation && ApplyProvidedPresentation();

            if (tier3Settings == null)
            {
                return;
            }

            Color tierColor = tier3Settings.GetProjectileTierColor(firedMode, firedTier);
            if (!usesProvidedPresentation
                && !usesDedicatedHeavyPresentation
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
                ApplyVisualScale(tierScale * heavyVisualScale);
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

        /// <summary>
        /// The shipped projectile prefab historically placed its renderer on
        /// the physical root. Preserve that serialized source while moving its
        /// live presentation to a child, so visual scaling cannot resize the
        /// Rigidbody2D or Collider2D.
        /// </summary>
        private void EnsureVisualChildForRootRenderer()
        {
            SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
            if (rootRenderer == null || !rootRenderer.enabled)
            {
                return;
            }

            Transform existing = transform.Find("Projectile Visual");
            if (existing != null)
            {
                rootRenderer.enabled = false;
                return;
            }

            GameObject visualObject = new GameObject("Projectile Visual");
            visualObject.transform.SetParent(transform, false);
            SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = rootRenderer.sprite;
            visual.color = rootRenderer.color;
            visual.flipX = rootRenderer.flipX;
            visual.flipY = rootRenderer.flipY;
            visual.drawMode = rootRenderer.drawMode;
            visual.size = rootRenderer.size;
            visual.sortingLayerID = rootRenderer.sortingLayerID;
            visual.sortingOrder = rootRenderer.sortingOrder;
            visual.sharedMaterial = rootRenderer.sharedMaterial;
            rootRenderer.enabled = false;
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
            BeginImpact(false);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (telemetryRegistered) { telemetryRegistered = false; RuntimeTelemetry.Returned(RuntimeWorkCategory.Projectile); }
            if (hasLaunched && !hasImpacted)
            {
                TraceFirstTerminal("disabled-without-impact", null);
                RaiseTerminalLifecycle(PlayerProjectileLifecycleEvent.Expired);
            }

            frenzyActivation?.Complete();
            frenzyActivation = null;
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }

            hitTargets.Clear();
            ApplyVisualScale(1f);
            heavyVisualScale = 1f;
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
                RaiseTerminalLifecycle(PlayerProjectileLifecycleEvent.Expired);
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

        private void RaiseTerminalLifecycle(PlayerProjectileLifecycleEvent lifecycleEvent)
        {
            if (lifecycleTerminalRaised)
            {
                return;
            }

            lifecycleTerminalRaised = true;
            LifecycleChanged?.Invoke(this, lifecycleEvent);
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

    /// <summary>
    /// Presentation-only manifest for the approved Heavy travel atlas and its
    /// mode-matched Fallout sheets. Physics, hit queries, and damage never
    /// read these assets.
    /// </summary>
    internal static class HeavyProjectilePresentation
    {
        private const string TravelPath = "Projectiles/Heavy/Travel/heavy_projectiles";
        private const string FalloutRoot = "Projectiles/Heavy/Fallout/";
        private const float TravelPixelsPerUnit = 1200f;
        // The Fallout sheets depict the full terminal area rather than a
        // travelling projectile, so their rendered diameter maps to the
        // established 3.5-unit Heavy radius at the authored base scale.
        private const float FalloutPixelsPerUnit = 90f;
        private const float FalloutFrameSeconds = .09f;
        private static readonly Dictionary<string, Sprite> TravelSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite[]> FalloutSprites = new Dictionary<string, Sprite[]>();

        // Rectangles are measured from the approved 1122x1402 travel atlas in
        // Unity's bottom-left texture coordinates. Each family retains its
        // original silhouette bounds; no resampling or source-pixel rewrite
        // is performed.
        private static readonly Rect[,] TravelRects =
        {
            { new Rect(0f, 1102f, 285f, 300f), new Rect(285f, 1102f, 380f, 300f), new Rect(665f, 1102f, 457f, 300f) },
            { new Rect(0f, 832f, 285f, 270f), new Rect(285f, 832f, 380f, 270f), new Rect(665f, 832f, 457f, 270f) },
            { new Rect(0f, 542f, 285f, 290f), new Rect(285f, 542f, 380f, 290f), new Rect(665f, 542f, 457f, 290f) },
            { new Rect(0f, 252f, 285f, 290f), new Rect(285f, 252f, 380f, 290f), new Rect(665f, 252f, 457f, 290f) },
            { new Rect(0f, 0f, 285f, 252f), new Rect(285f, 0f, 380f, 252f), new Rect(665f, 0f, 457f, 252f) }
        };

        internal static bool TryResolveTravel(
            SpecialMode mode,
            bool imaginaryOverride,
            float committedCharge,
            out Sprite sprite)
        {
            sprite = null;
            Texture2D texture = Resources.Load<Texture2D>(TravelPath);
            if (texture == null || texture.width != 1122 || texture.height != 1402)
            {
                return false;
            }

            int family = FamilyIndex(mode, imaginaryOverride);
            int stage = StageIndex(committedCharge);
            string key = family + ":" + stage;
            if (!TravelSprites.TryGetValue(key, out sprite))
            {
                sprite = Sprite.Create(
                    texture,
                    TravelRects[family, stage],
                    new Vector2(.5f, .5f),
                    TravelPixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
                TravelSprites[key] = sprite;
            }

            return sprite != null;
        }

        internal static bool TrySpawnFallout(
            SpecialMode mode,
            bool imaginaryOverride,
            Vector2 position,
            float explosionRadius,
            int sortingOrder)
        {
            Sprite[] frames;
            if (!TryResolveFallout(FamilyIndex(mode, imaginaryOverride), out frames))
            {
                return false;
            }

            GameObject visual = new GameObject("Heavy Projectile Fallout Visual");
            visual.transform.position = position;
            HeavyProjectileFalloutVisual player = visual.AddComponent<HeavyProjectileFalloutVisual>();
            player.Configure(
                frames,
                Mathf.Max(.01f, explosionRadius / ChargedProjectilePolicy.DefaultHeavyExplosionRadius),
                sortingOrder,
                FalloutFrameSeconds);
            return true;
        }

        internal static bool HasRequiredArt(out string failure)
        {
            Texture2D travel = Resources.Load<Texture2D>(TravelPath);
            if (travel == null || travel.width != 1122 || travel.height != 1402)
            {
                failure = "Heavy travel atlas is missing or is not the approved 1122x1402 source.";
                return false;
            }

            for (int family = 0; family < 5; family++)
            {
                Texture2D fallout = Resources.Load<Texture2D>(FalloutRoot + FalloutFileName(family));
                if (fallout == null || fallout.width != 1254 || fallout.height != 1254)
                {
                    failure = "Heavy Fallout sheet is missing or is not the approved 1254x1254 source: "
                        + FalloutFileName(family) + ".";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static bool TryResolveFallout(int family, out Sprite[] frames)
        {
            string key = FalloutFileName(family);
            if (FalloutSprites.TryGetValue(key, out frames))
            {
                return frames != null && frames.Length == 4;
            }

            Texture2D texture = Resources.Load<Texture2D>(FalloutRoot + key);
            if (texture == null || texture.width != 1254 || texture.height != 1254)
            {
                frames = null;
                return false;
            }

            const float CellSize = 627f;
            // Source order is top-left, top-right, bottom-left, bottom-right;
            // Sprite rects use Unity's bottom-left coordinate convention.
            frames = new[]
            {
                CreateFalloutFrame(texture, new Rect(0f, CellSize, CellSize, CellSize)),
                CreateFalloutFrame(texture, new Rect(CellSize, CellSize, CellSize, CellSize)),
                CreateFalloutFrame(texture, new Rect(0f, 0f, CellSize, CellSize)),
                CreateFalloutFrame(texture, new Rect(CellSize, 0f, CellSize, CellSize))
            };
            FalloutSprites[key] = frames;
            return true;
        }

        private static Sprite CreateFalloutFrame(Texture2D texture, Rect rect)
        {
            return Sprite.Create(texture, rect, new Vector2(.5f, .5f), FalloutPixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static int FamilyIndex(SpecialMode mode, bool imaginaryOverride)
        {
            if (imaginaryOverride) return 4;
            switch (mode)
            {
                case SpecialMode.BurnShot: return 0;
                case SpecialMode.SlowShot: return 1;
                case SpecialMode.Flight: return 2;
                case SpecialMode.DamageBoost: return 3;
                default: return 0;
            }
        }

        private static int StageIndex(float committedCharge)
        {
            float value = Mathf.Clamp01(committedCharge);
            return value < 1f / 3f ? 0 : value < 2f / 3f ? 1 : 2;
        }

        private static string FalloutFileName(int family)
        {
            switch (family)
            {
                case 0: return "fire_fallout";
                case 1: return "ice_fallout";
                case 2: return "wind_fallout";
                case 3: return "strength_fallout";
                default: return "imaginary_fallout";
            }
        }
    }

    /// <summary>One-shot, non-interactive playback for approved Fallout frames.</summary>
    internal sealed class HeavyProjectileFalloutVisual : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float frameSeconds;
        private float startedAt;

        internal void Configure(Sprite[] sourceFrames, float scale, int sortingOrder, float secondsPerFrame)
        {
            frames = sourceFrames;
            frameSeconds = Mathf.Max(.01f, secondsPerFrame);
            startedAt = Time.time;
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames[0];
            spriteRenderer.sortingOrder = sortingOrder;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Update()
        {
            int frame = Mathf.FloorToInt((Time.time - startedAt) / frameSeconds);
            if (frame >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            spriteRenderer.sprite = frames[frame];
        }
    }
}
