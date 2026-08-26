using System.Collections;
using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerPoisonStatus : MonoBehaviour
    {
        [SerializeField] private Color poisonColor = new Color(0.45f, 1f, 0.2f, 0.8f);
        [SerializeField, Min(0.1f)] private float visualRadius = 0.58f;

        private PlayerHealth playerHealth;
        private Coroutine poisonRoutine;
        private LineRenderer poisonVisual;
        private Material poisonMaterial;

        public bool IsPoisoned => poisonRoutine != null;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerHealth.Died += ClearPoison;
        }

        public void ApplyPoison(
            int damagePerTick,
            float tickInterval,
            float duration,
            GameObject source)
        {
            if (damagePerTick <= 0 || tickInterval <= 0f || duration <= 0f)
            {
                return;
            }

            if (poisonRoutine != null)
            {
                StopCoroutine(poisonRoutine);
            }

            EnsureVisual();
            poisonVisual.enabled = true;
            poisonRoutine = StartCoroutine(PoisonRoutine(
                damagePerTick,
                tickInterval,
                duration,
                source));
        }

        private IEnumerator PoisonRoutine(
            int damagePerTick,
            float tickInterval,
            float duration,
            GameObject source)
        {
            float expiresAt = Time.time + duration;
            WaitForSeconds delay = new WaitForSeconds(tickInterval);
            while (Time.time + tickInterval <= expiresAt + Mathf.Epsilon)
            {
                yield return delay;
                playerHealth.TryTakeDamage(
                    damagePerTick,
                    new DamageContext(source, DamageTrait.AreaOfEffect));
            }

            poisonRoutine = null;
            poisonVisual.enabled = false;
        }

        private void EnsureVisual()
        {
            if (poisonVisual != null)
            {
                return;
            }

            GameObject visualObject = new GameObject("Poison Status VFX");
            visualObject.transform.SetParent(transform, false);
            poisonVisual = visualObject.AddComponent<LineRenderer>();
            poisonMaterial = new Material(Shader.Find("Sprites/Default"));
            poisonVisual.material = poisonMaterial;
            poisonVisual.useWorldSpace = false;
            poisonVisual.loop = true;
            poisonVisual.positionCount = 32;
            poisonVisual.startWidth = 0.055f;
            poisonVisual.endWidth = 0.055f;
            poisonVisual.startColor = poisonColor;
            poisonVisual.endColor = poisonColor;
            poisonVisual.sortingOrder = 10;
            for (int index = 0; index < poisonVisual.positionCount; index++)
            {
                float angle = index / (float)poisonVisual.positionCount * Mathf.PI * 2f;
                poisonVisual.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * visualRadius);
            }
        }

        private void ClearPoison()
        {
            if (poisonRoutine != null)
            {
                StopCoroutine(poisonRoutine);
                poisonRoutine = null;
            }

            if (poisonVisual != null)
            {
                poisonVisual.enabled = false;
            }
        }

        private void OnDisable()
        {
            ClearPoison();
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= ClearPoison;
            }

            if (poisonMaterial != null)
            {
                Destroy(poisonMaterial);
            }
        }
    }
}
