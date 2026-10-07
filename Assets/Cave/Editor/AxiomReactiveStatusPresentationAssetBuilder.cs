#if UNITY_EDITOR
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class AxiomReactiveStatusPresentationAssetBuilder
    {
        private const string IconRegistryPath = "Assets/Cave/Resources/MobStatusIconRegistry.asset";
        private const string OverlayRegistryPath = "Assets/Cave/Resources/AxiomControlOverlayRegistry.asset";
        private const string BaseIconFolder = "Assets/Cave/UI/StatusIcons/Axioms/";
        private const string OverlayFolder = "Assets/Cave/Art/UI/AxiomControl/";

        [MenuItem("Tools/Cave/Axioms/Build Reactive Status Presentation Assets")]
        public static void Build()
        {
            MobStatusIconRegistry icons = AssetDatabase.LoadAssetAtPath<MobStatusIconRegistry>(IconRegistryPath);
            if (icons == null)
            {
                Debug.LogError("Reactive Axiom status presentation requires MobStatusIconRegistry.asset.");
                return;
            }

            icons.SetIconForEditor(MobStatusIconKind.AxiomPotential, Load(BaseIconFolder + "01_potential.png"));
            icons.SetIconForEditor(MobStatusIconKind.AxiomCompression, Load(BaseIconFolder + "02_compression.png"));
            icons.SetIconForEditor(MobStatusIconKind.AxiomOrder, Load(BaseIconFolder + "03_order.png"));
            icons.SetIconForEditor(MobStatusIconKind.AxiomHeat, Load(BaseIconFolder + "04_heat.png"));
            icons.SetIconForEditor(MobStatusIconKind.AxiomFlow, Load(BaseIconFolder + "05_flow.png"));
            icons.SetIconForEditor(MobStatusIconKind.AxiomMass, Load(BaseIconFolder + "06_mass.png"));
            icons.SetIconForEditor(MobStatusIconKind.AxiomResonance, Load(BaseIconFolder + "07_resonance.png"));
            icons.SetIconForEditor(MobStatusIconKind.AxiomPhase, Load(BaseIconFolder + "08_phase.png"));
            EditorUtility.SetDirty(icons);

            AxiomControlOverlayRegistry overlays = AssetDatabase.LoadAssetAtPath<AxiomControlOverlayRegistry>(OverlayRegistryPath);
            if (overlays == null)
            {
                overlays = ScriptableObject.CreateInstance<AxiomControlOverlayRegistry>();
                AssetDatabase.CreateAsset(overlays, OverlayRegistryPath);
            }

            overlays.ConfigureForEditor(
                Load(OverlayFolder + "state_S_active.png"),
                Load(OverlayFolder + "state_R_active.png"),
                Load(OverlayFolder + "state_A_active.png"),
                Load(OverlayFolder + "badge_S.png"),
                Load(OverlayFolder + "badge_R.png"),
                Load(OverlayFolder + "badge_A.png"),
                Load(OverlayFolder + "arrow_up.png"),
                Load(OverlayFolder + "arrow_down.png"),
                Load(OverlayFolder + "result_success.png"),
                Load(OverlayFolder + "result_failure.png"));
            EditorUtility.SetDirty(overlays);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built Reactive Axiom world-status presentation assets.");
        }

        [InitializeOnLoadMethod]
        private static void BindNewReactiveAssetsAfterCompilation()
        {
            // The approved art lives under Assets and is imported by Unity, but
            // the ScriptableObject bindings cannot be represented by a filename
            // alone. Delay until compilation/import has settled, then perform
            // this idempotent editor-only binding once when a newly added badge
            // is absent from the registry.
            EditorApplication.delayCall += BindIfNeeded;
        }

        private static void BindIfNeeded()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += BindIfNeeded;
                return;
            }

            AxiomControlOverlayRegistry overlays = AssetDatabase.LoadAssetAtPath<AxiomControlOverlayRegistry>(OverlayRegistryPath);
            if (overlays == null
                || overlays.GetFrame(Cave.Axioms.AxiomErrorKind.State) == null
                || overlays.GetBadge(Cave.Axioms.AxiomErrorKind.State) == null
                || overlays.GetMarker(AxiomReactiveMarker.Success) == null)
            {
                Build();
            }
        }

        private static Sprite Load(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException("Missing approved reactive status sprite: " + path);
            }

            return sprite;
        }
    }
}
#endif
