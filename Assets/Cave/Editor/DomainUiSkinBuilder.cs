#if UNITY_EDITOR
using Cave.UI;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Builds the one Resources-visible skin asset while extracted art
    /// itself remains organized under Art/UI/Domain.</summary>
    public static class DomainUiSkinBuilder
    {
        private const string SkinPath = "Assets/Cave/Resources/UI/Domain/DomainUiSkin.asset";
        [MenuItem("Tools/Cave/UI/Domain/Build Domain UI Skin")]
        public static void BuildOrUpdate()
        {
            System.IO.Directory.CreateDirectory("Assets/Cave/Resources/UI/Domain");
            DomainUiSkin skin = AssetDatabase.LoadAssetAtPath<DomainUiSkin>(SkinPath);
            if (skin == null) { skin = ScriptableObject.CreateInstance<DomainUiSkin>(); AssetDatabase.CreateAsset(skin, SkinPath); }
            skin.SetForEditor(Load("Frames/LargePanel"), Load("Frames/MediumPanel"), Load("Frames/SmallPanel"),
                Load("Frames/NavigationFrame"), Load("Frames/SelectedNavigationFrame"), Load("Frames/ButtonFrame"),
                Load("Frames/SelectedButtonFrame"), Load("Frames/DisabledCardFrame"), Load("Frames/ActionButtonFrame"),
                Load("Bars/ProgressBar"), Load("Bars/ReserveBar"), Load("Decorations/DividerLong"), Load("Icons/States/Reserve"));
            EditorUtility.SetDirty(skin); AssetDatabase.SaveAssets();
        }
        private static Sprite Load(string relative) { return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Cave/Art/UI/Domain/" + relative + ".png"); }
    }
}
#endif
