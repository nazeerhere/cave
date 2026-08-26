using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController), typeof(Damageable))]
    public sealed class DetectiveCloneMimic : MonoBehaviour
    {
        [Header("Deception Pressure")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.65f;
        [SerializeField, Min(0.1f)] private float minimumDistance = 3.4f;
        [SerializeField, Min(0.1f)] private float preferredDistance = 5f;
        [SerializeField, Min(0.1f)] private float maximumDistance = 7f;
        [SerializeField, Min(0f)] private float distanceTolerance = 0.55f;
        [SerializeField, Min(0f)] private float fakeActionInterval = 3.5f;
        [SerializeField] private Color fakeActionColor = new Color(0.3f, 0.85f, 1f, 0.5f);

        [Header("Three-Detective Sacrifice")]
        [SerializeField, Min(1)] private int sacrificialRealDetectiveCount = 3;
        [SerializeField, Min(0f)] private float baitApproachSpeed = 2.9f;
        [SerializeField, Min(0f)] private float minimumSacrificeDelay = 1.2f;
        [SerializeField, Min(0f)] private float maximumSacrificeDelay = 3.8f;

        private EnemyController movement;
        private Damageable damageable;
        private EnemyAllyCollisionPhasing allyPhasing;
        private DetectiveIdentity identity;
        private DetectiveEncounterCoordinator coordinator;
        private EnemyPoisonShooter poisonShooter;
        private DetectiveSacrificialAttack sacrificialAttack;
        private PlayerHealth player;
        private float nextFakeActionTime;
        private float sacrificeEligibleAt;
        private bool wasInSacrificialPopulation;
        private float idleStrafeDirection;
        private float nextStrafeChangeTime;

        private void Awake()
        {
            movement = GetComponent<EnemyController>();
            damageable = GetComponent<Damageable>();
            damageable.SetRuntimeMaximumHealth(1, true);
            damageable.Died += Dissolve;
            identity = GetComponent<DetectiveIdentity>();
            coordinator = identity != null ? identity.Coordinator : null;
            allyPhasing = GetComponent<EnemyAllyCollisionPhasing>();
            if (allyPhasing == null)
            {
                allyPhasing = gameObject.AddComponent<EnemyAllyCollisionPhasing>();
            }

            poisonShooter = GetComponent<EnemyPoisonShooter>();
            if (poisonShooter == null)
            {
                poisonShooter = gameObject.AddComponent<EnemyPoisonShooter>();
            }

            poisonShooter.SetBrainControlled(true);
            sacrificialAttack = GetComponent<DetectiveSacrificialAttack>();
            if (sacrificialAttack == null)
            {
                sacrificialAttack = gameObject.AddComponent<DetectiveSacrificialAttack>();
            }

            allyPhasing.SetAllyPhasing(false);
            nextFakeActionTime = Time.time + Random.Range(1.5f, fakeActionInterval);
            nextStrafeChangeTime = Time.time + Random.Range(0.6f, 1.4f);
            idleStrafeDirection = Random.value < 0.5f ? -1f : 1f;
        }

        private void Update()
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                player = FindObjectOfType<PlayerHealth>();
            }

            if (player == null)
            {
                allyPhasing.SetAllyPhasing(false);
                movement.SetCombatMovementIntent(0f, 0.2f);
                return;
            }

            if (sacrificialAttack != null && sacrificialAttack.IsCommitted)
            {
                allyPhasing.SetAllyPhasing(false);
                movement.SetBrainMoveSpeed(0f);
                movement.SetCombatMovementIntent(0f, 0.25f);
                return;
            }

            if (coordinator == null)
            {
                coordinator = identity != null
                    ? identity.Coordinator
                    : DetectiveEncounterCoordinator.GetOrCreate();
            }

            int realDetectiveCount = coordinator != null
                ? coordinator.RealDetectiveCount
                : 0;
            bool usesSacrificialBait = realDetectiveCount >= sacrificialRealDetectiveCount;
            if (usesSacrificialBait && !wasInSacrificialPopulation)
            {
                float minimumDelay = Mathf.Min(minimumSacrificeDelay, maximumSacrificeDelay);
                float maximumDelay = Mathf.Max(minimumSacrificeDelay, maximumSacrificeDelay);
                sacrificeEligibleAt = Time.time + Random.Range(minimumDelay, maximumDelay);
            }

            wasInSacrificialPopulation = usesSacrificialBait;
            if (usesSacrificialBait && Time.time >= sacrificeEligibleAt)
            {
                RunSacrificialBait();
            }
            else
            {
                RunDetectivePressure();
            }

            if (Time.time >= nextFakeActionTime)
            {
                nextFakeActionTime = Time.time + fakeActionInterval + Random.Range(-0.5f, 0.5f);
                CombatShapeEffect.Create(
                    transform.position + Vector3.up * 0.35f,
                    Random.value < 0.5f ? CombatShape.Diamond : CombatShape.Ring,
                    0.55f,
                    fakeActionColor,
                    0.3f);
            }
        }

        private void RunDetectivePressure()
        {
            float delta = player.transform.position.x - transform.position.x;
            float distance = Mathf.Abs(delta);
            float towardPlayer = ResolveHorizontalDirection(delta);
            movement.SetBrainMoveSpeed(moveSpeed);
            if (distance < minimumDistance - distanceTolerance)
            {
                allyPhasing.SetAllyPhasing(true);
                movement.SetCombatMovementIntent(-towardPlayer, 0.25f);
                return;
            }

            allyPhasing.SetAllyPhasing(false);
            if (distance > maximumDistance + distanceTolerance)
            {
                movement.SetCombatMovementIntent(towardPlayer, 0.25f);
                return;
            }

            if (poisonShooter != null && poisonShooter.TryUse(player.transform))
            {
                movement.SetBrainMoveSpeed(0f);
                movement.SetCombatMovementIntent(0f, 0.25f);
                return;
            }

            if (distance < preferredDistance - distanceTolerance)
            {
                allyPhasing.SetAllyPhasing(true);
                movement.SetCombatMovementIntent(-towardPlayer, 0.25f);
                return;
            }

            if (distance > preferredDistance + distanceTolerance)
            {
                movement.SetCombatMovementIntent(towardPlayer, 0.25f);
                return;
            }

            if (Time.time >= nextStrafeChangeTime)
            {
                idleStrafeDirection *= -1f;
                nextStrafeChangeTime = Time.time + Random.Range(0.65f, 1.35f);
            }

            movement.SetBrainMoveSpeed(moveSpeed * 0.55f);
            movement.SetCombatMovementIntent(idleStrafeDirection, 0.25f);
        }

        private void RunSacrificialBait()
        {
            float delta = player.transform.position.x - transform.position.x;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (sacrificialAttack != null && sacrificialAttack.TryCommit(player))
            {
                allyPhasing.SetAllyPhasing(false);
                movement.SetBrainMoveSpeed(0f);
                movement.SetCombatMovementIntent(0f, 0.25f);
                return;
            }

            allyPhasing.SetAllyPhasing(true);
            movement.SetBrainMoveSpeed(baitApproachSpeed);
            movement.SetCombatMovementIntent(
                distance > 0.05f ? ResolveHorizontalDirection(delta) : 0f,
                0.25f);
        }

        private float ResolveHorizontalDirection(float delta)
        {
            if (Mathf.Abs(delta) > 0.05f)
            {
                return Mathf.Sign(delta);
            }

            return GetInstanceID() % 2 == 0 ? -1f : 1f;
        }

        private void Dissolve()
        {
            CombatShapeEffect.Create(
                transform.position + Vector3.up * 0.35f,
                CombatShape.Diamond,
                0.75f,
                new Color(0.25f, 0.8f, 1f, 0.65f),
                0.35f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (damageable != null)
            {
                damageable.Died -= Dissolve;
            }

            allyPhasing?.SetAllyPhasing(false);
            poisonShooter?.SetBrainControlled(false);
        }
    }
}
