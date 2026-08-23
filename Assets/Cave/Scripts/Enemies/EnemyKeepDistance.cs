using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyKeepDistance : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float minimumRange = 4f;
        [SerializeField, Min(0f)] private float preferredRange = 6f;
        [SerializeField, Min(0f)] private float maximumRange = 9f;
        [SerializeField, Min(0f)] private float preferredRangeTolerance = 0.75f;
        [SerializeField, Min(0.02f)] private float decisionInterval = 0.1f;

        private EnemyController controller;
        private EnemyDefenseController defense;
        private EnemyStagger stagger;
        private float nextDecisionTime;
        private bool brainControlled;

        public float MinimumRange => minimumRange;
        public float PreferredRange => preferredRange;
        public float MaximumRange => maximumRange;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            defense = GetComponent<EnemyDefenseController>();
            stagger = GetComponent<EnemyStagger>();
        }

        private void Update()
        {
            if (brainControlled)
            {
                return;
            }

            if (Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            EnsureTarget();
            if (target == null || (stagger != null && !stagger.CanAct))
            {
                controller.SetCombatMovementIntent(0f, decisionInterval * 1.5f);
                return;
            }

            float horizontalDelta = target.position.x - transform.position.x;
            float distance = Mathf.Abs(horizontalDelta);
            float towardTarget = Mathf.Sign(horizontalDelta);
            defense?.FaceDirection(towardTarget);

            float intent;
            if (distance < minimumRange)
            {
                intent = -towardTarget;
            }
            else if (distance > maximumRange
                || distance > preferredRange + preferredRangeTolerance)
            {
                intent = towardTarget;
            }
            else
            {
                intent = 0f;
            }

            controller.SetCombatMovementIntent(intent, decisionInterval * 1.5f);
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        private void EnsureTarget()
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                return;
            }

            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            target = player != null ? player.transform : null;
        }

        private void OnValidate()
        {
            preferredRange = Mathf.Max(minimumRange, preferredRange);
            maximumRange = Mathf.Max(preferredRange, maximumRange);
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.SetCombatMovementIntent(0f, 0f);
            }
        }
    }
}
