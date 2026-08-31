using System.Collections;
using Cave.Combat;
using Cave.Enemies;
using Cave.World;
using UnityEngine;

namespace Cave.Player
{
    public enum CrossStepState
    {
        None,
        Available,
        Crossing,
        Rejected,
        Interrupted,
        Completed
    }

    public enum CrossStepOpportunitySource
    {
        None,
        Heavy,
        GuardBreak
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PlayerController))]
    public sealed class PlayerCrossStep : MonoBehaviour
    {
        [Header("Cross Step")]
        [SerializeField, Min(0.05f)] private float followUpWindow = 0.55f;
        [SerializeField, Min(0.1f)] private float movementSpeed = 22f;
        [SerializeField, Min(0f)] private float extraClearance = 0.6f;
        [SerializeField, Min(0f)] private float blockPushbackDistance = 0.5f;
        [SerializeField, Min(0f)] private float blockRecovery = 0.2f;
        [SerializeField, Min(0.01f)] private float destinationSkin = 0.05f;

        [Header("Optional VFX")]
        [SerializeField] private GameObject crossStepStartVfx;
        [SerializeField] private GameObject crossStepPassVfx;
        [SerializeField] private GameObject crossStepRejectedVfx;
        [SerializeField, Min(0.05f)] private float spawnedVfxLifetime = 1.25f;

        [Header("Current State (Read Only)")]
        [SerializeField] private bool opportunityAvailable;
        [SerializeField] private bool isCrossStepping;
        [SerializeField] private Damageable opportunityTarget;
        [SerializeField] private Damageable activeTarget;
        [SerializeField] private CrossStepOpportunitySource opportunitySource;
        [SerializeField] private CrossStepState currentState;

        private readonly RaycastHit2D[] castResults = new RaycastHit2D[16];
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private PlayerController controller;
        private ChargedAttack chargedAttack;
        private PlayerGuardBreak guardBreak;
        private PlayerAimDirection aimDirection;
        private PlayerDash playerDash;
        private PlayerHealth playerHealth;
        private PlayerRespawn playerRespawn;
        private CommittedAttackCollisionPhasing collisionPhasing;
        private Coroutine movementRoutine;
        private float opportunityExpiresAt;
        private float transientStateEndsAt;
        private int heavyAttackSequence = -1;
        private Vector2 crossingStartPosition;
        private Vector2 rejectionPosition;
        private float crossingDirection;

        public bool IsCrossStepping => isCrossStepping;
        public CrossStepState CurrentState => currentState;
        public Damageable CurrentTarget => isCrossStepping ? activeTarget : opportunityTarget;
        public bool CanStartDuringCurrentActionLock => opportunityAvailable
            && opportunitySource == CrossStepOpportunitySource.GuardBreak
            && guardBreak != null
            && guardBreak.CanReleaseSuccessfulGuardBreakForCrossStep;

        public void Configure(
            float requestedFollowUpWindow,
            float requestedMovementSpeed,
            float requestedExtraClearance,
            float requestedBlockPushback,
            float requestedBlockRecovery)
        {
            followUpWindow = Mathf.Max(0.05f, requestedFollowUpWindow);
            movementSpeed = Mathf.Max(0.1f, requestedMovementSpeed);
            extraClearance = Mathf.Max(0f, requestedExtraClearance);
            blockPushbackDistance = Mathf.Max(0f, requestedBlockPushback);
            blockRecovery = Mathf.Max(0f, requestedBlockRecovery);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            controller = GetComponent<PlayerController>();
            chargedAttack = GetComponent<ChargedAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            aimDirection = GetComponent<PlayerAimDirection>();
            playerDash = GetComponent<PlayerDash>();
            playerHealth = GetComponent<PlayerHealth>();
            playerRespawn = GetComponent<PlayerRespawn>();
            collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
            if (collisionPhasing == null)
            {
                collisionPhasing = gameObject.AddComponent<CommittedAttackCollisionPhasing>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void Update()
        {
            if (opportunityAvailable
                && (Time.time > opportunityExpiresAt || !IsValidTarget(opportunityTarget)))
            {
                ClearOpportunity();
            }

            if (!opportunityAvailable
                && !isCrossStepping
                && currentState != CrossStepState.None
                && Time.time >= transientStateEndsAt)
            {
                currentState = CrossStepState.None;
            }
        }

        public bool TryStart()
        {
            if (isCrossStepping
                || !opportunityAvailable
                || Time.time > opportunityExpiresAt
                || !IsValidTarget(opportunityTarget))
            {
                ClearOpportunity();
                return false;
            }

            Damageable target = opportunityTarget;
            CrossStepOpportunitySource source = opportunitySource;
            if (source == CrossStepOpportunitySource.GuardBreak
                && guardBreak != null
                && guardBreak.IsGuardBreaking
                && !guardBreak.TryReleaseSuccessfulGuardBreakForCrossStep())
            {
                return false;
            }

            ClearOpportunity();
            Transform targetRoot = ResolveCharacterRoot(target);
            if (IsActivelyBlocking(target))
            {
                currentState = CrossStepState.Rejected;
                transientStateEndsAt = Time.time + blockRecovery;
                RejectFromBlock(targetRoot.position);
                return true;
            }

            Collider2D targetBody = FindBodyCollider(targetRoot);
            if (targetBody == null)
            {
                return false;
            }

            float direction = Mathf.Sign(targetBody.bounds.center.x - transform.position.x);
            if (Mathf.Approximately(direction, 0f))
            {
                direction = 1f;
            }

            float playerHalfWidth = bodyCollider.bounds.extents.x;
            float targetHalfWidth = targetBody.bounds.extents.x;
            crossingStartPosition = body.position;
            crossingDirection = direction;
            rejectionPosition = new Vector2(
                targetBody.bounds.center.x
                    - direction * (targetHalfWidth + playerHalfWidth + destinationSkin),
                body.position.y);
            float desiredX = targetBody.bounds.center.x
                + direction * (targetHalfWidth + playerHalfWidth + extraClearance);
            float requestedDistance = Mathf.Abs(desiredX - body.position.x);
            // Set the active target before the safety cast. The cast must ignore
            // only this earned target, while every other character/world body
            // remains a valid obstruction.
            activeTarget = target;
            collisionPhasing.BeginTargetOnly(bodyCollider, targetRoot, Vector2.right * direction);
            float safeDistance = ResolveSafeTravelDistance(direction, requestedDistance);
            float minimumClearDistance = Mathf.Abs(
                targetBody.bounds.center.x
                + direction * (targetHalfWidth + playerHalfWidth + destinationSkin)
                - body.position.x);
            if (safeDistance + 0.001f < minimumClearDistance)
            {
                collisionPhasing.End();
                activeTarget = null;
                currentState = CrossStepState.Interrupted;
                transientStateEndsAt = Time.time + 0.15f;
                return false;
            }

            Vector2 destination = body.position + Vector2.right * direction * safeDistance;
            if (!IsSafeDestination(destination))
            {
                collisionPhasing.End();
                activeTarget = null;
                currentState = CrossStepState.Interrupted;
                transientStateEndsAt = Time.time + 0.15f;
                return false;
            }

            currentState = CrossStepState.Crossing;
            SpawnOptionalVfx(crossStepStartVfx, transform.position);
            movementRoutine = StartCoroutine(MoveAcross(destination, direction));
            return true;
        }

        public void InterruptForEnemyGuardBreak()
        {
            if (!isCrossStepping)
            {
                return;
            }

            InterruptMovement(CrossStepState.Interrupted);
        }

        private IEnumerator MoveAcross(Vector2 destination, float direction)
        {
            isCrossStepping = true;
            float distance = Mathf.Abs(destination.x - body.position.x);
            float duration = distance / Mathf.Max(0.1f, movementSpeed);
            float endsAt = Time.time + duration;
            controller.ApplyExternalControlLock(duration);

            while (Time.time < endsAt)
            {
                if (!IsValidTarget(activeTarget))
                {
                    movementRoutine = null;
                    FinishMovement(CrossStepState.Interrupted, false);
                    yield break;
                }

                if (IsActivelyBlocking(activeTarget))
                {
                    movementRoutine = null;
                    RejectDuringCrossing();
                    yield break;
                }

                body.velocity = new Vector2(direction * movementSpeed, body.velocity.y);
                if ((direction > 0f && body.position.x >= destination.x)
                    || (direction < 0f && body.position.x <= destination.x))
                {
                    break;
                }

                yield return new WaitForFixedUpdate();
            }

            if (!IsValidTarget(activeTarget))
            {
                movementRoutine = null;
                FinishMovement(CrossStepState.Interrupted, false);
                yield break;
            }

            if (IsActivelyBlocking(activeTarget))
            {
                movementRoutine = null;
                RejectDuringCrossing();
                yield break;
            }

            body.position = new Vector2(destination.x, body.position.y);
            movementRoutine = null;
            Physics2D.SyncTransforms();
            SpawnOptionalVfx(crossStepPassVfx, body.position);
            FaceBackTowardTarget(direction);
            FinishMovement(CrossStepState.Completed, true);
        }

        private float ResolveSafeTravelDistance(float direction, float requestedDistance)
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
            int count = bodyCollider.Cast(
                Vector2.right * direction,
                filter,
                castResults,
                requestedDistance);
            float safeDistance = requestedDistance;
            for (int index = 0; index < count; index++)
            {
                Collider2D hit = castResults[index].collider;
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (IsTargetBody(hit, activeTarget))
                {
                    continue;
                }

                if (IsSupportingSurface(castResults[index], direction))
                {
                    continue;
                }

                safeDistance = Mathf.Min(
                    safeDistance,
                    Mathf.Max(0f, castResults[index].distance - destinationSkin));
            }

            return safeDistance;
        }

        private static bool IsSupportingSurface(RaycastHit2D hit, float horizontalDirection)
        {
            if (hit.collider == null || hit.distance > 0.02f)
            {
                return false;
            }

            return hit.normal.y > 0.5f
                && Mathf.Abs(hit.normal.y) > Mathf.Abs(hit.normal.x)
                && Mathf.Abs(horizontalDirection) > 0f;
        }

        private bool IsSafeDestination(Vector2 destination)
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 centerOffset = (Vector2)bounds.center - body.position;
            Vector2 checkCenter = destination + centerOffset;
            Vector2 checkSize = new Vector2(
                Mathf.Max(0.02f, bounds.size.x - destinationSkin * 2f),
                Mathf.Max(0.02f, bounds.size.y - destinationSkin * 2f));
            foreach (Collider2D overlap in Physics2D.OverlapBoxAll(
                checkCenter,
                checkSize,
                0f,
                Physics2D.AllLayers))
            {
                if (overlap == null
                    || overlap == bodyCollider
                    || overlap.transform.IsChildOf(transform)
                    || IsTargetBody(overlap, activeTarget))
                {
                    continue;
                }

                if (overlap.GetComponentInParent<DeathBoundary>() != null)
                {
                    return false;
                }

                if (!overlap.isTrigger)
                {
                    if (IsSupportingColliderAtDestination(overlap, checkCenter, checkSize))
                    {
                        continue;
                    }

                    return false;
                }
            }

            return true;
        }

        private bool IsSupportingColliderAtDestination(
            Collider2D candidate,
            Vector2 checkCenter,
            Vector2 checkSize)
        {
            float feetY = checkCenter.y - checkSize.y * 0.5f;
            return candidate != null
                && controller != null
                && controller.IsValidGroundCollider(candidate)
                && candidate.bounds.max.y <= feetY + destinationSkin * 2f;
        }

        private void RejectFromBlock(Vector2 targetPosition)
        {
            float away = Mathf.Sign(transform.position.x - targetPosition.x);
            if (Mathf.Approximately(away, 0f))
            {
                away = -1f;
            }

            float speed = blockRecovery > 0f
                ? blockPushbackDistance / blockRecovery
                : 0f;
            guardBreak?.ApplyExternalInterruption(blockRecovery);
            controller.ApplyExternalKnockback(
                new Vector2(away * speed, 0f),
                blockRecovery);
            SpawnOptionalVfx(crossStepRejectedVfx, transform.position);
            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Hexagon,
                0.45f,
                new Color(0.35f, 0.8f, 1f, 0.85f),
                Mathf.Max(0.12f, blockRecovery));
        }

        private void RejectDuringCrossing()
        {
            Vector2 safeFront = IsSafeDestination(rejectionPosition)
                ? rejectionPosition
                : crossingStartPosition;
            body.position = safeFront;
            Physics2D.SyncTransforms();
            collisionPhasing?.End();
            isCrossStepping = false;
            activeTarget = null;
            currentState = CrossStepState.Rejected;
            transientStateEndsAt = Time.time + blockRecovery;
            RejectFromBlock(safeFront + Vector2.right * crossingDirection);
        }

        private static bool IsActivelyBlocking(Damageable target)
        {
            EnemyDefenseController defense = target != null
                ? target.GetComponentInParent<EnemyDefenseController>()
                : null;
            return defense != null && defense.IsActivelyBlocking;
        }

        private void OpenHeavyOpportunity(Damageable target)
        {
            if (!IsValidTarget(target))
            {
                return;
            }

            int sequence = chargedAttack != null ? chargedAttack.CurrentAttackSequence : -1;
            if (opportunityAvailable
                && opportunitySource == CrossStepOpportunitySource.Heavy
                && heavyAttackSequence == sequence
                && IsValidTarget(opportunityTarget)
                && Vector2.SqrMagnitude(opportunityTarget.transform.position - transform.position)
                    <= Vector2.SqrMagnitude(target.transform.position - transform.position))
            {
                opportunityExpiresAt = Time.time + followUpWindow;
                return;
            }

            heavyAttackSequence = sequence;
            SetOpportunity(target, CrossStepOpportunitySource.Heavy);
        }

        private void OpenGuardBreakOpportunity(Damageable target)
        {
            if (IsValidTarget(target))
            {
                SetOpportunity(target, CrossStepOpportunitySource.GuardBreak);
            }
        }

        private void SetOpportunity(
            Damageable target,
            CrossStepOpportunitySource source)
        {
            if (isCrossStepping)
            {
                return;
            }

            opportunityTarget = target;
            opportunityAvailable = true;
            opportunityExpiresAt = Time.time + followUpWindow;
            opportunitySource = source;
            currentState = CrossStepState.Available;
        }

        private static Collider2D FindBodyCollider(Transform target)
        {
            Collider2D best = null;
            float bestArea = 0f;
            foreach (Collider2D candidate in target.GetComponentsInChildren<Collider2D>(true))
            {
                if (candidate == null || candidate.isTrigger || !candidate.enabled)
                {
                    continue;
                }

                float area = candidate.bounds.size.x * candidate.bounds.size.y;
                if (best == null || area > bestArea)
                {
                    best = candidate;
                    bestArea = area;
                }
            }

            return best;
        }

        private static bool IsTargetBody(Collider2D candidate, Damageable target)
        {
            if (candidate == null || target == null)
            {
                return false;
            }

            Transform root = ResolveCharacterRoot(target);
            return candidate.transform == root || candidate.transform.IsChildOf(root);
        }

        private static bool IsValidTarget(Damageable target)
        {
            return target != null
                && target.CurrentHealth > 0
                && target.gameObject.activeInHierarchy
                && (target.GetComponentInParent<EnemyController>() != null
                    || target.GetComponentInParent<EnemyArchetypeProfile>() != null
                    || target.GetComponentInParent<FlyingSwarmController>() != null);
        }

        private static Transform ResolveCharacterRoot(Damageable target)
        {
            EnemyController controller = target.GetComponentInParent<EnemyController>();
            if (controller != null)
            {
                return controller.transform;
            }

            EnemyArchetypeProfile profile = target.GetComponentInParent<EnemyArchetypeProfile>();
            if (profile != null)
            {
                return profile.transform;
            }

            FlyingSwarmController flying = target.GetComponentInParent<FlyingSwarmController>();
            return flying != null ? flying.transform : target.transform;
        }

        private void InterruptMovement(CrossStepState result)
        {
            if (movementRoutine != null)
            {
                StopCoroutine(movementRoutine);
                movementRoutine = null;
            }

            FinishMovement(result, true);
        }

        private void FinishMovement(CrossStepState result, bool stopHorizontalVelocity)
        {
            isCrossStepping = false;
            collisionPhasing?.End();
            if (stopHorizontalVelocity && body != null)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
            }

            activeTarget = null;
            currentState = result;
            transientStateEndsAt = result == CrossStepState.Rejected
                ? Time.time + blockRecovery
                : Time.time + 0.15f;
        }

        private void FaceBackTowardTarget(float movementDirection)
        {
            float facing = -Mathf.Sign(movementDirection);
            aimDirection?.SetFacingDirection(facing);
            playerDash?.SetFacingDirection(facing);
        }

        private void SpawnOptionalVfx(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
            Destroy(spawned, spawnedVfxLifetime);
        }

        private void HandleDamageTaken()
        {
            if (isCrossStepping)
            {
                InterruptMovement(CrossStepState.Interrupted);
            }
        }

        private void HandleRespawned()
        {
            ClearOpportunity();
            if (isCrossStepping || activeTarget != null)
            {
                InterruptMovement(CrossStepState.Interrupted);
            }
        }

        private void ClearOpportunity()
        {
            opportunityAvailable = false;
            opportunityTarget = null;
            opportunityExpiresAt = 0f;
            opportunitySource = CrossStepOpportunitySource.None;
            heavyAttackSequence = -1;
            if (!isCrossStepping && currentState == CrossStepState.Available)
            {
                currentState = CrossStepState.None;
            }
        }

        private void Subscribe()
        {
            if (chargedAttack == null)
            {
                chargedAttack = GetComponent<ChargedAttack>();
            }

            if (guardBreak == null)
            {
                guardBreak = GetComponent<PlayerGuardBreak>();
            }

            if (chargedAttack != null)
            {
                chargedAttack.HeavyTargetHit -= OpenHeavyOpportunity;
                chargedAttack.HeavyTargetHit += OpenHeavyOpportunity;
            }

            if (guardBreak != null)
            {
                guardBreak.OffensiveGuardBreakTargetSucceeded -= OpenGuardBreakOpportunity;
                guardBreak.OffensiveGuardBreakTargetSucceeded += OpenGuardBreakOpportunity;
            }

            if (playerHealth != null)
            {
                playerHealth.DamageTaken -= HandleDamageTaken;
                playerHealth.DamageTaken += HandleDamageTaken;
                playerHealth.Died -= HandleRespawned;
                playerHealth.Died += HandleRespawned;
            }

            if (playerRespawn != null)
            {
                playerRespawn.Respawned -= HandleRespawned;
                playerRespawn.Respawned += HandleRespawned;
            }
        }

        private void OnDisable()
        {
            if (chargedAttack != null)
            {
                chargedAttack.HeavyTargetHit -= OpenHeavyOpportunity;
            }

            if (guardBreak != null)
            {
                guardBreak.OffensiveGuardBreakTargetSucceeded -= OpenGuardBreakOpportunity;
            }

            if (playerHealth != null)
            {
                playerHealth.DamageTaken -= HandleDamageTaken;
                playerHealth.Died -= HandleRespawned;
            }

            if (playerRespawn != null)
            {
                playerRespawn.Respawned -= HandleRespawned;
            }

            ClearOpportunity();
            InterruptMovement(CrossStepState.None);
        }

        private void OnDestroy()
        {
            collisionPhasing?.End();
        }
    }
}
