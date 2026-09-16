using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// Lightweight, altar-owned targeting preference used by Paranoia.  It has
    /// no Update loop: an existing attack capability asks for a target only when
    /// it is already ready to act, and expiry is checked at that point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurseAltarConfusion : MonoBehaviour
    {
        private CurseAltarZone ownerZone;
        private Damageable ownerDamageable;
        private float expiresAt;

        public bool Apply(CurseAltarZone zone, float duration)
        {
            if (zone == null || duration <= 0f)
            {
                return false;
            }

            bool changedOwner = ownerZone != zone;
            if (changedOwner)
            {
                ownerZone?.UnregisterParanoia(this);
                ownerZone = zone;
            }

            expiresAt = Mathf.Max(expiresAt, Time.time + duration);
            CancelInvoke(nameof(Expire));
            Invoke(nameof(Expire), Mathf.Max(0.01f, expiresAt - Time.time));
            return changedOwner || expiresAt > Time.time;
        }

        public bool TryGetPreferredTarget(out Transform target)
        {
            target = null;
            if (ownerZone == null || Time.time >= expiresAt || !ownerZone.IsActive)
            {
                ClearFromZone(ownerZone);
                return false;
            }

            if (ownerDamageable == null)
            {
                ownerDamageable = GetComponent<Damageable>();
            }

            return ownerDamageable != null
                && ownerZone.TryGetNearestConfusionTarget(ownerDamageable, out target);
        }

        internal void ClearFromZone(CurseAltarZone zone)
        {
            if (zone != null && ownerZone != zone)
            {
                return;
            }

            ownerZone = null;
            expiresAt = 0f;
            CancelInvoke(nameof(Expire));
        }

        private void Expire()
        {
            CurseAltarZone expiredZone = ownerZone;
            ownerZone = null;
            expiresAt = 0f;
            expiredZone?.UnregisterParanoia(this);
        }

        private void OnDisable()
        {
            ownerZone?.UnregisterParanoia(this);
            ownerZone = null;
            expiresAt = 0f;
            CancelInvoke(nameof(Expire));
        }
    }
}
