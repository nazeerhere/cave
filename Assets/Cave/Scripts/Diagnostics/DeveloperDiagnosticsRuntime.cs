using System;
using Cave.Enemies;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Diagnostics
{
    /// <summary>
    /// One development-only presentation/configuration seam.  It deliberately
    /// owns no combat, resolver, or governor state: every exposed value is
    /// forwarded to the system that already owns it.
    /// </summary>
    public static class DeveloperDiagnosticsSettings
    {
        private const string AvailabilityKey = "Cave.Settings.DeveloperDiagnostics.Available";
        private const string DetailKey = "Cave.Settings.DeveloperDiagnostics.Detail";
        private const string UnlockAllCosmeticsKey = "Cave.Settings.DeveloperDiagnostics.UnlockAllCosmetics";
        // This marker intentionally changes the old development default once only.  It
        // must remain separate from the setting itself so a later user choice of OFF
        // is never mistaken for a pre-migration value on the next launch.
        private const string UnlockAllCosmeticsDefaultOnMigrationKey =
            "Cave.Settings.DeveloperDiagnostics.UnlockAllCosmetics.DefaultOnV1";
        private static bool loaded;
        private static bool available;
        private static RuntimeInspectorDetail detail;
        private static bool unlockAllCosmetics;

        public static event Action Changed;
        public static bool FeatureAvailable => Application.isEditor || Debug.isDebugBuild;
        public static bool Available
        {
            get { Load(); return FeatureAvailable && available; }
            set
            {
                Load();
                bool next = FeatureAvailable && value;
                if (available == next) return;
                available = next;
                PlayerPrefs.SetInt(AvailabilityKey, available ? 1 : 0);
                PlayerPrefs.Save();
                DeveloperDiagnosticsRuntime.SetWindowVisible(false);
                Changed?.Invoke();
            }
        }
        public static RuntimeInspectorDetail Detail
        {
            get { Load(); return detail; }
            set
            {
                Load();
                if (detail == value) return;
                detail = value;
                RuntimeInspector inspector = RuntimeInspector.Active;
                if (inspector != null) inspector.Detail = detail;
                PlayerPrefs.SetInt(DetailKey, (int)detail);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Development-only availability override. It never writes cosmetic progression keys.</summary>
        public static bool UnlockAllCosmetics
        {
            get { Load(); return FeatureAvailable && unlockAllCosmetics; }
            set
            {
                Load();
                bool next = FeatureAvailable && value;
                if (unlockAllCosmetics == next) return;
                unlockAllCosmetics = next;
                PlayerPrefs.SetInt(UnlockAllCosmeticsKey, unlockAllCosmetics ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static ResolverTraceMode TraceMode { get => ResolverTraceService.Mode; set { ResolverTraceService.Mode = value; Changed?.Invoke(); } }
        public static bool CtcEnabled { get => CombatTacticalFormation.GloballyEnabled; set { CombatTacticalFormation.GloballyEnabled = value; Changed?.Invoke(); } }
        public static bool JevEnabled { get => CombatTacticalFormation.GloballyJevEnabled; set { CombatTacticalFormation.GloballyJevEnabled = value; Changed?.Invoke(); } }
        public static ResourceGovernorControl GovernorControl
        {
            get { return ResourceGovernor.Active != null ? ResourceGovernor.Active.Control : ResourceGovernorControl.Auto; }
            set { ResourceGovernor governor = DeveloperDiagnosticsRuntime.EnsureInfrastructure().Governor; governor.Control = value; Changed?.Invoke(); }
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            available = PlayerPrefs.GetInt(AvailabilityKey, FeatureAvailable ? 1 : 0) != 0;
            detail = (RuntimeInspectorDetail)Mathf.Clamp(PlayerPrefs.GetInt(DetailKey, (int)RuntimeInspectorDetail.Basic), 0, 1);
            bool persistedUnlockAllCosmetics = PlayerPrefs.GetInt(
                UnlockAllCosmeticsKey,
                FeatureAvailable ? 1 : 0
            ) != 0;
            bool persistUnlockAllCosmetics;
            bool persistMigrationMarker;
            unlockAllCosmetics = ResolveUnlockAllCosmeticsForLoad(
                FeatureAvailable,
                PlayerPrefs.HasKey(UnlockAllCosmeticsDefaultOnMigrationKey),
                persistedUnlockAllCosmetics,
                out persistUnlockAllCosmetics,
                out persistMigrationMarker);

            if (persistUnlockAllCosmetics)
            {
                PlayerPrefs.SetInt(UnlockAllCosmeticsKey, 1);
            }

            if (persistMigrationMarker)
            {
                PlayerPrefs.SetInt(UnlockAllCosmeticsDefaultOnMigrationKey, 1);
            }

            if (persistUnlockAllCosmetics || persistMigrationMarker)
            {
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Pure migration policy for the cosmetics override. Kept independent of
        /// PlayerPrefs so the one-time compatibility path can be verified without
        /// changing a developer's real cosmetic save data.
        /// </summary>
        internal static bool ResolveUnlockAllCosmeticsForLoad(
            bool featureAvailable,
            bool migrationMarkerExists,
            bool persistedValue,
            out bool persistUnlockAllCosmetics,
            out bool persistMigrationMarker)
        {
            bool applyDefaultOnMigration = featureAvailable && !migrationMarkerExists;
            persistUnlockAllCosmetics = applyDefaultOnMigration;
            persistMigrationMarker = applyDefaultOnMigration;
            return featureAvailable && (applyDefaultOnMigration || persistedValue);
        }
    }

    /// <summary>Persistent host for the optional inspector and governor.  It is
    /// the sole installer used by the in-game UI and the editor convenience.</summary>
    [DisallowMultipleComponent]
    public sealed class DeveloperDiagnosticsRuntime : MonoBehaviour
    {
        private static DeveloperDiagnosticsRuntime active;
        private RuntimeInspector inspector;
        private ResourceGovernor governor;

        public ResourceGovernor Governor => governor;
        public RuntimeInspector Inspector => inspector;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForDevelopment()
        {
            if (DeveloperDiagnosticsSettings.FeatureAvailable) EnsureInfrastructure();
        }

        public static DeveloperDiagnosticsRuntime EnsureInfrastructure()
        {
            if (active != null) return active;
            active = FindObjectOfType<DeveloperDiagnosticsRuntime>();
            if (active != null) { active.EnsureComponents(); return active; }
            GameObject root = new GameObject("Cave Developer Diagnostics");
            DontDestroyOnLoad(root);
            active = root.AddComponent<DeveloperDiagnosticsRuntime>();
            active.EnsureComponents();
            return active;
        }

        public static void SetWindowVisible(bool value)
        {
            if (active == null && !value) return;
            DeveloperDiagnosticsRuntime host = EnsureInfrastructure();
            if (host.inspector != null) host.inspector.Visible = value && DeveloperDiagnosticsSettings.Available;
        }

        private void Awake()
        {
            if (active != null && active != this) { Destroy(gameObject); return; }
            active = this;
            DontDestroyOnLoad(gameObject);
            EnsureComponents();
        }

        private void OnDestroy() { if (active == this) active = null; }

        private void Update()
        {
            if (!DeveloperDiagnosticsSettings.Available)
            {
                if (inspector != null) inspector.Visible = false;
                return;
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                inspector.Visible = !inspector.Visible;
            }
        }

        private void EnsureComponents()
        {
            governor = GetComponent<ResourceGovernor>();
            if (governor == null) governor = gameObject.AddComponent<ResourceGovernor>();
            inspector = GetComponent<RuntimeInspector>();
            if (inspector == null) inspector = gameObject.AddComponent<RuntimeInspector>();
            inspector.Detail = DeveloperDiagnosticsSettings.Detail;
            inspector.Visible = false;
        }
    }
}
