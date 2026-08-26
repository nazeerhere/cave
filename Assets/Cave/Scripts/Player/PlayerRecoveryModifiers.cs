using System.Collections.Generic;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerRecoveryModifiers : MonoBehaviour
    {
        private struct RecoveryModifier
        {
            public float Health;
            public float Stamina;
            public float Mana;
        }

        private readonly Dictionary<int, RecoveryModifier> modifiers =
            new Dictionary<int, RecoveryModifier>();

        public float HealthRegenerationMultiplier => ResolveMinimum(value => value.Health);
        public float StaminaRegenerationMultiplier => ResolveMinimum(value => value.Stamina);
        public float ManaRegenerationMultiplier => ResolveMinimum(value => value.Mana);
        public bool HasRecoverySuppression => HealthRegenerationMultiplier < 0.999f
            || StaminaRegenerationMultiplier < 0.999f
            || ManaRegenerationMultiplier < 0.999f;

        public void SetModifier(
            Object source,
            float healthMultiplier,
            float staminaMultiplier,
            float manaMultiplier)
        {
            if (source == null)
            {
                return;
            }

            modifiers[source.GetInstanceID()] = new RecoveryModifier
            {
                Health = Mathf.Clamp01(healthMultiplier),
                Stamina = Mathf.Clamp01(staminaMultiplier),
                Mana = Mathf.Clamp01(manaMultiplier)
            };
        }

        public void RemoveModifier(Object source)
        {
            if (source != null)
            {
                modifiers.Remove(source.GetInstanceID());
            }
        }

        public void ClearTemporaryModifiers()
        {
            modifiers.Clear();
        }

        private float ResolveMinimum(System.Func<RecoveryModifier, float> selector)
        {
            float result = 1f;
            foreach (RecoveryModifier modifier in modifiers.Values)
            {
                result = Mathf.Min(result, selector(modifier));
            }

            return result;
        }

        private void OnDisable()
        {
            modifiers.Clear();
        }
    }
}
