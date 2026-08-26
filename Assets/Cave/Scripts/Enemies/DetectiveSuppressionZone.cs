using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class DetectiveSuppressionZone : MonoBehaviour
    {
        private readonly Dictionary<PlayerRecoveryModifiers, int> contacts =
            new Dictionary<PlayerRecoveryModifiers, int>();
        private readonly List<PlayerRecoveryModifiers> cleanupBuffer =
            new List<PlayerRecoveryModifiers>();
        private readonly Dictionary<PlayerRecoveryModifiers, float> nextPoisonTick =
            new Dictionary<PlayerRecoveryModifiers, float>();

        private GameObject damageSource;
        private float radius;
        private float expiresAt;
        private float healthMultiplier;
        private float staminaMultiplier;
        private float manaMultiplier;
        private int poisonDamage;
        private float poisonInterval;
        private LineRenderer boundary;
        private Material boundaryMaterial;
        private CircleCollider2D zoneCollider;
        private Color zoneColor;

        public void Initialize(
            GameObject source,
            float zoneRadius,
            float duration,
            float healthRegenerationMultiplier,
            float staminaRegenerationMultiplier,
            float manaRegenerationMultiplier,
            int poisonDamagePerTick,
            float poisonTickInterval,
            Color color)
        {
            damageSource = source;
            radius = Mathf.Max(0.1f, zoneRadius);
            expiresAt = Time.time + Mathf.Max(0.1f, duration);
            healthMultiplier = Mathf.Clamp01(healthRegenerationMultiplier);
            staminaMultiplier = Mathf.Clamp01(staminaRegenerationMultiplier);
            manaMultiplier = Mathf.Clamp01(manaRegenerationMultiplier);
            poisonDamage = Mathf.Max(1, poisonDamagePerTick);
            poisonInterval = Mathf.Max(0.1f, poisonTickInterval);
            zoneColor = color;
            EnsureCollider();
            EnsureBoundary();
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            Color pulsedColor = zoneColor;
            pulsedColor.a *= Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
            if (boundary != null)
            {
                boundary.startColor = pulsedColor;
                boundary.endColor = pulsedColor;
            }

            TickPoisonPressure();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null)
            {
                return;
            }

            PlayerRecoveryModifiers modifiers = player.GetComponent<PlayerRecoveryModifiers>();
            if (modifiers == null)
            {
                modifiers = player.gameObject.AddComponent<PlayerRecoveryModifiers>();
            }

            contacts.TryGetValue(modifiers, out int count);
            contacts[modifiers] = count + 1;
            modifiers.SetModifier(this, healthMultiplier, staminaMultiplier, manaMultiplier);
            if (count == 0)
            {
                nextPoisonTick[modifiers] = Time.time + poisonInterval;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerRecoveryModifiers modifiers = other.GetComponentInParent<PlayerRecoveryModifiers>();
            if (modifiers == null || !contacts.TryGetValue(modifiers, out int count))
            {
                return;
            }

            if (count > 1)
            {
                contacts[modifiers] = count - 1;
                return;
            }

            contacts.Remove(modifiers);
            nextPoisonTick.Remove(modifiers);
            modifiers.RemoveModifier(this);
        }

        private void TickPoisonPressure()
        {
            cleanupBuffer.Clear();
            cleanupBuffer.AddRange(contacts.Keys);
            foreach (PlayerRecoveryModifiers modifiers in cleanupBuffer)
            {
                if (modifiers == null || !modifiers.gameObject.activeInHierarchy)
                {
                    if (modifiers != null)
                    {
                        modifiers.RemoveModifier(this);
                    }

                    contacts.Remove(modifiers);
                    nextPoisonTick.Remove(modifiers);
                    continue;
                }

                if (!nextPoisonTick.TryGetValue(modifiers, out float nextTick)
                    || Time.time < nextTick)
                {
                    continue;
                }

                PlayerHealth player = modifiers.GetComponent<PlayerHealth>();
                if (player != null)
                {
                    player.TryTakeDamage(
                        poisonDamage,
                        new DamageContext(
                            damageSource != null ? damageSource : gameObject,
                            DamageTrait.AreaOfEffect));
                }

                nextPoisonTick[modifiers] = Time.time + poisonInterval;
            }

            cleanupBuffer.Clear();
        }

        private void EnsureCollider()
        {
            zoneCollider = GetComponent<CircleCollider2D>();
            if (zoneCollider == null)
            {
                zoneCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            zoneCollider.isTrigger = true;
            zoneCollider.radius = radius;
        }

        private void EnsureBoundary()
        {
            boundary = GetComponent<LineRenderer>();
            if (boundary == null)
            {
                boundary = gameObject.AddComponent<LineRenderer>();
            }

            boundaryMaterial = new Material(Shader.Find("Sprites/Default"));
            boundary.material = boundaryMaterial;
            boundary.useWorldSpace = false;
            boundary.loop = true;
            boundary.positionCount = 48;
            boundary.endWidth = 0.11f;
            boundary.startWidth = 0.11f;
            boundary.sortingOrder = 6;
            for (int index = 0; index < boundary.positionCount; index++)
            {
                float angle = index / (float)boundary.positionCount * Mathf.PI * 2f;
                boundary.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void RemoveAllModifiers()
        {
            cleanupBuffer.Clear();
            cleanupBuffer.AddRange(contacts.Keys);
            foreach (PlayerRecoveryModifiers modifiers in cleanupBuffer)
            {
                if (modifiers != null)
                {
                    modifiers.RemoveModifier(this);
                }
            }

            contacts.Clear();
            nextPoisonTick.Clear();
            cleanupBuffer.Clear();
        }

        private void OnDisable()
        {
            RemoveAllModifiers();
        }

        private void OnDestroy()
        {
            RemoveAllModifiers();
            if (boundaryMaterial != null)
            {
                Destroy(boundaryMaterial);
            }
        }
    }
}
