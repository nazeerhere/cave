using System;
using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Hazards
{
    public sealed class BombExplosion : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float explosionRadius = 2f;
        [SerializeField, Min(1)] private int explosionDamage = 1;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float visualLifetime = 0.6f;
        [SerializeField] private LayerMask playerLayers = 1;

        private Action completionCallback;
        private HazardWarning warning;

        public void Initialize(HazardWarning activeWarning, Action onComplete)
        {
            warning = activeWarning;
            completionCallback = onComplete;
            StartCoroutine(ExplosionLifecycle());
        }

        private IEnumerator ExplosionLifecycle()
        {
            DamagePlayersOnce();
            yield return new WaitForSeconds(activeDuration);

            float remainingVisualTime = Mathf.Max(0f, visualLifetime - activeDuration);
            if (remainingVisualTime > 0f)
            {
                yield return new WaitForSeconds(remainingVisualTime);
            }

            if (warning != null)
            {
                Destroy(warning.gameObject);
            }

            completionCallback?.Invoke();
            Destroy(gameObject);
        }

        private void DamagePlayersOnce()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, playerLayers);
            HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();

            foreach (Collider2D hit in hits)
            {
                PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null && damagedPlayers.Add(playerHealth))
                {
                    DamageContext context = new DamageContext(
                        gameObject,
                        DamageTrait.Direct | DamageTrait.AreaOfEffect);
                    playerHealth.TryTakeDamage(explosionDamage, context);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
