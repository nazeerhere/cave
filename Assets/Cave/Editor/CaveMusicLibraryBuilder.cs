#if UNITY_EDITOR
using Cave.Audio;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Creates only the Cave-owned reference asset; vendor clips stay in place.</summary>
    public static class CaveMusicLibraryBuilder
    {
        private const string AssetPath = "Assets/Cave/Resources/CaveMusicLibrary.asset";

        [MenuItem("Tools/Cave/Audio/Create or Refresh Music Library")]
        public static void CreateOrRefresh()
        {
            CaveMusicLibrary library = AssetDatabase.LoadAssetAtPath<CaveMusicLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CaveMusicLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            SerializedObject serializedLibrary = new SerializedObject(library);
            Assign(serializedLibrary, "starting", "Assets/Action RPG Music 1.6/BGM12dungeon1.wav");
            Assign(serializedLibrary, "exploration", "Assets/Action RPG Music 1.6/BGM13dungeon2.wav");
            Assign(serializedLibrary, "combat", "Assets/Action RPG Music 1.6/BGM07battle2.wav");
            Assign(serializedLibrary, "intenseCombat", "Assets/Action RPG Music 1.6/BGM08boss2.wav");
            Assign(serializedLibrary, "heart", "Assets/Action RPG Music 1.6/BGM02evil.wav");
            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;
        }

        private static void Assign(SerializedObject library, string propertyName, string assetPath)
        {
            SerializedProperty property = library.FindProperty(propertyName);
            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }
        }
    }
}
#endif
