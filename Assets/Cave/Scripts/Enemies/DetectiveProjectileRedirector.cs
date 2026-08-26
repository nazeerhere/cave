using System.Collections.Generic;
using Cave.Combat;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class DetectiveProjectileRedirector : MonoBehaviour, IEnemyInterruptible
    {
        [Header("Interception")]
        [SerializeField, Min(0.1f)] private float interceptionRadius = 3.5f;
        [SerializeField] private LayerMask projectileLayers;
        [SerializeField, Min(0.1f)] private float directThreatCorridor = 0.8f;
        [SerializeField, Min(0.1f)] private float formationPredictionDistance = 4f;
        [SerializeField, Min(0.1f)] private float formationCorridor = 0.75f;
        [SerializeField, Min(1)] private int formationThreatCount = 2;

        [Header("Threat Weights")]
        [SerializeField, Min(0f)] private float directThreatWeight = 5f;
        [SerializeField, Min(0f)] private float supportThreatWeight = 4f;
        [SerializeField, Min(0f)] private float formationThreatWeight = 1.25f;
        [SerializeField, Min(0f)] private float piercingProjectileWeight = 3f;
        [SerializeField, Min(0f)] private float projectileTierWeight = 0.75f;
        [SerializeField, Min(0f)] private float minimumThreatScore = 3f;

        [Header("Commitment")]
        [SerializeField, Min(0f)] private float redirectWindup = 0.22f;
        [SerializeField, Min(0f)] private float redirectRecovery = 0.8f;
        [SerializeField, Min(0f)] private float redirectCooldown = 2f;
        [SerializeField, Min(0f)] private float failedRedirectRecovery = 0.35f;
        [SerializeField] private Color redirectTelegraphColor =
            new Color(0.35f, 0.95f, 1f, 0.95f);

        [Header("Debug")]
        [SerializeField] private bool drawInterceptionRadius = true;
        [SerializeField] private string currentRedirectDecision = "Ready";

        private readonly Collider2D[] projectileBuffer = new Collider2D[32];
        private readonly HashSet<int> evaluatedProjectiles = new HashSet<int>();
        private Damageable damageable;
        private EnemyStagger stagger;
        private EnemyPoisonShooter poisonShooter;
        private DetectiveStunGrenadeAbility grenade;
        private DetectiveSuppressionZoneAbility suppressionZone;
        private MonoBehaviour committedProjectileBehaviour;
        private IEnemyParryableProjectile committedProjectile;
        private float redirectCompletesAt;
        private float recoveringUntil;
        private float nextRedirectTime;
        private bool isWindingUp;

        public bool IsWindingUp => isWindingUp;
        public bool IsRecovering => Time.time < recoveringUntil;
        public bool IsBusy => IsWindingUp || IsRecovering;
        public bool IsReady => !IsBusy
            && Time.time >= nextRedirectTime
            && damageable != null
            && damageable.CurrentHealth > 0
            && (stagger == null || stagger.CanAct);

        private void Awake()
        {
            RefreshDependencies();
        }

        private void Start()
        {
            RefreshDependencies();
        }

        private void Update()
        {
            if (isWindingUp && Time.time >= redirectCompletesAt)
            {
                ResolveRedirect();
            }
            else if (!isWindingUp && Time.time >= recoveringUntil
                && currentRedirectDecision == "Recovering")
            {
                currentRedirectDecision = "Ready";
            }
        }

        public bool TryUseBestThreat()
        {
            RefreshLateDependencies();
            if (!IsReady || HasIncompatibleAction())
            {
                return false;
            }

            if (!TryFindBestThreat(out MonoBehaviour behaviour,
                    out IEnemyParryableProjectile projectile,
                    out float score))
            {
                return false;
            }

            committedProjectileBehaviour = behaviour;
            committedProjectile = projectile;
            isWindingUp = true;
            redirectCompletesAt = Time.time + redirectWindup;
            currentRedirectDecision = $"Redirect threat ({score:0.0})";
            CombatShapeEffect.Create(
                transform.position + Vector3.up * 0.35f,
                CombatShape.Diamond,
                0.72f,
                redirectTelegraphColor,
                Mathf.Max(0.15f, redirectWindup));
            return true;
        }

        private bool TryFindBestThreat(
            out MonoBehaviour bestBehaviour,
            out IEnemyParryableProjectile bestProjectile,
            out float bestScore)
        {
            bestBehaviour = null;
            bestProjectile = null;
            bestScore = minimumThreatScore;
            int mask = projectileLayers.value != 0
                ? projectileLayers.value
                : Physics2D.AllLayers;
            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                interceptionRadius,
                projectileBuffer,
                mask);
            Damageable[] allies = FindObjectsOfType<Damageable>();
            evaluatedProjectiles.Clear();

            for (int colliderIndex = 0; colliderIndex < count; colliderIndex++)
            {
                Collider2D candidateCollider = projectileBuffer[colliderIndex];
                if (!TryResolveProjectile(
                        candidateCollider,
                        out MonoBehaviour behaviour,
                        out IEnemyParryableProjectile projectile))
                {
                    continue;
                }

                int instanceId = behaviour.GetInstanceID();
                if (!evaluatedProjectiles.Add(instanceId)
                    || !projectile.CanBeEnemyParried)
                {
                    continue;
                }

                Rigidbody2D projectileBody = behaviour.GetComponent<Rigidbody2D>();
                if (projectileBody == null || projectileBody.velocity.sqrMagnitude <= 0.01f)
                {
                    continue;
                }

                float score = ScoreThreat(behaviour, projectileBody.velocity, allies);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestBehaviour = behaviour;
                    bestProjectile = projectile;
                }
            }

            return bestProjectile != null;
        }

        private float ScoreThreat(
            MonoBehaviour projectileBehaviour,
            Vector2 velocity,
            Damageable[] allies)
        {
            Vector2 projectilePosition = projectileBehaviour.transform.position;
            Vector2 direction = velocity.normalized;
            Vector2 toDetective = (Vector2)transform.position - projectilePosition;
            float forwardToDetective = Vector2.Dot(toDetective, direction);
            float lateralToDetective = Mathf.Abs(Cross(toDetective, direction));
            bool directlyThreatensDetective = forwardToDetective >= 0f
                && lateralToDetective <= directThreatCorridor;

            int threatenedAllies = 0;
            bool threatensSupport = false;
            foreach (Damageable ally in allies)
            {
                if (ally == null
                    || ally == damageable
                    || ally.CurrentHealth <= 0
                    || !ally.gameObject.activeInHierarchy)
                {
                    continue;
                }

                DetectiveIdentity allyIdentity = ally.GetComponent<DetectiveIdentity>();
                if (allyIdentity != null && allyIdentity.IsClone)
                {
                    continue;
                }

                Vector2 toAlly = (Vector2)ally.transform.position - projectilePosition;
                float forward = Vector2.Dot(toAlly, direction);
                float lateral = Mathf.Abs(Cross(toAlly, direction));
                if (forward < -0.15f
                    || forward > formationPredictionDistance
                    || lateral > formationCorridor)
                {
                    continue;
                }

                threatenedAllies++;
                EnemyArchetypeProfile profile = ally.GetComponent<EnemyArchetypeProfile>();
                threatensSupport |= profile != null
                    && profile.Includes(EnemyArchetype.Support);
            }

            PlayerProjectile playerProjectile = projectileBehaviour as PlayerProjectile;
            bool isPiercing = playerProjectile != null && playerProjectile.IsPiercing;
            bool formationThreat = threatenedAllies >= formationThreatCount;
            if (!directlyThreatensDetective
                && !threatensSupport
                && !(isPiercing && formationThreat))
            {
                return float.NegativeInfinity;
            }

            float score = directlyThreatensDetective ? directThreatWeight : 0f;
            score += threatensSupport ? supportThreatWeight : 0f;
            score += threatenedAllies * formationThreatWeight;
            if (playerProjectile != null)
            {
                score += playerProjectile.IsPiercing ? piercingProjectileWeight : 0f;
                score += Mathf.Max(0, playerProjectile.SkillTier - 1) * projectileTierWeight;
            }

            score += Mathf.Clamp01(1f - toDetective.magnitude / interceptionRadius);
            return score;
        }

        private void ResolveRedirect()
        {
            isWindingUp = false;
            bool redirected = committedProjectileBehaviour != null
                && committedProjectile != null
                && committedProjectile.CanBeEnemyParried
                && committedProjectile.TryEnemyParry(gameObject);
            float recovery = redirected ? redirectRecovery : failedRedirectRecovery;
            recoveringUntil = Time.time + recovery;
            nextRedirectTime = Time.time + redirectCooldown;
            currentRedirectDecision = "Recovering";

            if (redirected)
            {
                Vector2 impactPosition = committedProjectileBehaviour != null
                    ? committedProjectileBehaviour.transform.position
                    : transform.position;
                CombatShapeEffect.Create(
                    impactPosition,
                    CombatShape.Chevron,
                    0.65f,
                    redirectTelegraphColor,
                    0.24f);
            }

            ClearCommittedProjectile();
        }

        public void Interrupt()
        {
            if (!isWindingUp)
            {
                return;
            }

            isWindingUp = false;
            recoveringUntil = Time.time + failedRedirectRecovery;
            nextRedirectTime = Mathf.Max(
                nextRedirectTime,
                Time.time + redirectCooldown * 0.5f);
            currentRedirectDecision = "Interrupted";
            ClearCommittedProjectile();
        }

        private bool HasIncompatibleAction()
        {
            return (poisonShooter != null && poisonShooter.IsBusy)
                || (grenade != null && grenade.IsBusy)
                || (suppressionZone != null && suppressionZone.IsBusy);
        }

        private static bool TryResolveProjectile(
            Collider2D candidateCollider,
            out MonoBehaviour behaviour,
            out IEnemyParryableProjectile projectile)
        {
            behaviour = null;
            projectile = null;
            if (candidateCollider == null)
            {
                return false;
            }

            foreach (MonoBehaviour candidate in
                candidateCollider.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (candidate is IEnemyParryableProjectile redirectable)
                {
                    behaviour = candidate;
                    projectile = redirectable;
                    return true;
                }
            }

            return false;
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private void RefreshDependencies()
        {
            damageable = GetComponent<Damageable>();
            stagger = GetComponent<EnemyStagger>();
            RefreshLateDependencies();
        }

        private void RefreshLateDependencies()
        {
            if (stagger == null)
            {
                stagger = GetComponent<EnemyStagger>();
            }

            poisonShooter = poisonShooter != null
                ? poisonShooter
                : GetComponent<EnemyPoisonShooter>();
            grenade = grenade != null
                ? grenade
                : GetComponent<DetectiveStunGrenadeAbility>();
            suppressionZone = suppressionZone != null
                ? suppressionZone
                : GetComponent<DetectiveSuppressionZoneAbility>();
        }

        private void ClearCommittedProjectile()
        {
            committedProjectileBehaviour = null;
            committedProjectile = null;
        }

        private void OnDisable()
        {
            isWindingUp = false;
            recoveringUntil = 0f;
            nextRedirectTime = 0f;
            currentRedirectDecision = "Ready";
            ClearCommittedProjectile();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawInterceptionRadius)
            {
                return;
            }

            Gizmos.color = redirectTelegraphColor;
            Gizmos.DrawWireSphere(transform.position, interceptionRadius);
        }
    }
}
