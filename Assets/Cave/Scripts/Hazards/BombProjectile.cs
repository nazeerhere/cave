using System;
using System.Collections;
using UnityEngine;

namespace Cave.Hazards
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class BombProjectile : MonoBehaviour
    {
        [SerializeField] private BombExplosion explosionPrefab;
        [SerializeField] private LayerMask environmentLayers;
        [SerializeField, Min(0.1f)] private float maximumLifetime = 8f;
        [SerializeField, Min(0f)] private float explosionDelay = 0.05f;

        private Rigidbody2D body;
        private Collider2D bombCollider;
        private HazardWarning warning;
        private Action completionCallback;
        private bool explosionStarted;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bombCollider = GetComponent<Collider2D>();
        }

        public void Initialize(HazardWarning activeWarning, Action onComplete)
        {
            warning = activeWarning;
            completionCallback = onComplete;
            StartCoroutine(MaximumLifetimeCountdown());
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if ((environmentLayers.value & (1 << collision.gameObject.layer)) != 0)
            {
                BeginExplosion();
            }
        }

        private IEnumerator MaximumLifetimeCountdown()
        {
            yield return new WaitForSeconds(maximumLifetime);
            BeginExplosion();
        }

        private void BeginExplosion()
        {
            if (explosionStarted)
            {
                return;
            }

            explosionStarted = true;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            bombCollider.enabled = false;
            StartCoroutine(ExplodeAfterDelay());
        }

        private IEnumerator ExplodeAfterDelay()
        {
            if (explosionDelay > 0f)
            {
                yield return new WaitForSeconds(explosionDelay);
            }

            BombExplosion explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity, transform.parent);
            explosion.Initialize(warning, completionCallback);
            Destroy(gameObject);
        }
    }
}
