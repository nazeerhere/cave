using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Keeps Eye body colliders non-solid against characters while leaving its
    /// trigger-based Dive hitbox and all terrain contacts alone.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class EyeCollisionPhasing : MonoBehaviour
    {
        private const float RefreshInterval = .2f;
        private static readonly Dictionary<ColliderPair, int> PairClaims = new Dictionary<ColliderPair, int>();

        private readonly HashSet<ColliderPair> ownedPairs = new HashSet<ColliderPair>();
        private Collider2D[] ownBodies;
        private float nextRefreshAt;

        internal static void EnsureOn(GameObject owner)
        {
            if (owner != null && owner.GetComponent<EyeCollisionPhasing>() == null)
            {
                owner.AddComponent<EyeCollisionPhasing>();
            }
        }

        private void Awake()
        {
            ownBodies = CollectSolidColliders(gameObject);
        }

        private void OnEnable()
        {
            RefreshPairs();
        }

        private void FixedUpdate()
        {
            if (Time.time >= nextRefreshAt)
            {
                RefreshPairs();
            }
        }

        private void OnDisable()
        {
            ReleaseAllPairs();
        }

        private void OnDestroy()
        {
            ReleaseAllPairs();
        }

        private void RefreshPairs()
        {
            nextRefreshAt = Time.time + RefreshInterval;
            if (ownBodies == null || ownBodies.Length == 0)
            {
                ownBodies = CollectSolidColliders(gameObject);
            }

            foreach (Damageable candidate in FindObjectsOfType<Damageable>())
            {
                if (!IsPhaseTarget(candidate)) continue;
                Collider2D[] targetBodies = CollectSolidColliders(candidate.gameObject);
                for (int left = 0; left < ownBodies.Length; left++)
                {
                    for (int right = 0; right < targetBodies.Length; right++)
                    {
                        Claim(ownBodies[left], targetBodies[right]);
                    }
                }
            }
        }

        private bool IsPhaseTarget(Damageable candidate)
        {
            return candidate != null
                && candidate.gameObject != gameObject
                && (candidate.GetComponent<PlayerHealth>() != null || HostileMobQuery.IsRealHostile(candidate));
        }

        private static Collider2D[] CollectSolidColliders(GameObject owner)
        {
            Collider2D[] all = owner.GetComponentsInChildren<Collider2D>(true);
            List<Collider2D> result = new List<Collider2D>();
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].enabled && !all[index].isTrigger)
                {
                    result.Add(all[index]);
                }
            }
            return result.ToArray();
        }

        private void Claim(Collider2D first, Collider2D second)
        {
            if (first == null || second == null || first == second) return;
            ColliderPair pair = new ColliderPair(first, second);
            if (!ownedPairs.Add(pair)) return;

            int claims;
            PairClaims.TryGetValue(pair, out claims);
            PairClaims[pair] = claims + 1;
            if (claims == 0)
            {
                Physics2D.IgnoreCollision(first, second, true);
            }
        }

        private void ReleaseAllPairs()
        {
            foreach (ColliderPair pair in ownedPairs)
            {
                int claims;
                if (!PairClaims.TryGetValue(pair, out claims)) continue;
                if (claims <= 1)
                {
                    PairClaims.Remove(pair);
                    if (pair.First != null && pair.Second != null)
                    {
                        Physics2D.IgnoreCollision(pair.First, pair.Second, false);
                    }
                }
                else
                {
                    PairClaims[pair] = claims - 1;
                }
            }
            ownedPairs.Clear();
        }

        private struct ColliderPair : System.IEquatable<ColliderPair>
        {
            internal readonly Collider2D First;
            internal readonly Collider2D Second;
            private readonly int firstId;
            private readonly int secondId;

            internal ColliderPair(Collider2D first, Collider2D second)
            {
                if (first.GetInstanceID() <= second.GetInstanceID())
                {
                    First = first;
                    Second = second;
                }
                else
                {
                    First = second;
                    Second = first;
                }
                firstId = First.GetInstanceID();
                secondId = Second.GetInstanceID();
            }

            public bool Equals(ColliderPair other)
            {
                return firstId == other.firstId && secondId == other.secondId;
            }

            public override bool Equals(object obj)
            {
                return obj is ColliderPair && Equals((ColliderPair)obj);
            }

            public override int GetHashCode()
            {
                unchecked { return (firstId * 397) ^ secondId; }
            }
        }
    }
}
