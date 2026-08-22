using System.Collections;
using Cave.Combat;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerShieldHud : MonoBehaviour
    {
        [SerializeField] private Text statusText;
        [SerializeField] private Image progressFill;

        private PlayerStrengthShield shield;
        private PlayerSpecialModeUpgradeState upgradeState;
        private Coroutine flashRoutine;

        public void Configure(Text shieldStatusText, Image rechargeProgressFill)
        {
            statusText = shieldStatusText;
            progressFill = rechargeProgressFill;
        }

        public void Bind(PlayerStrengthShield strengthShield)
        {
            if (shield != strengthShield)
            {
                Unsubscribe();
                shield = strengthShield;
                upgradeState = shield != null
                    ? shield.GetComponent<PlayerSpecialModeUpgradeState>()
                    : null;
                Subscribe();
            }

            if (shield != null)
            {
                UpdateShield(shield.CurrentState, shield.RechargeProgress);
            }
        }

        private void Start()
        {
            if (shield == null)
            {
                Bind(FindObjectOfType<PlayerStrengthShield>());
            }
        }

        private void Subscribe()
        {
            if (shield != null)
            {
                shield.ShieldStateChanged += UpdateShield;
                shield.ShieldBroken += FlashBroken;
            }

            if (upgradeState != null)
            {
                upgradeState.Tier2OwnershipChanged += HandleTierChanged;
                upgradeState.Tier3OwnershipChanged += HandleTierChanged;
            }
        }

        private void Unsubscribe()
        {
            if (shield != null)
            {
                shield.ShieldStateChanged -= UpdateShield;
                shield.ShieldBroken -= FlashBroken;
            }

            if (upgradeState != null)
            {
                upgradeState.Tier2OwnershipChanged -= HandleTierChanged;
                upgradeState.Tier3OwnershipChanged -= HandleTierChanged;
            }
        }

        private void UpdateShield(StrengthShieldState state, float progress)
        {
            if (statusText != null)
            {
                string prefix = "SHIELD  •  " + GetTierLabel() + "  •  ";
                switch (state)
                {
                    case StrengthShieldState.Ready:
                        statusText.text = prefix + "READY";
                        statusText.color = new Color(0.35f, 0.9f, 1f, 1f);
                        break;
                    case StrengthShieldState.Broken:
                        statusText.text = prefix + "BROKEN";
                        statusText.color = new Color(1f, 0.35f, 0.25f, 1f);
                        break;
                    case StrengthShieldState.Recharging:
                        statusText.text = prefix + "RECHARGING " + Mathf.RoundToInt(progress * 100f) + "%";
                        statusText.color = new Color(0.55f, 0.75f, 1f, 1f);
                        break;
                    case StrengthShieldState.Inactive:
                        statusText.text = prefix + "INACTIVE";
                        statusText.color = new Color(0.55f, 0.55f, 0.6f, 1f);
                        break;
                    default:
                        statusText.text = prefix + "LOCKED";
                        statusText.color = new Color(0.45f, 0.45f, 0.5f, 1f);
                        break;
                }
            }

            if (progressFill != null)
            {
                float fill = state == StrengthShieldState.Ready ? 1f : Mathf.Clamp01(progress);
                progressFill.rectTransform.localScale = new Vector3(fill, 1f, 1f);
            }
        }

        private void HandleTierChanged(SpecialMode mode)
        {
            if (mode == SpecialMode.DamageBoost && shield != null)
            {
                UpdateShield(shield.CurrentState, shield.RechargeProgress);
            }
        }

        private string GetTierLabel()
        {
            if (upgradeState == null)
            {
                return "TIER I";
            }

            int tier = upgradeState.GetCurrentTier(SpecialMode.DamageBoost);
            return tier >= 3 ? "TIER III" : tier == 2 ? "TIER II" : "TIER I";
        }

        private void FlashBroken()
        {
            if (statusText == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(BrokenFlash());
        }

        private IEnumerator BrokenFlash()
        {
            statusText.color = Color.white;
            yield return new WaitForSeconds(0.12f);
            flashRoutine = null;
            if (shield != null)
            {
                UpdateShield(shield.CurrentState, shield.RechargeProgress);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
