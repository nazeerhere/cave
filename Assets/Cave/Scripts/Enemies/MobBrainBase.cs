using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum MobBrainState
    {
        Patrol,
        Alert,
        Chase,
        Reposition,
        Attack,
        Defend,
        Recover,
        ReturnToPatrol
    }

    public abstract class MobBrainBase : MonoBehaviour
    {
        private const float TargetSearchInterval = 1f;

        [Header("Patrol")]
        [SerializeField, Min(0f)] private float patrolRadius = 2.5f;
        [SerializeField, Min(0f)] private float patrolSpeed = 1.5f;
        [SerializeField, Min(0f)] private float patrolPause = 0.8f;
        [SerializeField, Min(0.05f)] private float patrolArrivalTolerance = 0.2f;

        [Header("Awareness")]
        [SerializeField, Min(0.1f)] private float detectionRange = 8f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 12f;
        [SerializeField, Min(0f)] private float returnToPatrolDelay = 2f;
        [SerializeField, Min(0.1f)] private float combatLeashRadius = 12f;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool drawDetectionRange = true;
        [SerializeField] private bool drawPatrolRadius = true;
        [SerializeField] private bool showCurrentState = true;
        [SerializeField] private MobBrainState currentState = MobBrainState.Patrol;
        [SerializeField] private string currentDecision = "Patrol";

        private EnemyController movement;
        private Damageable damageable;
        private EnemyStagger stagger;
        private EnemyDefenseController defense;
        private EnemyContactDamage[] contactDamageSources;
        private PlayerHealth target;
        private Vector2 spawnPosition;
        private float patrolTargetX;
        private float pauseUntil;
        private float nextDecisionTime;
        private float nextTargetSearchTime;
        private float outsideLoseRangeSince = -1f;
        private bool engaged;

        public MobBrainState CurrentState => currentState;
        public string CurrentDecision => currentDecision;
        protected EnemyController Movement => movement;
        protected EnemyStagger Stagger => stagger;
        protected EnemyDefenseController Defense => defense;
        protected Vector2 SpawnPosition => spawnPosition;
        protected float DecisionInterval => decisionInterval;
        protected PlayerHealth Target => target;

        protected virtual void Awake()
        {
            movement = GetComponent<EnemyController>();
            damageable = GetComponent<Damageable>();
            stagger = GetComponent<EnemyStagger>();
            defense = GetComponent<EnemyDefenseController>();
            contactDamageSources = GetComponentsInChildren<EnemyContactDamage>(true);
            SetContactDamageBrainControl(true);
            spawnPosition = transform.position;
            patrolTargetX = spawnPosition.x;
            ConfigureCapabilities();
            SetCapabilityBrainControl(true);
        }

        protected virtual void OnEnable()
        {
            SetContactDamageBrainControl(true);
            SetCapabilityBrainControl(true);
            spawnPosition = transform.position;
            patrolTargetX = spawnPosition.x;
            pauseUntil = 0f;
            nextDecisionTime = 0f;
            nextTargetSearchTime = 0f;
            outsideLoseRangeSince = -1f;
            engaged = false;
            SetState(MobBrainState.Patrol, "Patrol");
        }

        private void Update()
        {
            if (Time.time < nextDecisionTime || movement == null)
            {
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            if (damageable != null && damageable.CurrentHealth <= 0)
            {
                HoldPosition(MobBrainState.Recover, "Inactive");
                return;
            }

            if (stagger != null && !stagger.CanAct)
            {
                HoldPosition(MobBrainState.Recover, "Staggered");
                return;
            }

            if (defense != null
                && (defense.CurrentState == EnemyDefenseState.Blocking
                    || defense.CurrentState == EnemyDefenseState.Parrying
                    || defense.CurrentState == EnemyDefenseState.Recovering))
            {
                HoldPosition(MobBrainState.Defend, defense.CurrentState.ToString());
                return;
            }

            if (IsCapabilityBusy())
            {
                HoldPosition(MobBrainState.Recover, BusyDecisionLabel);
                return;
            }

            RefreshTarget();
            float targetDistance = target != null
                ? Vector2.Distance(transform.position, target.transform.position)
                : float.PositiveInfinity;
            if (!engaged && targetDistance <= detectionRange)
            {
                engaged = true;
                outsideLoseRangeSince = -1f;
                SetState(MobBrainState.Alert, "Player detected");
            }

            if (engaged)
            {
                if (target == null || targetDistance > loseTargetRange)
                {
                    if (outsideLoseRangeSince < 0f)
                    {
                        outsideLoseRangeSince = Time.time;
                    }

                    if (Time.time - outsideLoseRangeSince >= returnToPatrolDelay)
                    {
                        engaged = false;
                        outsideLoseRangeSince = -1f;
                    }
                }
                else
                {
                    outsideLoseRangeSince = -1f;
                }
            }

            if (engaged && target != null)
            {
                Vector2 toTarget = target.transform.position - transform.position;
                defense?.FaceDirection(toTarget.x);
                EvaluateCombat(target, toTarget);
                return;
            }

            if (Mathf.Abs(transform.position.x - spawnPosition.x) > patrolRadius + patrolArrivalTolerance)
            {
                ReturnToPatrol();
            }
            else
            {
                Patrol();
            }
        }

        protected abstract void ConfigureCapabilities();
        protected virtual void SetCapabilityBrainControl(bool controlled)
        {
        }

        protected abstract void EvaluateCombat(PlayerHealth player, Vector2 toPlayer);
        protected abstract bool IsCapabilityBusy();
        protected virtual string BusyDecisionLabel => "Committed action";

        protected void Move(float horizontalDirection, float speed, MobBrainState state, string decision)
        {
            float direction = Mathf.Clamp(horizontalDirection, -1f, 1f);
            float offsetFromSpawn = transform.position.x - spawnPosition.x;
            if ((offsetFromSpawn >= combatLeashRadius && direction > 0f)
                || (offsetFromSpawn <= -combatLeashRadius && direction < 0f))
            {
                direction = -Mathf.Sign(offsetFromSpawn);
                decision = "Respect combat leash";
            }

            movement.SetBrainMoveSpeed(speed);
            movement.SetCombatMovementIntent(direction, decisionInterval * 1.75f);
            SetState(state, decision);
        }

        protected void HoldPosition(MobBrainState state, string decision)
        {
            movement.SetBrainMoveSpeed(0f);
            movement.SetCombatMovementIntent(0f, decisionInterval * 1.75f);
            SetState(state, decision);
        }

        protected void SetState(MobBrainState state, string decision)
        {
            currentState = state;
            currentDecision = showCurrentState ? decision : string.Empty;
        }

        private void Patrol()
        {
            if (Time.time < pauseUntil || patrolRadius <= patrolArrivalTolerance)
            {
                HoldPosition(MobBrainState.Patrol, "Patrol pause");
                return;
            }

            if (Mathf.Abs(transform.position.x - patrolTargetX) <= patrolArrivalTolerance)
            {
                pauseUntil = Time.time + patrolPause;
                patrolTargetX = spawnPosition.x + Random.Range(-patrolRadius, patrolRadius);
                HoldPosition(MobBrainState.Patrol, "Choose patrol point");
                return;
            }

            Move(
                Mathf.Sign(patrolTargetX - transform.position.x),
                patrolSpeed,
                MobBrainState.Patrol,
                "Move to patrol point");
        }

        private void ReturnToPatrol()
        {
            float delta = spawnPosition.x - transform.position.x;
            if (Mathf.Abs(delta) <= patrolArrivalTolerance)
            {
                patrolTargetX = spawnPosition.x;
                pauseUntil = Time.time + patrolPause;
                HoldPosition(MobBrainState.Patrol, "Patrol resumed");
                return;
            }

            Move(
                Mathf.Sign(delta),
                patrolSpeed,
                MobBrainState.ReturnToPatrol,
                "Return to spawn area");
        }

        private void RefreshTarget()
        {
            if (target != null
                && target.gameObject.activeInHierarchy
                && target.CurrentHealth > 0)
            {
                return;
            }

            target = null;
            if (Time.time < nextTargetSearchTime)
            {
                return;
            }

            nextTargetSearchTime = Time.time + TargetSearchInterval;
            PlayerHealth candidate = FindObjectOfType<PlayerHealth>();
            target = candidate != null && candidate.CurrentHealth > 0
                ? candidate
                : null;
        }

        protected virtual void OnDisable()
        {
            movement?.ClearBrainMovement();
            SetContactDamageBrainControl(false);
            SetCapabilityBrainControl(false);
        }

        private void SetContactDamageBrainControl(bool controlled)
        {
            if (contactDamageSources == null)
            {
                return;
            }

            foreach (EnemyContactDamage contactDamage in contactDamageSources)
            {
                contactDamage?.SetBrainControlled(controlled);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
            if (drawPatrolRadius)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(center, patrolRadius);
            }

            if (drawDetectionRange)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, detectionRange);
                Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.75f);
                Gizmos.DrawWireSphere(transform.position, loseTargetRange);
            }
        }

        protected virtual void OnValidate()
        {
            loseTargetRange = Mathf.Max(detectionRange, loseTargetRange);
            combatLeashRadius = Mathf.Max(patrolRadius, combatLeashRadius);
        }
    }
}
