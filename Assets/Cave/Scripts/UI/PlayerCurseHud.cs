using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerCurseHud : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float deathClaimNoticeDuration = 2.5f;

        private GameObject distractionIcon;
        private GameObject detectiveIcon;
        private GameObject avariceIcon;
        private Text avariceLabel;
        private Text deathClaimNotice;
        private PlayerCurseController curses;
        private float hideClaimNoticeAt;

        public void Configure(
            GameObject distraction,
            GameObject detective,
            GameObject avarice,
            Text configuredAvariceLabel,
            Text configuredDeathClaimNotice,
            PlayerCurseController source)
        {
            distractionIcon = distraction;
            detectiveIcon = detective;
            avariceIcon = avarice;
            avariceLabel = configuredAvariceLabel;
            deathClaimNotice = configuredDeathClaimNotice;
            if (deathClaimNotice != null)
            {
                deathClaimNotice.gameObject.SetActive(false);
            }

            Bind(source);
        }

        public void Bind(PlayerCurseController source)
        {
            if (curses != null)
            {
                curses.CurseStateChanged -= Refresh;
                curses.AvariceStateChanged -= Refresh;
                curses.AvariceDeathClaimed -= HandleDeathClaimed;
            }

            curses = source;
            if (curses != null)
            {
                curses.CurseStateChanged += Refresh;
                curses.AvariceStateChanged += Refresh;
                curses.AvariceDeathClaimed += HandleDeathClaimed;
            }

            Refresh();
        }

        private void Update()
        {
            if (curses == null && PlayerCurseController.Active != null)
            {
                Bind(PlayerCurseController.Active);
            }

            if (deathClaimNotice != null
                && deathClaimNotice.gameObject.activeSelf
                && Time.unscaledTime >= hideClaimNoticeAt)
            {
                deathClaimNotice.gameObject.SetActive(false);
            }
        }

        private void Refresh()
        {
            if (distractionIcon != null)
            {
                distractionIcon.SetActive(curses != null && curses.CurseOfDistractionActive);
            }

            if (detectiveIcon != null)
            {
                detectiveIcon.SetActive(curses != null && curses.DetectivesCurseActive);
            }

            bool avariceActive = curses != null && curses.CurseOfAvariceActive;
            if (avariceIcon != null)
            {
                avariceIcon.SetActive(avariceActive);
            }

            if (avariceLabel != null && avariceActive)
            {
                string tier = curses.CurrentAvariceTier > 0
                    ? " " + ToRoman(curses.CurrentAvariceTier)
                    : string.Empty;
                avariceLabel.text = "AVARICE" + tier
                    + "\nCLAIM " + Mathf.RoundToInt(curses.CurrentAvariceDeathClaim * 100f) + "%";
            }
        }

        private void HandleDeathClaimed(int claimed, int retained, float claimRate)
        {
            if (deathClaimNotice == null || claimed <= 0)
            {
                return;
            }

            deathClaimNotice.text = "AVARICE CLAIM • "
                + Mathf.RoundToInt(claimRate * 100f) + "%\n-" + claimed
                + "  •  " + retained + " RETAINED";
            deathClaimNotice.gameObject.SetActive(true);
            hideClaimNoticeAt = Time.unscaledTime + deathClaimNoticeDuration;
        }

        private static string ToRoman(int tier)
        {
            switch (tier)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                default: return tier.ToString();
            }
        }

        private void OnDestroy()
        {
            Bind(null);
        }
    }
}
