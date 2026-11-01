using Cave.Environmental;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    [InitializeOnLoad]
    public static class EnvironmentalDestructibleVerificationMenu
    {
        private const string SessionKey = "Cave.EnvironmentalDestructibleVerification.RanV4";
        private const string RegressionSessionKey = "Cave.EnvironmentalDestructibleVerification.RegressionRanV3";

        static EnvironmentalDestructibleVerificationMenu()
        {
            EditorApplication.delayCall += RunOnceAfterCompile;
            // The combined sweep reads Resources-backed presentation assets.
            // Delay it until Unity completes the initial AssetDatabase refresh
            // rather than treating that transient startup state as missing art.
            EditorApplication.delayCall += RunRegressionOnceAfterCompile;
        }

        [MenuItem("Tools/Cave/Verification/Run Environmental Destructibles Verification")]
        private static void Run()
        {
            if (EnvironmentalDestructibleVerification.TryRunAll(out string failure))
                Debug.Log("[Cave] ENVIRONMENTAL DESTRUCTIBLES PASS");
            else
                Debug.LogError("[Cave] ENVIRONMENTAL DESTRUCTIBLES FAIL: " + failure);
        }

        private static void RunOnceAfterCompile()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            Run();
        }

        private static void RunRegressionOnceAfterCompile()
        {
            if (SessionState.GetBool(RegressionSessionKey, false)) return;
            SessionState.SetBool(RegressionSessionKey, true);
            AxiomDomainRegressionSweep.RunBatch();
        }
    }
}
