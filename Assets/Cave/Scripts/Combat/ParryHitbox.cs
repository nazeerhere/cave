using UnityEngine;

namespace Cave.Combat
{
    public sealed class ParryHitbox : MonoBehaviour
    {
        [SerializeField] private SidewaysParryAttack attack;

        public bool TryParry(IPlayerParryableProjectile projectile)
        {
            return attack.TryParry(projectile);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            IPlayerParryableProjectile projectile = other.GetComponent<IPlayerParryableProjectile>();
            if (projectile != null)
            {
                TryParry(projectile);
            }
        }
    }
}
