using Cave.Audio;
using Cave.Combat;
using Cave.Player;
using Cave.Progression;
using Cave.World;
using UnityEngine;

namespace Cave.Projectiles
{
    public enum ProjectileTeam
    {
        Enemy,
        Player,
        Neutral
    }

    public enum FireballEvolutionState
    {
        Normal,
        Evolved,
        Telegraphing,
        Split,
        Reflected,
        LateDeflected
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class FireballProjectile : MonoBehaviour, IDeflectableDamageSource, IPlayerParryableProjectile
    {
        [Header("Impact")]
        [SerializeField, Min(0.01f)] private float impactVisualLifetime = 0.25f;
        [SerializeField, Min(1f)] private float impactEndScale = 3f;

        [Header("Current Evolution State (Read Only)")]
        [SerializeField] private FireballEvolutionState evolutionState = FireballEvolutionState.Normal;

        private Rigidbody2D body;
        private Collider2D projectileCollider;
        private SpriteRenderer projectileRenderer;
        private GameObject originalShooter;
        private GameObject currentOwner;
        private Transform intendedTarget;
        private ProgressionDifficultySettings evolutionSettings;
        private float speed;
        private float expiresAt;
        private float splitAt;
        private bool explosionStarted;
        private bool canSplit;
        private bool splitTelegraphStarted;
        private DamageContext reflectedDamageContext;
        private bool suppressOwnerSecondaryEffects;
        private Vector3 normalVisualScale;
        private Color normalVisualColor;

        public GameObject OriginalShooter => originalShooter;
        public GameObject CurrentOwner => currentOwner;
        public ProjectileTeam CurrentTeam { get; private set; }
        public FireballEvolutionState EvolutionState => evolutionState;
        public Vector2 MovementDirection { get; private set; }
        public int Damage { get; private set; }
        public bool IsParried { get; private set; }
        public bool CanBeParried => !explosionStarted && CurrentTeam == ProjectileTeam.Enemy && !IsParried;

        public void ConfigureAsSecondaryEcho(Color tint, float visualScaleMultiplier)
        {
            suppressOwnerSecondaryEffects = true;
            transform.localScale *= Mathf.Max(0.1f, visualScaleMultiplier);
            normalVisualScale = transform.localScale;
            if (projectileRenderer != null)
            {
                projectileRenderer.color *= tint;
                normalVisualColor = projectileRenderer.color;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileRenderer = GetComponent<SpriteRenderer>();
            normalVisualScale = transform.localScale;
            if (projectileRenderer != null)
            {
                normalVisualColor = projectileRenderer.color;
            }
        }

        private void Update()
        {
            if (explosionStarted || !canSplit)
            {
                return;
            }

            if (intendedTarget == null || !intendedTarget.gameObject.activeInHierarchy)
            {
                CancelSplitTelegraph(true);
                return;
            }

            if (splitTelegraphStarted)
            {
                UpdateSplitTelegraph();
                if (Time.time >= splitAt)
                {
                    SplitProjectile();
                }

                return;
            }

            float splitDistance = Mathf.Max(0.01f, evolutionSettings.SplitDistance);
            if (((Vector2)intendedTarget.position - (Vector2)transform.position).sqrMagnitude
                <= splitDistance * splitDistance)
            {
                BeginSplitTelegraph();
            }
        }

        public void Initialize(
            GameObject shooter,
            Vector2 direction,
            float projectileSpeed,
            int damage,
            float lifetime)
        {
            Initialize(shooter, null, direction, projectileSpeed, damage, lifetime, null);
        }

        public void Initialize(
            GameObject shooter,
            Transform target,
            Vector2 direction,
            float projectileSpeed,
            int damage,
            float lifetime,
            WorldDifficultyManager difficultyManager)
        {
            originalShooter = shooter;
            currentOwner = shooter;
            intendedTarget = target;
            CurrentTeam = ProjectileTeam.Enemy;
            MovementDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.left;
            speed = Mathf.Max(0.01f, projectileSpeed);
            Damage = Mathf.Max(1, damage);
            IsParried = false;
            explosionStarted = false;
            splitTelegraphStarted = false;
            reflectedDamageContext = default;
            evolutionSettings = difficultyManager != null ? difficultyManager.Settings : null;
            canSplit = evolutionSettings != null
                && difficultyManager.DifficultyTier >= evolutionSettings.EvolutionStartTier;
            evolutionState = canSplit
                ? FireballEvolutionState.Evolved
                : FireballEvolutionState.Normal;
            RestoreNormalVisual();
            body.simulated = true;
            projectileCollider.enabled = true;
            body.velocity = MovementDirection * speed;
            expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
            Destroy(gameObject, Mathf.Max(0.01f, lifetime));
        }

        public bool Parry(GameObject newOwner, Vector2 fallbackDirection, float speedMultiplier)
        {
            return Parry(newOwner, fallbackDirection, speedMultiplier, 1f);
        }

        public bool Parry(
            GameObject newOwner,
            Vector2 fallbackDirection,
            float speedMultiplier,
            float damageMultiplier)
        {
            if (!CanBeParried)
            {
                return false;
            }

            Vector2 reflectedDirection = -MovementDirection;
            if (originalShooter != null && originalShooter.activeInHierarchy)
            {
                Vector2 directionToShooter = originalShooter.transform.position - transform.position;
                if (directionToShooter.sqrMagnitude > 0.001f)
                {
                    reflectedDirection = directionToShooter.normalized;
                }
            }
            else if (fallbackDirection.sqrMagnitude > 0.001f)
            {
                reflectedDirection = fallbackDirection.normalized;
            }

            CancelSplitTelegraph(false);
            currentOwner = newOwner;
            CurrentTeam = ProjectileTeam.Player;
            IsParried = true;
            PlayerResourceMastery resourceMastery = newOwner != null
                ? newOwner.GetComponentInParent<PlayerResourceMastery>()
                : null;
            reflectedDamageContext = resourceMastery != null
                ? resourceMastery.CreatePlayerDamageContext().WithTraits(DamageTrait.Projectile)
                : default;
            MovementDirection = reflectedDirection;
            speed *= Mathf.Max(1f, speedMultiplier);
            Damage = Mathf.Max(1, Mathf.RoundToInt(Damage * Mathf.Max(1f, damageMultiplier)));
            intendedTarget = originalShooter != null ? originalShooter.transform : null;
            evolutionState = FireballEvolutionState.Reflected;
            body.velocity = MovementDirection * speed;
            return true;
        }

        public bool DeflectToGround(GameObject newOwner, Vector2 targetPoint)
        {
            if (!CanBeParried)
            {
                return false;
            }

            Vector2 direction = targetPoint - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.down;
            }

            CancelSplitTelegraph(true);
            canSplit = false;
            currentOwner = newOwner;
            CurrentTeam = ProjectileTeam.Neutral;
            IsParried = true;
            intendedTarget = null;
            reflectedDamageContext = default;
            MovementDirection = direction.normalized;
            evolutionState = FireballEvolutionState.LateDeflected;
            body.velocity = MovementDirection * speed;
            return true;
        }

        public bool TryDeflect(GameObject defender)
        {
            Vector2 fallbackDirection = defender != null
                ? (Vector2)(transform.position - defender.transform.position)
                : -MovementDirection;
            return Parry(defender, fallbackDirection, 1f, 1f);
        }

        private void BeginSplitTelegraph()
        {
            splitTelegraphStarted = true;
            splitAt = Time.time + Mathf.Max(0f, evolutionSettings.SplitTelegraphDuration);
            evolutionState = FireballEvolutionState.Telegraphing;
        }

        private void UpdateSplitTelegraph()
        {
            float duration = Mathf.Max(0.01f, evolutionSettings.SplitTelegraphDuration);
            float progress = 1f - Mathf.Clamp01((splitAt - Time.time) / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            transform.localScale = normalVisualScale * Mathf.Lerp(1f, 1.35f, pulse);

            if (projectileRenderer != null)
            {
                projectileRenderer.color = Color.Lerp(normalVisualColor, Color.yellow, pulse * 0.65f);
            }
        }

        private void SplitProjectile()
        {
            if (explosionStarted || !canSplit || evolutionSettings == null)
            {
                return;
            }

            canSplit = false;
            splitTelegraphStarted = false;
            evolutionState = FireballEvolutionState.Split;
            int childCount = Mathf.Clamp(
                evolutionSettings.SplitCount,
                1,
                evolutionSettings.MaximumSplitCount);
            float spread = evolutionSettings.SplitSpreadAngle;
            float childLifetime = Mathf.Max(0.1f, expiresAt - Time.time);

            for (int index = 0; index < childCount; index++)
            {
                float angularOffset = childCount == 1
                    ? 0f
                    : Mathf.Lerp(-spread, spread, index / (float)(childCount - 1));
                Vector2 childDirection = Quaternion.Euler(0f, 0f, angularOffset) * MovementDirection;
                FireballProjectile child = Instantiate(this, transform.position, transform.rotation);
                child.InitializeSplitChild(
                    originalShooter,
                    currentOwner,
                    intendedTarget,
                    CurrentTeam,
                    childDirection,
                    speed * evolutionSettings.ChildSpeedMultiplier,
                    Mathf.Max(1, Mathf.RoundToInt(Damage * evolutionSettings.ChildDamageMultiplier)),
                    childLifetime,
                    reflectedDamageContext,
                    normalVisualScale,
                    normalVisualColor);
            }

            StopAndDestroyWithoutImpact();
        }

        private void InitializeSplitChild(
            GameObject sourceShooter,
            GameObject owner,
            Transform target,
            ProjectileTeam team,
            Vector2 direction,
            float projectileSpeed,
            int damage,
            float lifetime,
            DamageContext damageContext,
            Vector3 visualScale,
            Color visualColor)
        {
            originalShooter = sourceShooter;
            currentOwner = owner;
            intendedTarget = target;
            CurrentTeam = team;
            MovementDirection = direction.normalized;
            speed = Mathf.Max(0.01f, projectileSpeed);
            Damage = Mathf.Max(1, damage);
            IsParried = team != ProjectileTeam.Enemy;
            explosionStarted = false;
            canSplit = false;
            splitTelegraphStarted = false;
            evolutionSettings = null;
            reflectedDamageContext = damageContext;
            evolutionState = team == ProjectileTeam.Player
                ? FireballEvolutionState.Reflected
                : FireballEvolutionState.Normal;
            normalVisualScale = visualScale;
            normalVisualColor = visualColor;
            RestoreNormalVisual();
            body.simulated = true;
            projectileCollider.enabled = true;
            body.velocity = MovementDirection * speed;
            expiresAt = Time.time + lifetime;
            Destroy(gameObject, lifetime);
        }

        private void CancelSplitTelegraph(bool removeEvolution)
        {
            splitTelegraphStarted = false;
            if (removeEvolution)
            {
                canSplit = false;
            }

            RestoreNormalVisual();
        }

        private void RestoreNormalVisual()
        {
            transform.localScale = normalVisualScale;
            if (projectileRenderer != null)
            {
                projectileRenderer.color = normalVisualColor;
                projectileRenderer.enabled = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (explosionStarted)
            {
                return;
            }

            ParryHitbox parryHitbox = other.GetComponent<ParryHitbox>();
            if (parryHitbox != null)
            {
                parryHitbox.TryParry(this);
                return;
            }

            SpinSwordAttack swordAttack = other.GetComponentInParent<SpinSwordAttack>();
            if (swordAttack != null && swordAttack.UsesAttackCollider(other))
            {
                BeginExplosion();
                return;
            }

            if (other.GetComponent<ChargedAttackHitbox>() != null || IsOwnedCollider(other))
            {
                return;
            }

            if (CurrentTeam == ProjectileTeam.Enemy)
            {
                PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    DamageContext incomingContext = new DamageContext(
                        !suppressOwnerSecondaryEffects && currentOwner != null
                            ? currentOwner
                            : gameObject,
                        DamageTrait.Direct | DamageTrait.Projectile);
                    playerHealth.TryTakeDamage(Damage, incomingContext);
                    if (CurrentTeam == ProjectileTeam.Enemy)
                    {
                        BeginExplosion();
                    }

                    return;
                }

                if (other.GetComponentInParent<Damageable>() != null)
                {
                    return;
                }
            }
            else if (CurrentTeam == ProjectileTeam.Player)
            {
                Damageable damageable = other.GetComponentInParent<Damageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(Damage, reflectedDamageContext);
                    BeginExplosion();
                    return;
                }
            }

            if (!other.isTrigger)
            {
                BeginExplosion();
            }
        }

        private void BeginExplosion()
        {
            if (explosionStarted)
            {
                return;
            }

            explosionStarted = true;
            CaveSfx.Play(CaveSfxCue.Explosion, 0.65f);
            canSplit = false;
            splitTelegraphStarted = false;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            projectileCollider.enabled = false;

            if (projectileRenderer != null)
            {
                FireballImpactEffect.Create(
                    projectileRenderer,
                    impactVisualLifetime,
                    impactEndScale);
                projectileRenderer.enabled = false;
            }

            Destroy(gameObject);
        }

        private void StopAndDestroyWithoutImpact()
        {
            explosionStarted = true;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            projectileCollider.enabled = false;
            if (projectileRenderer != null)
            {
                projectileRenderer.enabled = false;
            }

            Destroy(gameObject);
        }

        private bool IsOwnedCollider(Collider2D other)
        {
            return currentOwner != null && other.transform.root == currentOwner.transform.root;
        }
    }
}
