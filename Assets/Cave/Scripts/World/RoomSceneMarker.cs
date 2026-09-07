using UnityEngine;

namespace Cave.World
{
    /// <summary>
    /// Authored-room metadata used by the editor scene builder and its validator.
    /// It intentionally has no runtime behaviour or update loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomSceneMarker : MonoBehaviour
    {
        public enum MarkerKind
        {
            Interactable,
            EnemySpawn,
            Transition,
            PlayerSpawn,
            CameraBounds
        }

        [SerializeField] private MarkerKind kind;
        [SerializeField] private string identifier;
        [SerializeField] private string destinationScene;
        [SerializeField] private Vector2 boundsSize = Vector2.one;

        public static RoomSceneMarker ActiveCameraBounds { get; private set; }

        public MarkerKind Kind => kind;
        public string Identifier => identifier;
        public string DestinationScene => destinationScene;
        public Vector2 BoundsSize => boundsSize;

        public void Configure(
            MarkerKind markerKind,
            string markerIdentifier,
            string destination = null,
            Vector2? size = null)
        {
            kind = markerKind;
            identifier = markerIdentifier ?? string.Empty;
            destinationScene = destination ?? string.Empty;
            boundsSize = size ?? Vector2.one;
            RefreshCameraBoundsRegistration();
        }

        public Vector3 ClampCameraPosition(Vector3 position, Camera camera)
        {
            if (kind != MarkerKind.CameraBounds || camera == null || !camera.orthographic)
            {
                return position;
            }

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            float halfBoundsWidth = boundsSize.x * 0.5f;
            float halfBoundsHeight = boundsSize.y * 0.5f;
            float minX = transform.position.x - halfBoundsWidth + halfWidth;
            float maxX = transform.position.x + halfBoundsWidth - halfWidth;
            float minY = transform.position.y - halfBoundsHeight + halfHeight;
            float maxY = transform.position.y + halfBoundsHeight - halfHeight;
            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : transform.position.x;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : transform.position.y;
            return position;
        }

        private void OnEnable()
        {
            RefreshCameraBoundsRegistration();
        }

        private void OnDisable()
        {
            if (ActiveCameraBounds == this)
            {
                ActiveCameraBounds = null;
            }
        }

        private void RefreshCameraBoundsRegistration()
        {
            if (isActiveAndEnabled && kind == MarkerKind.CameraBounds)
            {
                ActiveCameraBounds = this;
            }
        }
    }
}
