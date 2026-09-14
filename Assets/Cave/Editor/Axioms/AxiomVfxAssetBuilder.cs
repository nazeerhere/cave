#if UNITY_EDITOR
using System.Collections.Generic;
using Cave.Axioms.Vfx;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Editor-only creator for the Axiom VFX catalog and simple one-renderer prefabs.
    /// It uses only sprites sliced from CaveAxiomVfxSheet.png.
    /// </summary>
    [InitializeOnLoad]
    public static class AxiomVfxAssetBuilder
    {
        private const string CatalogPath = "Assets/Cave/Resources/Axioms/AxiomVfxCatalog.asset";
        private const string PrefabFolder = "Assets/Cave/Prefabs/VFX/Axioms";

        static AxiomVfxAssetBuilder()
        {
            EditorApplication.delayCall += CreateMissingAssetsAfterImport;
        }

        [MenuItem("Tools/Cave/Axioms/Build Axiom VFX Presentation Assets")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(CaveAxiomVfxSheetImporter.SheetPath, ImportAssetOptions.ForceUpdate);
            Dictionary<string, Sprite> sprites = LoadSprites();
            if (sprites.Count != 9)
            {
                Debug.LogError("Cave Axiom VFX build requires the nine sprites from the approved CaveAxiomVfxSheet.");
                return;
            }

            EnsureFolder("Assets/Cave/Resources/Axioms");
            EnsureFolder(PrefabFolder);

            AxiomVfxCatalog catalog = AssetDatabase.LoadAssetAtPath<AxiomVfxCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AxiomVfxCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(
                Definition(CreatePrefab("Vfx_ResonanceProgress", sprites["Axiom_CyanRing"]), .22f, .42f, new Vector2(0f, .45f), .06f),
                Definition(CreatePrefab("Vfx_ResonanceBreak", sprites["Axiom_CyanFracture"]), .42f, .72f, new Vector2(0f, .4f), .12f),
                Definition(CreatePrefab("Vfx_Destabilized", sprites["Axiom_Glitch"]), .5f, .62f, new Vector2(0f, .35f), .3f),
                Definition(CreatePrefab("Vfx_PhaseApply", sprites["Axiom_PhaseCrystal"]), .28f, .54f, new Vector2(0f, .4f), .08f),
                Definition(CreatePrefab("Vfx_PhaseCollapseStrong", sprites["Axiom_Glitch"]), .55f, .86f, new Vector2(0f, .42f), .15f),
                Definition(CreatePrefab("Vfx_PhaseCollapseWeak", sprites["Axiom_Void"]), .5f, .7f, new Vector2(0f, .42f), .15f),
                Definition(CreatePrefab("Vfx_PhaseCollapseEven", sprites["Axiom_Recohere"]), .3f, .54f, new Vector2(0f, .42f), .12f),
                Definition(CreatePrefab("Vfx_PhaseVulnerability", sprites["Axiom_Void"]), 3f, .48f, new Vector2(0f, .45f), 0f),
                Definition(CreatePrefab("Vfx_ErrorState", sprites["Axiom_Glitch"]), .34f, .54f, new Vector2(0f, .45f), .22f),
                Definition(CreatePrefab("Vfx_ErrorRate", sprites["Axiom_CyanRing"]), .3f, .46f, new Vector2(0f, .45f), .22f),
                Definition(CreatePrefab("Vfx_ErrorAcceleration", sprites["Axiom_CyanFracture"]), .36f, .58f, new Vector2(0f, .45f), .22f),
                Definition(CreatePrefab("Vfx_ControlSuccess", sprites["Axiom_Recohere"]), .3f, .52f, new Vector2(0f, .46f), .08f),
                Definition(CreatePrefab("Vfx_ControlFailure", sprites["Axiom_Glitch"]), .22f, .4f, new Vector2(0f, .46f), .16f),
                Definition(CreatePrefab("Vfx_Counterphase", sprites["Axiom_CyanRing"]), .32f, .58f, new Vector2(0f, .45f), .12f),
                Definition(CreatePrefab("Vfx_Heat", sprites["Axiom_Heat"]), .28f, .56f, new Vector2(0f, .35f), .08f),
                Definition(CreatePrefab("Vfx_Order", sprites["Axiom_Order"]), .3f, .52f, new Vector2(0f, .42f), .08f),
                Definition(CreatePrefab("Vfx_Flow", sprites["Axiom_Flow"]), .3f, .5f, new Vector2(0f, .25f), .08f),
                Definition(CreatePrefab("Vfx_Mass", sprites["Axiom_PhaseCrystal"]), .32f, .48f, new Vector2(0f, .1f), .08f));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built Cave Axiom VFX catalog and 18 approved-sheet prefabs.");
        }

        private static void CreateMissingAssetsAfterImport()
        {
            if (AssetDatabase.LoadAssetAtPath<AxiomVfxCatalog>(CatalogPath) != null
                || AssetDatabase.LoadAssetAtPath<Texture2D>(CaveAxiomVfxSheetImporter.SheetPath) == null)
            {
                return;
            }

            Build();
        }

        private static AxiomVfxDefinition Definition(GameObject prefab, float lifetime, float scale, Vector2 offset, float cooldown)
        {
            return new AxiomVfxDefinition(prefab, lifetime, scale, offset, cooldown);
        }

        private static GameObject CreatePrefab(string prefabName, Sprite sprite)
        {
            string path = PrefabFolder + "/" + prefabName + ".prefab";
            GameObject source = new GameObject(prefabName);
            try
            {
                SpriteRenderer renderer = source.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 80;
                source.AddComponent<AxiomVfxInstance>();
                return PrefabUtility.SaveAsPrefabAsset(source, path);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static Dictionary<string, Sprite> LoadSprites()
        {
            Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(CaveAxiomVfxSheetImporter.SheetPath);
            for (int index = 0; index < assets.Length; index++)
            {
                Sprite sprite = assets[index] as Sprite;
                if (sprite != null)
                {
                    result[sprite.name] = sprite;
                }
            }
            return result;
        }

        private static void EnsureFolder(string fullPath)
        {
            string[] pieces = fullPath.Split('/');
            string current = pieces[0];
            for (int index = 1; index < pieces.Length; index++)
            {
                string next = current + "/" + pieces[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, pieces[index]);
                }
                current = next;
            }
        }
    }
}
#endif
