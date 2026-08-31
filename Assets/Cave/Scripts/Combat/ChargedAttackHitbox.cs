using UnityEngine;

namespace Cave.Combat
{
    public sealed class ChargedAttackHitbox : MonoBehaviour
    {
        [SerializeField] private ChargedAttack attack;

        private void Awake()
        {
            ResolveAttackOwner();
        }

        private void OnEnable()
        {
            ResolveAttackOwner();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (attack != null)
            {
                attack.HandleHit(other);
            }
        }

        private void ResolveAttackOwner()
        {
            if (attack == null)
            {
                attack = GetComponentInParent<ChargedAttack>();
            }
        }
    }
}
