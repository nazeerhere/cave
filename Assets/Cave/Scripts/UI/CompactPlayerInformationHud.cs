using Cave.Combat;
using Cave.Domain;
using Cave.Player;
using Cave.Progression;
using Cave.Projectiles;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Read-only compact upper-right player information presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class CompactPlayerInformationHud : MonoBehaviour
    {
        [SerializeField] private Text currencyText;
        [SerializeField] private Text generalShardsText;
        [SerializeField] private Text skillPathText;
        [SerializeField] private Text firePatternText;
        [SerializeField] private Image chargeFill;
        [SerializeField] private Text chargeText;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private Image shardsIcon;
        [SerializeField] private Image skillIcon;
        [SerializeField] private Image firePatternIcon;
        [SerializeField] private Text manifestationText;

        private PlayerCurrency currency;
        private PlayerPermanentProgression progression;
        private PlayerSpecialMode specialMode;
        private PlayerFirePatternState firePatterns;
        private PlayerDomainReserve reserve;
        private PlayerManifestationOrigin manifestations;
        private float displayedCharge = -1f;

        public void Configure(Text currencyValue, Text shardsValue, Text skillValue, Text fireValue,
            Image domainChargeFill, Text domainChargeValue, Image currencySymbol, Image shardsSymbol,
            Image skillSymbol, Image fireSymbol, Text manifestationStatus)
        {
            currencyText = currencyValue;
            generalShardsText = shardsValue;
            skillPathText = skillValue;
            firePatternText = fireValue;
            chargeFill = domainChargeFill;
            chargeText = domainChargeValue;
            currencyIcon = currencySymbol;
            shardsIcon = shardsSymbol;
            skillIcon = skillSymbol;
            firePatternIcon = fireSymbol;
            manifestationText = manifestationStatus;
            RefreshAll();
        }

        public void Bind(PlayerCurrency configuredCurrency, PlayerPermanentProgression configuredProgression,
            PlayerSpecialMode configuredSpecialMode, PlayerFirePatternState configuredFirePatterns,
            PlayerDomainReserve configuredReserve)
        {
            Unsubscribe();
            currency = configuredCurrency;
            progression = configuredProgression;
            specialMode = configuredSpecialMode;
            firePatterns = configuredFirePatterns;
            reserve = configuredReserve;
            manifestations = specialMode != null
                ? PlayerManifestationOrigin.EnsureOn(specialMode.gameObject) : null;
            Subscribe();
            RefreshAll();
        }

        private void Subscribe()
        {
            if (currency != null) currency.CurrencyChanged += HandleCurrencyChanged;
            if (progression != null) progression.GeneralShardsChanged += HandleGeneralShardsChanged;
            if (specialMode != null) specialMode.SpecialModeChanged += HandleSpecialModeChanged;
            if (firePatterns != null) firePatterns.SelectionChanged += HandleFirePatternChanged;
            if (reserve != null) reserve.ReserveChanged += RefreshCharge;
            if (manifestations != null) manifestations.Changed += RefreshManifestation;
        }

        private void Unsubscribe()
        {
            if (currency != null) currency.CurrencyChanged -= HandleCurrencyChanged;
            if (progression != null) progression.GeneralShardsChanged -= HandleGeneralShardsChanged;
            if (specialMode != null) specialMode.SpecialModeChanged -= HandleSpecialModeChanged;
            if (firePatterns != null) firePatterns.SelectionChanged -= HandleFirePatternChanged;
            if (reserve != null) reserve.ReserveChanged -= RefreshCharge;
            if (manifestations != null) manifestations.Changed -= RefreshManifestation;
        }

        private void HandleCurrencyChanged(int value) { if (currencyText != null) currencyText.text = value.ToString(); }
        private void HandleGeneralShardsChanged(int current, int ignoredSpent) { if (generalShardsText != null) generalShardsText.text = current.ToString(); }
        private void HandleSpecialModeChanged(SpecialMode mode) { if (skillPathText != null) skillPathText.text = Format(mode); }
        private void HandleFirePatternChanged(FirePattern pattern) { if (firePatternText != null) firePatternText.text = Format(pattern); }

        private void RefreshAll()
        {
            HandleCurrencyChanged(currency != null ? currency.CurrentCurrency : 0);
            HandleGeneralShardsChanged(progression != null ? progression.GeneralShards : 0, 0);
            if (skillPathText != null) skillPathText.text = specialMode != null ? Format(specialMode.CurrentMode) : "UNAVAILABLE";
            if (firePatternText != null) firePatternText.text = firePatterns != null ? Format(firePatterns.SelectedPattern) : "UNAVAILABLE";
            RefreshCharge();
            RefreshManifestation();
        }

        private void RefreshCharge()
        {
            float maximum = reserve != null ? reserve.MaximumAvailableCharge : 0f;
            float current = reserve != null ? reserve.CurrentCharge : 0f;
            float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            if (chargeFill != null && !Mathf.Approximately(displayedCharge, normalized))
            {
                displayedCharge = normalized;
                chargeFill.rectTransform.localScale = new Vector3(normalized, 1f, 1f);
            }
            if (chargeText != null) chargeText.text = Mathf.RoundToInt(current) + " / " + Mathf.RoundToInt(maximum);
        }

        private void RefreshManifestation()
        {
            if (manifestationText == null) return;
            if (manifestations == null)
            {
                manifestationText.text = "ORIGIN  BODY";
                return;
            }
            bool body = manifestations.SelectedOrigin == ManifestationOrigin.Body;
            manifestationText.text = "ORIGIN  " + manifestations.SelectedOrigin.ToString().ToUpperInvariant()
                + "  •  " + (body ? "DIRECT" : manifestations.IsDeployed ? "DEPLOYED" : "READY")
                + (body ? string.Empty
                    : "  •  " + manifestations.CurrentUpkeepPerSecond.ToString("0") + " MANA/S"
                        + "  •  RECALL " + manifestations.RecallBinding.ToUpperInvariant());
        }

        private static string Format(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot: return "SLOW SHOT";
                case SpecialMode.BurnShot: return "BURN SHOT";
                case SpecialMode.Flight: return "FLIGHT";
                case SpecialMode.DamageBoost: return "STRENGTH";
                default: return mode.ToString().ToUpperInvariant();
            }
        }

        private static string Format(FirePattern pattern)
        {
            switch (pattern)
            {
                case FirePattern.SpoolUp: return "SPOOL-UP";
                default: return pattern.ToString().ToUpperInvariant();
            }
        }

        private void OnDestroy() { Unsubscribe(); }
    }
}
