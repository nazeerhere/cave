using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class DetectiveSuppressionZoneAbility : MonoBehaviour, IEnemyInterruptible
    {
        [Header("Optional Prefab")]
        [SerializeField] private DetectiveSuppressionZone zonePrefab;

        [Header("Suppression Zone")]
        [SerializeField, Min(0.1f)] private float castRange = 8f;
        [SerializeField, Min(0.1f)] private float radius = 3.5f;
        [SerializeField, Min(0.1f)] private float activeDuration = 6f;
        [SerializeField, Range(0f, 1f)] private float healthRegenerationMultiplier = 0f;
        [SerializeField, Range(0f, 1f)] private float staminaRegenerationMultiplier = 0.35f;
        [SerializeField, Range(0f, 1f)] private float manaRegenerationMultiplier = 0.35f;
        [SerializeField, Min(1)] private int poisonDamagePerTick = 1;
        [SerializeField, Min(0.1f)] private float poisonTickInterval = 1.5f;
        [SerializeField, Min(0f)] private float cooldown = 9f;
        [SerializeField, Min(0f)] private float castWindup = 0.65f;
        [SerializeField] private Color zoneColor = new Color(0.45f, 0.2f, 0.8f, 0.88f);

        private EnemyStagger stagger;
        private Vector2 committedPosition;
        private float castCompletesAt;
        private float nextUseTime;
        private bool isCasting;

        public bool IsBusy => isCasting;
        public bool IsReady => !isCasting
            && Time.time >= nextUseTime
            && (stagger == null || stagger.CanAct);

        private void Awake()
        {
            stagger = GetComponent<EnemyStagger>();
        }

        private void Update()
        {
            if (isCasting && Time.time >= castCompletesAt)
            {
                CreateZone();
            }
        }

        public bool CanUse(PlayerHealth target)
        {
            return IsReady
                && target != null
                && target.gameObject.activeInHierarchy
                && ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude
                    <= castRange * castRange;
        }

        public bool TryUse(PlayerHealth target)
        {
            if (!CanUse(target))
            {
                return false;
            }

            committedPosition = target.transform.position;
            isCasting = true;
            castCompletesAt = Time.time + castWindup;
            CombatShapeEffect.Create(
                committedPosition,
                CombatShape.Ring,
                radius,
                zoneColor,
                Mathf.Max(0.15f, castWindup),
                0f,
                0.075f);
            return true;
        }

        private void CreateZone()
        {
            isCasting = false;
            nextUseTime = Time.time + cooldown;
            DetectiveSuppressionZone zone;
            if (zonePrefab != null)
            {
                zone = Instantiate(zonePrefab, committedPosition, Quaternion.identity);
            }
            else
            {
                GameObject zoneObject = new GameObject("Detective Suppression Zone");
                zoneObject.transform.position = committedPosition;
                zone = zoneObject.AddComponent<DetectiveSuppressionZone>();
            }

            zone.Initialize(
                gameObject,
                radius,
                activeDuration,
                healthRegenerationMultiplier,
                staminaRegenerationMultiplier,
                manaRegenerationMultiplier,
                poisonDamagePerTick,
                poisonTickInterval,
                zoneColor);
        }

        public void Interrupt()
        {
            if (!isCasting)
            {
                return;
            }

            isCasting = false;
            nextUseTime = Mathf.Max(nextUseTime, Time.time + cooldown * 0.35f);
        }

        private void OnDisable()
        {
            isCasting = false;
            nextUseTime = 0f;
        }
    }
}
