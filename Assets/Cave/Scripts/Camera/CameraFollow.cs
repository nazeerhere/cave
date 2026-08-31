using UnityEngine;
using Cave.Player;
using Cave.World;

namespace Cave.CameraSystem
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;
        [SerializeField] private Vector2 offset = new Vector2(0f, 1f);

        private Vector3 velocity;
        private Camera gameplayCamera;
        private float authoredOrthographicSize;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            velocity = Vector3.zero;
        }

        private void Awake()
        {
            gameplayCamera = GetComponent<Camera>();
            if (gameplayCamera != null && gameplayCamera.orthographic)
            {
                authoredOrthographicSize = gameplayCamera.orthographicSize;
                ApplySavedZoom();
            }
        }

        private void OnEnable()
        {
            ApplySavedZoom();
        }

        /// <summary>Called by the shared Settings preference whenever it changes.</summary>
        public void ApplySavedZoom()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = GetComponent<Camera>();
            }

            if (gameplayCamera == null || !gameplayCamera.orthographic)
            {
                return;
            }

            if (authoredOrthographicSize <= 0f)
            {
                authoredOrthographicSize = gameplayCamera.orthographicSize;
            }

            gameplayCamera.orthographicSize = authoredOrthographicSize
                / CameraZoomSettings.ZoomScale;
        }

        private void LateUpdate()
        {
            Transform authoritativePlayer = PlayerRunPersistence.CurrentPlayerTransform;
            if (authoritativePlayer != null
                && (target == null || target.GetComponentInParent<PlayerHealth>() != null)
                && target != authoritativePlayer)
            {
                target = authoritativePlayer;
                velocity = Vector3.zero;
            }

            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                transform.position.z);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref velocity,
                smoothTime);
        }
    }
}
