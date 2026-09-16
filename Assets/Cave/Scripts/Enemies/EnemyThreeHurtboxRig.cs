using Cave.Combat;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cave.Enemies
{
    /// <summary>
    /// Shared authored rig for ordinary Cave mobs. It preserves simple stable
    /// body colliders for physics and keeps exactly three trigger-only damage
    /// regions: lower, middle, and upper. Animation/sprite bounds never alter
    /// any collider in this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(Rigidbody2D))]
    public sealed class EnemyThreeHurtboxRig : MonoBehaviour
    {
        [Header("Stable Physical Body")]
        [SerializeField] private Collider2D[] bodyColliders = System.Array.Empty<Collider2D>();
        [SerializeField] private Transform visualRoot;

        [Header("Trigger-Only Damage Receivers")]
        [SerializeField] private EnemyAnatomicalHurtbox lowerHurtbox;
        [SerializeField] private EnemyAnatomicalHurtbox middleHurtbox;
        [SerializeField] private EnemyAnatomicalHurtbox upperHurtbox;

        [Header("Runtime Debug (Read Only)")]
        [SerializeField] private int activeHurtboxCount;
        [SerializeField] private int physicalBodyColliderCount;

        private Damageable damageable;

        public Collider2D[] BodyColliders => bodyColliders;
        public Transform VisualRoot => visualRoot;
        public int HurtboxCount => 3;

        public void Configure(
            Collider2D[] configuredBodyColliders,
            Transform configuredVisualRoot,
            EnemyAnatomicalHurtbox lower,
            EnemyAnatomicalHurtbox middle,
            EnemyAnatomicalHurtbox upper)
        {
            bodyColliders = configuredBodyColliders ?? System.Array.Empty<Collider2D>();
            visualRoot = configuredVisualRoot;
            lowerHurtbox = lower;
            middleHurtbox = middle;
            upperHurtbox = upper;
            CacheDebugState();
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
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
            SetHurtboxesActive(false);
        }

        private void SetHurtboxesActive(bool active)
        {
            activeHurtboxCount = 0;
            SetHurtboxActive(lowerHurtbox, active);
            SetHurtboxActive(middleHurtbox, active);
            SetHurtboxActive(upperHurtbox, active);
        }

        private void SetHurtboxActive(EnemyAnatomicalHurtbox hurtbox, bool active)
        {
            if (hurtbox == null)
            {
                return;
            }

            hurtbox.SetDamageReceptionActive(active);
            if (active)
            {
                activeHurtboxCount++;
            }
        }

        private void CacheDebugState()
        {
            physicalBodyColliderCount = 0;
            if (bodyColliders == null)
            {
                return;
            }

            for (int index = 0; index < bodyColliders.Length; index++)
            {
                if (bodyColliders[index] != null)
                {
                    physicalBodyColliderCount++;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawBodyGizmos();
            DrawHurtboxGizmo(lowerHurtbox, "LOWER — TORSO / LEGS", new Color(1f, 0.7f, 0.15f, 0.9f));
            DrawHurtboxGizmo(middleHurtbox, "MIDDLE — CHEST / BODY", new Color(1f, 0.28f, 0.25f, 0.9f));
            DrawHurtboxGizmo(upperHurtbox, "UPPER — HEAD", new Color(0.8f, 0.3f, 1f, 0.9f));
        }

        private void DrawBodyGizmos()
        {
            if (bodyColliders == null)
            {
                return;
            }

            Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.9f);
            for (int index = 0; index < bodyColliders.Length; index++)
            {
                Collider2D bodyCollider = bodyColliders[index];
                if (bodyCollider != null)
                {
                    Gizmos.DrawWireCube(bodyCollider.bounds.center, bodyCollider.bounds.size);
                }
            }
        }

        private static void DrawHurtboxGizmo(EnemyAnatomicalHurtbox hurtbox, string label, Color color)
        {
            Collider2D collider = hurtbox != null ? hurtbox.Collider : null;
            if (collider == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
#if UNITY_EDITOR
            Handles.Label(collider.bounds.center, label);
#endif
        }
    }
}
