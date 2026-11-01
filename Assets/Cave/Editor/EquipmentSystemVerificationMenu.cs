#if UNITY_EDITOR
using Cave.Player;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class EquipmentSystemVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Equipment System Verification")]
        private static void Run()
        {
            if (!EquipmentSystemVerification.Run())
            {
                throw new System.InvalidOperationException(
                    "Equipment System Verification failed. See Console.");
            }

            Debug.Log("Equipment input, selection, and activation policies verified.");
        }
    }
}
#endif
