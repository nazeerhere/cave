using Cave.Combat;
using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class FrenzyBreakHud : MonoBehaviour
    {
        [SerializeField] private Text stateText;
        [SerializeField, Min(0.1f)] private float feedbackDuration = 1.1f;

        private PlayerCombatFlow flow;
        private PlayerCurseAltarController altar;
        private string transientFeedback;
        private float feedbackExpiresAt;

        public void Configure(Text displayText, GameObject player)
        {
            stateText = displayText;
            flow = player != null ? player.GetComponent<PlayerCombatFlow>() : null;
            altar = player != null ? player.GetComponent<PlayerCurseAltarController>() : null;
            Subscribe();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (stateText == null || flow == null)
            {
                return;
            }

            string key = SettingsMenuController.FormatBinding(GameAction.Interact);
            if (flow.IsPreparingFrenzy)
            {
                stateText.text = "HOLD " + key + "  •  INFUSE FRENZY";
            }
            else if (flow.IsFrenzyArmed)
            {
                stateText.text = BuildFrenzyState(false);
            }
            else if (flow.IsFrenzyBound)
            {
                stateText.text = BuildFrenzyState(true);
            }
            else if (Time.time <= feedbackExpiresAt && !string.IsNullOrEmpty(transientFeedback))
            {
                stateText.text = transientFeedback;
            }
            else if (flow.ChargedStartingTier > 0)
            {
                stateText.text = "CHARGED FOLLOW-UP  •  TIER " + flow.ChargedStartingTier;
            }
            else if (flow.CanChainSpinToBash)
            {
                stateText.text = "SPIN  →  ["
                    + SettingsMenuController.FormatBinding(GameAction.GuardBreak)
                    + "] BASH";
            }
            else
            {
                bool hasAltar = altar != null && altar.HasAvailableAltar();
                string action = flow.ShouldPrioritizeFrenzyBreak(hasAltar)
                    ? "FRENZY BREAK"
                    : "INTERACT";
                stateText.text = "[" + key + "]  " + action;
            }
        }

        private string BuildFrenzyState(bool bound)
        {
            string level = flow.ArmedInfusionLevel > 0
                ? " " + ToRoman(flow.ArmedInfusionLevel)
                : string.Empty;
            string title = "FRENZY BREAK • "
                + GetInfusionName(flow.ArmedInfusion)
                + level;
            string value = Mathf.RoundToInt(flow.CurrentTheoreticalMultiplier * 100f) + "%";
            if (bound)
            {
                return title
                    + "\nBOUND • "
                    + GetAttackName(flow.BoundAttackKind)
                    + " • "
                    + flow.ArmedRole.ToString().ToUpperInvariant()
                    + "\n"
                    + value;
            }

            string infusion = flow.ArmedInfusionLevel > 0
                ? "INFUSION " + ToRoman(flow.ArmedInfusionLevel) + " • "
                : string.Empty;
            string elemental = flow.ArmedInfusion == FrenzyBreakInfusion.Ice
                && flow.ArmedInfusionLevel >= 3
                    ? "\nPIN + FROST ZONE"
                    : string.Empty;
            return title
                + "\n"
                + flow.ArmedTimeRemaining.ToString("0.0")
                + "s\n"
                + infusion
                + value
                + elemental;
        }

        private static string ToRoman(int level)
        {
            return level >= 3 ? "III" : level == 2 ? "II" : "I";
        }

        private static string GetInfusionName(FrenzyBreakInfusion infusion)
        {
            return infusion == FrenzyBreakInfusion.Ice
                ? "FROST"
                : infusion.ToString().ToUpperInvariant();
        }

        private static string GetAttackName(FrenzyBreakAttackKind attackKind)
        {
            return attackKind == FrenzyBreakAttackKind.GroundSlam
                ? "GROUND SLAM"
                : attackKind.ToString().ToUpperInvariant();
        }

        private void HandleFeedback(string message)
        {
            transientFeedback = message;
            feedbackExpiresAt = Time.time + feedbackDuration;
            Refresh();
        }

        private void Subscribe()
        {
            if (flow != null)
            {
                flow.FeedbackRequested -= HandleFeedback;
                flow.FeedbackRequested += HandleFeedback;
            }
        }

        private void OnDestroy()
        {
            if (flow != null)
            {
                flow.FeedbackRequested -= HandleFeedback;
            }
        }
    }
}
