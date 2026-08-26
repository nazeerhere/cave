using System;
using System.Collections.Generic;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum EnemyDamageModifierType
    {
        NecromancerBuff,
        WizardBuff
    }

    [DisallowMultipleComponent]
    public sealed class EnemyDamageModifiers : MonoBehaviour
    {
        [Header("Buff Visual Hooks")]
        [SerializeField] private Color necromancerBuffColor = new Color(0.72f, 0.25f, 1f, 0.8f);
        [SerializeField] private Color wizardBuffColor = new Color(0.2f, 0.75f, 1f, 0.8f);
        [SerializeField, Min(0.1f)] private float visualRadius = 0.65f;

        [Header("Persistent Runtime Multipliers (Read Only)")]
        [SerializeField, Min(0f)] private float inheritanceDamageMultiplier = 1f;

        private readonly Dictionary<EnemyDamageModifierType, ModifierEntry> modifiers =
            new Dictionary<EnemyDamageModifierType, ModifierEntry>();
        private readonly List<EnemyDamageModifierType> expiredTypes =
            new List<EnemyDamageModifierType>();
        private LineRenderer necromancerVisual;
        private LineRenderer wizardVisual;
        private Material necromancerMaterial;
        private Material wizardMaterial;
        private float fractionalDamageCarry;

        private struct ModifierEntry
        {
            public GameObject Source;
            public float Bonus;
            public float ExpiresAt;
        }

        public event Action ModifiersChanged;

        public float AdditiveBonus
        {
            get
            {
                RemoveExpiredModifiers();
                float total = 0f;
                foreach (ModifierEntry entry in modifiers.Values)
                {
                    total += entry.Bonus;
                }

                return total;
            }
        }

        public bool HasModifier(EnemyDamageModifierType type)
        {
            RemoveExpiredModifiers();
            return modifiers.ContainsKey(type);
        }

        public void ApplyModifier(
            EnemyDamageModifierType type,
            float additiveBonus,
            float duration,
            GameObject source)
        {
            if (additiveBonus <= 0f || duration <= 0f)
            {
                return;
            }

            ModifierEntry updated = new ModifierEntry
            {
                Source = source,
                Bonus = additiveBonus,
                ExpiresAt = Time.time + duration
            };
            if (modifiers.TryGetValue(type, out ModifierEntry existing))
            {
                updated.Bonus = Mathf.Max(existing.Bonus, updated.Bonus);
                updated.ExpiresAt = Mathf.Max(existing.ExpiresAt, updated.ExpiresAt);
            }

            modifiers[type] = updated;
            // Start the fractional cycle near its next whole point so a low-damage
            // attacker receives visible benefit on its first buffed hit. Later hits
            // repay that advance and preserve the requested average multiplier.
            fractionalDamageCarry = 0.999f;
            RefreshVisuals();
            ModifiersChanged?.Invoke();
        }

        public void SetInheritanceDamageMultiplier(float multiplier)
        {
            float resolved = Mathf.Max(0f, multiplier);
            if (Mathf.Approximately(inheritanceDamageMultiplier, resolved))
            {
                return;
            }

            inheritanceDamageMultiplier = resolved;
            ResetFractionalCarry();
            ModifiersChanged?.Invoke();
        }

        public int ResolveDamage(int baseDamage)
        {
            int safeBaseDamage = Mathf.Max(1, baseDamage);
            float additiveBonus = AdditiveBonus;
            float avariceMultiplier = PlayerCurseController.Active != null
                ? PlayerCurseController.Active.EnemyDangerMultiplier
                : 1f;
            if (additiveBonus <= 0f
                && Mathf.Approximately(inheritanceDamageMultiplier, 1f)
                && Mathf.Approximately(avariceMultiplier, 1f))
            {
                fractionalDamageCarry = 0f;
                return safeBaseDamage;
            }

            float exactDamage = safeBaseDamage
                * inheritanceDamageMultiplier
                * (1f + additiveBonus)
                * avariceMultiplier
                + fractionalDamageCarry;
            int resolvedDamage = Mathf.Max(1, Mathf.FloorToInt(exactDamage + 0.0001f));
            fractionalDamageCarry = Mathf.Clamp(exactDamage - resolvedDamage, 0f, 0.999f);
            return resolvedDamage;
        }

        private void Update()
        {
            RemoveExpiredModifiers();
        }

        private void RemoveExpiredModifiers()
        {
            expiredTypes.Clear();
            foreach (KeyValuePair<EnemyDamageModifierType, ModifierEntry> modifier in modifiers)
            {
                if (Time.time >= modifier.Value.ExpiresAt)
                {
                    expiredTypes.Add(modifier.Key);
                }
            }

            if (expiredTypes.Count == 0)
            {
                return;
            }

            foreach (EnemyDamageModifierType type in expiredTypes)
            {
                modifiers.Remove(type);
            }

            ResetFractionalCarry();
            RefreshVisuals();
            ModifiersChanged?.Invoke();
        }

        private void RefreshVisuals()
        {
            bool hasNecromancer = modifiers.ContainsKey(EnemyDamageModifierType.NecromancerBuff);
            bool hasWizard = modifiers.ContainsKey(EnemyDamageModifierType.WizardBuff);
            if (hasNecromancer && necromancerVisual == null)
            {
                necromancerVisual = CreateVisual(
                    "Necromancer Damage Buff VFX",
                    necromancerBuffColor,
                    visualRadius,
                    7,
                    out necromancerMaterial);
            }

            if (hasWizard && wizardVisual == null)
            {
                wizardVisual = CreateVisual(
                    "Wizard Damage Buff VFX",
                    wizardBuffColor,
                    visualRadius * 0.82f,
                    8,
                    out wizardMaterial);
            }

            if (necromancerVisual != null)
            {
                necromancerVisual.enabled = hasNecromancer;
            }

            if (wizardVisual != null)
            {
                wizardVisual.enabled = hasWizard;
            }
        }

        private LineRenderer CreateVisual(
            string objectName,
            Color color,
            float radius,
            int sortingOrder,
            out Material material)
        {
            GameObject visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(transform, false);
            LineRenderer line = visualObject.AddComponent<LineRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            line.material = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 36;
            line.startWidth = 0.055f;
            line.endWidth = 0.055f;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            return line;
        }

        private void OnDisable()
        {
            modifiers.Clear();
            ResetFractionalCarry();
            RefreshVisuals();
        }

        private void ResetFractionalCarry()
        {
            fractionalDamageCarry = inheritanceDamageMultiplier > 1f
                || modifiers.Count > 0
                    ? 0.999f
                    : 0f;
        }

        private void OnDestroy()
        {
            if (necromancerMaterial != null)
            {
                Destroy(necromancerMaterial);
            }

            if (wizardMaterial != null)
            {
                Destroy(wizardMaterial);
            }
        }
    }
}
