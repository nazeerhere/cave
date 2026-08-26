using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SidewaysParryAttack), typeof(SpinSwordAttack), typeof(PlayerDash))]
    public sealed class PlayerCrowdResponse : MonoBehaviour
    {
        [Header("Slip")]
        [SerializeField, Min(0.05f)] private float slipDuration = 0.24f;
        [SerializeField, Min(0.1f)] private float blockSlipSpeedMultiplier = 0.9f;
        [SerializeField, Min(0.1f)] private float normalSlipSpeedMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float perfectSlipSpeedMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float blockSlipStaminaCost = 15f;
        [SerializeField, Min(0f)] private float normalSlipStaminaCost = 8f;
        [SerializeField, Min(0f)] private float perfectSlipStaminaCost;

        [Header("Counter Push")]
        [SerializeField, Min(0.1f)] private float blockPushRadius = 1.65f;
        [SerializeField, Min(0.1f)] private float normalPushRadius = 2.05f;
        [SerializeField, Min(0.1f)] private float perfectPushRadius = 2.5f;
        [SerializeField, Min(0f)] private float blockPushKnockback = 8f;
        [SerializeField, Min(0f)] private float normalPushKnockback = 10f;
        [SerializeField, Min(0f)] private float perfectPushKnockback = 13f;
        [SerializeField, Min(0f)] private float blockPushStaminaCost = 18f;
        [SerializeField, Min(0f)] private float normalPushStaminaCost = 9f;
        [SerializeField, Min(0f)] private float perfectPushStaminaCost;
        [SerializeField, Min(0)] private int counterPushDamage;
        [SerializeField] private LayerMask enemyLayers = ~0;

        private readonly HashSet<Damageable> affectedEnemies = new HashSet<Damageable>();
        private readonly List<ColliderPair> ignoredColliderPairs = new List<ColliderPair>();
        private SidewaysParryAttack defense;
        private SpinSwordAttack stamina;
        private PlayerDash dash;
        private PlayerResourceMastery mastery;
        private PlayerPermanentProgression progression;
        private float slipCollisionRestoreTime;

        public bool IsSlipping => slipCollisionRestoreTime > Time.time;

        private struct ColliderPair
        {
            public Collider2D Player;
            public Collider2D Enemy;
        }

        private void Awake()
        {
            defense = GetComponent<SidewaysParryAttack>();
            stamina = GetComponent<SpinSwordAttack>();
            dash = GetComponent<PlayerDash>();
            mastery = GetComponent<PlayerResourceMastery>();
            progression = GetComponent<PlayerPermanentProgression>();
        }

        private void Update()
        {
            if (slipCollisionRestoreTime > 0f && Time.time >= slipCollisionRestoreTime)
            {
                RestoreEnemyCollisions();
            }
        }

        public bool TrySlip(float horizontalInput, float facingDirection)
        {
            if (defense == null
                || !defense.TryGetCounterWindow(out PlayerDefenseQuality quality))
            {
                return false;
            }

            float staminaCost = ResolveSlipCost(quality);
            if (stamina == null || !stamina.TrySpendStamina(staminaCost))
            {
                return false;
            }

            if (dash == null
                || !dash.TryStartSpecialDash(
                    horizontalInput,
                    facingDirection,
                    ResolveSlipSpeedMultiplier(quality),
                    slipDuration))
            {
                stamina.RestoreStamina(staminaCost);
                return false;
            }

            if (!defense.TryConsumeCounterWindow(out _))
            {
                return false;
            }

            IgnoreEnemyBodyCollisions();
            slipCollisionRestoreTime = Time.time + slipDuration;
            progression?.NotifyDefensiveCounterUsed(quality);
            CombatShapeEffect.Create(
                transform.position + Vector3.up * 0.25f,
                CombatShape.Chevron,
                0.75f,
                new Color(0.25f, 0.95f, 1f, 0.85f),
                slipDuration);
            return true;
        }

        public bool TryCounterPush()
        {
            if (defense == null
                || !defense.TryGetCounterWindow(out PlayerDefenseQuality quality))
            {
                return false;
            }

            float staminaCost = ResolvePushCost(quality);
            if (stamina == null || !stamina.TrySpendStamina(staminaCost))
            {
                return false;
            }

            if (!defense.TryConsumeCounterWindow(out _))
            {
                stamina.RestoreStamina(staminaCost);
                return false;
            }

            float radius = ResolvePushRadius(quality);
            float knockback = ResolvePushKnockback(quality);
            affectedEnemies.Clear();
            foreach (Collider2D overlap in Physics2D.OverlapCircleAll(
                transform.position,
                radius,
                enemyLayers))
            {
                Damageable damageable = overlap.GetComponentInParent<Damageable>();
                if (!IsValidEnemy(damageable) || !affectedEnemies.Add(damageable))
                {
                    continue;
                }

                if (counterPushDamage > 0)
                {
                    DamageContext context = mastery != null
                        ? mastery.CreatePlayerDamageContext().WithTraits(
                            DamageTrait.AreaOfEffect | DamageTrait.StaggerHeavy)
                        : default;
                    damageable.TakeDamage(counterPushDamage, context);
                }

                if (!damageable.gameObject.activeInHierarchy)
                {
                    continue;
                }

                damageable.GetComponent<EnemyStagger>()?.TryStagger(StaggerStrength.Heavy);
                KnockbackReceiver receiver = damageable.GetComponent<KnockbackReceiver>();
                if (receiver != null)
                {
                    Vector2 away = (Vector2)damageable.transform.position - (Vector2)transform.position;
                    if (away.sqrMagnitude <= 0.001f)
                    {
                        away = Vector2.right;
                    }

                    receiver.ApplyKnockback((away.normalized + Vector2.up * 0.18f).normalized * knockback);
                }
            }

            progression?.NotifyDefensiveCounterUsed(quality);
            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Ring,
                radius,
                quality == PlayerDefenseQuality.PerfectParry
                    ? new Color(1f, 0.92f, 0.3f, 0.9f)
                    : new Color(0.25f, 0.82f, 1f, 0.82f),
                0.28f);
            return true;
        }

        private static bool IsValidEnemy(Damageable damageable)
        {
            return damageable != null
                && damageable.CurrentHealth > 0
                && damageable.GetComponent<EnemyArchetypeProfile>() != null;
        }

        private void IgnoreEnemyBodyCollisions()
        {
            RestoreEnemyCollisions();
            Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>(true);
            foreach (EnemyArchetypeProfile enemy in FindObjectsOfType<EnemyArchetypeProfile>())
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Collider2D[] enemyColliders = enemy.GetComponentsInChildren<Collider2D>(true);
                foreach (Collider2D playerCollider in playerColliders)
                {
                    if (playerCollider == null || playerCollider.isTrigger)
                    {
                        continue;
                    }

                    foreach (Collider2D enemyCollider in enemyColliders)
                    {
                        if (enemyCollider == null
                            || enemyCollider.isTrigger
                            || Physics2D.GetIgnoreCollision(playerCollider, enemyCollider))
                        {
                            continue;
                        }

                        Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
                        ignoredColliderPairs.Add(new ColliderPair
                        {
                            Player = playerCollider,
                            Enemy = enemyCollider
                        });
                    }
                }
            }
        }

        private void RestoreEnemyCollisions()
        {
            foreach (ColliderPair pair in ignoredColliderPairs)
            {
                if (pair.Player != null && pair.Enemy != null)
                {
                    Physics2D.IgnoreCollision(pair.Player, pair.Enemy, false);
                }
            }

            ignoredColliderPairs.Clear();
            slipCollisionRestoreTime = 0f;
        }

        private float ResolveSlipCost(PlayerDefenseQuality quality)
        {
            return quality == PlayerDefenseQuality.PerfectParry
                ? perfectSlipStaminaCost
                : quality == PlayerDefenseQuality.NormalParry
                    ? normalSlipStaminaCost
                    : blockSlipStaminaCost;
        }

        private float ResolveSlipSpeedMultiplier(PlayerDefenseQuality quality)
        {
            return quality == PlayerDefenseQuality.PerfectParry
                ? perfectSlipSpeedMultiplier
                : quality == PlayerDefenseQuality.NormalParry
                    ? normalSlipSpeedMultiplier
                    : blockSlipSpeedMultiplier;
        }

        private float ResolvePushCost(PlayerDefenseQuality quality)
        {
            return quality == PlayerDefenseQuality.PerfectParry
                ? perfectPushStaminaCost
                : quality == PlayerDefenseQuality.NormalParry
                    ? normalPushStaminaCost
                    : blockPushStaminaCost;
        }

        private float ResolvePushRadius(PlayerDefenseQuality quality)
        {
            return quality == PlayerDefenseQuality.PerfectParry
                ? perfectPushRadius
                : quality == PlayerDefenseQuality.NormalParry
                    ? normalPushRadius
                    : blockPushRadius;
        }

        private float ResolvePushKnockback(PlayerDefenseQuality quality)
        {
            return quality == PlayerDefenseQuality.PerfectParry
                ? perfectPushKnockback
                : quality == PlayerDefenseQuality.NormalParry
                    ? normalPushKnockback
                    : blockPushKnockback;
        }

        private void OnDisable()
        {
            RestoreEnemyCollisions();
        }
    }
}
