using Cave.Axioms.Elemental;
using UnityEditor;
using UnityEngine;
namespace Cave.EditorTools { public static class ExpandedPhenomenonRuntimeVerificationMenu { [MenuItem("Tools/Cave/Verification/Run Expanded Phenomenon Runtime Verification")] private static void RunFromMenu() { string f; if (ExpandedPhenomenonRuntimeVerification.TryRunAll(out f)) Debug.Log("[Cave] EXPANDED PHENOMENON RUNTIME PASS"); else Debug.LogError("[Cave] Expanded phenomenon runtime verification failed: " + f); } } }
