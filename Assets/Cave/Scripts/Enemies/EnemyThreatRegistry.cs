using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Event-driven registry used by passive presentation. Registration is
    /// intentionally broad; consumers apply the authoritative hostile filter.
    /// </summary>
    public static class EnemyThreatRegistry
    {
        private static readonly HashSet<Damageable> ActiveDamageables =
            new HashSet<Damageable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            ActiveDamageables.Clear();
        }

        public static void Register(Damageable damageable)
        {
            if (damageable != null)
            {
                ActiveDamageables.Add(damageable);
            }
        }

        public static void Unregister(Damageable damageable)
        {
            if (damageable != null)
            {
                ActiveDamageables.Remove(damageable);
            }
        }

        public static int CopyActive(Damageable[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (Damageable damageable in ActiveDamageables)
            {
                if (damageable == null)
                {
                    continue;
                }

                buffer[count++] = damageable;
                if (count >= buffer.Length)
                {
                    break;
                }
            }

            return count;
        }
    }
}
