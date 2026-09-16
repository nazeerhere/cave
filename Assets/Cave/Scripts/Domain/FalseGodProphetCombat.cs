using System;
using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Interactions;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Presentation categories for the Prophet body.  They intentionally stay
    /// broader than individual moves so final animation can replace temporary
    /// feedback without becoming combat authority.
    /// </summary>
    public enum FalseGodProphetPresentationState
    {
        Idle,
        MoveFloat,
        Hurt,
        Block,
        BasicCast,
        ShadowCast,
        ClaimCommand,
        CausalBeam,
        Ascension
    }

    public enum FalseGodTriuneAnchorKind
    {
        Heal,
        Empower,
        Suppress
    }

    /// <summary>Single presentation mapping shared by runtime and verification.</summary>
    public static class FalseGodProphetPresentationMap
    {
        public static FalseGodProphetPresentationState ForAbility(FalseGodAbilityKind ability)
        {
            switch (ability)
            {
                case FalseGodAbilityKind.EchoProjectile:
                case FalseGodAbilityKind.SlowBolt:
                    return FalseGodProphetPresentationState.BasicCast;
                case FalseGodAbilityKind.DashShadow:
                    return FalseGodProphetPresentationState.ShadowCast;
                case FalseGodAbilityKind.CrystalRain:
                case FalseGodAbilityKind.CrystalChainHook:
                case FalseGodAbilityKind.TriuneAnchors:
                case FalseGodAbilityKind.Appropriation:
                    return FalseGodProphetPresentationState.ClaimCommand;
                case FalseGodAbilityKind.CausalBeam:
                    return FalseGodProphetPresentationState.CausalBeam;
                case FalseGodAbilityKind.Block:
                    return FalseGodProphetPresentationState.Block;
                default:
                    return FalseGodProphetPresentationState.Idle;
            }
        }
    }

    /// <summary>
    /// Bounded projectile used only by Prophet manifestations.  It uses the
    /// normal PlayerHealth, DamageContext and PlayerSlowStatus contracts; no
    /// alternative damage or status system is introduced.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class FalseGodProphetProjectile : MonoBehaviour
    {
        private Rigidbody2D body;
        private Collider2D hitCollider;
        private GameObject owner;
        private Vector2 direction;
        private int damage;
        private float slowMultiplier = 1f;
        private float slowDuration;
        private bool appliesSlow;
        private bool spent;

        public bool AppliesSlow => appliesSlow;
        public int Damage => damage;
        public float SlowMovementMultiplier => slowMultiplier;

        private void Awake()
        {
            EnsureReferences();
        }

        public void Initialize(
            GameObject projectileOwner,
            Vector2 travelDirection,
            float speed,
            int resolvedDamage,
            float lifetime,
            bool applySlow = false,
            float requestedSlowMultiplier = 1f,
            float requestedSlowDuration = 0f)
        {
            EnsureReferences();
            owner = projectileOwner;
            direction = travelDirection.sqrMagnitude > 0.001f
                ? travelDirection.normalized
                : Vector2.right;
            damage = Mathf.Max(1, resolvedDamage);
            appliesSlow = applySlow;
            slowMultiplier = Mathf.Clamp(requestedSlowMultiplier, 0.1f, 1f);
            slowDuration = Mathf.Max(0f, requestedSlowDuration);
            spent = false;
            hitCollider.enabled = true;
            body.simulated = true;
            body.velocity = direction * Mathf.Max(0.01f, speed);
            if (Application.isPlaying)
            {
                Destroy(gameObject, Mathf.Max(0.1f, lifetime));
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (spent || other == null || IsOwnedCollider(other))
            {
                return;
            }

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                TryApplyToPlayer(player);
                Retire();
                return;
            }

            if (!other.isTrigger)
            {
                Retire();
            }
        }

        /// <summary>
        /// Narrow direct-resolution seam used by deterministic verification and
        /// by the trigger path above. It retains the normal player health and
        /// slow-status authority instead of simulating Unity trigger messages.
        /// </summary>
        public bool TryApplyToPlayer(PlayerHealth player)
        {
            if (spent || player == null)
            {
                return false;
            }

            bool accepted = player.TryTakeDamage(
                damage,
                new DamageContext(owner != null ? owner : gameObject,
                    DamageTrait.Direct | DamageTrait.Projectile));
            if (accepted && appliesSlow)
            {
                PlayerSlowStatus slow = player.GetComponent<PlayerSlowStatus>();
                if (slow == null)
                {
                    slow = player.gameObject.AddComponent<PlayerSlowStatus>();
                }

                slow.ApplySlow(slowMultiplier, slowDuration);
            }

            return accepted;
        }

        private bool IsOwnedCollider(Collider2D other)
        {
            return owner != null
                && (other.gameObject == owner || other.transform.IsChildOf(owner.transform));
        }

        private void Retire()
        {
            if (spent)
            {
                return;
            }

            spent = true;
            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }

            if (body != null)
            {
                body.velocity = Vector2.zero;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void EnsureReferences()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (hitCollider == null)
            {
                hitCollider = GetComponent<Collider2D>();
                hitCollider.isTrigger = true;
            }
        }
    }

    /// <summary>One shared cast family for Echo projectile and Slow Bolt.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodRuntimeFoundation))]
    public sealed class FalseGodProphetBasicCast : MonoBehaviour
    {
        [Header("Shared Cast")]
        [SerializeField, Min(0f)] private float windup = 0.45f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
        [SerializeField, Min(0.1f)] private float castRange = 9f;

        [Header("Echo Projectile")]
        [SerializeField, Min(0.01f)] private float echoProjectileSpeed = 7f;
        [SerializeField, Min(1)] private int echoDamage = 1;
        [SerializeField, Min(0.01f)] private float echoDelay = 0.45f;
        [SerializeField, Range(0.1f, 1f)] private float echoDamageMultiplier = 0.6f;
        [SerializeField, Min(0f)] private float echoCooldown = 3.25f;

        [Header("Slow Bolt")]
        [SerializeField, Min(0.01f)] private float slowBoltSpeed = 6.25f;
        [SerializeField, Min(1)] private int slowBoltDamage = 1;
        [SerializeField, Range(0.1f, 1f)] private float slowMultiplier = 0.7f;
        [SerializeField, Min(0.1f)] private float slowDuration = 1.75f;
        [SerializeField, Min(0f)] private float slowBoltCooldown = 4f;

        private Coroutine castRoutine;
        private float nextEchoTime;
        private float nextSlowTime;
        private readonly List<FalseGodProphetProjectile> spawned = new List<FalseGodProphetProjectile>(2);

        public bool IsCasting => castRoutine != null;
        public int ActiveProjectileCount
        {
            get
            {
                PruneSpawned();
                return spawned.Count;
            }
        }

        public bool TryCastEcho(Transform target, float damageMultiplier = 1f)
        {
            if (!CanCast(target) || IsCasting || Time.time < nextEchoTime)
            {
                return false;
            }

            nextEchoTime = Time.time + echoCooldown;
            castRoutine = StartCoroutine(PerformEcho(target, damageMultiplier));
            return true;
        }

        public bool TryCastSlowBolt(Transform target, float damageMultiplier = 1f)
        {
            if (!CanCast(target) || IsCasting || Time.time < nextSlowTime)
            {
                return false;
            }

            nextSlowTime = Time.time + slowBoltCooldown;
            castRoutine = StartCoroutine(PerformSlowBolt(target, damageMultiplier));
            return true;
        }

        /// <summary>Deterministic verification seam: same spawn setup, without edit-time coroutines.</summary>
        public int FireEchoImmediatelyForVerification(Vector2 targetPoint, float damageMultiplier = 1f)
        {
            Vector2 direction = ResolveDirection(targetPoint);
            SpawnProjectile(direction, echoProjectileSpeed,
                Mathf.Max(1, Mathf.RoundToInt(echoDamage * damageMultiplier)), false, 1f, 0f,
                new Color(0.9f, 0.55f, 1f, 1f));
            SpawnProjectile(direction, echoProjectileSpeed,
                Mathf.Max(1, Mathf.RoundToInt(echoDamage * echoDamageMultiplier * damageMultiplier)), false, 1f, 0f,
                new Color(0.55f, 0.35f, 1f, 0.72f));
            return ActiveProjectileCount;
        }

        public FalseGodProphetProjectile FireSlowImmediatelyForVerification(Vector2 targetPoint, float damageMultiplier = 1f)
        {
            return SpawnProjectile(ResolveDirection(targetPoint), slowBoltSpeed,
                Mathf.Max(1, Mathf.RoundToInt(slowBoltDamage * damageMultiplier)), true,
                slowMultiplier, slowDuration, new Color(0.35f, 0.7f, 1f, 1f));
        }

        public void CancelCast()
        {
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
                castRoutine = null;
            }
        }

        public void RetireAllProjectiles()
        {
            for (int index = 0; index < spawned.Count; index++)
            {
                if (spawned[index] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(spawned[index].gameObject);
                }
                else
                {
                    DestroyImmediate(spawned[index].gameObject);
                }
            }

            spawned.Clear();
        }

        private IEnumerator PerformEcho(Transform target, float damageMultiplier)
        {
            FalseGodCombatPresentation.CreateImpactTelegraph(transform.position, 0.38f, windup);
            yield return new WaitForSeconds(windup);
            if (!IsValidTarget(target))
            {
                castRoutine = null;
                yield break;
            }

            Vector2 direction = ResolveDirection(target.position);
            SpawnProjectile(direction, echoProjectileSpeed,
                Mathf.Max(1, Mathf.RoundToInt(echoDamage * damageMultiplier)), false, 1f, 0f,
                new Color(0.9f, 0.55f, 1f, 1f));
            yield return new WaitForSeconds(echoDelay);
            if (isActiveAndEnabled)
            {
                SpawnProjectile(direction, echoProjectileSpeed,
                    Mathf.Max(1, Mathf.RoundToInt(echoDamage * echoDamageMultiplier * damageMultiplier)), false,
                    1f, 0f, new Color(0.55f, 0.35f, 1f, 0.72f));
            }

            castRoutine = null;
        }

        private IEnumerator PerformSlowBolt(Transform target, float damageMultiplier)
        {
            FalseGodCombatPresentation.CreateImpactTelegraph(transform.position, 0.42f, windup);
            yield return new WaitForSeconds(windup);
            if (IsValidTarget(target))
            {
                SpawnProjectile(ResolveDirection(target.position), slowBoltSpeed,
                    Mathf.Max(1, Mathf.RoundToInt(slowBoltDamage * damageMultiplier)), true,
                    slowMultiplier, slowDuration, new Color(0.35f, 0.7f, 1f, 1f));
            }

            castRoutine = null;
        }

        private bool CanCast(Transform target)
        {
            return IsValidTarget(target)
                && ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude <= castRange * castRange;
        }

        private static bool IsValidTarget(Transform target)
        {
            return target != null && target.gameObject.activeInHierarchy;
        }

        private Vector2 ResolveDirection(Vector2 targetPoint)
        {
            Vector2 direction = targetPoint - (Vector2)transform.position;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        }

        private FalseGodProphetProjectile SpawnProjectile(
            Vector2 direction,
            float speed,
            int resolvedDamage,
            bool appliesSlow,
            float requestedSlowMultiplier,
            float requestedSlowDuration,
            Color color)
        {
            GameObject projectileObject = new GameObject(appliesSlow ? "False God Slow Bolt" : "False God Echo Projectile");
            projectileObject.transform.position = transform.position;
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.14f;
            collider.isTrigger = true;
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 4f);
            renderer.color = color;
            renderer.sortingOrder = 18;
            projectileObject.transform.localScale = appliesSlow ? Vector3.one * 0.18f : Vector3.one * 0.15f;
            FalseGodProphetProjectile projectile = projectileObject.AddComponent<FalseGodProphetProjectile>();
            projectile.Initialize(gameObject, direction, speed, resolvedDamage, projectileLifetime,
                appliesSlow, requestedSlowMultiplier, requestedSlowDuration);
            spawned.Add(projectile);
            return projectile;
        }

        private void PruneSpawned()
        {
            for (int index = spawned.Count - 1; index >= 0; index--)
            {
                if (spawned[index] == null)
                {
                    spawned.RemoveAt(index);
                }
            }
        }
    }

    /// <summary>Committed horizontal shadow projectile; it samples its trajectory once at cast time.</summary>
    [DisallowMultipleComponent]
    public sealed class FalseGodDashShadow : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float startupTelegraph = 0.35f;
        [SerializeField, Min(0.01f)] private float speed = 12f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0.1f)] private float lifetime = 1.25f;
        [SerializeField, Min(0f)] private float cooldown = 4.5f;

        private Coroutine castRoutine;
        private float nextCastTime;
        private FalseGodProphetProjectile lastShadow;
        private Vector2 lastCommittedDirection;

        public bool IsCasting => castRoutine != null;
        public Vector2 LastCommittedDirection => lastCommittedDirection;
        public FalseGodProphetProjectile LastShadow => lastShadow;

        public bool TryCast(Transform target, float damageMultiplier = 1f)
        {
            if (target == null || !target.gameObject.activeInHierarchy || IsCasting || Time.time < nextCastTime)
            {
                return false;
            }

            lastCommittedDirection = ResolveHorizontalDirection(target.position);
            nextCastTime = Time.time + cooldown;
            castRoutine = StartCoroutine(PerformCast(damageMultiplier));
            return true;
        }

        public FalseGodProphetProjectile FireImmediatelyForVerification(Vector2 targetPoint, float damageMultiplier = 1f)
        {
            lastCommittedDirection = ResolveHorizontalDirection(targetPoint);
            return SpawnShadow(damageMultiplier);
        }

        public void CancelCast()
        {
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
                castRoutine = null;
            }
        }

        public void RetireLastShadow()
        {
            if (lastShadow == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(lastShadow.gameObject);
            }
            else
            {
                DestroyImmediate(lastShadow.gameObject);
            }

            lastShadow = null;
        }

        private IEnumerator PerformCast(float damageMultiplier)
        {
            FalseGodCombatPresentation.CreateImpactTelegraph(transform.position, 0.5f, startupTelegraph);
            yield return new WaitForSeconds(startupTelegraph);
            if (isActiveAndEnabled)
            {
                SpawnShadow(damageMultiplier);
            }

            castRoutine = null;
        }

        private FalseGodProphetProjectile SpawnShadow(float damageMultiplier)
        {
            GameObject shadowObject = new GameObject("False God Dash Shadow");
            shadowObject.transform.position = transform.position;
            Rigidbody2D body = shadowObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = shadowObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.2f;
            collider.isTrigger = true;
            SpriteRenderer renderer = shadowObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 4f);
            renderer.color = new Color(0.35f, 0.1f, 0.55f, 0.8f);
            renderer.sortingOrder = 17;
            shadowObject.transform.localScale = new Vector3(0.4f, 0.18f, 1f);
            lastShadow = shadowObject.AddComponent<FalseGodProphetProjectile>();
            lastShadow.Initialize(gameObject, lastCommittedDirection, speed,
                Mathf.Max(1, Mathf.RoundToInt(damage * damageMultiplier)), lifetime);
            return lastShadow;
        }

        private Vector2 ResolveHorizontalDirection(Vector2 targetPoint)
        {
            float horizontal = targetPoint.x - transform.position.x;
            return new Vector2(Mathf.Abs(horizontal) > 0.01f ? Mathf.Sign(horizontal) : 1f, 0f);
        }
    }

    /// <summary>
    /// Claim-crystal sourced tether.  It holds direct source/target references
    /// only, and its durability is an explicit response seam rather than an
    /// unbreakable forced-pull state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodRuntimeFoundation))]
    public sealed class FalseGodCrystalChainHook : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float maximumSourceRange = 10f;
        [SerializeField, Min(0f)] private float telegraphDuration = 0.6f;
        [SerializeField, Min(0.1f)] private float travelSpeed = 7f;
        [SerializeField, Min(0.1f)] private float responseWindow = 0.35f;
        [SerializeField, Min(0.1f)] private float reelDuration = 0.9f;
        [SerializeField, Min(0f)] private float reelSpeed = 5.5f;
        [SerializeField, Min(1)] private int tetherDurability = 2;
        [SerializeField, Min(0.1f)] private float maximumTetherLength = 11f;
        [SerializeField, Min(0f)] private float cooldown = 6.5f;

        private readonly List<ClaimAnchor> anchorScratch = new List<ClaimAnchor>(8);
        private FalseGodRuntimeFoundation foundation;
        private Coroutine routine;
        private LineRenderer tetherLine;
        private Material tetherMaterial;
        private ClaimAnchor source;
        private PlayerHealth target;
        private int remainingDurability;
        private float nextCastTime;

        public bool IsActive => routine != null || (source != null && target != null);
        public ClaimAnchor Source => source;
        public int RemainingDurability => remainingDurability;

        private void Awake()
        {
            foundation = GetComponent<FalseGodRuntimeFoundation>();
        }

        private void Update()
        {
            if (source == null || target == null)
            {
                return;
            }

            if (!source.IsActive
                || (Vector2.Distance(source.transform.position, target.transform.position) > maximumTetherLength))
            {
                BreakTether();
                return;
            }

            UpdateLine(target.transform.position);
        }

        private void OnDisable()
        {
            BreakTether();
        }

        public bool TryCast(PlayerHealth requestedTarget)
        {
            if (requestedTarget == null || IsActive || Time.time < nextCastTime || !TrySelectSource(out ClaimAnchor selected))
            {
                return false;
            }

            source = selected;
            target = requestedTarget;
            nextCastTime = Time.time + cooldown;
            routine = StartCoroutine(PerformHook());
            return true;
        }

        public bool AttachForVerification(PlayerHealth requestedTarget)
        {
            if (requestedTarget == null || !TrySelectSource(out ClaimAnchor selected))
            {
                return false;
            }

            source = selected;
            target = requestedTarget;
            remainingDurability = Mathf.Max(1, tetherDurability);
            SubscribeSource();
            EnsureLine();
            UpdateLine(target.transform.position);
            return true;
        }

        public bool ApplyTetherDamage(int amount)
        {
            if (source == null || amount <= 0)
            {
                return false;
            }

            remainingDurability -= amount;
            if (remainingDurability <= 0)
            {
                BreakTether();
            }

            return true;
        }

        public void BreakTether()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            UnsubscribeSource();
            source = null;
            target = null;
            remainingDurability = 0;
            if (tetherLine != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(tetherLine.gameObject);
                }
                else
                {
                    DestroyImmediate(tetherLine.gameObject);
                }

                tetherLine = null;
            }

            if (tetherMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(tetherMaterial);
                }
                else
                {
                    DestroyImmediate(tetherMaterial);
                }

                tetherMaterial = null;
            }
        }

        private IEnumerator PerformHook()
        {
            FalseGodCombatPresentation.CreateImpactTelegraph(source.transform.position, 0.42f, telegraphDuration);
            yield return new WaitForSeconds(telegraphDuration);
            if (source == null || !source.IsActive || target == null || !target.gameObject.activeInHierarchy)
            {
                BreakTether();
                yield break;
            }

            Vector2 hookPosition = source.transform.position;
            Vector2 destination = target.transform.position;
            Vector2 direction = (destination - hookPosition).normalized;
            float travelRemaining = Vector2.Distance(hookPosition, destination);
            EnsureLine();
            while (travelRemaining > 0f && source != null && source.IsActive && target != null)
            {
                float step = Mathf.Min(travelRemaining, travelSpeed * Time.deltaTime);
                hookPosition += direction * step;
                travelRemaining -= step;
                UpdateLine(hookPosition);
                yield return null;
            }

            if (source == null || !source.IsActive || target == null)
            {
                BreakTether();
                yield break;
            }

            remainingDurability = Mathf.Max(1, tetherDurability);
            SubscribeSource();
            yield return new WaitForSeconds(responseWindow);
            float end = Time.time + reelDuration;
            while (Time.time < end && source != null && source.IsActive && target != null && remainingDurability > 0)
            {
                Vector2 pull = (Vector2)source.transform.position - (Vector2)target.transform.position;
                target.GetComponent<PlayerController>()?.ApplyExternalKnockback(
                    pull.normalized * reelSpeed,
                    0.05f);
                UpdateLine(target.transform.position);
                yield return new WaitForFixedUpdate();
            }

            BreakTether();
        }

        private bool TrySelectSource(out ClaimAnchor selected)
        {
            selected = null;
            if (foundation == null)
            {
                foundation = GetComponent<FalseGodRuntimeFoundation>();
            }

            ClaimAuthorityNetwork network = foundation != null ? foundation.AuthorityNetwork : null;
            if (network == null || !network.CanMaintainAuthority)
            {
                return false;
            }

            network.CopyActiveAnchors(anchorScratch);
            float bestDistance = float.MaxValue;
            int bestId = int.MaxValue;
            for (int index = 0; index < anchorScratch.Count; index++)
            {
                ClaimAnchor candidate = anchorScratch[index];
                if (candidate == null || !candidate.IsActive
                    || candidate.GetComponent<ClaimCrystal>() == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(transform.position, candidate.transform.position);
                int candidateId = candidate.GetInstanceID();
                if (distance <= maximumSourceRange
                    && (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && candidateId < bestId)))
                {
                    selected = candidate;
                    bestDistance = distance;
                    bestId = candidateId;
                }
            }

            return selected != null;
        }

        private void SubscribeSource()
        {
            if (source != null)
            {
                source.ActiveStateChanged -= HandleSourceStateChanged;
                source.ActiveStateChanged += HandleSourceStateChanged;
                source.AnchorDestroyed -= HandleSourceDestroyed;
                source.AnchorDestroyed += HandleSourceDestroyed;
            }
        }

        private void UnsubscribeSource()
        {
            if (source != null)
            {
                source.ActiveStateChanged -= HandleSourceStateChanged;
                source.AnchorDestroyed -= HandleSourceDestroyed;
            }
        }

        private void HandleSourceStateChanged(ClaimAnchor changed, bool active)
        {
            if (changed == source && !active)
            {
                BreakTether();
            }
        }

        private void HandleSourceDestroyed(ClaimAnchor destroyed)
        {
            if (destroyed == source)
            {
                BreakTether();
            }
        }

        private void EnsureLine()
        {
            if (tetherLine != null)
            {
                return;
            }

            GameObject lineObject = new GameObject("False God Crystal Chain Tether");
            lineObject.transform.SetParent(transform, false);
            tetherLine = lineObject.AddComponent<LineRenderer>();
            tetherLine.useWorldSpace = true;
            tetherLine.positionCount = 2;
            tetherLine.startWidth = 0.045f;
            tetherLine.endWidth = 0.045f;
            tetherLine.startColor = new Color(0.8f, 0.25f, 1f, 1f);
            tetherLine.endColor = tetherLine.startColor;
            tetherLine.sortingOrder = 19;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                tetherMaterial = new Material(shader);
                tetherLine.sharedMaterial = tetherMaterial;
            }
        }

        private void UpdateLine(Vector2 end)
        {
            if (tetherLine == null || source == null)
            {
                return;
            }

            tetherLine.SetPosition(0, source.transform.position);
            tetherLine.SetPosition(1, end);
        }
    }

    /// <summary>Uses the shared defense controller rather than a Prophet-only negation rule.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class FalseGodProphetBlock : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float blockChance = 0.42f;
        [SerializeField, Min(0.05f)] private float blockDuration = 0.38f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.55f;
        [SerializeField, Min(0f)] private float cooldown = 3f;

        private EnemyDefenseController defense;

        public EnemyDefenseController Defense => defense;
        public bool IsBlocking => defense != null && defense.IsActivelyBlocking;

        private void Awake()
        {
            InitializeRuntime();
        }

        public void InitializeRuntime()
        {
            defense = GetComponent<EnemyDefenseController>();
            if (defense == null)
            {
                defense = gameObject.AddComponent<EnemyDefenseController>();
            }

            defense.ConfigureSkeletonBlock(blockChance, blockDuration, recoveryDuration, cooldown);
            defense.InitializeRuntimeDependencies();
        }

        public bool TryBeginBlock()
        {
            InitializeRuntime();
            return defense != null && defense.TryEnterDefensivePosture(blockDuration);
        }
    }

    /// <summary>One destructible support node. It deliberately creates no Claim territory.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class FalseGodTriuneAnchor : MonoBehaviour
    {
        private Damageable damageable;
        private bool subscribed;

        public FalseGodTriuneAnchorKind Kind { get; private set; }
        public bool IsActive => gameObject.activeInHierarchy && damageable != null && damageable.CurrentHealth > 0;
        public event Action<FalseGodTriuneAnchor> Retired;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Retired?.Invoke(this);
        }

        public void InitializeRuntime(FalseGodTriuneAnchorKind kind, int durability)
        {
            Kind = kind;
            damageable = GetComponent<Damageable>();
            damageable.SetRuntimeMaximumHealth(Mathf.Max(1, durability), true);
            Subscribe();
        }

        public void Retire()
        {
            Retired?.Invoke(this);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void HandleDied()
        {
            Retired?.Invoke(this);
        }

        private void Subscribe()
        {
            if (subscribed || damageable == null)
            {
                return;
            }

            subscribed = true;
            damageable.Died += HandleDied;
        }

        private void Unsubscribe()
        {
            if (!subscribed || damageable == null)
            {
                return;
            }

            subscribed = false;
            damageable.Died -= HandleDied;
        }
    }

    /// <summary>
    /// Owns at most one of each Triune support node.  Heal modifies only the
    /// owner Damageable, Empower only changes Prophet basic-cast damage, and
    /// Suppress contributes one stamina-regeneration modifier.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class FalseGodTriuneAnchors : MonoBehaviour
    {
        [SerializeField, Min(1)] private int anchorDurability = 3;
        [SerializeField, Min(0.1f)] private float placementRadius = 2.3f;
        [SerializeField, Min(0.1f)] private float healInterval = 1.5f;
        [SerializeField, Min(1)] private int healAmount = 1;
        [SerializeField, Min(1f)] private float empowerBasicCastMultiplier = 1.35f;
        [SerializeField, Range(0.1f, 1f)] private float suppressStaminaRegenMultiplier = 0.6f;

        private readonly Dictionary<FalseGodTriuneAnchorKind, FalseGodTriuneAnchor> anchors =
            new Dictionary<FalseGodTriuneAnchorKind, FalseGodTriuneAnchor>();
        private Damageable ownerHealth;
        private Transform suppressionTarget;
        private PlayerRecoveryModifiers appliedSuppression;
        private float nextHealTime;

        public int ActiveAnchorCount
        {
            get
            {
                PruneAnchors();
                return anchors.Count;
            }
        }

        private void Awake()
        {
            InitializeRuntimeDependencies();
        }

        /// <summary>Resolves the owner health used by anchors without requiring an Awake callback.</summary>
        public void InitializeRuntimeDependencies()
        {
            ownerHealth = GetComponent<Damageable>();
        }

        private void Update()
        {
            if (Time.time >= nextHealTime)
            {
                ApplyHealTick();
                nextHealTime = Time.time + healInterval;
            }

            RefreshSuppression();
        }

        private void OnDisable()
        {
            RetireAll();
        }

        public void SetSuppressionTarget(Transform target)
        {
            if (suppressionTarget == target)
            {
                return;
            }

            RemoveSuppression();
            suppressionTarget = target;
            RefreshSuppression();
        }

        public bool TryCreate(FalseGodTriuneAnchorKind kind)
        {
            InitializeRuntimeDependencies();
            PruneAnchors();
            if (anchors.ContainsKey(kind))
            {
                return false;
            }

            GameObject anchorObject = new GameObject("False God Triune " + kind + " Anchor");
            anchorObject.transform.position = (Vector2)transform.position + ResolveOffset(kind) * placementRadius;
            anchorObject.transform.SetParent(transform, true);
            anchorObject.AddComponent<InteractionIdentity>();
            Damageable health = anchorObject.AddComponent<Damageable>();
            health.InitializeRuntimeState();
            CircleCollider2D collider = anchorObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.28f;
            LineRenderer presentation = anchorObject.AddComponent<LineRenderer>();
            ConfigurePresentation(presentation, kind);
            FalseGodTriuneAnchor anchor = anchorObject.AddComponent<FalseGodTriuneAnchor>();
            anchor.InitializeRuntime(kind, anchorDurability);
            anchor.Retired += HandleAnchorRetired;
            anchors.Add(kind, anchor);
            FalseGodCombatPresentation.CreateImpactTelegraph(anchorObject.transform.position, 0.48f, 0.45f);
            RefreshSuppression();
            return true;
        }

        public bool HasActive(FalseGodTriuneAnchorKind kind)
        {
            PruneAnchors();
            return anchors.TryGetValue(kind, out FalseGodTriuneAnchor anchor) && anchor != null && anchor.IsActive;
        }

        public bool TryGetAnchor(FalseGodTriuneAnchorKind kind, out FalseGodTriuneAnchor anchor)
        {
            PruneAnchors();
            return anchors.TryGetValue(kind, out anchor) && anchor != null;
        }

        public float ResolveBasicCastDamageMultiplier()
        {
            return HasActive(FalseGodTriuneAnchorKind.Empower) ? empowerBasicCastMultiplier : 1f;
        }

        public int ApplyHealTickForVerification()
        {
            return ApplyHealTick();
        }

        public void RetireAll()
        {
            RemoveSuppression();
            List<FalseGodTriuneAnchor> retire = new List<FalseGodTriuneAnchor>(anchors.Values);
            anchors.Clear();
            for (int index = 0; index < retire.Count; index++)
            {
                if (retire[index] != null)
                {
                    retire[index].Retired -= HandleAnchorRetired;
                    retire[index].Retire();
                }
            }
        }

        private int ApplyHealTick()
        {
            InitializeRuntimeDependencies();
            return HasActive(FalseGodTriuneAnchorKind.Heal) && ownerHealth != null
                ? ownerHealth.RestoreHealthResolved(healAmount)
                : 0;
        }

        private void RefreshSuppression()
        {
            if (!HasActive(FalseGodTriuneAnchorKind.Suppress)
                || suppressionTarget == null
                || !suppressionTarget.gameObject.activeInHierarchy
                || suppressionTarget.GetComponentInParent<PlayerHealth>() == null)
            {
                RemoveSuppression();
                return;
            }

            PlayerRecoveryModifiers modifiers = suppressionTarget.GetComponent<PlayerRecoveryModifiers>();
            if (modifiers == null)
            {
                modifiers = suppressionTarget.gameObject.AddComponent<PlayerRecoveryModifiers>();
            }

            if (appliedSuppression != null && appliedSuppression != modifiers)
            {
                appliedSuppression.RemoveModifier(this);
            }

            appliedSuppression = modifiers;
            appliedSuppression.SetModifier(this, 1f, suppressStaminaRegenMultiplier, 1f);
        }

        private void RemoveSuppression()
        {
            if (appliedSuppression != null)
            {
                appliedSuppression.RemoveModifier(this);
                appliedSuppression = null;
            }
        }

        private void HandleAnchorRetired(FalseGodTriuneAnchor anchor)
        {
            if (anchor == null)
            {
                return;
            }

            anchor.Retired -= HandleAnchorRetired;
            if (anchors.TryGetValue(anchor.Kind, out FalseGodTriuneAnchor current) && current == anchor)
            {
                anchors.Remove(anchor.Kind);
            }

            if (anchor.Kind == FalseGodTriuneAnchorKind.Suppress)
            {
                RemoveSuppression();
            }
        }

        private void PruneAnchors()
        {
            for (int value = 0; value <= (int)FalseGodTriuneAnchorKind.Suppress; value++)
            {
                FalseGodTriuneAnchorKind kind = (FalseGodTriuneAnchorKind)value;
                if (anchors.TryGetValue(kind, out FalseGodTriuneAnchor anchor)
                    && (anchor == null || !anchor.IsActive))
                {
                    anchors.Remove(kind);
                    if (kind == FalseGodTriuneAnchorKind.Suppress)
                    {
                        RemoveSuppression();
                    }
                }
            }
        }

        private static Vector2 ResolveOffset(FalseGodTriuneAnchorKind kind)
        {
            switch (kind)
            {
                case FalseGodTriuneAnchorKind.Heal:
                    return new Vector2(-1f, 0.65f).normalized;
                case FalseGodTriuneAnchorKind.Empower:
                    return new Vector2(1f, 0.65f).normalized;
                default:
                    return Vector2.down;
            }
        }

        private static void ConfigurePresentation(LineRenderer line, FalseGodTriuneAnchorKind kind)
        {
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.sortingOrder = 21;
            Color color = kind == FalseGodTriuneAnchorKind.Heal
                ? new Color(0.35f, 1f, 0.55f, 1f)
                : kind == FalseGodTriuneAnchorKind.Empower
                    ? new Color(1f, 0.35f, 0.6f, 1f)
                    : new Color(0.35f, 0.65f, 1f, 1f);
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, new Vector3(0f, 0.32f));
            line.SetPosition(1, new Vector3(0.32f, 0f));
            line.SetPosition(2, new Vector3(0f, -0.32f));
            line.SetPosition(3, new Vector3(-0.32f, 0f));
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.sharedMaterial = new Material(shader);
            }
        }
    }

    /// <summary>Event-only Version 1 defeat seam. Permanent Domain rewards remain true-body-only work.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class FalseGodProphetPhaseBoundary : MonoBehaviour
    {
        private Damageable damageable;
        private bool requested;

        public event Action ProphetDefeated;
        public event Action AscensionRequested;
        public bool HasRequestedAscension => requested;

        private void Awake()
        {
            InitializeRuntimeDependencies();
        }

        private void OnEnable()
        {
            InitializeRuntimeDependencies();
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.Died -= RequestAscension;
            }
        }

        /// <summary>
        /// Resolves and binds the defeat authority for explicit factories and
        /// deterministic verification without invoking Unity lifecycle methods.
        /// </summary>
        public void InitializeRuntimeDependencies()
        {
            Damageable resolvedDamageable = GetComponent<Damageable>();
            if (damageable != resolvedDamageable && damageable != null)
            {
                damageable.Died -= RequestAscension;
            }

            damageable = resolvedDamageable;
            if (damageable != null)
            {
                damageable.Died -= RequestAscension;
                damageable.Died += RequestAscension;
            }
        }

        public void RequestAscension()
        {
            if (requested)
            {
                return;
            }

            requested = true;
            ProphetDefeated?.Invoke();
            AscensionRequested?.Invoke();
        }
    }
}
