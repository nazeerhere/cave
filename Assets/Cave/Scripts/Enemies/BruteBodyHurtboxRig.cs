using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Brute-only collision prototype. The root body's collider stays fixed for
    /// movement, while authored child triggers are the only melee hit targets.
    /// No sprite bounds or animation frame data participate in this rig.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Damageable))]
    public sealed class BruteBodyHurtboxRig : MonoBehaviour
    {
        [Header("Stable Gameplay Body")]
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private Transform visualRoot;

        [Header("Damage Receivers")]
        [SerializeField] private BruteHurtbox[] hurtboxes = System.Array.Empty<BruteHurtbox>();

        [Header("Runtime Debug (Read Only)")]
        [SerializeField] private int activeHurtboxCount;
        [SerializeField] private Vector2 bodyColliderSize;
        [SerializeField] private Vector2 bodyColliderOffset;

        private Damageable damageable;

        public Collider2D BodyCollider => bodyCollider;
        public Transform VisualRoot => visualRoot;
        public int HurtboxCount => hurtboxes != null ? hurtboxes.Length : 0;

        public void Configure(
            Collider2D stableBodyCollider,
            Transform approvedVisualRoot,
            BruteHurtbox[] configuredHurtboxes)
        {
            bodyCollider = stableBodyCollider;
            visualRoot = approvedVisualRoot;
            hurtboxes = configuredHurtboxes ?? System.Array.Empty<BruteHurtbox>();
            CacheDebugState();
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            CacheDebugState();
        }

        private void OnEnable()
        {
            if (damageable == null)
            {
                damageable = GetComponent<Damageable>();
            }

            if (damageable != null)
            {
                damageable.Died -= HandleDied;
                damageable.Died += HandleDied;
            }

            SetHurtboxesActive(damageable == null || damageable.CurrentHealth > 0);
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.Died -= HandleDied;
            }
        }

        private void OnValidate()
        {
            CacheDebugState();
        }

        private void HandleDied()
        {
            // Damageable remains authoritative for death/corpse lifecycle. We
            // only stop future incoming hits while its existing death path runs.
            SetHurtboxesActive(false);
        }

        private void SetHurtboxesActive(bool active)
        {
            activeHurtboxCount = 0;
            for (int index = 0; index < hurtboxes.Length; index++)
            {
                BruteHurtbox hurtbox = hurtboxes[index];
                if (hurtbox == null)
                {
                    continue;
                }

                hurtbox.SetDamageReceptionActive(active);
                if (active)
                {
                    activeHurtboxCount++;
                }
            }
        }

        private void CacheDebugState()
        {
            if (bodyCollider is BoxCollider2D box)
            {
                bodyColliderSize = box.size;
                bodyColliderOffset = box.offset;
            }
            else if (bodyCollider is CapsuleCollider2D capsule)
            {
                bodyColliderSize = capsule.size;
                bodyColliderOffset = capsule.offset;
            }
            else
            {
                bodyColliderSize = Vector2.zero;
                bodyColliderOffset = Vector2.zero;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (bodyCollider != null)
            {
                Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.9f);
                Gizmos.DrawWireCube(bodyCollider.bounds.center, bodyCollider.bounds.size);
            }

            if (hurtboxes == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.9f);
            for (int index = 0; index < hurtboxes.Length; index++)
            {
                Collider2D hurtboxCollider = hurtboxes[index] != null
                    ? hurtboxes[index].Collider
                    : null;
                if (hurtboxCollider != null)
                {
                    Gizmos.DrawWireCube(hurtboxCollider.bounds.center, hurtboxCollider.bounds.size);
                }
            }
        }
    }
}
