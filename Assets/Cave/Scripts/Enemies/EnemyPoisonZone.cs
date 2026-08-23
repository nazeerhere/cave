using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public sealed class EnemyPoisonZone : MonoBehaviour
    {
        private GameObject source;
        private float radius;
        private int tickDamage;
        private float tickInterval;
        private float expiresAt;
        private bool appliedToPlayer;

        public static void Create(
            Vector2 position,
            GameObject damageSource,
            float zoneRadius,
            float zoneDuration,
            int poisonDamage,
            float poisonTickInterval,
            Color color)
        {
            GameObject zoneObject = new GameObject("Evolved Poison Zone");
            zoneObject.transform.position = position;
            EnemyPoisonZone zone = zoneObject.AddComponent<EnemyPoisonZone>();
            zone.source = damageSource;
            zone.radius = Mathf.Max(0.1f, zoneRadius);
            zone.tickDamage = Mathf.Max(1, poisonDamage);
            zone.tickInterval = Mathf.Max(0.05f, poisonTickInterval);
            zone.expiresAt = Time.time + Mathf.Max(0.1f, zoneDuration);
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

            PlayerHealth player = FindObjectOfType<PlayerHealth>();
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
                tickDamage,
                tickInterval,
                Mathf.Max(tickInterval, expiresAt - Time.time),
                source);
            appliedToPlayer = true;
        }
    }
}
