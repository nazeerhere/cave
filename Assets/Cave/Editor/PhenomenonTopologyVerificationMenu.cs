using Cave.Domain;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Editor entry point for pure bounded Propagation verification.</summary>
    public static class PhenomenonTopologyVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Phenomenon Topology Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (PhenomenonTopologyVerification.TryRunAll(out failure))
            {
                Debug.Log("[Cave] Phenomenon topology verification passed.");
                return;
            }

            Debug.LogError("[Cave] Phenomenon topology verification failed: " + failure);
        }
    }
}
