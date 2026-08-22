using System;
using System.Collections.Generic;
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

        private readonly Dictionary<EnemyDamageModifierType, ModifierEntry> modifiers =
            new Dictionary<EnemyDamageModifierType, ModifierEntry>();
        private readonly List<EnemyDamageModifierType> expiredTypes =
            new List<EnemyDamageModifierType>();
        private LineRenderer necromancerVisual;
        private LineRenderer wizardVisual;
        private Material necromancerMaterial;
        private Material wizardMaterial;

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
            RefreshVisuals();
            ModifiersChanged?.Invoke();
        }

        public int ResolveDamage(int baseDamage)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * (1f + AdditiveBonus)));
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
            RefreshVisuals();
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
