using UnityEngine;

namespace Cave.Player
{
    /// <summary>Pure placement test for the player-to-disk pull corridor.</summary>
    public static class OblivionTrapPullGeometry
    {
        public static bool IsWithinCorridor(
            Vector2 playerPosition,
            Vector2 targetPosition,
            Vector2 trapPosition,
            float corridorHalfWidth,
            float effectivePullRange)
        {
            Vector2 segment = trapPosition - playerPosition;
            float segmentLength = segment.magnitude;
            if (segmentLength <= 0.001f
                || corridorHalfWidth < 0f
                || effectivePullRange < 0f
                || Vector2.Distance(targetPosition, trapPosition) > effectivePullRange)
            {
                return false;
            }

            Vector2 direction = segment / segmentLength;
            Vector2 relative = targetPosition - playerPosition;
            float projection = Vector2.Dot(relative, direction);
            if (projection < 0f || projection > segmentLength)
            {
                return false;
            }

            float lateralDistance = Mathf.Abs(Vector2.Dot(relative, new Vector2(-direction.y, direction.x)));
            return lateralDistance <= corridorHalfWidth;
        }
    }
}
