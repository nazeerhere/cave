#if UNITY_EDITOR
using Cave.Diagnostics;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>
    /// Explicit opt-in, editor-only Play Mode bootstrap. It creates no assets
    /// and never changes a production scene; use Tools/Cave/Development to
    /// enable it before entering Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PreGambitDiagnosticsPlayModeInstaller
    {
        private const string EnabledKey = "Cave.PreGambitDiagnostics.AutoInstall";
        private static GameObject runtimeRoot;

        static PreGambitDiagnosticsPlayModeInstaller()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeState;
        }

        [MenuItem("Tools/Cave/Development/Pre-Gambit Diagnostics/Enable Auto-Install")]
        private static void Enable()
        {
            EditorPrefs.SetBool(EnabledKey, true);
            InstallIfPlaying();
            Debug.Log("[Cave] Pre-Gambit diagnostics auto-install enabled. Enter Play Mode to receive the temporary inspector/governor root.");
        }

        [MenuItem("Tools/Cave/Development/Pre-Gambit Diagnostics/Enable Auto-Install", true)]
        private static bool ValidateEnable()
        {
            Menu.SetChecked("Tools/Cave/Development/Pre-Gambit Diagnostics/Enable Auto-Install", EditorPrefs.GetBool(EnabledKey, false));
            return true;
        }

        [MenuItem("Tools/Cave/Development/Pre-Gambit Diagnostics/Disable Auto-Install")]
        private static void Disable()
        {
            EditorPrefs.SetBool(EnabledKey, false);
            Debug.Log("[Cave] Pre-Gambit diagnostics auto-install disabled. Existing Play Mode root remains only until Play Mode ends.");
        }

        [MenuItem("Tools/Cave/Development/Pre-Gambit Diagnostics/Install Into Current Play Mode")]
        private static void InstallIfPlaying()
        {
            if (!Application.isPlaying || runtimeRoot != null)
            {
                return;
            }

            runtimeRoot = new GameObject("Cave Pre-Gambit Diagnostics (Editor Only)");
            runtimeRoot.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(runtimeRoot);
            ResourceGovernor governor = runtimeRoot.AddComponent<ResourceGovernor>();
            governor.Control = ResourceGovernorControl.Auto;
            RuntimeInspector inspector = runtimeRoot.AddComponent<RuntimeInspector>();
            inspector.Visible = true;
            inspector.Detail = RuntimeInspectorDetail.Basic;

            // Defined baseline: systems remain independently usable and a
            // developer can turn each diagnostic control on from the overlay.
            CombatTacticalFormation.GloballyEnabled = false;
            CombatTacticalFormation.GloballyJevEnabled = false;
            ResolverTraceService.Mode = ResolverTraceMode.Off;
        }

        private static void HandlePlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(EnabledKey, false))
            {
                InstallIfPlaying();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                runtimeRoot = null;
            }
        }
    }
}
#endif
