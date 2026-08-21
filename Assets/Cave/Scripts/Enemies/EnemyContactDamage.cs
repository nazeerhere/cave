using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public sealed class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(1)] private int contactDamage = 1;
        [SerializeField, Min(0f)] private float contactCooldown = 0.75f;

        private float nextDamageTime;

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (Time.time < nextDamageTime)
            {
                return;
            }

            PlayerHealth playerHealth = collision.collider.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && playerHealth.TryTakeDamage(contactDamage))
            {
                nextDamageTime = Time.time + contactCooldown;
            }
        }
    }
}
