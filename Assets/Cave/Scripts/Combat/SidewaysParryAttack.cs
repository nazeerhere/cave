using System.Collections;
using System.Collections.Generic;
using Cave.InputSystem;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Combat
{
    public sealed class SidewaysParryAttack : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float activeDuration = 0.2f;
        [SerializeField, Min(0f)] private float cooldown = 0.45f;
        [SerializeField, Min(1f)] private float reflectionSpeedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float horizontalOffset = 1.1f;

        [Header("References")]
        [SerializeField] private Transform parryTransform;
        [SerializeField] private GameObject parryVisual;
        [SerializeField] private Collider2D parryCollider;

        private readonly HashSet<FireballProjectile> parriedProjectiles = new HashSet<FireballProjectile>();
        private float facingDirection = 1f;
        private float nextAttackTime;
        private bool isActive;

        private void Awake()
        {
            SetParryActive(false);
        }

        private void Update()
        {
            float horizontalInput = GameInput.Horizontal;
            if (!Mathf.Approximately(horizontalInput, 0f))
            {
                facingDirection = Mathf.Sign(horizontalInput);
            }

            if (GameInput.ParryPressed && !isActive && Time.time >= nextAttackTime)
            {
                StartCoroutine(PerformParry());
            }
        }

        public bool TryParry(FireballProjectile projectile)
        {
            if (!isActive || projectile == null || !projectile.CanBeParried || !parriedProjectiles.Add(projectile))
            {
                return false;
            }

            Vector2 fallbackDirection = new Vector2(facingDirection, 0f);
            return projectile.Parry(gameObject, fallbackDirection, reflectionSpeedMultiplier);
        }

        private IEnumerator PerformParry()
        {
            isActive = true;
            nextAttackTime = Time.time + activeDuration + cooldown;
            parriedProjectiles.Clear();
            parryTransform.localPosition = new Vector3(facingDirection * horizontalOffset, 0f, 0f);
            SetParryActive(true);

            yield return new WaitForSeconds(activeDuration);

            SetParryActive(false);
            isActive = false;
        }

        private void SetParryActive(bool active)
        {
            if (parryVisual != null)
            {
                parryVisual.SetActive(active);
            }

            if (parryCollider != null)
            {
                parryCollider.enabled = active;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isActive = false;
            SetParryActive(false);
        }
    }
}
