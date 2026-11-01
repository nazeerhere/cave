using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Presentation-only Carapace layer inside the existing HP track.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCarapaceHealthHud : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField, Range(0f, 1f)] private float carapaceHudOverlayAlpha = .25f;
        [SerializeField, Min(.01f)] private float responseDuration = .45f;
        [SerializeField] private Image overlayImage;
        [SerializeField] private RectTransform overlayFill;
        [SerializeField] private Image overlayDetailImage;
        [SerializeField] private Image emblemImage;
        [SerializeField] private Image stateImage;
        [SerializeField] private Image terminalResponseImage;

        private PlayerResonantCarapace carapace;
        private int displayedDurability = -1;
        private float recoveringUntil;
        private float impactUntil;
        private float terminalResponseUntil;

        public float CarapaceHudOverlayAlpha => carapaceHudOverlayAlpha;

        public void Configure(
            Image configuredOverlay,
            RectTransform configuredOverlayFill,
            Image configuredOverlayDetail,
            Image configuredEmblem,
            Image configuredState,
            Image configuredTerminalResponse)
        {
            overlayImage = configuredOverlay;
            overlayFill = configuredOverlayFill;
            overlayDetailImage = configuredOverlayDetail;
            emblemImage = configuredEmblem;
            stateImage = configuredState;
            terminalResponseImage = configuredTerminalResponse;
            LoadArt();
            Refresh();
        }

        public void Bind(PlayerResonantCarapace owner)
        {
            if (carapace == owner)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            carapace = owner;
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            if (carapace == null) Bind(FindObjectOfType<PlayerResonantCarapace>());
        }

        private void Update()
        {
            if (recoveringUntil > 0f && Time.unscaledTime >= recoveringUntil)
            {
                recoveringUntil = 0f;
                Refresh();
            }
            if (impactUntil > 0f && Time.unscaledTime >= impactUntil)
            {
                impactUntil = 0f;
                Refresh();
            }
            if (terminalResponseImage != null && terminalResponseUntil > 0f && Time.unscaledTime >= terminalResponseUntil)
            {
                terminalResponseUntil = 0f;
                terminalResponseImage.enabled = false;
            }
        }

        private void Subscribe()
        {
            if (carapace == null) return;
            carapace.StateChanged += HandleCarapaceChanged;
            carapace.DamageAbsorbed += HandleDamageAbsorbed;
            carapace.TerminalFractured += HandleTerminalFractured;
        }

        private void Unsubscribe()
        {
            if (carapace == null) return;
            carapace.StateChanged -= HandleCarapaceChanged;
            carapace.DamageAbsorbed -= HandleDamageAbsorbed;
            carapace.TerminalFractured -= HandleTerminalFractured;
        }

        private void HandleCarapaceChanged()
        {
            int current = carapace != null ? carapace.CurrentDurability : 0;
            if (displayedDurability >= 0 && current > displayedDurability)
            {
                recoveringUntil = Time.unscaledTime + responseDuration;
            }
            Refresh();
        }

        private void HandleDamageAbsorbed(int ignored)
        {
            if (stateImage != null)
            {
                stateImage.sprite = LoadSprite("Carapace_Icon_Impact");
                stateImage.enabled = true;
                impactUntil = Time.unscaledTime + responseDuration;
            }
        }

        private void HandleTerminalFractured()
        {
            if (terminalResponseImage != null)
            {
                terminalResponseImage.sprite = LoadSprite("Carapace_Fracture_01");
                terminalResponseImage.enabled = terminalResponseImage.sprite != null;
                terminalResponseUntil = Time.unscaledTime + responseDuration;
            }
            Refresh();
        }

        private void Refresh()
        {
            bool show = carapace != null && carapace.IsEquipped;
            int current = show ? carapace.CurrentDurability : 0;
            int maximum = show ? carapace.MaximumDurability : 0;
            if (!show && (carapace == null || carapace.State != ResonantCarapaceState.TerminallyFractured)
                && terminalResponseImage != null)
            {
                terminalResponseImage.enabled = false;
                terminalResponseUntil = 0f;
            }
            displayedDurability = current;
            float fill = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;

            if (overlayFill != null)
            {
                overlayFill.localScale = new Vector3(fill, 1f, 1f);
            }
            if (overlayImage != null)
            {
                overlayImage.enabled = show && fill > 0f && carapace.State != ResonantCarapaceState.ResonanceDisabled;
                overlayImage.color = new Color(1f, 1f, 1f, carapaceHudOverlayAlpha);
            }
            if (overlayDetailImage != null)
            {
                overlayDetailImage.enabled = show && fill > 0f && carapace.State != ResonantCarapaceState.ResonanceDisabled;
                overlayDetailImage.color = new Color(1f, 1f, 1f, carapaceHudOverlayAlpha * .45f);
            }
            if (emblemImage != null)
            {
                emblemImage.enabled = show;
                emblemImage.sprite = LoadSprite("Carapace_Shield_UnlockEmblem");
                emblemImage.color = carapace != null && carapace.State == ResonantCarapaceState.ResonanceDisabled
                    ? new Color(.58f, .58f, .7f, .72f) : Color.white;
            }
            if (stateImage == null) return;
            stateImage.enabled = show;
            if (!show) return;

            if (recoveringUntil > Time.unscaledTime)
            {
                stateImage.sprite = LoadSprite("Carapace_Icon_Recovering");
                return;
            }
            if (impactUntil > Time.unscaledTime)
            {
                stateImage.sprite = LoadSprite("Carapace_Icon_Impact");
                return;
            }
            switch (carapace.State)
            {
                case ResonantCarapaceState.Depleted:
                    stateImage.sprite = LoadSprite("Carapace_Icon_Depleted");
                    break;
                case ResonantCarapaceState.ResonanceDisabled:
                    stateImage.sprite = LoadSprite("Carapace_Icon_Resonance");
                    break;
                default:
                    stateImage.sprite = LoadSprite("Carapace_Icon_Active");
                    break;
            }
        }

        private void LoadArt()
        {
            if (overlayImage != null) overlayImage.sprite = LoadSprite("Carapace_HpOverlay_Idle_Faint");
            if (overlayDetailImage != null) overlayDetailImage.sprite = LoadSprite("Carapace_HpOverlay_Idle_Faint");
            if (emblemImage != null) emblemImage.sprite = LoadSprite("Carapace_Shield_UnlockEmblem");
            if (stateImage != null) stateImage.sprite = LoadSprite("Carapace_Icon_Active");
        }

        private static Sprite LoadSprite(string name)
        {
            return Resources.Load<Sprite>("UI/Hud/Carapace/" + name);
        }

        private void OnDestroy() { Unsubscribe(); }
    }
}
