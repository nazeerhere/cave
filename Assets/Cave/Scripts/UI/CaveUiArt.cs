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
        private const string MoveIconResourcePath = "MovesList/MovesListIconSheet";
        private const string CurseIconResourcePath = "UI/Icons/11_Curse_Icons";
        private const string ApprovedHudResourcePath = "UI/HUD/Approved";
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
            ApplyPanel(gameplayHud, "Player Status Effects", "Ui_HeaderStrip", CaveUiTheme.Border);
            ApplyPanel(gameplayHud, "Player Curse Strip", "Ui_HeaderStrip", new Color(0.68f, 0.34f, 0.92f, 1f));
            ApplyPanel(gameplayHud, "Curse Altar Selection", "Ui_MainFrame", new Color(0.68f, 0.34f, 0.92f, 1f));
            ApplyPanel(gameplayHud, "Special Mode Selection/MODES Button", "Ui_TabNormal", CaveUiTheme.BorderBright);
            ApplyPanel(gameplayHud, "Special Mode Selection/SHOP Button", "Ui_TabNormal", CaveUiTheme.BronzeLight);

            ApplyIcon(gameplayHud, "Player Health/Health Icon", "Ui_IconVitality", CaveUiTheme.Health);
            ApplyIcon(gameplayHud, "Player Stamina/Stamina Icon", "Ui_IconMode", CaveUiTheme.Stamina);
            ApplyIcon(gameplayHud, "Player Mana/Mana Icon", "Ui_IconMana", CaveUiTheme.Mana);
            ApplyIcon(gameplayHud, "Player Strength Shield/Shield Icon", "Ui_IconShield", CaveUiTheme.BorderBright);

            ApplyPanel(menus, "Pause Menu", "Ui_MainFrame", CaveUiTheme.BronzeLight);
            ApplyPanel(menus, "Settings Panel", "Ui_MainFrame", CaveUiTheme.BronzeLight);
            ApplyPanel(menus, "Moves List Panel", "Ui_MainFrame", CaveUiTheme.BronzeLight);
            ApplyPanel(menus, "Enemy Ledger Panel", "Ui_MainFrame", CaveUiTheme.BronzeLight);

            // The bootstrap owns behaviour.  This pass only replaces the generic Image
            // presentation with the shared authored chrome once the hierarchy exists.
            ApplyChromeTree(gameplayHud);
            ApplyChromeTree(menus);
            ApplyApprovedHud(gameplayHud);
        }

        public static Sprite GetSprite(string spriteName)
        {
            EnsureLoaded();
            Sprites.TryGetValue(spriteName, out Sprite sprite);
            return sprite;
        }

        public static void ApplyCard(Image image, bool selected = false, Color? accent = null)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = GetSprite(selected ? "Ui_SlotActive" : "Ui_Row");
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Tint(accent ?? CaveUiTheme.BronzeLight, selected ? 1f : 0.94f);
        }

        public static void ApplyButton(Button button, bool selected = false)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            Sprite normal = GetSprite(selected ? "Ui_ButtonActive" : "Ui_Button");
            Sprite highlighted = GetSprite("Ui_ButtonActive");
            if (image == null || normal == null)
            {
                return;
            }

            image.sprite = normal;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            if (highlighted != null)
            {
                SpriteState state = button.spriteState;
                state.highlightedSprite = highlighted;
                state.selectedSprite = highlighted;
                state.pressedSprite = highlighted;
                state.disabledSprite = normal;
                button.spriteState = state;
                button.transition = Selectable.Transition.SpriteSwap;
            }
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
            Cache(Resources.LoadAll<Sprite>(MoveIconResourcePath));
            Cache(Resources.LoadAll<Sprite>(CurseIconResourcePath));
            Cache(Resources.LoadAll<Sprite>(ApprovedHudResourcePath));
        }

        public static void ApplySkillTab(Button button, bool selected, bool shop)
        {
            if (button == null) return;
            string name = shop
                ? (selected ? "ShopTabActive" : "ShopTabInactive")
                : (selected ? "SkillPathTabActive" : "SkillPathTabInactive");
            Sprite sprite = GetSprite(name);
            Image image = button.GetComponent<Image>();
            if (sprite != null && image != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
        }

        private static void ApplyApprovedHud(Transform gameplayHud)
        {
            if (gameplayHud == null || GetSprite("ResourceHudHealthFrame") == null) return;

            ConfigureResourcePanel(gameplayHud, "Player Health", new Vector2(20f, -18f),
                new Vector2(360f, 82f), "ResourceHudHealthFrame", "Health", 218f);
            ConfigureResourcePanel(gameplayHud, "Player Stamina", new Vector2(20f, -104f),
                new Vector2(270f, 52f), "ResourceHudShortFrame", "Stamina", 178f);
            ConfigureResourcePanel(gameplayHud, "Player Mana", new Vector2(20f, -160f),
                new Vector2(270f, 52f), "ResourceHudShortFrame", "Mana", 178f);
            DisableDetachedGryphon(gameplayHud.Find("Player Health"));
            DisableLegacyDecoration(gameplayHud.Find("Player Health"));
            DisableLegacyDecoration(gameplayHud.Find("Player Stamina"));
            DisableLegacyDecoration(gameplayHud.Find("Player Mana"));
            DisableLegacyTrack(gameplayHud.Find("Player Health/Health Bar Background"));
            DisableLegacyTrack(gameplayHud.Find("Player Stamina/Stamina Bar Background"));
            DisableLegacyTrack(gameplayHud.Find("Player Mana/Mana Bar Background"));

            Transform world = gameplayHud.Find("World Level");
            Image worldImage = world != null ? world.GetComponent<Image>() : null;
            Sprite worldSprite = GetSprite("WorldLevelFrame");
            if (world is RectTransform worldRect && worldImage != null && worldSprite != null)
            {
                worldRect.anchoredPosition = new Vector2(0f, -12f);
                worldRect.sizeDelta = new Vector2(386f, 140f);
                worldImage.sprite = worldSprite;
                worldImage.type = Image.Type.Simple;
                worldImage.color = Color.white;
                Text bakedDuplicate = world.Find("World Level Label")?.GetComponent<Text>();
                if (bakedDuplicate != null) bakedDuplicate.gameObject.SetActive(false);
                RectTransform crest = world.Find("World Level Crest") as RectTransform;
                if (crest != null)
                {
                    crest.anchoredPosition = new Vector2(0f, -8f);
                    crest.sizeDelta = new Vector2(74f, 74f);
                    Image crestBacking = crest.GetComponent<Image>();
                    if (crestBacking != null) crestBacking.enabled = false;
                    DisableLegacyDecoration(crest);
                }
                DisableLegacyDecoration(world);
            }

            // Shield remains a separate gameplay readout, but its duplicated
            // procedural metal strips/end cap are not part of the approved HUD.
            Transform shield = gameplayHud.Find("Player Strength Shield");
            DisableLegacyDecoration(shield);
            DisableLegacyTrack(shield != null ? shield.Find("Shield Recharge Background") : null);

            Transform selection = gameplayHud.Find("Special Mode Selection");
            if (selection is RectTransform selectionRect)
            {
                selectionRect.sizeDelta = new Vector2(560f, 650f);
                Image panel = selection.GetComponent<Image>();
                Sprite panelSprite = GetSprite("SkillPathPanelFrame");
                if (panel != null && panelSprite != null)
                {
                    panel.sprite = panelSprite;
                    panel.type = Image.Type.Sliced;
                    panel.color = Color.white;
                }

                Button skillTab = selection.Find("MODES Button")?.GetComponent<Button>();
                Button shopTab = selection.Find("SHOP Button")?.GetComponent<Button>();
                Button domainTab = selection.Find("DOMAIN Button")?.GetComponent<Button>();
                Text skillLabel = skillTab != null ? skillTab.GetComponentInChildren<Text>() : null;
                if (skillLabel != null) skillLabel.text = "SKILL PATH";
                ApplySkillTab(skillTab, true, false);
                ApplySkillTab(shopTab, false, true);
                ApplySkillTab(domainTab, false, false);
                ApplyApprovedRows(selection.Find("Modes Content"));
                ApplyApprovedRows(selection.Find("Shop Content"));
                ApplyApprovedRows(selection.Find("Domain Content"));
            }
        }

        private static void DisableDetachedGryphon(Transform healthPanel)
        {
            if (healthPanel == null) return;
            Transform existing = healthPanel.Find("Approved Gryphon");
            if (existing != null) existing.gameObject.SetActive(false);
        }

        private static void DisableLegacyDecoration(Transform root)
        {
            if (root == null) return;
            string[] names =
            {
                "Iron Top", "Iron Bottom", "Iron Left", "Iron Right",
                "Bronze Top Inlay", "Accent Bottom Inlay", "Forged End Cap", "End Cap Rune",
                "Left Forged Wing", "Right Forged Wing", "Left World Rune", "Right World Rune",
                "Free UI Package Frame", "Corner Ornament 0", "Corner Ornament 1",
                "Corner Ornament 2", "Corner Ornament 3"
            };
            for (int index = 0; index < names.Length; index++)
            {
                Transform child = root.Find(names[index]);
                if (child != null) child.gameObject.SetActive(false);
            }

            Outline outline = root.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        private static void DisableLegacyTrack(Transform track)
        {
            Image image = track != null ? track.GetComponent<Image>() : null;
            if (image != null) image.enabled = false;
        }

        private static void ApplyApprovedRows(Transform content)
        {
            Sprite row = GetSprite("HudCardRow");
            if (content == null || row == null) return;
            Image[] images = content.GetComponentsInChildren<Image>(true);
            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                if (image.GetComponent<Button>() != null || image.gameObject.name.IndexOf(" Card") < 0) continue;
                image.sprite = row;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
        }

        private static void ConfigureResourcePanel(
            Transform root,
            string panelName,
            Vector2 position,
            Vector2 size,
            string frameName,
            string prefix,
            float barWidth)
        {
            Transform panelTransform = root.Find(panelName);
            if (!(panelTransform is RectTransform panel)) return;
            panel.anchoredPosition = position;
            panel.sizeDelta = size;
            Image panelImage = panel.GetComponent<Image>();
            Sprite frame = GetSprite(frameName);
            if (panelImage != null && frame != null)
            {
                panelImage.sprite = frame;
                panelImage.type = Image.Type.Simple;
                panelImage.preserveAspect = false;
                panelImage.color = Color.white;
                panelImage.raycastTarget = false;
            }

            Transform icon = panel.Find(prefix + " Icon");
            if (icon != null) icon.gameObject.SetActive(false);
            Text label = panel.Find(prefix + " Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = prefix == "Health" ? "HP" : prefix == "Stamina" ? "STA" : "MANA";
                bool health = prefix == "Health";
                label.rectTransform.anchoredPosition = new Vector2(health ? 110f : 54f, health ? -22f : -8f);
            }
            RectTransform value = panel.Find(prefix + " Value") as RectTransform;
            if (value != null)
            {
                bool health = prefix == "Health";
                value.anchoredPosition = new Vector2(size.x - (health ? 52f : 34f), health ? -22f : -8f);
            }
            RectTransform bar = panel.Find(prefix + " Bar Background") as RectTransform;
            if (bar != null)
            {
                bool health = prefix == "Health";
                bar.anchoredPosition = new Vector2(health ? 110f : 54f, health ? -58f : -35f);
                bar.sizeDelta = new Vector2(barWidth, 13f);
            }
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

        private static void ApplyChromeTree(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                ApplyButton(buttons[index]);
            }

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                if (image == null || image.GetComponent<Button>() != null || image.sprite != null)
                {
                    continue;
                }

                string name = image.gameObject.name;
                if (name.IndexOf("Surface") >= 0 || name.IndexOf(" Card") >= 0 || name.IndexOf(" Row") >= 0)
                {
                    ApplyCard(image);
                }
            }
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
