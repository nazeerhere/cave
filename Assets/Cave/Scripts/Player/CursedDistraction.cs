using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class CursedDistraction : MonoBehaviour
    {
        private static readonly HashSet<CursedDistraction> ActiveDistractions =
            new HashSet<CursedDistraction>();
        private static readonly List<CursedDistraction> StaleDistractions =
            new List<CursedDistraction>();

        private GameObject owner;
        private float influenceRadius;
        private float attentionPriority;
        private float expiresAt;

        public float InfluenceRadius => influenceRadius;
        public float AttentionPriority => attentionPriority;

        public static CursedDistraction Create(
            GameObject source,
            Vector2 position,
            float lifetime,
            float radius,
            float priority)
        {
            GameObject distractionObject = new GameObject("Cursed Distraction");
            distractionObject.transform.position = position;
            CursedDistraction distraction = distractionObject.AddComponent<CursedDistraction>();
            distraction.owner = source;
            distraction.influenceRadius = Mathf.Max(0.1f, radius);
            distraction.attentionPriority = Mathf.Max(0f, priority);
            distraction.expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
            CombatShapeEffect.Create(
                position,
                CombatShape.Diamond,
                Mathf.Min(1f, distraction.influenceRadius * 0.18f),
                new Color(0.72f, 0.28f, 1f, 0.9f),
                Mathf.Max(0.2f, lifetime));
            return distraction;
        }

        public static bool TryGetInterest(
            MobBrainBase brain,
            out Vector2 interestPosition)
        {
            interestPosition = default;
            if (brain == null || brain.GetComponent<BossPhaseController>() != null)
            {
                return false;
            }

            RemoveStale();
            CursedDistraction selected = null;
            float selectedScore = float.NegativeInfinity;
            foreach (CursedDistraction distraction in ActiveDistractions)
            {
                if (distraction == null || distraction.owner == brain.gameObject)
                {
                    continue;
                }

                float distance = Vector2.Distance(
                    brain.transform.position,
                    distraction.transform.position);
                if (distance > distraction.influenceRadius)
                {
                    continue;
                }

                float score = distraction.attentionPriority
                    + 1f - distance / distraction.influenceRadius;
                if (score > selectedScore)
                {
                    selected = distraction;
                    selectedScore = score;
                }
            }

            if (selected == null)
            {
                return false;
            }

            interestPosition = selected.transform.position;
            return true;
        }

        public static void ClearOwnedBy(GameObject source)
        {
            if (source == null)
            {
                return;
            }

            RemoveStale();
            StaleDistractions.Clear();
            foreach (CursedDistraction distraction in ActiveDistractions)
            {
                if (distraction != null && distraction.owner == source)
                {
                    StaleDistractions.Add(distraction);
                }
            }

            foreach (CursedDistraction distraction in StaleDistractions)
            {
                if (distraction != null)
                {
                    Destroy(distraction.gameObject);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveDistractions.Clear();
            StaleDistractions.Clear();
        }

        private void OnEnable()
        {
            ActiveDistractions.Add(this);
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }

        private static void RemoveStale()
        {
            StaleDistractions.Clear();
            foreach (CursedDistraction distraction in ActiveDistractions)
            {
                if (distraction == null || !distraction.gameObject.activeInHierarchy)
                {
                    StaleDistractions.Add(distraction);
                }
            }

            foreach (CursedDistraction stale in StaleDistractions)
            {
                ActiveDistractions.Remove(stale);
            }
        }

        private void OnDisable()
        {
            ActiveDistractions.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveDistractions.Remove(this);
        }
    }
}
