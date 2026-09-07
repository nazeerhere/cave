#if UNITY_EDITOR
using Cave.Axioms;
using Cave.Player;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Explicit, one-shot installation for the canonical player prefab.</summary>
    public static class AxiomPlayerPrefabInstaller
    {
        private const string PlayerPrefabPath = "Assets/Cave/Prefabs/Player.prefab";

        [MenuItem("Tools/Cave/Axioms/Install Passive Player Observation")]
        public static void InstallPassivePlayerObservation()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (playerRoot.GetComponent<PlayerController>() == null)
                {
                    Debug.LogError("Cave Axioms installer found no PlayerController on the canonical player prefab.");
                    return;
                }

                bool changed = false;
                if (playerRoot.GetComponent<PlayerMovementTelemetry>() == null)
                {
                    playerRoot.AddComponent<PlayerMovementTelemetry>();
                    changed = true;
                }

                if (playerRoot.GetComponent<AxiomRuntimeState>() == null)
                {
                    playerRoot.AddComponent<AxiomRuntimeState>();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }
    }
}
#endif
