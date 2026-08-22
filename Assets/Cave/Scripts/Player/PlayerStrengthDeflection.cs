using System;
using Cave.Combat;
using Cave.Enemies;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSpecialMode), typeof(PlayerSpecialModeUpgradeState))]
    [RequireComponent(typeof(PlayerAttackState))]
    public sealed class PlayerStrengthDeflection : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float feedbackDuration = 0.12f;
        [SerializeField] private Color feedbackColor = new Color(1f, 0.9f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float contactDeflectionKnockback = 6f;

        private PlayerSpecialMode specialMode;
        private PlayerSpecialModeUpgradeState upgrades;
        private PlayerAttackState attackState;

        public event Action Deflected;

        public bool IsActivelyDefending => specialMode != null
            && upgrades != null
            && attackState != null
            && specialMode.CurrentMode == SpecialMode.DamageBoost
            && upgrades.IsTier3Owned(SpecialMode.DamageBoost)
            && attackState.IsActivelyAttacking;

        private void Awake()
        {
            specialMode = GetComponent<PlayerSpecialMode>();
            upgrades = GetComponent<PlayerSpecialModeUpgradeState>();
            attackState = GetComponent<PlayerAttackState>();
        }

        internal void Configure(SpecialModeTier2Settings settings)
        {
            if (settings == null)
            {
                return;
            }

            feedbackDuration = settings.DeflectionFlashDuration;
            feedbackColor = settings.DeflectionFlashColor;
            contactDeflectionKnockback = settings.DeflectionKnockback;
        }

        public bool TryDeflect(DamageContext damageContext)
        {
            if (!IsActivelyDefending
                || damageContext.HasTrait(DamageTrait.AreaOfEffect)
                || damageContext.HasTrait(DamageTrait.Piercing)
                || damageContext.HasTrait(DamageTrait.GuardBreak))
            {
                return false;
            }

            GameObject source = damageContext.Source;
            IDeflectableDamageSource deflectable = source != null
                ? source.GetComponent<IDeflectableDamageSource>()
                : null;
            bool sourceDeflected = deflectable != null && deflectable.TryDeflect(gameObject);
            if (!sourceDeflected && source != null)
            {
                KnockbackReceiver receiver = source.GetComponentInParent<KnockbackReceiver>();
                if (receiver != null)
                {
                    Vector2 direction = source.transform.position - transform.position;
                    if (direction.sqrMagnitude <= 0.001f)
                    {
                        direction = Vector2.right;
                    }

                    receiver.ApplyKnockback(direction.normalized * contactDeflectionKnockback);
                }
            }

            AreaPulseEffect.Create(transform.position, 0.7f, feedbackColor, feedbackDuration);
            Deflected?.Invoke();
            return true;
        }
    }
}
