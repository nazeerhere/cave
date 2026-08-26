using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class DetectiveStunGrenadeAbility : MonoBehaviour, IEnemyInterruptible
    {
        [Header("References")]
        [SerializeField] private Transform throwPoint;
        [SerializeField] private DetectiveStunGrenadeProjectile grenadePrefab;

        [Header("Grenade")]
        [SerializeField, Min(0.1f)] private float maximumThrowRange = 8f;
        [SerializeField, Min(0.1f)] private float throwSpeed = 7f;
        [SerializeField, Min(0.1f)] private float fuseTime = 0.9f;
        [SerializeField, Min(0.1f)] private float blastRadius = 1.65f;
        [SerializeField, Range(0.1f, 1.5f)] private float playerStaggerDuration = 0.75f;
        [SerializeField, Min(0f)] private float cooldown = 6f;
        [SerializeField, Min(0f)] private float throwWindup = 0.35f;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.72f, 0.15f, 0.9f);

        private EnemyStagger stagger;
        private PlayerHealth target;
        private Vector2 committedTargetPoint;
        private float throwCompletesAt;
        private float nextUseTime;
        private bool isWindingUp;

        public bool IsBusy => isWindingUp;
        public bool IsReady => !isWindingUp
            && Time.time >= nextUseTime
            && (stagger == null || stagger.CanAct);

        private void Awake()
        {
            stagger = GetComponent<EnemyStagger>();
        }

        private void Update()
        {
            if (!isWindingUp || Time.time < throwCompletesAt)
            {
                return;
            }

            SpawnGrenade();
        }

        public bool CanUse(PlayerHealth requestedTarget)
        {
            return IsReady
                && requestedTarget != null
                && requestedTarget.gameObject.activeInHierarchy
                && ((Vector2)requestedTarget.transform.position - (Vector2)transform.position)
                    .sqrMagnitude <= maximumThrowRange * maximumThrowRange;
        }

        public bool TryUse(PlayerHealth requestedTarget)
        {
            if (!CanUse(requestedTarget))
            {
                return false;
            }

            target = requestedTarget;
            committedTargetPoint = requestedTarget.transform.position;
            isWindingUp = true;
            throwCompletesAt = Time.time + throwWindup;
            CombatShapeEffect.Create(
                committedTargetPoint,
                CombatShape.Ring,
                blastRadius,
                telegraphColor,
                Mathf.Max(0.15f, throwWindup),
                0f,
                0.06f);
            return true;
        }

        private void SpawnGrenade()
        {
            isWindingUp = false;
            nextUseTime = Time.time + cooldown;
            Vector2 origin = throwPoint != null ? throwPoint.position : transform.position;
            DetectiveStunGrenadeProjectile grenade;
            if (grenadePrefab != null)
            {
                grenade = Instantiate(grenadePrefab, origin, Quaternion.identity);
            }
            else
            {
                GameObject grenadeObject = new GameObject("Detective Stun Grenade");
                grenadeObject.transform.position = origin;
                grenade = grenadeObject.AddComponent<DetectiveStunGrenadeProjectile>();
            }

            grenade.Initialize(
                target,
                origin,
                committedTargetPoint,
                throwSpeed,
                fuseTime,
                blastRadius,
                playerStaggerDuration,
                telegraphColor);
        }

        public void Interrupt()
        {
            if (!isWindingUp)
            {
                return;
            }

            isWindingUp = false;
            target = null;
            nextUseTime = Mathf.Max(nextUseTime, Time.time + cooldown * 0.35f);
        }

        private void OnDisable()
        {
            isWindingUp = false;
            target = null;
            nextUseTime = 0f;
        }
    }
}
