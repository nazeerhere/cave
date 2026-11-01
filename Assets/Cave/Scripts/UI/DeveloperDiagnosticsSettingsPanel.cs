using Cave.Diagnostics;
using Cave.Domain;
using Cave.Inventory;
using Cave.Player;
using Cave.Projectiles;
using Cave.World;
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
        private Button grantCarapace;
        private Button unlockManifestations;
        private Button resetManifestations;
        private Text manifestationDiagnostics;
        private float nextRefresh;

        public void Configure(GameObject ownerSettingsPanel, SettingsMenuController ownerSettingsController,
            Button configuredAvailability, Button configuredDetail, Button configuredTrace, Button configuredCtc,
            Button configuredJev, Button configuredGovernor, Button configuredDomainTestOverride,
            Button configuredUnlockAllCosmetics, Button configuredGrantCarapace,
            Button configuredUnlockManifestations, Button configuredResetManifestations,
            Text configuredManifestationDiagnostics, Button back)
        {
            settingsPanel = ownerSettingsPanel;
            settingsController = ownerSettingsController;
            availability = configuredAvailability; detail = configuredDetail; trace = configuredTrace; ctc = configuredCtc; jev = configuredJev; governor = configuredGovernor; domainTestOverride = configuredDomainTestOverride; unlockAllCosmetics = configuredUnlockAllCosmetics; grantCarapace = configuredGrantCarapace; unlockManifestations = configuredUnlockManifestations; resetManifestations = configuredResetManifestations; manifestationDiagnostics = configuredManifestationDiagnostics;
            availability.onClick.AddListener(() => DeveloperDiagnosticsSettings.Available = !DeveloperDiagnosticsSettings.Available);
            detail.onClick.AddListener(() => DeveloperDiagnosticsSettings.Detail = DeveloperDiagnosticsSettings.Detail == RuntimeInspectorDetail.Basic ? RuntimeInspectorDetail.Full : RuntimeInspectorDetail.Basic);
            trace.onClick.AddListener(() => DeveloperDiagnosticsSettings.TraceMode = DeveloperDiagnosticsSettings.TraceMode == ResolverTraceMode.Off ? ResolverTraceMode.Summary : DeveloperDiagnosticsSettings.TraceMode == ResolverTraceMode.Summary ? ResolverTraceMode.Full : ResolverTraceMode.Off);
            ctc.onClick.AddListener(() => DeveloperDiagnosticsSettings.CtcEnabled = !DeveloperDiagnosticsSettings.CtcEnabled);
            jev.onClick.AddListener(() => DeveloperDiagnosticsSettings.JevEnabled = !DeveloperDiagnosticsSettings.JevEnabled);
            governor.onClick.AddListener(() => DeveloperDiagnosticsSettings.GovernorControl = NextGovernor(DeveloperDiagnosticsSettings.GovernorControl));
            domainTestOverride.onClick.AddListener(() => DomainTestOverride.Enabled = !DomainTestOverride.Enabled);
            unlockAllCosmetics.onClick.AddListener(() => DeveloperDiagnosticsSettings.UnlockAllCosmetics = !DeveloperDiagnosticsSettings.UnlockAllCosmetics);
            grantCarapace.onClick.AddListener(GrantAndEquipCarapace);
            unlockManifestations.onClick.AddListener(UnlockManifestations);
            resetManifestations.onClick.AddListener(ResetManifestations);
            back.onClick.AddListener(ReturnToSettings);
            Refresh();
        }

        private void OnEnable()
        {
            DeveloperDiagnosticsSettings.Changed += Refresh;
            Debug.Log("[Cave] " + DeveloperDiagnosticsSettings.CosmeticsEnvironmentSummary);
            Refresh();
        }
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
            IResonantCarapaceInventorySource carapace = ResolveCarapace();
            if (grantCarapace != null)
            {
                grantCarapace.interactable = DeveloperDiagnosticsSettings.FeatureAvailable && carapace != null;
                Set(grantCarapace, carapace == null
                    ? "GRANT / EQUIP CARAPACE\nCORE UNAVAILABLE"
                    : "GRANT / EQUIP CARAPACE\n" + (carapace.IsOwned ? "EQUIPPED / OWNED" : "DEV TEST GRANT"));
            }
            PlayerManifestationOrigin manifestations = ResolveManifestations();
            if (unlockManifestations != null)
            {
                unlockManifestations.interactable = DeveloperDiagnosticsSettings.FeatureAvailable;
                Set(unlockManifestations, "UNLOCK MANIFESTATIONS\n"
                    + (DeveloperDiagnosticsSettings.EffectiveUnlockManifestations
                        ? "ON — DEV OVERRIDE" : "GRANT TEST ACCESS"));
            }
            if (resetManifestations != null)
            {
                resetManifestations.interactable = DeveloperDiagnosticsSettings.FeatureAvailable
                    && DeveloperDiagnosticsSettings.UnlockManifestations;
                Set(resetManifestations, "RESET MANIFESTATION TEST OVERRIDE");
            }
            if (manifestationDiagnostics != null)
            {
                manifestationDiagnostics.text = manifestations == null
                    ? "MANIFESTATION RUNTIME: PLAYER UNAVAILABLE"
                    : "Seed " + (manifestations.HasNormalSeed ? "YES" : "NO")
                        + "  |  Normal Anchor " + (manifestations.IsNormallyUnlocked(ManifestationOrigin.Anchor) ? "YES" : "NO")
                        + "  |  Normal Orbital " + (manifestations.IsNormallyUnlocked(ManifestationOrigin.Orbital) ? "YES" : "NO")
                        + "  |  Override " + (manifestations.DevelopmentOverride ? "ON" : "OFF")
                        + "\nSelected " + manifestations.SelectedOrigin
                        + "  |  Deployed " + (manifestations.IsDeployed ? manifestations.DeployedOrigin.ToString() : "NONE")
                        + "  |  Upkeep " + manifestations.CurrentUpkeepPerSecond.ToString("0") + "/s";
            }
        }
        private void GrantAndEquipCarapace()
        {
            if (!DeveloperDiagnosticsSettings.FeatureAvailable) return;
            IResonantCarapaceInventorySource carapace = ResolveCarapace();
            if (carapace != null && carapace.TryGrantAndEquipForDevelopment()) Refresh();
        }
        private void UnlockManifestations()
        {
            if (!DeveloperDiagnosticsSettings.FeatureAvailable) return;
            DeveloperDiagnosticsSettings.UnlockManifestations = true;
            ResolveManifestations();
            Refresh();
        }
        private void ResetManifestations()
        {
            if (!DeveloperDiagnosticsSettings.FeatureAvailable) return;
            DeveloperDiagnosticsSettings.UnlockManifestations = false;
            Refresh();
        }
        private static PlayerManifestationOrigin ResolveManifestations()
        {
            PlayerHealth player = PlayerRunPersistence.CurrentPlayerHealth ?? Object.FindObjectOfType<PlayerHealth>();
            return player != null ? PlayerManifestationOrigin.EnsureOn(player.gameObject) : null;
        }
        private static IResonantCarapaceInventorySource ResolveCarapace()
        {
            PlayerHealth player = PlayerRunPersistence.CurrentPlayerHealth ?? Object.FindObjectOfType<PlayerHealth>();
            if (player == null) return null;
            MonoBehaviour[] candidates = player.GetComponents<MonoBehaviour>();
            for (int index = 0; index < candidates.Length; index++)
            {
                IResonantCarapaceInventorySource source = candidates[index] as IResonantCarapaceInventorySource;
                if (source != null) return source;
            }
            return null;
        }
        private static void Set(Button button, string value) { if (button == null) return; Text label = button.GetComponentInChildren<Text>(); if (label != null) { label.text = value; label.fontSize = 14; } }
        private static ResourceGovernorControl NextGovernor(ResourceGovernorControl value) { return value == ResourceGovernorControl.Auto ? ResourceGovernorControl.ForceNormal : value == ResourceGovernorControl.ForceNormal ? ResourceGovernorControl.ForceDegraded : value == ResourceGovernorControl.ForceDegraded ? ResourceGovernorControl.ForceCritical : ResourceGovernorControl.Auto; }
        private static string GovernorName(ResourceGovernorControl value) { return value == ResourceGovernorControl.ForceNormal ? "NORMAL" : value == ResourceGovernorControl.ForceDegraded ? "DEGRADED" : value == ResourceGovernorControl.ForceCritical ? "CRITICAL" : "AUTO"; }
    }
}
