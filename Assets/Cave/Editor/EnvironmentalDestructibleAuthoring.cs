using System;
using System.IO;
using Cave.Domain;
using Cave.Environmental;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Idempotent creation of the approved environmental data profiles.</summary>
    [InitializeOnLoad]
    public static class EnvironmentalDestructibleAuthoring
    {
        private const string Root = "Assets/Cave/Resources/Environment";
        private const string MaterialRoot = Root + "/Materials";
        private const string FloraRoot = Root + "/Flora";

        static EnvironmentalDestructibleAuthoring()
        {
            EditorApplication.delayCall += EnsureProfilesWhenEditorIsReady;
        }

        [MenuItem("Tools/Cave/Environment/Build Environmental Profiles")]
        public static void BuildProfiles()
        {
            EnsureFolder("Assets/Cave/Resources", "Environment");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Flora");
            foreach (EnvironmentalMaterialFamily family in Enum.GetValues(typeof(EnvironmentalMaterialFamily)))
                EnsureMaterial(family);
            foreach (CaveFloraSpecies species in Enum.GetValues(typeof(CaveFloraSpecies)))
                EnsureFlora(species);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Cave] Environmental material and flora profiles are present.");
        }

        private static void EnsureProfilesWhenEditorIsReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureProfilesWhenEditorIsReady;
                return;
            }
            BuildProfiles();
        }

        private static void EnsureMaterial(EnvironmentalMaterialFamily family)
        {
            string path = MaterialRoot + "/" + family + ".asset";
            EnvironmentalMaterialProfile profile = AssetDatabase.LoadAssetAtPath<EnvironmentalMaterialProfile>(path);
            if (profile != null) return;
            profile = ScriptableObject.CreateInstance<EnvironmentalMaterialProfile>();
            SerializedObject serialized = new SerializedObject(profile);
            serialized.FindProperty("family").enumValueIndex = (int)family;
            serialized.FindProperty("structuralResistance").floatValue = Resistance(family);
            serialized.FindProperty("impactSensitivity").floatValue = Sensitivity(family);
            serialized.FindProperty("heavyImpactMultiplier").floatValue = HeavyMultiplier(family);
            serialized.FindProperty("collisionBehavior").enumValueIndex = (int)EnvironmentalCollisionBehavior.Static;
            serialized.FindProperty("supportsRegeneration").boolValue = family == EnvironmentalMaterialFamily.Crystal
                || family == EnvironmentalMaterialFamily.FungalTissue
                || family == EnvironmentalMaterialFamily.MineralizedBiologicalTissue
                || family == EnvironmentalMaterialFamily.PhenomenalBiologicalTissue;
            serialized.FindProperty("defaultRegenerationSeconds").floatValue = 3f;
            serialized.FindProperty("destructionPresentation").enumValueIndex =
                (int)EnvironmentalDestructionPresentation.AuthoredSpriteSwap;
            serialized.FindProperty("maximumAuthoredDebrisPieces").intValue = family == EnvironmentalMaterialFamily.Metal ? 0 : 3;
            SerializedProperty responses = serialized.FindProperty("phenomenonResponses");
            Array values = Enum.GetValues(typeof(LawPhenomenon));
            responses.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                LawPhenomenon phenomenon = (LawPhenomenon)values.GetValue(i);
                SerializedProperty response = responses.GetArrayElementAtIndex(i);
                response.FindPropertyRelative("phenomenon").enumValueIndex = (int)phenomenon - 1;
                response.FindPropertyRelative("integrityMultiplier").floatValue = PhenomenonMultiplier(family, phenomenon);
                response.FindPropertyRelative("stabilityChange").floatValue = StabilityChange(family, phenomenon);
                response.FindPropertyRelative("canTriggerStoredRelease").boolValue = family == EnvironmentalMaterialFamily.Crystal;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, path);
        }

        private static void EnsureFlora(CaveFloraSpecies species)
        {
            string path = FloraRoot + "/" + species + ".asset";
            if (AssetDatabase.LoadAssetAtPath<FloraSpeciesProfile>(path) != null) return;
            FloraSpeciesProfile profile = ScriptableObject.CreateInstance<FloraSpeciesProfile>();
            SerializedObject serialized = new SerializedObject(profile);
            serialized.FindProperty("species").enumValueIndex = (int)species;
            serialized.FindProperty("maximumPropagationChildren").intValue = species == CaveFloraSpecies.SovereignBloom ? 0 : 2;
            serialized.FindProperty("propagationRadius").floatValue = species == CaveFloraSpecies.VeilMycelium ? 5f : 3f;
            string materialName = species <= CaveFloraSpecies.VeilMycelium
                ? "FungalTissue" : species <= CaveFloraSpecies.WhisperFans
                    ? "MineralizedBiologicalTissue" : "PhenomenalBiologicalTissue";
            serialized.FindProperty("tissueMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnvironmentalMaterialProfile>(MaterialRoot + "/" + materialName + ".asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, path);
        }

        private static float Resistance(EnvironmentalMaterialFamily family)
        {
            switch (family)
            {
                case EnvironmentalMaterialFamily.Wood: return .8f;
                case EnvironmentalMaterialFamily.Stone: return 1.8f;
                case EnvironmentalMaterialFamily.Crystal: return 1.1f;
                case EnvironmentalMaterialFamily.Metal: return 2.2f;
                default: return 1f;
            }
        }

        private static float Sensitivity(EnvironmentalMaterialFamily family) => family == EnvironmentalMaterialFamily.Crystal ? 1.2f : 1f;
        private static float HeavyMultiplier(EnvironmentalMaterialFamily family) => family == EnvironmentalMaterialFamily.Metal ? 1.25f : 1.5f;
        private static float PhenomenonMultiplier(EnvironmentalMaterialFamily family, LawPhenomenon phenomenon)
        {
            if (phenomenon == LawPhenomenon.Heat && family == EnvironmentalMaterialFamily.Wood) return 1.5f;
            if (phenomenon == LawPhenomenon.Compression && (family == EnvironmentalMaterialFamily.Stone || family == EnvironmentalMaterialFamily.Crystal)) return 1.35f;
            if (phenomenon == LawPhenomenon.Phase && family == EnvironmentalMaterialFamily.Metal) return .75f;
            if (phenomenon == LawPhenomenon.Order) return .5f;
            return 1f;
        }
        private static float StabilityChange(EnvironmentalMaterialFamily family, LawPhenomenon phenomenon)
        {
            if (phenomenon == LawPhenomenon.Order) return 0f;
            if (phenomenon == LawPhenomenon.Resonance || phenomenon == LawPhenomenon.Compression) return .25f;
            return .1f;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string full = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
