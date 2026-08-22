using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    public sealed class PlayerCurrencyHud : MonoBehaviour
    {
        [SerializeField] private Text valueText;

        private PlayerCurrency playerCurrency;

        public void Configure(Text currencyValueText)
        {
            valueText = currencyValueText;
        }

        public void Bind(PlayerCurrency currency)
        {
            if (playerCurrency != currency)
            {
                Unsubscribe();
                playerCurrency = currency;
                Subscribe();
            }

            if (playerCurrency != null)
            {
                UpdateCurrency(playerCurrency.CurrentCurrency);
            }
        }

        private void Start()
        {
            if (playerCurrency == null)
            {
                Bind(FindObjectOfType<PlayerCurrency>());
            }
        }

        private void Subscribe()
        {
            if (playerCurrency != null)
            {
                playerCurrency.CurrencyChanged += UpdateCurrency;
            }
        }

        private void Unsubscribe()
        {
            if (playerCurrency != null)
            {
                playerCurrency.CurrencyChanged -= UpdateCurrency;
            }
        }

        private void UpdateCurrency(int currentCurrency)
        {
            if (valueText != null)
            {
                valueText.text = "CURRENCY   " + currentCurrency;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
