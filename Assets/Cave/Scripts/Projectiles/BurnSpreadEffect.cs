using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Projectiles
{
    public static class BurnSpreadEffect
    {
        public static void Spread(
            Damageable source,
            float radius,
            int maximumTargets,
            int damagePerTick,
            float tickInterval,
            float duration,
            DamageContext damageContext,
            LayerMask damageableLayers)
        {
            if (source == null || radius <= 0f || maximumTargets <= 0)
            {
                return;
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                source.transform.position,
                radius,
                damageableLayers);
            HashSet<Damageable> affected = new HashSet<Damageable>();
            int appliedCount = 0;
            foreach (Collider2D overlap in overlaps)
            {
                Damageable target = overlap.GetComponentInParent<Damageable>();
                if (target == null
                    || target == source
                    || target.CurrentHealth <= 0
                    || !affected.Add(target))
                {
                    continue;
                }

                EnemyStatusEffects statusEffects = target.GetComponent<EnemyStatusEffects>();
                if (statusEffects == null)
                {
                    continue;
                }

                statusEffects.ApplyBurn(damagePerTick, tickInterval, duration, damageContext);
                appliedCount++;
                if (appliedCount >= maximumTargets)
                {
                    break;
                }
            }
        }
    }
}
