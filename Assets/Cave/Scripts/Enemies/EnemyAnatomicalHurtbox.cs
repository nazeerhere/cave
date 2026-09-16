using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    public enum EnemyHurtboxRegion
    {
        Lower,
        Middle,
        Upper
    }

    /// <summary>
    /// Trigger-only incoming-damage region. Existing attack code resolves the
    /// root health authority through GetComponentInParent&lt;Damageable&gt;(), so
    /// this component deliberately owns neither health nor collision response.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class EnemyAnatomicalHurtbox : MonoBehaviour
    {
        [SerializeField] private EnemyHurtboxRegion region;
        [SerializeField] private Damageable owner;
        [SerializeField] private Collider2D triggerCollider;

        public EnemyHurtboxRegion Region => region;
        public Damageable Owner => owner;
        public Collider2D Collider => triggerCollider;

        public void Configure(
            EnemyHurtboxRegion configuredRegion,
            Damageable configuredOwner,
            Collider2D configuredCollider)
        {
            region = configuredRegion;
            owner = configuredOwner;
            triggerCollider = configuredCollider;
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        public void SetDamageReceptionActive(bool active)
        {
            if (triggerCollider != null)
            {
                triggerCollider.enabled = active;
            }
        }

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponentInParent<Damageable>();
            }

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }
        }

        private void OnValidate()
        {
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }
    }
}
