using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerVisionPenalty : MonoBehaviour
    {
        private Camera affectedCamera;
        private float baselineOrthographicSize;
        private float expiresAt;
        private float sizeMultiplier = 1f;

        public float RemainingDuration => Mathf.Max(0f, expiresAt - Time.time);
        public bool IsActive => RemainingDuration > 0f;

        public void Apply(float duration, float requestedSizeMultiplier, float maximumDuration)
        {
            float clampedDuration = Mathf.Clamp(duration, 0f, Mathf.Max(0f, maximumDuration));
            if (clampedDuration <= 0f)
            {
                return;
            }

            ResolveCamera();
            sizeMultiplier = Mathf.Clamp(requestedSizeMultiplier, 0.2f, 1f);
            expiresAt = Mathf.Max(expiresAt, Time.time + clampedDuration);
            expiresAt = Mathf.Min(expiresAt, Time.time + Mathf.Max(0f, maximumDuration));
            ApplyToCamera();
        }

        private void LateUpdate()
        {
            if (!IsActive)
            {
                RestoreCamera();
                return;
            }

            ResolveCamera();
            ApplyToCamera();
        }

        private void ResolveCamera()
        {
            Camera candidate = Camera.main;
            if (candidate == affectedCamera)
            {
                return;
            }

            RestoreCamera();
            affectedCamera = candidate;
            if (affectedCamera != null && affectedCamera.orthographic)
            {
                baselineOrthographicSize = affectedCamera.orthographicSize;
            }
        }

        private void ApplyToCamera()
        {
            if (affectedCamera != null && affectedCamera.orthographic)
            {
                affectedCamera.orthographicSize = baselineOrthographicSize * sizeMultiplier;
            }
        }

        public void Clear()
        {
            expiresAt = 0f;
            RestoreCamera();
        }

        private void RestoreCamera()
        {
            if (affectedCamera != null && affectedCamera.orthographic && baselineOrthographicSize > 0f)
            {
                affectedCamera.orthographicSize = baselineOrthographicSize;
            }

            affectedCamera = null;
            baselineOrthographicSize = 0f;
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
