using System.Collections.Generic;
using UnityEngine;

namespace Cave.Environmental
{
    /// <summary>
    /// Temporary, source-owned environmental reinforcement. It only reduces
    /// incoming structural impact to an already-existing destructible and never
    /// creates, restores, or revives world geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentalReinforcement : MonoBehaviour
    {
        private readonly Dictionary<int, Reinforcement> activeSources = new Dictionary<int, Reinforcement>();
        private readonly List<int> expiredSourceIds = new List<int>();

        public float IncomingImpactMultiplier
        {
            get
            {
                PruneExpired();
                float multiplier = 1f;
                foreach (Reinforcement source in activeSources.Values)
                {
                    multiplier = Mathf.Min(multiplier, source.impactMultiplier);
                }

                return multiplier;
            }
        }

        public bool Apply(Object source, float duration, float impactMultiplier)
        {
            if (source == null || duration <= 0f)
            {
                return false;
            }

            int id = source.GetInstanceID();
            Reinforcement next = new Reinforcement
            {
                expiresAt = Time.time + duration,
                impactMultiplier = Mathf.Clamp01(impactMultiplier)
            };
            if (activeSources.TryGetValue(id, out Reinforcement previous))
            {
                next.expiresAt = Mathf.Max(previous.expiresAt, next.expiresAt);
                next.impactMultiplier = Mathf.Min(previous.impactMultiplier, next.impactMultiplier);
            }

            activeSources[id] = next;
            return true;
        }

        public void Release(Object source)
        {
            if (source != null)
            {
                activeSources.Remove(source.GetInstanceID());
            }
        }

        private void Update()
        {
            PruneExpired();
        }

        private void PruneExpired()
        {
            if (activeSources.Count == 0)
            {
                return;
            }

            expiredSourceIds.Clear();
            foreach (KeyValuePair<int, Reinforcement> pair in activeSources)
            {
                if (pair.Value.expiresAt > Time.time)
                {
                    continue;
                }

                expiredSourceIds.Add(pair.Key);
            }

            for (int index = 0; index < expiredSourceIds.Count; index++)
            {
                activeSources.Remove(expiredSourceIds[index]);
            }
        }

        private struct Reinforcement
        {
            public float expiresAt;
            public float impactMultiplier;
        }
    }
}
