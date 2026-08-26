using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class DetectiveStunGrenadeProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float arcHeight = 1.2f;
        [SerializeField, Min(0.01f)] private float grenadeVisualRadius = 0.12f;

        private PlayerHealth target;
        private Vector2 origin;
        private Vector2 targetPoint;
        private float flightDuration;
        private float fuseTime;
        private float blastRadius;
        private float playerStaggerDuration;
        private float startedAt;
        private Color color;
        private LineRenderer visual;
        private Material visualMaterial;
        private bool initialized;
        private bool detonated;

        public void Initialize(
            PlayerHealth requestedTarget,
            Vector2 start,
            Vector2 destination,
            float throwSpeed,
            float requestedFuseTime,
            float requestedBlastRadius,
            float requestedPlayerStaggerDuration,
            Color effectColor)
        {
            target = requestedTarget;
            origin = start;
            targetPoint = destination;
            fuseTime = Mathf.Max(0.1f, requestedFuseTime);
            float travelBySpeed = Vector2.Distance(origin, targetPoint)
                / Mathf.Max(0.1f, throwSpeed);
            flightDuration = Mathf.Clamp(travelBySpeed, 0.15f, fuseTime * 0.8f);
            blastRadius = Mathf.Max(0.1f, requestedBlastRadius);
            playerStaggerDuration = Mathf.Max(0.05f, requestedPlayerStaggerDuration);
            color = effectColor;
            startedAt = Time.time;
            initialized = true;
            EnsureVisual();
        }

        private void Update()
        {
            if (!initialized || detonated)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float travelProgress = Mathf.Clamp01(elapsed / flightDuration);
            Vector2 linearPosition = Vector2.Lerp(origin, targetPoint, travelProgress);
            float height = 4f * arcHeight * travelProgress * (1f - travelProgress);
            transform.position = linearPosition + Vector2.up * height;
            transform.Rotate(0f, 0f, 540f * Time.deltaTime);

            if (elapsed >= fuseTime)
            {
                Detonate();
            }
        }

        private void Detonate()
        {
            if (detonated)
            {
                return;
            }

            detonated = true;
            transform.position = targetPoint;
            CombatShapeEffect.Create(
                targetPoint,
                CombatShape.Ring,
                blastRadius,
                color,
                0.3f,
                0f,
                0.1f);

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                target = FindObjectOfType<PlayerHealth>();
            }

            if (target != null
                && ((Vector2)target.transform.position - targetPoint).sqrMagnitude
                    <= blastRadius * blastRadius)
            {
                PlayerGuardBreak playerActionState = target.GetComponent<PlayerGuardBreak>();
                if (playerActionState != null)
                {
                    playerActionState.ApplyPlayerStagger(playerStaggerDuration);
                }
                else
                {
                    // Older/minimal player setups may not yet have the shared
                    // action gate. Preserve movement interruption without
                    // recreating the Detective-specific stun status.
                    target.GetComponent<PlayerController>()
                        ?.ApplyExternalControlLock(playerStaggerDuration);
                }
            }

            Destroy(gameObject);
        }

        private void EnsureVisual()
        {
            if (GetComponentInChildren<Renderer>() != null)
            {
                return;
            }

            visual = gameObject.AddComponent<LineRenderer>();
            visualMaterial = new Material(Shader.Find("Sprites/Default"));
            visual.material = visualMaterial;
            visual.useWorldSpace = false;
            visual.loop = true;
            visual.positionCount = 12;
            visual.startWidth = 0.045f;
            visual.endWidth = 0.045f;
            visual.startColor = color;
            visual.endColor = color;
            visual.sortingOrder = 8;
            for (int index = 0; index < visual.positionCount; index++)
            {
                float angle = index / (float)visual.positionCount * Mathf.PI * 2f;
                visual.SetPosition(
                    index,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * grenadeVisualRadius);
            }
        }

        private void OnDestroy()
        {
            if (visualMaterial != null)
            {
                Destroy(visualMaterial);
            }
        }
    }
}
