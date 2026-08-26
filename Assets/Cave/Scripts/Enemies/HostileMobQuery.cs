using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public static class HostileMobQuery
    {
        public static int CountRealHostiles(Vector2 center, float radius, List<Damageable> results = null)
        {
            int count = 0;
            float radiusSquared = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            if (results != null)
            {
                results.Clear();
            }

            foreach (Damageable candidate in Object.FindObjectsOfType<Damageable>())
            {
                if (!IsRealHostile(candidate)
                    || ((Vector2)candidate.transform.position - center).sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                count++;
                results?.Add(candidate);
            }

            return count;
        }

        public static bool IsRealHostile(Damageable candidate)
        {
            if (candidate == null
                || candidate.CurrentHealth <= 0
                || !candidate.gameObject.activeInHierarchy
                || candidate.GetComponent<PlayerHealth>() != null
                || candidate.GetComponent<DetectiveTower>() != null)
            {
                return false;
            }

            DetectiveIdentity detective = candidate.GetComponent<DetectiveIdentity>();
            if (detective != null && detective.IsClone)
            {
                return false;
            }

            return candidate.GetComponent<EnemyController>() != null
                || candidate.GetComponent<FlyingSwarmController>() != null
                || candidate.GetComponent<MobBrainBase>() != null
                || candidate.GetComponent<EnemyArchetypeProfile>() != null
                || candidate.GetComponentInChildren<EnemyContactDamage>(true) != null;
        }
    }
}
