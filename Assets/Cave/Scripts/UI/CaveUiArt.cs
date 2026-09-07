using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>One-time application of the approved UI sheets to the existing UI hierarchy.</summary>
    public static class CaveUiArt
    {
        private const string ChromeResourcePath = "UI/Shared/CaveUiChromeSheet";
        private const string IconResourcePath = "UI/Icons/CaveUiIconSheet";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static bool loadAttempted;

        public static void ApplyOnce(Transform gameplayHud, Transform menus)
        {
            EnsureLoaded();
            if (Sprites.Count == 0)
            {
                return;
            }

            ApplyPanel(gameplayHud, "Player Health", "Ui_HeaderStrip", CaveUiTheme.Health);
            ApplyPanel(gameplayHud, "Player Stamina", "Ui_HeaderStrip", CaveUiTheme.Stamina);
            ApplyPanel(gameplayHud, "Player Mana", "Ui_HeaderStrip", CaveUiTheme.Mana);
            ApplyPanel(gameplayHud, "Player Strength Shield", "Ui_HeaderStrip", CaveUiTheme.BorderBright);
            ApplyPanel(gameplayHud, "World Level", "Ui_HeaderStrip", CaveUiTheme.BronzeLight);
            ApplyPanel(gameplayHud, "Player Action Hotbar", "Ui_HeaderStrip", CaveUiTheme.BronzeLight);
            ApplyPanel(gameplayHud, "Player Special Mode", "Ui_HeaderStrip", CaveUiTheme.BorderBright);
            ApplyPanel(gameplayHud, "Special Mode Selection", "Ui_MainFrame", CaveUiTheme.BronzeLight);

            ApplyIcon(gameplayHud, "Player Health/Health Icon", "Ui_IconVitality", CaveUiTheme.Health);
            ApplyIcon(gameplayHud, "Player Stamina/Stamina Icon", "Ui_IconMode", CaveUiTheme.Stamina);
            ApplyIcon(gameplayHud, "Player Mana/Mana Icon", "Ui_IconMana", CaveUiTheme.Mana);
            ApplyIcon(gameplayHud, "Player Strength Shield/Shield Icon", "Ui_IconShield", CaveUiTheme.BorderBright);
            ApplyIcon(gameplayHud, "World Level/World Level Crest", "Ui_IconMode", CaveUiTheme.BorderBright);

            ApplyPanel(menus, "Pause Menu", "Ui_MainFrame", CaveUiTheme.BronzeLight);
            ApplyPanel(menus, "Settings Panel", "Ui_MainFrame", CaveUiTheme.BronzeLight);
            ApplyPanel(menus, "Moves List Panel", "Ui_MainFrame", CaveUiTheme.BronzeLight);
            ApplyPanel(menus, "Enemy Ledger Panel", "Ui_MainFrame", CaveUiTheme.BronzeLight);
        }

        private static void EnsureLoaded()
        {
            if (loadAttempted)
            {
                return;
            }

            loadAttempted = true;
            Cache(Resources.LoadAll<Sprite>(ChromeResourcePath));
            Cache(Resources.LoadAll<Sprite>(IconResourcePath));
        }

        private static void Cache(Sprite[] sprites)
        {
            for (int index = 0; index < sprites.Length; index++)
            {
                Sprite sprite = sprites[index];
                if (sprite != null)
                {
                    Sprites[sprite.name] = sprite;
                }
            }
        }

        private static void ApplyPanel(Transform root, string path, string spriteName, Color accent)
        {
            Transform target = root != null ? root.Find(path) : null;
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image == null || !Sprites.TryGetValue(spriteName, out Sprite sprite))
            {
                return;
            }

            if (image.sprite != sprite)
            {
                image.sprite = sprite;
            }

            image.type = Image.Type.Sliced;
            image.color = Tint(accent, 0.94f);
        }

        private static void ApplyIcon(Transform root, string path, string spriteName, Color accent)
        {
            Transform target = root != null ? root.Find(path) : null;
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image == null || !Sprites.TryGetValue(spriteName, out Sprite sprite))
            {
                return;
            }

            if (image.sprite != sprite)
            {
                image.sprite = sprite;
            }

            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Tint(accent, 1f);
        }

        private static Color Tint(Color accent, float alpha)
        {
            Color tint = Color.Lerp(new Color(0.72f, 0.7f, 0.66f, 1f), accent, 0.28f);
            tint.a = alpha;
            return tint;
        }
    }
}
