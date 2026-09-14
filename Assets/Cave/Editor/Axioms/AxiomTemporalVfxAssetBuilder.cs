#if UNITY_EDITOR
using System.Collections.Generic;
using Cave.Axioms.Vfx;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Imports already-split approved temporal frames; it never slices the source sheet.</summary>
    [InitializeOnLoad]
    public static class AxiomTemporalVfxAssetBuilder
    {
        private const string Root = "Assets/Cave/Art/VFX/Axioms/TemporalControl";
        private const string CatalogPath = "Assets/Cave/Resources/Axioms/AxiomTemporalVfxCatalog.asset";
        private const string PrefabPath = "Assets/Cave/Prefabs/VFX/Axioms/Vfx_TemporalControl.prefab";

        static AxiomTemporalVfxAssetBuilder() { EditorApplication.delayCall += CreateMissingAssets; }

        [MenuItem("Tools/Cave/Axioms/Build Temporal Control VFX Assets")]
        public static void Build()
        {
            ImportFrames();
            AxiomTemporalVfxCatalog catalog = AssetDatabase.LoadAssetAtPath<AxiomTemporalVfxCatalog>(CatalogPath);
            if (catalog == null)
            {
                EnsureFolder("Assets/Cave/Resources/Axioms");
                catalog = ScriptableObject.CreateInstance<AxiomTemporalVfxCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            GameObject prefab = CreatePrefab();
            catalog.Configure(prefab,
                Animation("01_pulse_ring", 12f, .42f), Animation("02_lock_star", 11f, .44f),
                Animation("03_orbital_lock", 10f, .48f), Animation("08_coherence_ring", 9f, .48f),
                Animation("06_lock_flash", 16f, .42f), Animation("05_error_glitch", 14f, .44f),
                Animation("07_fracture_collapse", 12f, .48f), Animation("10_unstable_break", 13f, .44f),
                Animation("09_ground_pulse", 12f, .4f), Animation("04_resonance_burst", 14f, .52f));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void CreateMissingAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<AxiomTemporalVfxCatalog>(CatalogPath) == null
                && AssetDatabase.IsValidFolder(Root)) Build();
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
                // TextureImporter.spriteAlignment is not available in Unity
                // 2021.3. Use the supported single-sprite pivot property instead.
                importer.spritePivot = path.Contains("09_ground_pulse")
                    ? new Vector2(0.5f, 0f)
                    : new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static AxiomSpriteAnimation Animation(string folder, float fps, float scale)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { Root + "/" + folder });
            List<Sprite> frames = new List<Sprite>();
            for (int index = 0; index < guids.Length; index++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[index]));
                if (sprite != null) frames.Add(sprite);
            }
            frames.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return new AxiomSpriteAnimation(frames.ToArray(), fps, scale);
        }

        private static GameObject CreatePrefab()
        {
            EnsureFolder("Assets/Cave/Prefabs/VFX/Axioms");
            GameObject source = new GameObject("Vfx_TemporalControl");
            try { source.AddComponent<SpriteRenderer>().sortingOrder = 81; source.AddComponent<AxiomVfxInstance>(); return PrefabUtility.SaveAsPrefabAsset(source, PrefabPath); }
            finally { Object.DestroyImmediate(source); }
        }

        private static void EnsureFolder(string fullPath)
        {
            string[] pieces = fullPath.Split('/'); string current = pieces[0];
            for (int index = 1; index < pieces.Length; index++) { string next = current + "/" + pieces[index]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, pieces[index]); current = next; }
        }
    }
}
#endif
