#if UNITY_EDITOR
using Cave.Player;
using Cave.Combat;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Focused editor validation for approved legendary cosmetic weapon sources and their non-gameplay contract.</summary>
    internal static class LegendaryWeaponImportVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Legendary Weapon Verification")]
        private static void Run()
        {
            string failure;
            if (!TryVerify(out failure))
            {
                Debug.LogError("[Cave] Legendary weapon verification failed: " + failure);
                return;
            }

            Debug.Log("[Cave] LEGENDARY WEAPON PASS");
        }

        internal static bool TryVerify(out string failure)
        {
            if (!PlayerSwordUpgradeVerification.TryRunAll(out failure))
            {
                return false;
            }

            if (!VerifyPlayerWeaponWiring("Assets/Cave/Prefabs/Player.prefab", out failure)
                || !VerifyPlayerWeaponWiring("Assets/Cave/Prefabs/Player_Sprint2.prefab", out failure))
            {
                return false;
            }

            if (!VerifyAsset(
                    "Assets/Cave/Resources/Cosmetics/Weapons/Astral_Nocturne.png",
                    new Vector2(.5f, .32f),
                    SwordCosmeticSelection.AstralNocturne,
                    out failure)
                || !VerifyAsset(
                    "Assets/Cave/Resources/Cosmetics/Weapons/Heavens_Defiance.png",
                    new Vector2(.5f, .35f),
                    SwordCosmeticSelection.HeavensDefiance,
                    out failure)
                || !VerifyAsset(
                    "Assets/Cave/Resources/Cosmetics/Weapons/Abyssal_Ossuary.png",
                    new Vector2(.5f, .47f),
                    SwordCosmeticSelection.AbyssalOssuary,
                    out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyPlayerWeaponWiring(string path, out string failure)
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            SpinSwordAttack spin = player != null ? player.GetComponent<SpinSwordAttack>() : null;
            ChargedAttack heavy = player != null ? player.GetComponent<ChargedAttack>() : null;
            GameObject sword = spin != null ? spin.SwordVisualObject : null;
            SpriteRenderer renderer = sword != null
                ? sword.GetComponentInChildren<SpriteRenderer>(true)
                : null;
            if (player == null || spin == null || heavy == null || spin.SwordPivot == null
                || sword == null || renderer == null || renderer.sprite == null)
            {
                failure = path + " does not resolve the inherited shared sword, pivot, renderer, and Heavy owner.";
                return false;
            }

            // Player.prefab is a variant of Player_Sprint2; GetComponent must
            // resolve that inherited SpinSwordAttack rather than requiring a
            // duplicate serialized component or weapon renderer.
            failure = null;
            return true;
        }

        private static bool VerifyAsset(
            string path,
            Vector2 expectedPivot,
            SwordCosmeticSelection selection,
            out string failure)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || importer == null)
            {
                failure = "Missing legendary weapon sprite: " + path;
                return false;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool correctImport = importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, 1024f)
                && settings.spriteAlignment == (int)SpriteAlignment.Custom
                && Vector2.Distance(settings.spritePivot, expectedPivot) < .0001f
                && importer.alphaIsTransparency
                && !importer.mipmapEnabled
                && importer.wrapMode == TextureWrapMode.Clamp
                && importer.filterMode == FilterMode.Bilinear
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.maxTextureSize == 2048;
            if (!correctImport)
            {
                failure = "Incorrect import settings for " + path + ".";
                return false;
            }

            if (!PlayerSwordCosmetics.IsLegendaryWeapon(selection)
                || !PlayerSwordCosmetics.IsTwoHandedWeapon(selection)
                || !Mathf.Approximately(PlayerSwordCosmetics.ReachMultiplierFor(selection), 1f)
                || PlayerSwordCosmetics.LoadSpriteFor(selection) == null)
            {
                failure = "Legendary weapon registration is incomplete for " + selection + ".";
                return false;
            }

            failure = null;
            return true;
        }
    }
}
#endif
