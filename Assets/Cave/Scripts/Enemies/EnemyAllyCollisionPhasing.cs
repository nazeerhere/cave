using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyAllyCollisionPhasing : MonoBehaviour
    {
        private struct ColliderPair
        {
            public Collider2D Own;
            public Collider2D Ally;
        }

        [Header("Current State (Read Only)")]
        [SerializeField] private bool allyPhasingActive;

        private readonly List<ColliderPair> changedPairs = new List<ColliderPair>();

        public bool IsAllyPhasingActive => allyPhasingActive;

        public void SetAllyPhasing(bool active)
        {
            if (allyPhasingActive == active)
            {
                return;
            }

            allyPhasingActive = active;
            if (active)
            {
                IgnoreCurrentAllies();
            }
            else
            {
                RestoreChangedPairs();
            }
        }

        private void IgnoreCurrentAllies()
        {
            Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(true);
            foreach (EnemyArchetypeProfile ally in FindObjectsOfType<EnemyArchetypeProfile>())
            {
                if (ally == null || ally.gameObject == gameObject)
                {
                    continue;
                }

                IgnorePairs(ownColliders, ally.GetComponentsInChildren<Collider2D>(true));
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!allyPhasingActive || collision.collider == null)
            {
                return;
            }

            EnemyArchetypeProfile ally =
                collision.collider.GetComponentInParent<EnemyArchetypeProfile>();
            if (ally != null && ally.gameObject != gameObject)
            {
                IgnorePairs(
                    GetComponentsInChildren<Collider2D>(true),
                    ally.GetComponentsInChildren<Collider2D>(true));
            }
        }

        private void IgnorePairs(Collider2D[] ownColliders, Collider2D[] allyColliders)
        {
            foreach (Collider2D ownCollider in ownColliders)
            {
                if (ownCollider == null)
                {
                    continue;
                }

                foreach (Collider2D allyCollider in allyColliders)
                {
                    if (allyCollider == null
                        || allyCollider == ownCollider
                        || Physics2D.GetIgnoreCollision(ownCollider, allyCollider))
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(ownCollider, allyCollider, true);
                    changedPairs.Add(new ColliderPair
                    {
                        Own = ownCollider,
                        Ally = allyCollider
                    });
                }
            }
        }

        private void RestoreChangedPairs()
        {
            foreach (ColliderPair pair in changedPairs)
            {
                if (pair.Own != null && pair.Ally != null)
                {
                    Physics2D.IgnoreCollision(pair.Own, pair.Ally, false);
                }
            }

            changedPairs.Clear();
        }

        private void OnDisable()
        {
            allyPhasingActive = false;
            RestoreChangedPairs();
        }

        private void OnDestroy()
        {
            RestoreChangedPairs();
        }
    }
}
