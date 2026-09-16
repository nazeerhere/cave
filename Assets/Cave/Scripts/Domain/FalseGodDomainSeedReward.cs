using Cave.Combat;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Dormant reward seam for the future False God prefab. It reuses the
    /// existing authoritative Damageable death signal without introducing combat
    /// behaviour. No current boss is modified or granted this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class FalseGodDomainSeedReward : MonoBehaviour
    {
        private Damageable damageable;
        private bool processedDefeat;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            if (damageable != null)
            {
                damageable.Died += GrantForFalseGodDefeat;
            }
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.Died -= GrantForFalseGodDefeat;
            }
        }

        /// <summary>
        /// Public so a future authoritative completion/reward event can invoke
        /// the same idempotent grant without depending on death presentation.
        /// </summary>
        public void GrantForFalseGodDefeat()
        {
            if (processedDefeat)
            {
                return;
            }

            processedDefeat = true;
            DomainProgression.GrantDomainSeed();
        }
    }
}
