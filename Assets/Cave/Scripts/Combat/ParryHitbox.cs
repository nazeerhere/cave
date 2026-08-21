using Cave.Projectiles;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class ParryHitbox : MonoBehaviour
    {
        [SerializeField] private SidewaysParryAttack attack;

        public bool TryParry(FireballProjectile projectile)
        {
            return attack.TryParry(projectile);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            FireballProjectile projectile = other.GetComponent<FireballProjectile>();
            if (projectile != null)
            {
                TryParry(projectile);
            }
        }
    }
}
