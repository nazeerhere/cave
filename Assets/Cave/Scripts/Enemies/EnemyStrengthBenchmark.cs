using Cave.Combat;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Enemies
{
    public static class EnemyStrengthBenchmark
    {
        public static float Resolve(GameObject source)
        {
            if (source == null)
            {
                return 1f;
            }

            FireballProjectile projectile = source.GetComponent<FireballProjectile>();
            if (projectile != null && projectile.CurrentOwner != null)
            {
                source = projectile.CurrentOwner;
            }

            Damageable damageable = source.GetComponentInParent<Damageable>();
            GameObject root = damageable != null ? damageable.gameObject : source;
            EnemyDamageModifiers modifiers = root.GetComponent<EnemyDamageModifiers>();
            float damageStrength = modifiers != null ? modifiers.CurrentDamageMultiplier : 1f;
            float healthStrength = damageable != null
                ? Mathf.Sqrt(
                    damageable.MaximumHealth
                    / (float)Mathf.Max(1, damageable.BaseMaximumHealth))
                : 1f;
            EnemyTank tank = root.GetComponent<EnemyTank>();
            float authoredStrength = tank != null
                ? Mathf.Sqrt(Mathf.Max(1f, tank.DamageMultiplier))
                : 1f;
            return Mathf.Max(1f, damageStrength * healthStrength * authoredStrength);
        }
    }
}
