#if UNITY_EDITOR
using System.Collections.Generic;
using Cave.Axioms.Vfx;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Creates references and lightweight prefabs from the already split,
    /// approved Imaginary VFX frames. It never slices or imports the source sheet.
    /// </summary>
    [InitializeOnLoad]
    public static class AxiomImaginaryVfxAssetBuilder
    {
        private const string Root = "Assets/Cave/Art/VFX/Axioms/Imaginary";
        private const string ShadowFolder = Root + "/ShadowSmear";
        private const string ImpactFolder = Root + "/ImpactRupture";
        private const string CatalogPath = "Assets/Cave/Resources/Axioms/AxiomImaginaryVfxCatalog.asset";
        private const string PrefabFolder = "Assets/Cave/Prefabs/VFX/Axioms";

        static AxiomImaginaryVfxAssetBuilder()
        {
            EditorApplication.delayCall += CreateMissingAssets;
        }

        [MenuItem("Tools/Cave/Axioms/Build Imaginary VFX Assets")]
        public static void Build()
        {
            ImportFrames();
            AxiomSpriteAnimation shadow = Animation(ShadowFolder, 10f, .55f);
            AxiomSpriteAnimation impact = Animation(ImpactFolder, 16f, .64f);
            if (!shadow.IsValid || !impact.IsValid)
            {
                Debug.LogError("Cave Imaginary VFX build requires the approved ShadowSmear and ImpactRupture frame folders.");
                return;
            }

            EnsureFolder("Assets/Cave/Resources/Axioms");
            EnsureFolder(PrefabFolder);
            AxiomImaginaryVfxCatalog catalog = AssetDatabase.LoadAssetAtPath<AxiomImaginaryVfxCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AxiomImaginaryVfxCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(
                CreatePrefab("Vfx_ImaginaryShadow", -1, new Color(1f, 1f, 1f, .42f)),
                // Keep impacts above actors but beneath the world-status icon band (18+).
                CreatePrefab("Vfx_ImaginaryImpact", 17, Color.white),
                shadow,
                impact);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void CreateMissingAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<AxiomImaginaryVfxCatalog>(CatalogPath) == null
                && AssetDatabase.IsValidFolder(ShadowFolder)
                && AssetDatabase.IsValidFolder(ImpactFolder))
            {
                Build();
            }
        }

        private static void ImportFrames()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 256f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.spritePivot = new Vector2(.5f, .5f);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static AxiomSpriteAnimation Animation(string folder, float framesPerSecond, float scale)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            List<Sprite> frames = new List<Sprite>();
            for (int index = 0; index < guids.Length; index++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[index]));
                if (sprite != null) frames.Add(sprite);
            }
            frames.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return new AxiomSpriteAnimation(frames.ToArray(), framesPerSecond, scale);
        }

        private static GameObject CreatePrefab(string prefabName, int sortingOrder, Color color)
        {
            string path = PrefabFolder + "/" + prefabName + ".prefab";
            GameObject source = new GameObject(prefabName);
            try
            {
                SpriteRenderer renderer = source.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = sortingOrder;
                renderer.color = color;
                AxiomVfxInstance instance = source.AddComponent<AxiomVfxInstance>();
                SerializedObject serialized = new SerializedObject(instance);
                serialized.FindProperty("rotationsPerSecond").floatValue = 0f;
                serialized.FindProperty("endScaleMultiplier").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(source, path);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static void EnsureFolder(string fullPath)
        {
            string[] pieces = fullPath.Split('/');
            string current = pieces[0];
            for (int index = 1; index < pieces.Length; index++)
            {
                string next = current + "/" + pieces[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, pieces[index]);
                current = next;
            }
        }
    }
}
#endif
