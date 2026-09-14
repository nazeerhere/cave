using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public sealed class EnemyPoisonZone : MonoBehaviour
    {
        private GameObject source;
        private float radius;
        private float poisonDamagePercentPerSecond;
        private int minimumTickDamage;
        private int maximumTickDamage;
        private float tickInterval;
        private float expiresAt;
        private bool appliedToPlayer;
        private PlayerHealth player;

        public static void Create(
            Vector2 position,
            GameObject damageSource,
            float zoneRadius,
            float zoneDuration,
            float damagePercentPerSecond,
            int requestedMinimumTickDamage,
            int requestedMaximumTickDamage,
            float poisonTickInterval,
            Color color)
        {
            GameObject zoneObject = new GameObject("Evolved Poison Zone");
            zoneObject.transform.position = position;
            EnemyPoisonZone zone = zoneObject.AddComponent<EnemyPoisonZone>();
            zone.source = damageSource;
            zone.radius = Mathf.Max(0.1f, zoneRadius);
            zone.poisonDamagePercentPerSecond = Mathf.Max(0f, damagePercentPerSecond);
            zone.minimumTickDamage = Mathf.Max(1, requestedMinimumTickDamage);
            zone.maximumTickDamage = Mathf.Max(zone.minimumTickDamage, requestedMaximumTickDamage);
            zone.tickInterval = Mathf.Max(0.05f, poisonTickInterval);
            zone.expiresAt = Time.time + Mathf.Max(0.1f, zoneDuration);
            // Zones resolve the single player target once at creation, rather than
            // scanning the scene during their lifetime.
            zone.player = Object.FindObjectOfType<PlayerHealth>();
            Cave.Combat.AreaPulseEffect.Create(position, zone.radius, color, 0.35f);
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            if (appliedToPlayer)
            {
                return;
            }

            if (player == null
                || ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude
                    > radius * radius)
            {
                return;
            }

            PlayerPoisonStatus poison = player.GetComponent<PlayerPoisonStatus>();
            if (poison == null)
            {
                poison = player.gameObject.AddComponent<PlayerPoisonStatus>();
            }

            poison.ApplyPoison(
                poisonDamagePercentPerSecond,
                minimumTickDamage,
                maximumTickDamage,
                tickInterval,
                Mathf.Max(tickInterval, expiresAt - Time.time),
                source);
            appliedToPlayer = true;
        }
    }
}
