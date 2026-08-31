using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerWarpStatus : MonoBehaviour
    {
        private WizardSupportAbilities source;
        private PlayerMana mana;
        private SpinSwordAttack stamina;
        private GameObject markVfx;
        private float lowManaThreshold;
        private float rejectCostPercent;
        private float expiresAt;

        public bool IsMarked => source != null && Time.time < expiresAt;
        public float RejectCost => stamina != null
            ? stamina.MaximumStamina * rejectCostPercent
            : 0f;
        public bool CanAffordReject => stamina != null && stamina.CanSpendStamina(RejectCost);
        public WizardSupportAbilities ActiveSource => IsMarked ? source : null;

        private void Awake()
        {
            GetComponent<PlayerHealth>().Died += HandlePlayerDied;
        }

        private void Update()
        {
            if (!IsMarked)
            {
                if (source != null)
                {
                    source.CancelForcedPlayerTeleport(this);
                }

                Clear();
                return;
            }

            if (mana == null
                || mana.MaximumMana <= 0f
                || mana.CurrentMana / mana.MaximumMana > lowManaThreshold)
            {
                source.CancelForcedPlayerTeleport(this);
                Clear();
            }
        }

        public bool CanBeClaimedBy(WizardSupportAbilities wizard)
        {
            ReleaseExpiredClaim();
            return wizard != null && (source == null || source == wizard);
        }

        public bool TryBegin(
            WizardSupportAbilities wizard,
            PlayerMana playerMana,
            SpinSwordAttack playerStamina,
            float vulnerabilityThreshold,
            float staminaCostPercent,
            float duration,
            GameObject markVfxPrefab)
        {
            if (!CanBeClaimedBy(wizard))
            {
                return false;
            }

            Clear();
            source = wizard;
            mana = playerMana;
            stamina = playerStamina;
            lowManaThreshold = Mathf.Clamp01(vulnerabilityThreshold);
            rejectCostPercent = Mathf.Clamp01(staminaCostPercent);
            expiresAt = Time.time + Mathf.Max(0.1f, duration);
            if (markVfxPrefab != null)
            {
                markVfx = Instantiate(
                    markVfxPrefab,
                    transform.position,
                    Quaternion.identity,
                    transform);
                markVfx.transform.localPosition = Vector3.zero;
            }

            return true;
        }

        public bool TryReject()
        {
            if (!IsMarked || stamina == null)
            {
                return false;
            }

            float cost = RejectCost;
            if (!stamina.TrySpendStamina(cost))
            {
                return false;
            }

            WizardSupportAbilities activeSource = source;
            Clear();
            activeSource?.RejectForcedPlayerTeleport(this);
            return true;
        }

        public void ClearFrom(WizardSupportAbilities wizard)
        {
            if (source == wizard)
            {
                Clear();
            }
        }

        private void HandlePlayerDied()
        {
            WizardSupportAbilities activeSource = source;
            Clear();
            activeSource?.CancelForcedPlayerTeleport(this);
        }

        private void Clear()
        {
            source = null;
            mana = null;
            stamina = null;
            expiresAt = 0f;
            if (markVfx != null)
            {
                Destroy(markVfx);
                markVfx = null;
            }
        }

        private void ReleaseExpiredClaim()
        {
            if (source == null || Time.time < expiresAt)
            {
                return;
            }

            WizardSupportAbilities expiredSource = source;
            Clear();
            expiredSource.CancelForcedPlayerTeleport(this);
        }

        private void OnDisable()
        {
            WizardSupportAbilities activeSource = source;
            Clear();
            activeSource?.CancelForcedPlayerTeleport(this);
        }

        private void OnDestroy()
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Died -= HandlePlayerDied;
            }
        }
    }
}
