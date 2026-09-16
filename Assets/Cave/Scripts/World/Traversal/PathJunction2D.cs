using System;
using System.Collections.Generic;
using Cave.InputSystem;
using Cave.Player;
using Cave.World;
using UnityEngine;

namespace Cave.World.Traversal
{
    /// <summary>
    /// Commits a player to one configured branch while inside a trigger, then
    /// releases only that branch's temporary terrain bypass after the player exits.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PathJunction2D : MonoBehaviour
    {
        public enum RouteDirection
        {
            Up,
            Down,
            Left,
            Right,
            CustomPositive,
            CustomNegative
        }

        [Serializable]
        public sealed class RouteDefinition
        {
            [SerializeField] private RouteDirection direction;
            [SerializeField] private Collider2D[] bypassColliders = Array.Empty<Collider2D>();

            public RouteDirection Direction => direction;
            public Collider2D[] BypassColliders => bypassColliders;

            public RouteDefinition(RouteDirection routeDirection, Collider2D[] colliders)
            {
                direction = routeDirection;
                bypassColliders = colliders ?? Array.Empty<Collider2D>();
            }
        }

        [Header("Route Selection")]
        [SerializeField, Range(0.01f, 1f)] private float intentThreshold = 0.5f;
        [SerializeField] private Vector2 customPositiveDirection = new Vector2(1f, 1f);
        [SerializeField] private Vector2 customNegativeDirection = new Vector2(-1f, -1f);
        [SerializeField] private RouteDefinition[] routes = Array.Empty<RouteDefinition>();

        [Header("Current State (Read Only)")]
        [SerializeField] private bool isEligible;
        [SerializeField] private bool isCommitted;
        [SerializeField] private RouteDirection committedRoute;

        private readonly List<Collider2D> playerContacts = new List<Collider2D>();
        private readonly List<Collider2D> bypassedColliders = new List<Collider2D>();
        private TemporaryCollisionBypass2D activeBypass;
        private PlayerHealth activeHealth;
        private PlayerRespawn activeRespawn;

        public bool IsEligible => isEligible;
        public bool IsCommitted => isCommitted;

        private void Awake()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnDisable()
        {
            CancelTraversal();
        }

        private void OnDestroy()
        {
            CancelTraversal();
        }

        private void Update()
        {
            if (!isEligible || isCommitted || activeBypass == null)
            {
                return;
            }

            Vector2 intent = new Vector2(GameInput.Horizontal, GameInput.AimVertical);
            if (intent.sqrMagnitude < intentThreshold * intentThreshold)
            {
                return;
            }

            int routeIndex = FindBestRoute(intent.normalized);
            if (routeIndex >= 0)
            {
                CommitRoute(routes[routeIndex]);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TemporaryCollisionBypass2D bypass = other.GetComponentInParent<TemporaryCollisionBypass2D>();
            if (bypass == null)
            {
                return;
            }

            if (activeBypass != null && activeBypass != bypass)
            {
                return;
            }

            if (!ContainsContact(other))
            {
                playerContacts.Add(other);
            }

            if (activeBypass == null)
            {
                BeginTraversal(bypass);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (activeBypass == null || other.GetComponentInParent<TemporaryCollisionBypass2D>() != activeBypass)
            {
                return;
            }

            RemoveContact(other);
            if (playerContacts.Count == 0)
            {
                CancelTraversal();
            }
        }

        /// <summary>
        /// Editor/test setup helper that keeps the runtime route state serialized.
        /// </summary>
        public void ConfigureRoutes(
            RouteDefinition[] configuredRoutes,
            Vector2 configuredCustomPositiveDirection,
            Vector2 configuredCustomNegativeDirection)
        {
            routes = configuredRoutes ?? Array.Empty<RouteDefinition>();
            customPositiveDirection = configuredCustomPositiveDirection;
            customNegativeDirection = configuredCustomNegativeDirection;
        }

        private void BeginTraversal(TemporaryCollisionBypass2D bypass)
        {
            activeBypass = bypass;
            isEligible = true;
            isCommitted = false;
            bypassedColliders.Clear();
            playerContacts.RemoveAll(contact => contact == null);
            activeBypass.BypassesCleared += HandleBypassesCleared;

            activeHealth = activeBypass.GetComponentInParent<PlayerHealth>();
            activeRespawn = activeBypass.GetComponentInParent<PlayerRespawn>();
            if (activeHealth != null)
            {
                activeHealth.Died += HandleTraversalInterrupted;
                activeHealth.Respawned += HandleTraversalInterrupted;
            }

            if (activeRespawn != null)
            {
                activeRespawn.Respawned += HandleTraversalInterrupted;
            }
        }

        private int FindBestRoute(Vector2 normalizedIntent)
        {
            int bestRouteIndex = -1;
            float bestAlignment = intentThreshold;
            for (int index = 0; index < routes.Length; index++)
            {
                RouteDefinition route = routes[index];
                if (route == null || route.BypassColliders == null || route.BypassColliders.Length == 0)
                {
                    continue;
                }

                Vector2 routeDirection = GetDirectionVector(route.Direction);
                if (routeDirection.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                float alignment = Vector2.Dot(normalizedIntent, routeDirection.normalized);
                if (alignment > bestAlignment)
                {
                    bestAlignment = alignment;
                    bestRouteIndex = index;
                }
            }

            return bestRouteIndex;
        }

        private void CommitRoute(RouteDefinition route)
        {
            if (route == null || activeBypass == null)
            {
                CancelTraversal();
                return;
            }

            Collider2D[] routeColliders = route.BypassColliders;
            for (int index = 0; index < routeColliders.Length; index++)
            {
                Collider2D targetCollider = routeColliders[index];
                if (targetCollider == null || ContainsBypassedCollider(targetCollider))
                {
                    continue;
                }

                if (activeBypass.BeginBypass(targetCollider))
                {
                    bypassedColliders.Add(targetCollider);
                }
            }

            isCommitted = bypassedColliders.Count > 0;
            committedRoute = route.Direction;
        }

        private Vector2 GetDirectionVector(RouteDirection direction)
        {
            switch (direction)
            {
                case RouteDirection.Up:
                    return Vector2.up;
                case RouteDirection.Down:
                    return Vector2.down;
                case RouteDirection.Left:
                    return Vector2.left;
                case RouteDirection.Right:
                    return Vector2.right;
                case RouteDirection.CustomPositive:
                    return customPositiveDirection;
                case RouteDirection.CustomNegative:
                    return customNegativeDirection;
                default:
                    return Vector2.zero;
            }
        }

        private void HandleTraversalInterrupted()
        {
            CancelTraversal();
        }

        private void HandleBypassesCleared()
        {
            CancelTraversal();
        }

        private void CancelTraversal()
        {
            if (activeBypass != null)
            {
                for (int index = 0; index < bypassedColliders.Count; index++)
                {
                    activeBypass.EndBypass(bypassedColliders[index]);
                }
            }

            bypassedColliders.Clear();
            playerContacts.Clear();
            UnsubscribeResetEvents();
            activeBypass = null;
            activeHealth = null;
            activeRespawn = null;
            isEligible = false;
            isCommitted = false;
        }

        private bool ContainsContact(Collider2D candidate)
        {
            for (int index = 0; index < playerContacts.Count; index++)
            {
                if (playerContacts[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveContact(Collider2D candidate)
        {
            for (int index = playerContacts.Count - 1; index >= 0; index--)
            {
                if (playerContacts[index] == candidate || playerContacts[index] == null)
                {
                    playerContacts.RemoveAt(index);
                }
            }
        }

        private bool ContainsBypassedCollider(Collider2D candidate)
        {
            for (int index = 0; index < bypassedColliders.Count; index++)
            {
                if (bypassedColliders[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void UnsubscribeResetEvents()
        {
            if (activeHealth != null)
            {
                activeHealth.Died -= HandleTraversalInterrupted;
                activeHealth.Respawned -= HandleTraversalInterrupted;
            }

            if (activeRespawn != null)
            {
                activeRespawn.Respawned -= HandleTraversalInterrupted;
            }

            if (activeBypass != null)
            {
                activeBypass.BypassesCleared -= HandleBypassesCleared;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider == null)
            {
                return;
            }

            Bounds bounds = triggerCollider.bounds;
            Gizmos.color = isCommitted ? Color.cyan : Color.yellow;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (isCommitted)
            {
                Gizmos.DrawRay(bounds.center, GetDirectionVector(committedRoute).normalized * 1.2f);
            }
        }
    }
}
