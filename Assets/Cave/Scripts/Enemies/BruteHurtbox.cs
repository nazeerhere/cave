using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Brute-only prototype receiver. Its trigger is intentionally separate
    /// from the physical body; all damage authority remains on the root
    /// Damageable resolved by existing combat code through GetComponentInParent.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BruteHurtbox : MonoBehaviour
    {
        [SerializeField] private Damageable owner;
        [SerializeField] private Collider2D hurtboxCollider;

        public Damageable Owner => owner;
        public Collider2D Collider => hurtboxCollider;

        public void Configure(Damageable damageable, Collider2D triggerCollider)
        {
            owner = damageable;
            hurtboxCollider = triggerCollider;
            if (hurtboxCollider != null)
            {
                hurtboxCollider.isTrigger = true;
            }
        }

        public void SetDamageReceptionActive(bool active)
        {
            if (hurtboxCollider != null)
            {
                hurtboxCollider.enabled = active;
            }
        }

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponentInParent<Damageable>();
            }

            if (hurtboxCollider == null)
            {
                hurtboxCollider = GetComponent<Collider2D>();
            }
        }

        private void OnValidate()
        {
            if (hurtboxCollider == null)
            {
                hurtboxCollider = GetComponent<Collider2D>();
            }

            if (hurtboxCollider != null)
            {
                hurtboxCollider.isTrigger = true;
            }
        }
    }
}
