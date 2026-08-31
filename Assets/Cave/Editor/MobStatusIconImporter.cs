#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Applies pixel-art import settings and binds Cave-owned status icons.</summary>
    public sealed class MobStatusIconImporter : AssetPostprocessor
    {
        private const string IconDirectory = "Assets/Cave/UI/StatusIcons/";
        private const string RegistryPath = "Assets/Cave/Resources/MobStatusIconRegistry.asset";

        private static readonly KeyValuePair<MobStatusIconKind, string>[] Icons =
        {
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Poison, "Status_Poison.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Burn, "Status_Burn.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Slow, "Status_Slow.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.PinRoot, "Status_PinRoot.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Stagger, "Status_Stagger.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.TowerSuppression, "Status_TowerInterference.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Freeze, "Status_Freeze.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.StrengthBuff, "Status_StrengthBuff.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Overheal, "Status_Overheal.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Regeneration, "Status_Regeneration.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.WatcherMark, "Status_WatcherMark.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.GazeLock, "Status_GazeLock.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Possessed, "Status_Possessed.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.ElementallyBuffed, "Status_ElementallyBuffed.png"),
            new KeyValuePair<MobStatusIconKind, string>(MobStatusIconKind.Frenzied, "Status_Frenzied.png")
        };

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconDirectory, StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (path.StartsWith(IconDirectory, StringComparison.Ordinal))
                {
                    EditorApplication.delayCall += ApplyIconRegistry;
                    return;
                }
            }
        }

        [InitializeOnLoadMethod]
        private static void ScheduleInitialRegistryBind()
        {
            EditorApplication.delayCall += ApplyIconRegistry;
        }

        [MenuItem("Tools/Cave/Status Icons/Apply Import Settings and Registry")]
        public static void ApplyIconRegistry()
        {
            MobStatusIconRegistry registry = AssetDatabase.LoadAssetAtPath<MobStatusIconRegistry>(RegistryPath);
            if (registry == null)
            {
                return;
            }

            SerializedObject serializedRegistry = new SerializedObject(registry);
            SerializedProperty entries = serializedRegistry.FindProperty("icons");
            if (entries == null)
            {
                return;
            }

            foreach (KeyValuePair<MobStatusIconKind, string> icon in Icons)
            {
                string path = IconDirectory + icon.Value;
                ConfigureImporter(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    if ((MobStatusIconKind)entry.FindPropertyRelative("Kind").enumValueIndex != icon.Key)
                    {
                        continue;
                    }

                    entry.FindPropertyRelative("Icon").objectReferenceValue = sprite;
                    break;
                }
            }

            serializedRegistry.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool requiresReimport = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.filterMode != FilterMode.Point
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency;
            if (!requiresReimport)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
#endif
