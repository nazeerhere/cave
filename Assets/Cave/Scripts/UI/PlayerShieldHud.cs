using System.Collections;
using Cave.Combat;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerShieldHud : MonoBehaviour
    {
        private const float VisualRefreshInterval = 0.1f;

        [SerializeField] private Text statusText;
        [SerializeField] private Image progressFill;

        private PlayerStrengthShield shield;
        private PlayerSpecialModeUpgradeState upgradeState;
        private Coroutine flashRoutine;
        private string displayedStatus;
        private Color displayedColor = new Color(-1f, -1f, -1f, -1f);
        private float displayedProgress = -1f;
        private float nextVisualRefreshTime;
        private StrengthShieldState displayedState = (StrengthShieldState)(-1);

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
            bool stateChanged = displayedState != state;
            if (!stateChanged
                && Time.unscaledTime < nextVisualRefreshTime
                && displayedProgress >= 0f)
            {
                return;
            }

            nextVisualRefreshTime = Time.unscaledTime + VisualRefreshInterval;
            displayedState = state;
            if (statusText != null)
            {
                string prefix = "SHIELD  •  " + GetTierLabel() + "  •  ";
                string nextStatus;
                Color nextColor;
                switch (state)
                {
                    case StrengthShieldState.Ready:
                        nextStatus = prefix + "READY";
                        nextColor = new Color(0.35f, 0.9f, 1f, 1f);
                        break;
                    case StrengthShieldState.Broken:
                        nextStatus = prefix + "BROKEN";
                        nextColor = new Color(1f, 0.35f, 0.25f, 1f);
                        break;
                    case StrengthShieldState.Recharging:
                        nextStatus = prefix + "RECHARGING " + Mathf.RoundToInt(progress * 100f) + "%";
                        nextColor = new Color(0.55f, 0.75f, 1f, 1f);
                        break;
                    case StrengthShieldState.Inactive:
                        nextStatus = prefix + "INACTIVE";
                        nextColor = new Color(0.55f, 0.55f, 0.6f, 1f);
                        break;
                    default:
                        nextStatus = prefix + "LOCKED";
                        nextColor = new Color(0.45f, 0.45f, 0.5f, 1f);
                        break;
                }

                if (displayedStatus != nextStatus)
                {
                    displayedStatus = nextStatus;
                    statusText.text = nextStatus;
                }

                if (displayedColor != nextColor)
                {
                    displayedColor = nextColor;
                    statusText.color = nextColor;
                }
            }

            if (progressFill != null)
            {
                float fill = state == StrengthShieldState.Ready ? 1f : Mathf.Clamp01(progress);
                if (!Mathf.Approximately(displayedProgress, fill))
                {
                    displayedProgress = fill;
                    progressFill.rectTransform.localScale = new Vector3(fill, 1f, 1f);
                }
            }
        }

        private void HandleTierChanged(SpecialMode mode)
        {
            if (mode == SpecialMode.DamageBoost && shield != null)
            {
                displayedState = (StrengthShieldState)(-1);
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
            displayedColor = Color.white;
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
