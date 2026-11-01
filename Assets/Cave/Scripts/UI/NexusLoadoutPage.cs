using System.Collections.Generic;
using Cave.Diagnostics;
using Cave.Inventory;
using Cave.Player;
using Cave.Progression;
using Cave.World;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>
    /// Presentation-only Nexus Loadout page. It combines the former Cosmetics
    /// sections without creating any new equipment, cosmetic, or inventory
    /// authority. Preview selections remain local until their explicit equip
    /// control is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NexusLoadoutPage : MonoBehaviour
    {
        private PlayerSkinCosmetics skins;
        private PlayerSwordCosmetics swords;
        private Font font;
        private PlayerSkinSelection previewSkin;
        private SwordCosmeticSelection previewSword;
        private bool selectedWeapon;
        private InventoryItemCategory inventoryCategory = InventoryItemCategory.Materials;
        private int inventoryIndex;

        private Image previewBody;
        private Image previewSwordImage;
        private Sprite defaultBody;
        private Text previewSummary;
        private Text selectionTitle;
        private Text selectionDetail;
        private Text selectionStatus;
        private Button equipSelectionButton;
        private Text equipSelectionLabel;
        private Button[] skinButtons;
        private Text[] skinLabels;
        private Button[] weaponButtons;
        private Text[] weaponLabels;

        private Text carapaceStatus;
        private Text carapaceUpgrades;
        private Image carapaceIntegrityFill;
        private Button carapaceButton;
        private Text carapaceButtonLabel;
        private Button[] inventoryCategoryButtons;
        private Button[] inventoryButtons;
        private Text[] inventoryLabels;
        private Text inventoryTitle;
        private Text inventoryDetail;
        private readonly List<InventoryEntrySnapshot> inventoryEntries = new List<InventoryEntrySnapshot>();

        private PlayerCollectedInventory observedCollected;
        private PlayerLandmineInventory observedConsumables;
        private PlayerResonantCarapace observedCarapace;

        public void Configure(PlayerSkinCosmetics skinCosmetics, PlayerSwordCosmetics swordCosmetics, Font uiFont)
        {
            skins = skinCosmetics;
            swords = swordCosmetics;
            font = uiFont;
            previewSkin = skins != null ? skins.SelectedAppearance : PlayerSkinSelection.DefaultMiner;
            previewSword = swords != null ? swords.SelectedAppearance : SwordCosmeticSelection.Default;
            Build();
            Refresh();
        }

        private void OnEnable()
        {
            DeveloperDiagnosticsSettings.Changed += Refresh;
            BindAuthorities();
            Refresh();
        }

        private void OnDisable()
        {
            DeveloperDiagnosticsSettings.Changed -= Refresh;
            UnbindAuthorities();
        }

        private void Build()
        {
            if (skinButtons != null) return;
            Image backdrop = gameObject.AddComponent<Image>();
            backdrop.color = CaveUiTheme.SurfaceInset;

            BuildPreviewPanel();
            BuildCarapacePanel();
            BuildCollectionsPanel();
            BuildInventoryPanel();
        }

        private void BuildPreviewPanel()
        {
            Panel("Character Preview Panel", transform, new Vector2(-315f, 132f), new Vector2(590f, 260f));
            Heading("Character Preview Heading", "CHARACTER PREVIEW", new Vector2(-315f, 237f), new Vector2(540f, 24f));
            defaultBody = FirstSprite("Player/MinerFullActionSheet", "Miner_Idle_01");
            previewBody = Image("Loadout Preview Body", transform, new Vector2(-355f, 112f), new Vector2(190f, 190f), defaultBody);
            previewBody.preserveAspect = true;
            previewSwordImage = Image("Loadout Preview Sword", transform, new Vector2(-245f, 93f), new Vector2(92f, 138f), null);
            previewSwordImage.preserveAspect = true;
            previewSummary = Label("Loadout Preview Summary", transform, string.Empty, 12,
                new Vector2(-60f, 132f), new Vector2(210f, 128f), TextAnchor.UpperLeft, CaveUiTheme.PrimaryText);
            Label("Loadout Preview Hint", transform, "Preview only — no combat objects or hitboxes are created.", 10,
                new Vector2(-315f, 28f), new Vector2(540f, 22f), TextAnchor.MiddleCenter, CaveUiTheme.SecondaryText);
        }

        private void BuildCarapacePanel()
        {
            Panel("Resonant Carapace Panel", transform, new Vector2(315f, 132f), new Vector2(590f, 260f));
            Heading("Resonant Carapace Heading", "RESONANT CARAPACE", new Vector2(315f, 237f), new Vector2(540f, 24f));
            carapaceStatus = Label("Loadout Carapace Status", transform, string.Empty, 13,
                new Vector2(315f, 158f), new Vector2(510f, 86f), TextAnchor.UpperLeft, CaveUiTheme.PrimaryText);
            Image integrityTrack = Image("Loadout Carapace Integrity Track", transform,
                new Vector2(315f, 105f), new Vector2(440f, 10f), null);
            integrityTrack.color = CaveUiTheme.SurfaceInset;
            carapaceIntegrityFill = Image("Loadout Carapace Integrity Fill", integrityTrack.transform,
                Vector2.zero, new Vector2(436f, 6f), null);
            RectTransform integrityFillRect = carapaceIntegrityFill.rectTransform;
            integrityFillRect.anchorMin = new Vector2(0f, .5f);
            integrityFillRect.anchorMax = new Vector2(0f, .5f);
            integrityFillRect.pivot = new Vector2(0f, .5f);
            integrityFillRect.anchoredPosition = new Vector2(2f, 0f);
            carapaceIntegrityFill.color = new Color(.64f, .38f, 1f, .9f);
            carapaceUpgrades = Label("Loadout Carapace Upgrades", transform, string.Empty, 11,
                new Vector2(315f, 77f), new Vector2(510f, 36f), TextAnchor.MiddleCenter, CaveUiTheme.SecondaryText);
            carapaceButton = Button("CARAPACE EQUIP", transform, new Vector2(315f, 37f), new Vector2(238f, 30f));
            carapaceButtonLabel = carapaceButton.GetComponentInChildren<Text>();
            carapaceButton.onClick.AddListener(ToggleCarapace);
        }

        private void BuildCollectionsPanel()
        {
            Panel("Skins And Weapons Panel", transform, new Vector2(-315f, -178f), new Vector2(590f, 300f));
            Heading("Skins And Weapons Heading", "SKINS & WEAPONS", new Vector2(-315f, -48f), new Vector2(540f, 24f));
            Label("Skins Heading", transform, "SKINS", 12, new Vector2(-448f, -78f), new Vector2(230f, 18f), TextAnchor.MiddleCenter, CaveUiTheme.Gold);
            Label("Weapons Heading", transform, "WEAPONS", 12, new Vector2(-182f, -78f), new Vector2(230f, 18f), TextAnchor.MiddleCenter, CaveUiTheme.Gold);

            skinButtons = new Button[PlayerSkinLibrary.Count];
            skinLabels = new Text[skinButtons.Length];
            for (int index = 0; index < skinButtons.Length; index++)
            {
                int captured = index;
                skinButtons[index] = Button("Skin " + index, transform, new Vector2(-448f, -100f - index * 20f), new Vector2(244f, 19f));
                skinButtons[index].onClick.AddListener(() => { selectedWeapon = false; previewSkin = (PlayerSkinSelection)captured; Refresh(); });
                skinLabels[index] = ConfigureRowLabel(skinButtons[index]);
            }

            weaponButtons = new Button[PlayerSwordCosmetics.SelectionCount];
            weaponLabels = new Text[weaponButtons.Length];
            for (int index = 0; index < weaponButtons.Length; index++)
            {
                int captured = index;
                weaponButtons[index] = Button("Weapon " + index, transform, new Vector2(-182f, -100f - index * 20f), new Vector2(244f, 19f));
                weaponButtons[index].onClick.AddListener(() => { selectedWeapon = true; previewSword = (SwordCosmeticSelection)captured; Refresh(); });
                weaponLabels[index] = ConfigureRowLabel(weaponButtons[index]);
            }

            selectionTitle = Label("Loadout Selection Title", transform, string.Empty, 12,
                new Vector2(-315f, -258f), new Vector2(500f, 18f), TextAnchor.MiddleCenter, CaveUiTheme.PrimaryText);
            selectionDetail = Label("Loadout Selection Detail", transform, string.Empty, 10,
                new Vector2(-315f, -276f), new Vector2(500f, 18f), TextAnchor.MiddleCenter, CaveUiTheme.SecondaryText);
            selectionStatus = Label("Loadout Selection Status", transform, string.Empty, 10,
                new Vector2(-438f, -298f), new Vector2(190f, 18f), TextAnchor.MiddleCenter, CaveUiTheme.SecondaryText);
            equipSelectionButton = Button("EQUIP", transform, new Vector2(-190f, -298f), new Vector2(180f, 19f));
            equipSelectionLabel = equipSelectionButton.GetComponentInChildren<Text>();
            equipSelectionButton.onClick.AddListener(EquipSelectedCosmetic);
        }

        private void BuildInventoryPanel()
        {
            Panel("Inventory Panel", transform, new Vector2(315f, -178f), new Vector2(590f, 300f));
            Heading("Inventory Heading", "INVENTORY", new Vector2(315f, -48f), new Vector2(540f, 24f));
            inventoryCategoryButtons = new[]
            {
                Button("MATERIALS", transform, new Vector2(132f, -78f), new Vector2(112f, 24f)),
                Button("CONSUMABLES", transform, new Vector2(265f, -78f), new Vector2(130f, 24f)),
                Button("EQUIPMENT", transform, new Vector2(405f, -78f), new Vector2(112f, 24f))
            };
            inventoryCategoryButtons[0].onClick.AddListener(() => SelectInventoryCategory(InventoryItemCategory.Materials));
            inventoryCategoryButtons[1].onClick.AddListener(() => SelectInventoryCategory(InventoryItemCategory.Consumables));
            inventoryCategoryButtons[2].onClick.AddListener(() => SelectInventoryCategory(InventoryItemCategory.Equipment));
            inventoryButtons = new Button[5];
            inventoryLabels = new Text[inventoryButtons.Length];
            for (int index = 0; index < inventoryButtons.Length; index++)
            {
                int captured = index;
                inventoryButtons[index] = Button("Inventory Item " + index, transform, new Vector2(166f, -110f - index * 25f), new Vector2(180f, 22f));
                inventoryButtons[index].onClick.AddListener(() => { inventoryIndex = captured; RefreshInventory(); });
                inventoryLabels[index] = ConfigureRowLabel(inventoryButtons[index]);
            }
            inventoryTitle = Label("Inventory Item Title", transform, string.Empty, 12,
                new Vector2(405f, -120f), new Vector2(175f, 25f), TextAnchor.MiddleCenter, CaveUiTheme.PrimaryText);
            inventoryDetail = Label("Inventory Item Detail", transform, string.Empty, 10,
                new Vector2(405f, -181f), new Vector2(175f, 102f), TextAnchor.UpperLeft, CaveUiTheme.SecondaryText);
        }

        private void Refresh()
        {
            if (skinButtons == null) return;
            RefreshPreviewAndCollections();
            RefreshCarapace();
            RefreshInventory();
        }

        private void RefreshPreviewAndCollections()
        {
            for (int index = 0; index < skinButtons.Length; index++)
            {
                PlayerSkinSelection skin = (PlayerSkinSelection)index;
                bool skinUnlocked = SkinUnlocked(skin);
                bool skinAvailable = SkinAvailable(skin);
                bool selected = !selectedWeapon && previewSkin == skin;
                skinLabels[index].text = PlayerSkinLibrary.GetDefinition(skin).DisplayName + (skinUnlocked ? "  ✓" : skinAvailable ? "  DEV" : "  LOCKED");
                StyleRow(skinButtons[index], skinLabels[index], selected, skinAvailable);
            }
            for (int index = 0; index < weaponButtons.Length; index++)
            {
                SwordCosmeticSelection weapon = (SwordCosmeticSelection)index;
                bool weaponUnlocked = SwordUnlocked(weapon);
                bool weaponAvailable = SwordAvailable(weapon);
                bool selected = selectedWeapon && previewSword == weapon;
                weaponLabels[index].text = PlayerSwordCosmetics.DisplayNameFor(weapon) + (weaponUnlocked ? "  ✓" : weaponAvailable ? "  DEV" : "  LOCKED");
                StyleRow(weaponButtons[index], weaponLabels[index], selected, weaponAvailable);
            }

            bool available = selectedWeapon ? SwordAvailable(previewSword) : SkinAvailable(previewSkin);
            bool unlocked = selectedWeapon ? SwordUnlocked(previewSword) : SkinUnlocked(previewSkin);
            bool equipped = selectedWeapon
                ? swords != null && swords.SelectedAppearance == previewSword
                : skins != null && skins.SelectedAppearance == previewSkin;
            selectionTitle.text = selectedWeapon ? PlayerSwordCosmetics.DisplayNameFor(previewSword) : PlayerSkinLibrary.GetDefinition(previewSkin).DisplayName;
            selectionDetail.text = selectedWeapon ? PlayerSwordCosmetics.DescriptionFor(previewSword) : PlayerSkinLibrary.GetDefinition(previewSkin).Description;
            selectionStatus.text = equipped ? "EQUIPPED" : unlocked ? "UNLOCKED" : available ? "DEV UNLOCK" : "LOCKED";
            selectionStatus.color = equipped || unlocked ? new Color(.35f, .95f, .62f, 1f) : available ? CaveUiTheme.BorderBright : new Color(.95f, .35f, .31f, 1f);
            equipSelectionButton.interactable = available && !equipped;
            equipSelectionLabel.text = equipped ? "EQUIPPED" : available ? "EQUIP" : "LOCKED";

            Sprite body = PlayerSkinLibrary.LoadPreview(previewSkin);
            previewBody.sprite = body != null ? body : defaultBody;
            previewBody.enabled = previewBody.sprite != null;
            previewSwordImage.sprite = PlayerSwordCosmetics.LoadSpriteFor(previewSword);
            previewSwordImage.enabled = previewSwordImage.sprite != null;
            previewSummary.text = "SKIN\n" + PlayerSkinLibrary.GetDefinition(previewSkin).DisplayName
                + "\n\nWEAPON\n" + PlayerSwordCosmetics.DisplayNameFor(previewSword);
        }

        private void RefreshCarapace()
        {
            PlayerResonantCarapace carapace = FindPlayer() != null ? FindPlayer().GetComponent<PlayerResonantCarapace>() : null;
            if (carapace == null)
            {
                carapaceStatus.text = "No player Carapace authority is currently available.";
                if (carapaceIntegrityFill != null) carapaceIntegrityFill.rectTransform.localScale = new Vector3(0f, 1f, 1f);
                carapaceUpgrades.text = string.Empty;
                carapaceButton.interactable = false;
                carapaceButtonLabel.text = "UNAVAILABLE";
                return;
            }
            float integrity = carapace.IntegrityRatio;
            float effectiveShare = carapace.EffectiveDamageShare;
            carapaceStatus.text = (carapace.IsOwned ? "OWNED" : "NOT OWNED")
                + "  •  " + (carapace.IsEquipped ? "EQUIPPED" : "UNEQUIPPED")
                + "\nINTEGRITY  " + carapace.CurrentDurability + " / " + carapace.MaximumDurability
                + "  (" + Mathf.RoundToInt(integrity * 100f) + "%)"
                + "\n" + carapace.IntegrityStage.ToString().ToUpperInvariant()
                + "  •  EFFECTIVE SHARE " + Mathf.RoundToInt(effectiveShare * 100f) + "%"
                + (carapace.IsResonanceDisabled ? "  •  RESONANCE DISABLED" : string.Empty);
            if (carapaceIntegrityFill != null)
            {
                carapaceIntegrityFill.rectTransform.localScale = new Vector3(integrity, 1f, 1f);
                carapaceIntegrityFill.color = carapace.IsResonanceDisabled
                    ? new Color(.42f, .38f, .55f, .72f)
                    : carapace.IntegrityStage == ResonantCarapaceIntegrityStage.Critical
                        ? new Color(.92f, .28f, .44f, .9f)
                        : new Color(.64f, .38f, 1f, .9f);
            }
            PlayerPermanentProgression progression = carapace.GetComponent<PlayerPermanentProgression>();
            carapaceUpgrades.text = progression == null ? string.Empty
                : "STRUCTURAL REINFORCEMENT " + progression.StructuralReinforcementLevel
                    + "   LIVING REGENERATION " + progression.LivingRegenerationLevel
                    + "   ANCHORED LATTICE " + progression.AnchoredLatticeLevel + "/5";
            carapaceButton.interactable = carapace.IsOwned;
            carapaceButtonLabel.text = carapace.IsEquipped ? "UNEQUIP" : carapace.IsOwned ? "EQUIP" : "NO CARAPACE";
        }

        private void RefreshInventory()
        {
            PlayerHealth player = FindPlayer();
            inventoryEntries.Clear();
            PlayerCollectedInventory collected = player != null ? player.GetComponent<PlayerCollectedInventory>() : null;
            collected?.CopyEntries(inventoryEntries, inventoryCategory);
            PlayerLandmineInventory consumables = player != null ? player.GetComponent<PlayerLandmineInventory>() : null;
            if (consumables != null && inventoryCategory == InventoryItemCategory.Consumables)
            {
                inventoryEntries.Add(new InventoryEntrySnapshot("HEALTH POTION", "Existing consumable authority.", consumables.OwnedHealthPotions, null));
                inventoryEntries.Add(new InventoryEntrySnapshot("MANA POTION", "Existing consumable authority.", consumables.OwnedManaPotions, null));
                inventoryEntries.Add(new InventoryEntrySnapshot("OBLIVION DISK", "Available charges; capacity " + consumables.DiskCapacity + ".", consumables.StoredDiskCharges, null));
            }
            PlayerResonantCarapace carapace = player != null ? player.GetComponent<PlayerResonantCarapace>() : null;
            if (carapace != null && inventoryCategory == InventoryItemCategory.Equipment)
                inventoryEntries.Add(new InventoryEntrySnapshot("RESONANT CARAPACE", carapace.ConditionText, carapace.IsOwned ? 1 : 0, null));
            inventoryIndex = Mathf.Clamp(inventoryIndex, 0, Mathf.Max(0, inventoryEntries.Count - 1));
            for (int index = 0; index < inventoryButtons.Length; index++)
            {
                bool used = index < inventoryEntries.Count;
                inventoryButtons[index].gameObject.SetActive(used);
                if (!used) continue;
                InventoryEntrySnapshot entry = inventoryEntries[index];
                inventoryLabels[index].text = entry.Name + "  x" + entry.Quantity;
                StyleRow(inventoryButtons[index], inventoryLabels[index], index == inventoryIndex, true);
            }
            for (int index = 0; index < inventoryCategoryButtons.Length; index++)
            {
                InventoryItemCategory category = (InventoryItemCategory)index;
                CaveUiArt.ApplyButton(inventoryCategoryButtons[index], category == inventoryCategory);
            }
            if (inventoryEntries.Count == 0)
            {
                inventoryTitle.text = "EMPTY";
                inventoryDetail.text = inventoryCategory == InventoryItemCategory.Materials
                    ? "No collected materials. Future Flora materials appear here when their authority exists."
                    : "No " + inventoryCategory.ToString().ToLowerInvariant() + " are currently held.";
            }
            else
            {
                InventoryEntrySnapshot entry = inventoryEntries[inventoryIndex];
                inventoryTitle.text = entry.Name;
                inventoryDetail.text = entry.Description + "\n\nQuantity: " + entry.Quantity;
            }
        }

        private void EquipSelectedCosmetic()
        {
            if (selectedWeapon) swords?.Select(previewSword); else skins?.Select(previewSkin);
            Refresh();
        }

        private void ToggleCarapace()
        {
            PlayerResonantCarapace carapace = FindPlayer() != null ? FindPlayer().GetComponent<PlayerResonantCarapace>() : null;
            if (carapace == null) return;
            if (carapace.IsEquipped) carapace.TryUnequip(); else carapace.TryEquip();
            Refresh();
        }

        private void SelectInventoryCategory(InventoryItemCategory category)
        {
            inventoryCategory = category;
            inventoryIndex = 0;
            RefreshInventory();
        }

        private void BindAuthorities()
        {
            UnbindAuthorities();
            PlayerHealth player = FindPlayer();
            if (player == null) return;
            observedCollected = player.GetComponent<PlayerCollectedInventory>();
            observedConsumables = player.GetComponent<PlayerLandmineInventory>();
            observedCarapace = player.GetComponent<PlayerResonantCarapace>();
            if (observedCollected != null) observedCollected.Changed += Refresh;
            if (observedConsumables != null) observedConsumables.ConsumableQuantityChanged += HandleConsumableChanged;
            if (observedCarapace != null) observedCarapace.StateChanged += Refresh;
        }

        private void UnbindAuthorities()
        {
            if (observedCollected != null) observedCollected.Changed -= Refresh;
            if (observedConsumables != null) observedConsumables.ConsumableQuantityChanged -= HandleConsumableChanged;
            if (observedCarapace != null) observedCarapace.StateChanged -= Refresh;
            observedCollected = null;
            observedConsumables = null;
            observedCarapace = null;
        }

        private void HandleConsumableChanged(PlayerConsumableType type, int quantity) { Refresh(); }
        private PlayerHealth FindPlayer() => PlayerRunPersistence.CurrentPlayerHealth ?? FindObjectOfType<PlayerHealth>();
        private bool SkinUnlocked(PlayerSkinSelection selection) => skins != null && skins.IsUnlocked(selection);
        private bool SkinAvailable(PlayerSkinSelection selection) => skins != null && skins.IsAvailable(selection);
        private bool SwordUnlocked(SwordCosmeticSelection selection) => swords == null ? selection == SwordCosmeticSelection.Default : swords.IsUnlocked(selection);
        private bool SwordAvailable(SwordCosmeticSelection selection) => swords != null && swords.IsAvailable(selection);

        private static Text ConfigureRowLabel(Button button)
        {
            Text label = button.GetComponentInChildren<Text>();
            label.fontSize = 9;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.sizeDelta = button.GetComponent<RectTransform>().sizeDelta - new Vector2(10f, 4f);
            return label;
        }

        private static void StyleRow(Button button, Text label, bool selected, bool available)
        {
            CaveUiArt.ApplyButton(button, selected);
            label.color = selected ? CaveUiTheme.BorderBright : available ? CaveUiTheme.PrimaryText : new Color(.88f, .32f, .28f, 1f);
        }

        private void Heading(string name, string value, Vector2 position, Vector2 size)
        {
            Text heading = Label(name, transform, value, 15, position, size, TextAnchor.MiddleCenter, CaveUiTheme.Gold);
            heading.fontStyle = FontStyle.Bold;
        }

        private Button Button(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = CaveUiTheme.SurfaceRaised;
            Button button = go.GetComponent<Button>();
            CaveUiArt.ApplyButton(button, false);
            Label("Label", go.transform, name.ToUpperInvariant(), 11, Vector2.zero, size - new Vector2(8f, 4f), TextAnchor.MiddleCenter, CaveUiTheme.PrimaryText);
            return button;
        }

        private static void Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = CaveUiTheme.Surface;
            CaveUiArt.ApplyCard(image, false, CaveUiTheme.BronzeLight);
        }

        private Text Label(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Text text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.text = value;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite FirstSprite(string resourcePath, string name)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            for (int index = 0; index < sprites.Length; index++) if (sprites[index].name == name) return sprites[index];
            return sprites.Length > 0 ? sprites[0] : null;
        }
    }
}
