using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Mirrors authored visual branches only. Rigidbody/collider transforms and
    /// world-space UI remain outside these roots and are never scaled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVisualChildFacing : MonoBehaviour
    {
        private struct VisualRoot
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Vector3 LocalScale;
        }

        private readonly List<VisualRoot> roots = new List<VisualRoot>();
        private bool sourceFacesRight = true;

        public void Configure(Transform visualRoot, bool authoredFacesRight)
        {
            Configure(visualRoot, null, authoredFacesRight);
        }

        public void Configure(
            Transform primaryVisualRoot,
            Transform secondaryVisualRoot,
            bool authoredFacesRight)
        {
            sourceFacesRight = authoredFacesRight;
            roots.Clear();
            AddVisualRoot(primaryVisualRoot);
            AddVisualRoot(secondaryVisualRoot);
        }

        public void Face(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) < 0.001f)
            {
                return;
            }

            float mirror = horizontalDirection > 0f == sourceFacesRight ? 1f : -1f;
            foreach (VisualRoot visualRoot in roots)
            {
                if (visualRoot.Transform == null)
                {
                    continue;
                }

                visualRoot.Transform.localPosition = new Vector3(
                    visualRoot.LocalPosition.x * mirror,
                    visualRoot.LocalPosition.y,
                    visualRoot.LocalPosition.z);
                visualRoot.Transform.localScale = new Vector3(
                    visualRoot.LocalScale.x * mirror,
                    visualRoot.LocalScale.y,
                    visualRoot.LocalScale.z);
            }
        }

        private void AddVisualRoot(Transform candidate)
        {
            if (candidate == null)
            {
                return;
            }

            foreach (VisualRoot existing in roots)
            {
                if (candidate == existing.Transform
                    || candidate.IsChildOf(existing.Transform)
                    || existing.Transform.IsChildOf(candidate))
                {
                    return;
                }
            }

            roots.Add(new VisualRoot
            {
                Transform = candidate,
                LocalPosition = candidate.localPosition,
                LocalScale = candidate.localScale
            });
        }
    }
}
