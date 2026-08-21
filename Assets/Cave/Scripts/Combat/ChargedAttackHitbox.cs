using UnityEngine;

namespace Cave.Combat
{
    public sealed class ChargedAttackHitbox : MonoBehaviour
    {
        [SerializeField] private ChargedAttack attack;

        private void OnTriggerEnter2D(Collider2D other)
        {
            attack.HandleHit(other);
        }
    }
}
