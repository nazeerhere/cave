using Cave.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Read-only upper-HUD view of the player-owned Domain Charge ledger.</summary>
    public sealed class PlayerDomainChargeHud : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;

        private PlayerDomainReserve reserve;
        private CanvasGroup canvasGroup;
        private float displayedFill = -1f;
        private int displayedCurrent = int.MinValue;
        private int displayedMaximum = int.MinValue;

        public void Configure(Image chargeFillImage, Text chargeValueText)
        {
            fillImage = chargeFillImage;
            valueText = chargeValueText;
        }

        public void Bind(PlayerDomainReserve reserveOwner)
        {
            if (reserve == reserveOwner)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            reserve = reserveOwner;
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            DomainProgression.DomainSeedGranted += Refresh;
        }

        private void Start()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (reserve == null)
            {
                Bind(FindObjectOfType<PlayerDomainReserve>());
            }
            else
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            DomainProgression.DomainSeedGranted -= Refresh;
        }

        private void Subscribe()
        {
            if (reserve != null)
            {
                reserve.ReserveChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (reserve != null)
            {
                reserve.ReserveChanged -= Refresh;
            }
        }

        private void Refresh()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            bool available = reserve != null && DomainTestOverride.HasDomainAccess;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = available ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (!available)
            {
                return;
            }

            float currentCharge = reserve.CurrentCharge;
            float maximumCharge = reserve.MaximumAvailableCharge;
            float fill = maximumCharge > 0f ? Mathf.Clamp01(currentCharge / maximumCharge) : 0f;
            if (fillImage != null && !Mathf.Approximately(displayedFill, fill))
            {
                displayedFill = fill;
                fillImage.rectTransform.localScale = new Vector3(fill, 1f, 1f);
            }

            int current = Mathf.RoundToInt(currentCharge);
            int maximum = Mathf.RoundToInt(maximumCharge);
            if (valueText != null && (displayedCurrent != current || displayedMaximum != maximum))
            {
                displayedCurrent = current;
                displayedMaximum = maximum;
                valueText.text = current + " / " + maximum;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DomainProgression.DomainSeedGranted -= Refresh;
        }
    }
}
