using System;
using UnityEngine;

namespace Cave.Pickups
{
    [Serializable]
    public sealed class DropEntry
    {
        [SerializeField] private PickupBase pickupPrefab;
        [SerializeField, Range(0f, 1f)] private float chance;

        public PickupBase PickupPrefab => pickupPrefab;
        public float Chance => chance;

        public DropEntry(PickupBase pickupPrefab, float chance)
        {
            this.pickupPrefab = pickupPrefab;
            this.chance = Mathf.Clamp01(chance);
        }
    }
}
