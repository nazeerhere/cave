using UnityEngine;

namespace Cave.World.Traversal
{
    /// <summary>
    /// Thin configuration wrapper for Unity's native PlatformEffector2D. The
    /// platform collider remains the sole collision authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D), typeof(PlatformEffector2D))]
    public sealed class OneWayPlatform2D : MonoBehaviour
    {
        public enum SurfaceDirection
        {
            Up,
            Right,
            Down,
            Left
        }

        [Header("Native Effector Settings")]
        [SerializeField] private SurfaceDirection surfaceDirection = SurfaceDirection.Up;
        [SerializeField, Range(1f, 360f)] private float surfaceArc = 180f;
        [SerializeField] private bool useOneWayGrouping = true;
        [SerializeField] private bool useSideFriction;
        [SerializeField] private bool useSideBounce;

        private Collider2D platformCollider;
        private PlatformEffector2D platformEffector;

        public Collider2D PlatformCollider => platformCollider;
        public PlatformEffector2D PlatformEffector => platformEffector;
        public Vector2 SurfaceNormal => (Vector2)(Quaternion.Euler(0f, 0f, GetRotationalOffset()) * transform.up);

        private void Awake()
        {
            ConfigureEffector();
        }

        private void Reset()
        {
            ConfigureEffector();
        }

        private void OnValidate()
        {
            ConfigureEffector();
        }

        private void ConfigureEffector()
        {
            if (platformCollider == null)
            {
                platformCollider = GetComponent<Collider2D>();
            }

            if (platformEffector == null)
            {
                platformEffector = GetComponent<PlatformEffector2D>();
            }

            if (platformCollider == null || platformEffector == null)
            {
                return;
            }

            platformCollider.usedByEffector = true;
            platformEffector.useOneWay = true;
            platformEffector.useOneWayGrouping = useOneWayGrouping;
            platformEffector.useSideFriction = useSideFriction;
            platformEffector.useSideBounce = useSideBounce;
            platformEffector.surfaceArc = surfaceArc;
            platformEffector.rotationalOffset = GetRotationalOffset();
        }

        private float GetRotationalOffset()
        {
            switch (surfaceDirection)
            {
                case SurfaceDirection.Right:
                    return -90f;
                case SurfaceDirection.Down:
                    return 180f;
                case SurfaceDirection.Left:
                    return 90f;
                default:
                    return 0f;
            }
        }
    }
}
