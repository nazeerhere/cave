using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Short-lived local sighting shares from Eyes and possessed hosts.</summary>
    public static class EyeWatcherNetwork
    {
        private struct Sighting
        {
            public Vector2 Position;
            public float ExpiresAt;
            public float Radius;
            public Object Source;
            public BattlefieldSignal Signals;
        }

        private static readonly List<Sighting> Sightings = new List<Sighting>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Sightings.Clear();
        }

        public static void Broadcast(Object source, Vector2 position, float radius, float memoryDuration)
        {
            Broadcast(source, position, radius, memoryDuration, BattlefieldSignal.None);
        }

        public static void Broadcast(
            Object source,
            Vector2 position,
            float radius,
            float memoryDuration,
            BattlefieldSignal signals)
        {
            if (source == null || radius <= 0f)
            {
                return;
            }

            for (int index = Sightings.Count - 1; index >= 0; index--)
            {
                if (Sightings[index].Source == null)
                {
                    Sightings.RemoveAt(index);
                    continue;
                }

                if (Sightings[index].Source == source)
                {
                    Sightings[index] = new Sighting
                    {
                        Source = source,
                        Position = position,
                        Radius = radius,
                        ExpiresAt = Time.time + memoryDuration,
                        Signals = signals
                    };
                    return;
                }
            }

            Sightings.Add(new Sighting
            {
                Source = source,
                Position = position,
                Radius = radius,
                ExpiresAt = Time.time + memoryDuration,
                Signals = signals
            });
        }

        public static void Remove(Object source)
        {
            for (int index = Sightings.Count - 1; index >= 0; index--)
            {
                if (Sightings[index].Source == null || Sightings[index].Source == source)
                {
                    Sightings.RemoveAt(index);
                }
            }
        }

        public static bool TryGetSharedLocation(Vector2 recipientPosition, out Vector2 location)
        {
            for (int index = Sightings.Count - 1; index >= 0; index--)
            {
                Sighting sighting = Sightings[index];
                if (sighting.Source == null || Time.time > sighting.ExpiresAt)
                {
                    Sightings.RemoveAt(index);
                    continue;
                }

                if ((recipientPosition - sighting.Position).sqrMagnitude <= sighting.Radius * sighting.Radius)
                {
                    location = sighting.Position;
                    return true;
                }
            }

            location = default;
            return false;
        }

        public static bool TryGetSharedContext(Vector2 recipientPosition, out BattlefieldSignal signals)
        {
            signals = BattlefieldSignal.None;
            bool found = false;
            for (int index = Sightings.Count - 1; index >= 0; index--)
            {
                Sighting sighting = Sightings[index];
                if (sighting.Source == null || Time.time > sighting.ExpiresAt)
                {
                    Sightings.RemoveAt(index);
                    continue;
                }

                if ((recipientPosition - sighting.Position).sqrMagnitude <= sighting.Radius * sighting.Radius)
                {
                    signals |= sighting.Signals;
                    found = true;
                }
            }

            return found;
        }
    }
}
