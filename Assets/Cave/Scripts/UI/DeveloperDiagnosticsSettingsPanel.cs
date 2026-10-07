using Cave.Diagnostics;
using Cave.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Presentation-only Dev page inside the existing Settings flow.
    /// Buttons forward directly to DeveloperDiagnosticsSettings, whose values
    /// in turn forward to the existing runtime authorities.</summary>
    public sealed class DeveloperDiagnosticsSettingsPanel : MonoBehaviour
    {
        private GameObject settingsPanel;
        private SettingsMenuController settingsController;
        private Button availability;
        private Button detail;
        private Button trace;
        private Button ctc;
        private Button jev;
        private Button governor;
        private Button domainTestOverride;
        private Button unlockAllCosmetics;
        private float nextRefresh;

        public void Configure(GameObject ownerSettingsPanel, SettingsMenuController ownerSettingsController,
            Button configuredAvailability, Button configuredDetail, Button configuredTrace, Button configuredCtc,
            Button configuredJev, Button configuredGovernor, Button configuredDomainTestOverride,
            Button configuredUnlockAllCosmetics, Button back)
        {
            settingsPanel = ownerSettingsPanel;
            settingsController = ownerSettingsController;
            availability = configuredAvailability; detail = configuredDetail; trace = configuredTrace; ctc = configuredCtc; jev = configuredJev; governor = configuredGovernor; domainTestOverride = configuredDomainTestOverride; unlockAllCosmetics = configuredUnlockAllCosmetics;
            availability.onClick.AddListener(() => DeveloperDiagnosticsSettings.Available = !DeveloperDiagnosticsSettings.Available);
            detail.onClick.AddListener(() => DeveloperDiagnosticsSettings.Detail = DeveloperDiagnosticsSettings.Detail == RuntimeInspectorDetail.Basic ? RuntimeInspectorDetail.Full : RuntimeInspectorDetail.Basic);
            trace.onClick.AddListener(() => DeveloperDiagnosticsSettings.TraceMode = DeveloperDiagnosticsSettings.TraceMode == ResolverTraceMode.Off ? ResolverTraceMode.Summary : DeveloperDiagnosticsSettings.TraceMode == ResolverTraceMode.Summary ? ResolverTraceMode.Full : ResolverTraceMode.Off);
            ctc.onClick.AddListener(() => DeveloperDiagnosticsSettings.CtcEnabled = !DeveloperDiagnosticsSettings.CtcEnabled);
            jev.onClick.AddListener(() => DeveloperDiagnosticsSettings.JevEnabled = !DeveloperDiagnosticsSettings.JevEnabled);
            governor.onClick.AddListener(() => DeveloperDiagnosticsSettings.GovernorControl = NextGovernor(DeveloperDiagnosticsSettings.GovernorControl));
            domainTestOverride.onClick.AddListener(() => DomainTestOverride.Enabled = !DomainTestOverride.Enabled);
            unlockAllCosmetics.onClick.AddListener(() => DeveloperDiagnosticsSettings.UnlockAllCosmetics = !DeveloperDiagnosticsSettings.UnlockAllCosmetics);
            back.onClick.AddListener(ReturnToSettings);
            Refresh();
        }

        private void OnEnable() { DeveloperDiagnosticsSettings.Changed += Refresh; Refresh(); }
        private void OnDisable() { DeveloperDiagnosticsSettings.Changed -= Refresh; }
        private void Update() { if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .25f; Refresh(); } }
        private void ReturnToSettings() { gameObject.SetActive(false); settingsPanel.SetActive(true); settingsController?.PrepareToShow(); }
        private void Refresh()
        {
            Set(availability, "DEVELOPER DIAGNOSTICS\n" + (DeveloperDiagnosticsSettings.Available ? "ON — F10 AVAILABLE" : "OFF"));
            Set(detail, "INSPECTOR DETAIL\n" + DeveloperDiagnosticsSettings.Detail.ToString().ToUpperInvariant());
            Set(trace, "RESOLVER TRACE\n" + DeveloperDiagnosticsSettings.TraceMode.ToString().ToUpperInvariant());
            Set(ctc, "CTC\n" + (DeveloperDiagnosticsSettings.CtcEnabled ? "ON" : "OFF"));
            Set(jev, "JEV\n" + (DeveloperDiagnosticsSettings.JevEnabled ? "ON" : "OFF"));
            Set(governor, "RESOURCE GOVERNOR\n" + GovernorName(DeveloperDiagnosticsSettings.GovernorControl));
            Set(domainTestOverride, "DOMAIN TEST OVERRIDE\n" + (DomainTestOverride.Enabled ? "ON" : "OFF"));
            Set(unlockAllCosmetics, "UNLOCK ALL COSMETICS\n" + (DeveloperDiagnosticsSettings.UnlockAllCosmetics ? "ON — DEV OVERRIDE" : "OFF"));
        }
        private static void Set(Button button, string value) { if (button == null) return; Text label = button.GetComponentInChildren<Text>(); if (label != null) { label.text = value; label.fontSize = 14; } }
        private static ResourceGovernorControl NextGovernor(ResourceGovernorControl value) { return value == ResourceGovernorControl.Auto ? ResourceGovernorControl.ForceNormal : value == ResourceGovernorControl.ForceNormal ? ResourceGovernorControl.ForceDegraded : value == ResourceGovernorControl.ForceDegraded ? ResourceGovernorControl.ForceCritical : ResourceGovernorControl.Auto; }
        private static string GovernorName(ResourceGovernorControl value) { return value == ResourceGovernorControl.ForceNormal ? "NORMAL" : value == ResourceGovernorControl.ForceDegraded ? "DEGRADED" : value == ResourceGovernorControl.ForceCritical ? "CRITICAL" : "AUTO"; }
    }
}
