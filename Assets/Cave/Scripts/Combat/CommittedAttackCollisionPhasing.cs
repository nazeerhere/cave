using System.Collections.Generic;
using Cave.Enemies;
using Cave.Player;
using UnityEngine;

namespace Cave.Combat
{
    [DisallowMultipleComponent]
    public sealed class CommittedAttackCollisionPhasing : MonoBehaviour
    {
        private struct IgnoredPair
        {
            public Collider2D OwnBody;
            public Collider2D OtherBody;
        }

        [Header("Restore Safety")]
        [SerializeField, Min(0f)] private float maximumDepenetrationDistance = 0.35f;
        [SerializeField, Range(1, 4)] private int maximumDepenetrationSteps = 2;

        [Header("Current State (Read Only)")]
        [SerializeField] private bool isPhasingCharacterBodies;

        private readonly List<IgnoredPair> ignoredPairs = new List<IgnoredPair>();
        private readonly Collider2D[] overlapBuffer = new Collider2D[24];
        private Collider2D ownBody;
        private Rigidbody2D body;

        public bool IsPhasingCharacterBodies => isPhasingCharacterBodies;

        public void Begin(Collider2D committedBody)
        {
            RestoreIgnoredPairs(false);
            ownBody = committedBody;
            body = ownBody != null ? ownBody.attachedRigidbody : GetComponent<Rigidbody2D>();
            if (ownBody == null || ownBody.isTrigger)
            {
                return;
            }

            isPhasingCharacterBodies = true;
            IgnoreCurrentCharacterBodies();
        }

        public void End()
        {
            RestoreIgnoredPairs(true);
        }

        private void IgnoreCurrentCharacterBodies()
        {
            foreach (Collider2D candidate in FindObjectsOfType<Collider2D>())
            {
                if (candidate == null
                    || candidate == ownBody
                    || candidate.isTrigger
                    || candidate.transform.IsChildOf(transform)
                    || !IsCharacterBody(candidate)
                    || Physics2D.GetIgnoreCollision(ownBody, candidate))
                {
                    continue;
                }

                Physics2D.IgnoreCollision(ownBody, candidate, true);
                ignoredPairs.Add(new IgnoredPair
                {
                    OwnBody = ownBody,
                    OtherBody = candidate
                });
            }
        }

        private void RestoreIgnoredPairs(bool resolveOverlaps)
        {
            if (resolveOverlaps && isPhasingCharacterBodies)
            {
                ResolveCharacterOverlaps();
            }

            foreach (IgnoredPair pair in ignoredPairs)
            {
                if (pair.OwnBody != null && pair.OtherBody != null)
                {
                    Physics2D.IgnoreCollision(pair.OwnBody, pair.OtherBody, false);
                }
            }

            ignoredPairs.Clear();
            isPhasingCharacterBodies = false;
            ownBody = null;
            body = null;
        }

        private void ResolveCharacterOverlaps()
        {
            if (ownBody == null
                || !ownBody.gameObject.activeInHierarchy
                || maximumDepenetrationDistance <= 0f)
            {
                return;
            }

            float remainingDistance = maximumDepenetrationDistance;
            for (int step = 0; step < maximumDepenetrationSteps && remainingDistance > 0f; step++)
            {
                Vector2 correction = CalculateSeparationCorrection();
                if (correction.sqrMagnitude <= 0.000001f)
                {
                    break;
                }

                correction = Vector2.ClampMagnitude(correction, remainingDistance);
                Vector2 previousPosition = body != null
                    ? body.position
                    : (Vector2)transform.position;
                MoveBody(previousPosition + correction);
                Physics2D.SyncTransforms();
                if (OverlapsWorldGeometry())
                {
                    MoveBody(previousPosition);
                    Physics2D.SyncTransforms();
                    break;
                }

                remainingDistance -= correction.magnitude;
            }
        }

        private Vector2 CalculateSeparationCorrection()
        {
            Vector2 correction = Vector2.zero;
            foreach (IgnoredPair pair in ignoredPairs)
            {
                if (pair.OwnBody == null || pair.OtherBody == null)
                {
                    continue;
                }

                ColliderDistance2D distance = Physics2D.Distance(
                    pair.OwnBody,
                    pair.OtherBody);
                if (distance.isOverlapped)
                {
                    // Overlap distances are negative, so this moves the committed
                    // body opposite the normal and out of the other character.
                    correction += distance.normal * distance.distance;
                }
            }

            return correction;
        }

        private bool OverlapsWorldGeometry()
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
            int count = ownBody.OverlapCollider(filter, overlapBuffer);
            for (int index = 0; index < count; index++)
            {
                Collider2D candidate = overlapBuffer[index];
                if (candidate == null
                    || candidate == ownBody
                    || candidate.transform.IsChildOf(transform)
                    || IsCharacterBody(candidate))
                {
                    continue;
                }

                if (Physics2D.Distance(ownBody, candidate).isOverlapped)
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveBody(Vector2 position)
        {
            if (body != null)
            {
                body.position = position;
            }
            else
            {
                transform.position = new Vector3(
                    position.x,
                    position.y,
                    transform.position.z);
            }
        }

        private static bool IsCharacterBody(Collider2D candidate)
        {
            return candidate != null
                && !candidate.isTrigger
                && (candidate.GetComponentInParent<PlayerController>() != null
                    || candidate.GetComponentInParent<EnemyController>() != null
                    || candidate.GetComponentInParent<EnemyArchetypeProfile>() != null
                    || candidate.GetComponentInParent<FlyingSwarmController>() != null);
        }

        private void OnDisable()
        {
            RestoreIgnoredPairs(false);
        }

        private void OnDestroy()
        {
            RestoreIgnoredPairs(false);
        }
    }
}
