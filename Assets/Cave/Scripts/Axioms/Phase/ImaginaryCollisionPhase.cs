using System.Collections.Generic;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Axioms.Phase
{
    /// <summary>Temporarily ignores only player-body to enemy-body collider pairs.</summary>
    [DisallowMultipleComponent]
    public sealed class ImaginaryCollisionPhase : MonoBehaviour
    {
        private readonly HashSet<ColliderPair> ignoredPairs = new HashSet<ColliderPair>();
        private PhaseCombatState phaseState;
        private float nextRefreshAt;

        private void Awake() { phaseState = GetComponent<PhaseCombatState>(); }

        private void Update()
        {
            if (phaseState != null && phaseState.HasImaginaryActive)
            {
                if (Time.time >= nextRefreshAt)
                {
                    RefreshPairs();
                    nextRefreshAt = Time.time + 0.15f;
                }
                return;
            }

            RestorePairs();
        }

        public void SetImaginaryActive(bool active)
        {
            if (active)
            {
                RefreshPairs();
                nextRefreshAt = Time.time + 0.15f;
            }
            else RestorePairs();
        }

        private void RefreshPairs()
        {
            Collider2D[] playerBodies = GetComponentsInChildren<Collider2D>(true);
            EnemyArchetypeProfile[] enemies = FindObjectsOfType<EnemyArchetypeProfile>();
            for (int enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
            {
                EnemyArchetypeProfile enemy = enemies[enemyIndex];
                if (enemy == null || enemy.gameObject == gameObject || !enemy.gameObject.activeInHierarchy) continue;
                Collider2D[] enemyBodies = enemy.GetComponentsInChildren<Collider2D>(true);
                for (int playerIndex = 0; playerIndex < playerBodies.Length; playerIndex++)
                {
                    Collider2D playerBody = playerBodies[playerIndex];
                    if (!IsBodyCollider(playerBody)) continue;
                    for (int bodyIndex = 0; bodyIndex < enemyBodies.Length; bodyIndex++)
                    {
                        Collider2D enemyBody = enemyBodies[bodyIndex];
                        if (!IsBodyCollider(enemyBody)) continue;
                        ColliderPair pair = new ColliderPair(playerBody, enemyBody);
                        if (ignoredPairs.Add(pair)) Physics2D.IgnoreCollision(playerBody, enemyBody, true);
                    }
                }
            }
        }

        private void RestorePairs()
        {
            foreach (ColliderPair pair in ignoredPairs)
            {
                if (pair.First != null && pair.Second != null) Physics2D.IgnoreCollision(pair.First, pair.Second, false);
            }
            ignoredPairs.Clear();
        }

        private static bool IsBodyCollider(Collider2D collider) => collider != null && collider.enabled && !collider.isTrigger;
        private void OnDisable() { RestorePairs(); }
        private void OnDestroy() { RestorePairs(); }

        private struct ColliderPair
        {
            public ColliderPair(Collider2D first, Collider2D second) { First = first; Second = second; }
            public Collider2D First { get; }
            public Collider2D Second { get; }
        }
    }
}
