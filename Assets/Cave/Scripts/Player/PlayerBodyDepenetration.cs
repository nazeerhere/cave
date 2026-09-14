using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// Bounded last-resort correction for unexpected solid player/enemy body overlap.
    /// Intentional Cross Step and Slip phasing owns its own exit resolution instead.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerBodyDepenetration : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximumCorrectionPerPhysicsStep = 0.18f;
        [SerializeField, Min(0.0001f)] private float minimumPenetration = 0.005f;

        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private CommittedAttackCollisionPhasing collisionPhasing;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
        }

        private void FixedUpdate()
        {
            if (body == null || bodyCollider == null || bodyCollider.isTrigger)
            {
                return;
            }

            if (collisionPhasing == null)
            {
                collisionPhasing = GetComponent<CommittedAttackCollisionPhasing>();
            }

            if (collisionPhasing != null && collisionPhasing.IsPhasingCharacterBodies)
            {
                return;
            }

            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
            int count = bodyCollider.OverlapCollider(filter, overlapBuffer);
            Vector2 correction = Vector2.zero;
            for (int index = 0; index < count; index++)
            {
                Collider2D candidate = overlapBuffer[index];
                if (!IsEnemyBody(candidate))
                {
                    continue;
                }

                ColliderDistance2D distance = Physics2D.Distance(bodyCollider, candidate);
                if (!distance.isOverlapped || distance.distance >= -minimumPenetration)
                {
                    continue;
                }

                correction += distance.normal * distance.distance;
            }

            if (correction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            // In a side-scroller, escaping sideways avoids lifting the player onto
            // a mob. Keep a genuine vertical-only separation when walls require it.
            if (Mathf.Abs(correction.x) >= Mathf.Abs(correction.y) * 0.25f)
            {
                correction.y = 0f;
            }

            correction = Vector2.ClampMagnitude(correction, maximumCorrectionPerPhysicsStep);
            body.position += correction;
            if (Mathf.Abs(correction.x) > 0.0001f)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
            }
            Physics2D.SyncTransforms();
        }

        private static bool IsEnemyBody(Collider2D candidate)
        {
            return candidate != null
                && candidate.enabled
                && !candidate.isTrigger
                && (candidate.GetComponentInParent<EnemyController>() != null
                    || candidate.GetComponentInParent<EnemyArchetypeProfile>() != null
                    || candidate.GetComponentInParent<FlyingSwarmController>() != null);
        }
    }
}
