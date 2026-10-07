using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Reusable visual adaptor for a Domain frame. It has no model or
    /// gameplay authority and simply applies the configured skin.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class DomainUiPanel : MonoBehaviour
    {
        [SerializeField] private DomainUiPanelKind kind = DomainUiPanelKind.Medium;
        private void Awake() { DomainUiSkin.ApplyPanel(GetComponent<Image>(), kind); }
        private void OnEnable() { DomainUiSkin.ApplyPanel(GetComponent<Image>(), kind); }
        public void Configure(DomainUiPanelKind configuredKind)
        {
            kind = configuredKind;
            DomainUiSkin.ApplyPanel(GetComponent<Image>(), kind);
        }
    }

    /// <summary>Reusable, presentation-only fill for live complexity/reserve
    /// ratios. Existing authoritative owners provide the values.</summary>
    public sealed class DomainUiProgressBar : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private float maximumWidth = 260f;
        public void SetRatio(float value)
        {
            if (fill == null) return;
            fill.rectTransform.sizeDelta = new Vector2(maximumWidth * Mathf.Clamp01(value), fill.rectTransform.sizeDelta.y);
        }
    }
}
