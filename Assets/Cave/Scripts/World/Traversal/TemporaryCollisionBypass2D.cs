using System;
using System.Collections.Generic;
using Cave.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.World.Traversal
{
    /// <summary>
    /// Owns short-lived collision ignores for a single traversal actor. Only pairs
    /// changed by this component are restored, so it never changes layer-wide
    /// collision rules or unrelated terrain contacts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TemporaryCollisionBypass2D : MonoBehaviour
    {
        private struct IgnoredPair
        {
            public Collider2D PlayerCollider;
            public Collider2D TargetCollider;
        }

        private struct ActiveTarget
        {
            public Collider2D TargetCollider;
            public int ReferenceCount;
        }

        [Header("Player Colliders")]
        [Tooltip("Leave empty to cache every non-trigger Collider2D on this actor in Awake.")]
        [SerializeField] private Collider2D[] playerColliders = Array.Empty<Collider2D>();

        private readonly List<IgnoredPair> ignoredPairs = new List<IgnoredPair>();
        private readonly List<ActiveTarget> activeTargets = new List<ActiveTarget>();
        private PlayerHealth playerHealth;
        private PlayerRespawn playerRespawn;

        public int ActiveBypassCount => activeTargets.Count;
        public event Action BypassesCleared;

        private void Awake()
        {
            CachePlayerColliders();
            playerHealth = GetComponentInParent<PlayerHealth>();
            playerRespawn = GetComponentInParent<PlayerRespawn>();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died += EndAllBypasses;
                playerHealth.Respawned += EndAllBypasses;
            }

            if (playerRespawn != null)
            {
                playerRespawn.Respawned += EndAllBypasses;
            }

            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            EndAllBypasses();
            UnsubscribeResetEvents();
        }

        private void OnDestroy()
        {
            EndAllBypasses();
            UnsubscribeResetEvents();
        }

        /// <summary>
        /// Begins a bypass against one explicitly supplied terrain collider.
        /// Repeated requests for the same target are reference counted.
        /// </summary>
        public bool BeginBypass(Collider2D targetCollider)
        {
            if (targetCollider == null || targetCollider.isTrigger)
            {
                return false;
            }

            int activeTargetIndex = FindActiveTarget(targetCollider);
            if (activeTargetIndex >= 0)
            {
                ActiveTarget existing = activeTargets[activeTargetIndex];
                existing.ReferenceCount++;
                activeTargets[activeTargetIndex] = existing;
                return true;
            }

            bool changedAnyPair = false;
            for (int index = 0; index < playerColliders.Length; index++)
            {
                Collider2D playerCollider = playerColliders[index];
                if (playerCollider == null
                    || playerCollider.isTrigger
                    || playerCollider == targetCollider
                    || Physics2D.GetIgnoreCollision(playerCollider, targetCollider))
                {
                    continue;
                }

                Physics2D.IgnoreCollision(playerCollider, targetCollider, true);
                ignoredPairs.Add(new IgnoredPair
                {
                    PlayerCollider = playerCollider,
                    TargetCollider = targetCollider
                });
                changedAnyPair = true;
            }

            if (changedAnyPair)
            {
                activeTargets.Add(new ActiveTarget
                {
                    TargetCollider = targetCollider,
                    ReferenceCount = 1
                });
            }

            return changedAnyPair;
        }

        /// <summary>
        /// Releases one request for a target collider and restores only the pairs
        /// that this component previously changed once the final request ends.
        /// </summary>
        public void EndBypass(Collider2D targetCollider)
        {
            int activeTargetIndex = FindActiveTarget(targetCollider);
            if (activeTargetIndex < 0)
            {
                return;
            }

            ActiveTarget activeTarget = activeTargets[activeTargetIndex];
            activeTarget.ReferenceCount--;
            if (activeTarget.ReferenceCount > 0)
            {
                activeTargets[activeTargetIndex] = activeTarget;
                return;
            }

            RestoreTargetPairs(targetCollider);
            activeTargets.RemoveAt(activeTargetIndex);
        }

        /// <summary>
        /// Restores every collider pair owned by this bypass immediately.
        /// </summary>
        public void EndAllBypasses()
        {
            bool hadActiveBypasses = ignoredPairs.Count > 0 || activeTargets.Count > 0;
            for (int index = ignoredPairs.Count - 1; index >= 0; index--)
            {
                IgnoredPair pair = ignoredPairs[index];
                if (pair.PlayerCollider != null && pair.TargetCollider != null)
                {
                    Physics2D.IgnoreCollision(pair.PlayerCollider, pair.TargetCollider, false);
                }
            }

            ignoredPairs.Clear();
            activeTargets.Clear();
            if (hadActiveBypasses)
            {
                BypassesCleared?.Invoke();
            }
        }

        private void CachePlayerColliders()
        {
            if (playerColliders != null && playerColliders.Length > 0)
            {
                return;
            }

            Collider2D[] candidates = GetComponentsInChildren<Collider2D>(true);
            int usableCount = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] != null && !candidates[index].isTrigger)
                {
                    usableCount++;
                }
            }

            playerColliders = new Collider2D[usableCount];
            int destinationIndex = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                Collider2D candidate = candidates[index];
                if (candidate != null && !candidate.isTrigger)
                {
                    playerColliders[destinationIndex++] = candidate;
                }
            }
        }

        private int FindActiveTarget(Collider2D targetCollider)
        {
            for (int index = 0; index < activeTargets.Count; index++)
            {
                if (activeTargets[index].TargetCollider == targetCollider)
                {
                    return index;
                }
            }

            return -1;
        }

        private void RestoreTargetPairs(Collider2D targetCollider)
        {
            for (int index = ignoredPairs.Count - 1; index >= 0; index--)
            {
                IgnoredPair pair = ignoredPairs[index];
                if (pair.TargetCollider != targetCollider)
                {
                    continue;
                }

                if (pair.PlayerCollider != null && pair.TargetCollider != null)
                {
                    Physics2D.IgnoreCollision(pair.PlayerCollider, pair.TargetCollider, false);
                }

                ignoredPairs.RemoveAt(index);
            }
        }

        private void HandleSceneUnloaded(Scene unloadedScene)
        {
            EndAllBypasses();
        }

        private void UnsubscribeResetEvents()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= EndAllBypasses;
                playerHealth.Respawned -= EndAllBypasses;
            }

            if (playerRespawn != null)
            {
                playerRespawn.Respawned -= EndAllBypasses;
            }

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }
    }
}
