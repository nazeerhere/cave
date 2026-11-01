using Cave.Diagnostics;
using Cave.Player;
using Cave.Inventory;
using Cave.World;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Presentation-only Nexus cosmetics page. Preview choice is local until Equip is pressed.</summary>
    [DisallowMultipleComponent]
    public sealed class NexusCosmeticsPage : MonoBehaviour
    {
        private PlayerSkinCosmetics skins;
        private PlayerSwordCosmetics swords;
        private Font font;
        private PlayerSkinSelection previewSkin;
        private SwordCosmeticSelection previewSword;
        private bool showSwords;
        private bool showInventory;
        private InventoryItemCategory inventoryCategory = InventoryItemCategory.Materials;
        private readonly List<InventoryEntrySnapshot> inventoryEntries = new List<InventoryEntrySnapshot>();
        private int inventoryIndex;
        private Button[] collectionButtons;
        private Text[] collectionLabels;
        private Text detailTitle;
        private Text detailBody;
        private Text status;
        private Text previewTitle;
        private Text equipLabel;
        private Image previewBody;
        private Image previewSwordImage;
        private Sprite defaultBody;
        private Button equipButton;
        private Button materialsInventoryButton;
        private Button consumablesInventoryButton;
        private Button equipmentInventoryButton;
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
            BindInventoryAuthorities();
            Refresh();
        }

        private void OnDisable()
        {
            DeveloperDiagnosticsSettings.Changed -= Refresh;
            UnbindInventoryAuthorities();
        }

        private void Build()
        {
            if (collectionButtons != null) return;

            Image backdrop = gameObject.AddComponent<Image>();
            backdrop.color = CaveUiTheme.SurfaceInset;

            Button skinsTab = Button("SKINS", transform, new Vector2(-132f, 270f), new Vector2(212f, 36f));
            Button swordsTab = Button("WEAPONS", transform, new Vector2(92f, 270f), new Vector2(212f, 36f));
            Button inventoryTab = Button("INVENTORY", transform, new Vector2(316f, 270f), new Vector2(180f, 36f));
            skinsTab.onClick.AddListener(() => { showSwords = false; showInventory = false; Refresh(); });
            swordsTab.onClick.AddListener(() => { showSwords = true; showInventory = false; Refresh(); });
            inventoryTab.onClick.AddListener(() => { showInventory = true; Refresh(); });

            materialsInventoryButton = Button("MATERIALS", transform, new Vector2(-188f, 235f), new Vector2(112f, 25f));
            consumablesInventoryButton = Button("CONSUMABLES", transform, new Vector2(-65f, 235f), new Vector2(126f, 25f));
            equipmentInventoryButton = Button("EQUIPMENT", transform, new Vector2(68f, 235f), new Vector2(112f, 25f));
            materialsInventoryButton.onClick.AddListener(() => SelectInventoryCategory(InventoryItemCategory.Materials));
            consumablesInventoryButton.onClick.AddListener(() => SelectInventoryCategory(InventoryItemCategory.Consumables));
            equipmentInventoryButton.onClick.AddListener(() => SelectInventoryCategory(InventoryItemCategory.Equipment));

            Panel("Collection", transform, new Vector2(-435f, 5f), new Vector2(310f, 500f));
            Text heading = Label("Collection Header", transform, "SKIN COLLECTION", 16, new Vector2(-435f, 232f), new Vector2(280f, 28f), TextAnchor.MiddleCenter, CaveUiTheme.Gold);
            heading.fontStyle = FontStyle.Bold;

            int collectionCapacity = Mathf.Max(PlayerSkinLibrary.Count, PlayerSwordCosmetics.SelectionCount);
            collectionButtons = new Button[collectionCapacity];
            collectionLabels = new Text[collectionCapacity];
            for (int i = 0; i < collectionButtons.Length; i++)
            {
                int captured = i;
                Button entry = Button("Collection Entry " + i, transform, new Vector2(-435f, 188f - i * 53f), new Vector2(280f, 47f));
                entry.onClick.AddListener(() => SelectEntry(captured));
                collectionButtons[i] = entry;
                // Button already creates its single visible label. Reuse it so collection
                // entries do not render a second overlapping "COLLECTION ENTRY X" label.
                Text label = entry.GetComponentInChildren<Text>();
                label.text = string.Empty;
                label.fontSize = 10;
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.sizeDelta = new Vector2(258f, 40f);
                collectionLabels[i] = label;
            }

            Panel("Preview", transform, new Vector2(0f, 5f), new Vector2(520f, 500f));
            previewTitle = Label("Preview Header", transform, "CHARACTER PREVIEW", 16, new Vector2(0f, 232f), new Vector2(480f, 28f), TextAnchor.MiddleCenter, CaveUiTheme.Gold);
            previewTitle.fontStyle = FontStyle.Bold;
            defaultBody = FirstSprite("Player/MinerFullActionSheet", "Miner_Idle_01");
            previewBody = Image("Preview Body", transform, new Vector2(-28f, 30f), new Vector2(270f, 330f), defaultBody);
            previewBody.preserveAspect = true;
            previewSwordImage = Image("Preview Sword", transform, new Vector2(115f, -3f), new Vector2(120f, 170f), null);
            previewSwordImage.preserveAspect = true;
            Text previewHint = Label("Preview Note", transform,
                "Preview only — no combat objects or hitboxes are created.", 10,
                new Vector2(0f, -207f), new Vector2(480f, 32f), TextAnchor.MiddleCenter, CaveUiTheme.SecondaryText);

            Panel("Details", transform, new Vector2(435f, 5f), new Vector2(310f, 500f));
            detailTitle = Label("Selected Title", transform, string.Empty, 16, new Vector2(435f, 202f), new Vector2(270f, 42f), TextAnchor.MiddleCenter, CaveUiTheme.PrimaryText);
            detailTitle.fontStyle = FontStyle.Bold;
            detailBody = Label("Selected Details", transform, string.Empty, 12, new Vector2(435f, 72f), new Vector2(258f, 205f), TextAnchor.UpperLeft, CaveUiTheme.PrimaryText);
            status = Label("Status", transform, string.Empty, 12, new Vector2(435f, -114f), new Vector2(258f, 34f), TextAnchor.MiddleCenter, CaveUiTheme.SecondaryText);
            equipButton = Button("Equip", transform, new Vector2(435f, -184f), new Vector2(238f, 48f));
            equipLabel = equipButton.GetComponentInChildren<Text>();
            equipButton.onClick.AddListener(EquipPreview);
        }

        private void SelectEntry(int index)
        {
            if (showInventory) { inventoryIndex = index; Refresh(); return; }
            if (showSwords)
            {
                previewSword = (SwordCosmeticSelection)Mathf.Clamp(index, 0, PlayerSwordCosmetics.SelectionCount - 1);
            }
            else
            {
                previewSkin = (PlayerSkinSelection)Mathf.Clamp(index, 0, PlayerSkinLibrary.Count - 1);
            }
            Refresh();
        }

        private void SelectInventoryCategory(InventoryItemCategory category)
        {
            inventoryCategory = category;
            inventoryIndex = 0;
            Refresh();
        }

        private void EquipPreview()
        {
            if (showSwords)
            {
                swords?.Select(previewSword);
            }
            else
            {
                skins?.Select(previewSkin);
            }
            Refresh();
        }

        private void Refresh()
        {
            if (collectionButtons == null) return;
            if (showInventory) { RefreshInventory(); return; }
            SetInventoryCategoryButtons(false);
            if (previewTitle != null) previewTitle.text = "CHARACTER PREVIEW";
            previewBody.enabled = true;
            equipButton.gameObject.SetActive(true);
            Text heading = transform.Find("Collection Header")?.GetComponent<Text>();
            if (heading != null) heading.text = showSwords ? "WEAPON COLLECTION" : "SKIN COLLECTION";
            for (int i = 0; i < collectionButtons.Length; i++)
            {
                bool used = showSwords ? i < PlayerSwordCosmetics.SelectionCount : i < PlayerSkinLibrary.Count;
                collectionButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                bool actuallyUnlocked = showSwords ? SwordUnlocked((SwordCosmeticSelection)i) : SkinUnlocked((PlayerSkinSelection)i);
                bool available = showSwords ? SwordAvailable((SwordCosmeticSelection)i) : SkinAvailable((PlayerSkinSelection)i);
                bool selected = showSwords ? previewSword == (SwordCosmeticSelection)i : previewSkin == (PlayerSkinSelection)i;
                collectionLabels[i].text = showSwords
                    ? SwordText((SwordCosmeticSelection)i, actuallyUnlocked, available)
                    : SkinText((PlayerSkinSelection)i, actuallyUnlocked, available);
                collectionLabels[i].color = selected ? CaveUiTheme.BorderBright : available ? CaveUiTheme.PrimaryText : new Color(.88f, .32f, .28f, 1f);
                CaveUiArt.ApplyButton(collectionButtons[i], selected);
            }

            bool actuallyUnlockedPreview = showSwords ? SwordUnlocked(previewSword) : SkinUnlocked(previewSkin);
            bool availablePreview = showSwords ? SwordAvailable(previewSword) : SkinAvailable(previewSkin);
            string name = showSwords ? SwordName(previewSword) : PlayerSkinLibrary.GetDefinition(previewSkin).DisplayName;
            detailTitle.text = name;
            detailBody.text = showSwords ? SwordDetails(previewSword) : SkinDetails(previewSkin);
            bool equipped = showSwords ? swords != null && swords.SelectedAppearance == previewSword : skins != null && skins.SelectedAppearance == previewSkin;
            status.text = equipped ? "EQUIPPED" : actuallyUnlockedPreview ? "UNLOCKED"
                : availablePreview ? "DEV UNLOCK" : RequirementForCurrent();
            status.color = equipped || actuallyUnlockedPreview ? new Color(.35f, .95f, .62f, 1f)
                : availablePreview ? CaveUiTheme.BorderBright : new Color(.95f, .35f, .31f, 1f);
            equipButton.interactable = availablePreview && !equipped;
            equipLabel.text = equipped ? "EQUIPPED" : availablePreview ? "EQUIP" : "LOCKED";

            PlayerSkinSelection appearanceForPreview = showSwords && skins != null
                ? skins.SelectedAppearance
                : previewSkin;
            Sprite skinPreview = PlayerSkinLibrary.LoadPreview(appearanceForPreview);
            previewBody.sprite = skinPreview != null ? skinPreview : defaultBody;
            previewSwordImage.sprite = SwordSprite(previewSword);
            previewSwordImage.enabled = previewSwordImage.sprite != null;
        }

        private void RefreshInventory()
        {
            Text heading = transform.Find("Collection Header")?.GetComponent<Text>();
            if (heading != null) heading.text = "INVENTORY — " + inventoryCategory.ToString().ToUpperInvariant();
            if (previewTitle != null) previewTitle.text = "ITEM PREVIEW";
            SetInventoryCategoryButtons(true);
            inventoryEntries.Clear();
            PlayerHealth player = PlayerRunPersistence.CurrentPlayerHealth ?? FindObjectOfType<PlayerHealth>();
            PlayerCollectedInventory collected = player != null ? player.GetComponent<PlayerCollectedInventory>() : null;
            collected?.CopyEntries(inventoryEntries, inventoryCategory);
            PlayerLandmineInventory consumables = player != null ? player.GetComponent<PlayerLandmineInventory>() : null;
            if (consumables != null && inventoryCategory == InventoryItemCategory.Consumables)
            {
                inventoryEntries.Add(new InventoryEntrySnapshot("HEALTH POTION", "Existing consumable authority.", consumables.OwnedHealthPotions, null));
                inventoryEntries.Add(new InventoryEntrySnapshot("MANA POTION", "Existing consumable authority.", consumables.OwnedManaPotions, null));
                inventoryEntries.Add(new InventoryEntrySnapshot("OBLIVION DISK", "Available charges; capacity " + consumables.DiskCapacity + ".", consumables.StoredDiskCharges, null));
            }
            IResonantCarapaceInventorySource carapace = null;
            if (player != null) foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>()) { carapace = component as IResonantCarapaceInventorySource; if (carapace != null) break; }
            if (carapace != null && inventoryCategory == InventoryItemCategory.Equipment)
                inventoryEntries.Add(new InventoryEntrySnapshot("RESONANT CARAPACE", carapace.ConditionText, carapace.IsOwned ? 1 : 0, null));
            inventoryIndex = Mathf.Clamp(inventoryIndex, 0, Mathf.Max(0, inventoryEntries.Count - 1));
            for (int i = 0; i < collectionButtons.Length; i++)
            {
                bool used = i < inventoryEntries.Count;
                collectionButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                InventoryEntrySnapshot entry = inventoryEntries[i];
                collectionLabels[i].text = entry.Name + "  x" + entry.Quantity;
                collectionLabels[i].color = i == inventoryIndex ? CaveUiTheme.BorderBright : CaveUiTheme.PrimaryText;
                CaveUiArt.ApplyButton(collectionButtons[i], i == inventoryIndex);
            }
            if (inventoryEntries.Count == 0)
            {
                detailTitle.text = "EMPTY INVENTORY";
                detailBody.text = inventoryCategory == InventoryItemCategory.Materials
                    ? "No collected materials. Future Flora materials will appear here when their collection authority is implemented."
                    : "No " + inventoryCategory.ToString().ToLowerInvariant() + " are currently held.";
            }
            else
            {
                InventoryEntrySnapshot selected = inventoryEntries[inventoryIndex];
                detailTitle.text = selected.Name;
                detailBody.text = selected.Description + "\n\nQuantity: " + selected.Quantity;
                previewBody.sprite = selected.Icon;
                previewBody.enabled = selected.Icon != null;
            }
            if (inventoryEntries.Count == 0) previewBody.enabled = false;
            previewSwordImage.enabled = false;
            status.text = "INVENTORY — READ ONLY";
            status.color = CaveUiTheme.SecondaryText;
            equipButton.gameObject.SetActive(false);
        }

        private void BindInventoryAuthorities()
        {
            UnbindInventoryAuthorities();
            PlayerHealth player = PlayerRunPersistence.CurrentPlayerHealth ?? FindObjectOfType<PlayerHealth>();
            if (player == null) return;
            observedCollected = player.GetComponent<PlayerCollectedInventory>();
            observedConsumables = player.GetComponent<PlayerLandmineInventory>();
            observedCarapace = player.GetComponent<PlayerResonantCarapace>();
            if (observedCollected != null) observedCollected.Changed += Refresh;
            if (observedConsumables != null) observedConsumables.ConsumableQuantityChanged += HandleConsumableChanged;
            if (observedCarapace != null) observedCarapace.StateChanged += Refresh;
        }

        private void UnbindInventoryAuthorities()
        {
            if (observedCollected != null) observedCollected.Changed -= Refresh;
            if (observedConsumables != null) observedConsumables.ConsumableQuantityChanged -= HandleConsumableChanged;
            if (observedCarapace != null) observedCarapace.StateChanged -= Refresh;
            observedCollected = null;
            observedConsumables = null;
            observedCarapace = null;
        }

        private void HandleConsumableChanged(PlayerConsumableType type, int quantity) { Refresh(); }

        private void SetInventoryCategoryButtons(bool visible)
        {
            if (materialsInventoryButton == null) return;
            materialsInventoryButton.gameObject.SetActive(visible);
            consumablesInventoryButton.gameObject.SetActive(visible);
            equipmentInventoryButton.gameObject.SetActive(visible);
            CaveUiArt.ApplyButton(materialsInventoryButton, inventoryCategory == InventoryItemCategory.Materials);
            CaveUiArt.ApplyButton(consumablesInventoryButton, inventoryCategory == InventoryItemCategory.Consumables);
            CaveUiArt.ApplyButton(equipmentInventoryButton, inventoryCategory == InventoryItemCategory.Equipment);
        }

        private bool SkinUnlocked(PlayerSkinSelection selection) => skins != null && skins.IsUnlocked(selection);
        private bool SkinAvailable(PlayerSkinSelection selection) => skins != null && skins.IsAvailable(selection);
        private bool SwordUnlocked(SwordCosmeticSelection selection)
        {
            if (swords == null) return selection == SwordCosmeticSelection.Default;
            return swords.IsUnlocked(selection);
        }
        private bool SwordAvailable(SwordCosmeticSelection selection) => swords != null && swords.IsAvailable(selection);

        private string RequirementForCurrent()
        {
            if (showSwords)
            {
                if (PlayerSwordCosmetics.IsLegendaryWeapon(previewSword))
                {
                    return "PRODUCTION REQUIREMENT TBD";
                }

                return previewSword == SwordCosmeticSelection.Sword1 ? "DEFEAT A GENERAL-RANK SKELETON"
                    : previewSword == SwordCosmeticSelection.Sword2 ? "DEFEAT ALL FIVE ARCHETYPES IN ONE RUN"
                    : previewSword == SwordCosmeticSelection.Sword3 ? "REACH WORLD LEVEL 10" : "LOCKED";
            }
            return PlayerSkinLibrary.GetDefinition(previewSkin).UnlockDescription;
        }

        private static string SkinText(PlayerSkinSelection skin, bool actuallyUnlocked, bool available)
        {
            PlayerSkinLibrary.Definition definition = PlayerSkinLibrary.GetDefinition(skin);
            return definition.DisplayName + "\n" + (actuallyUnlocked ? "UNLOCKED"
                : available ? "DEV UNLOCK" : definition.UnlockDescription);
        }
        private static string SwordText(SwordCosmeticSelection sword, bool actuallyUnlocked, bool available) => SwordName(sword) + "\n"
            + (actuallyUnlocked ? "UNLOCKED" : available ? "DEV UNLOCK" : "LOCKED");
        private static string SwordName(SwordCosmeticSelection sword) => PlayerSwordCosmetics.DisplayNameFor(sword);
        private static string SkinDetails(PlayerSkinSelection skin) => PlayerSkinLibrary.GetDefinition(skin).Description
            + "\n\nCosmetic only — no gameplay stats or hitboxes change.";
        private static string SwordDetails(SwordCosmeticSelection sword) => PlayerSwordCosmetics.DescriptionFor(sword);
        private static Sprite SwordSprite(SwordCosmeticSelection sword) => PlayerSwordCosmetics.LoadSpriteFor(sword);

        private static Sprite FirstSprite(string resourcePath, string name)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            for (int i = 0; i < sprites.Length; i++) if (sprites[i].name == name) return sprites[i];
            return sprites.Length > 0 ? sprites[0] : null;
        }

        private Button Button(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = size;
            Image image = go.GetComponent<Image>(); image.color = CaveUiTheme.SurfaceRaised;
            Button button = go.GetComponent<Button>(); CaveUiArt.ApplyButton(button, false);
            Label("Label", go.transform, name.ToUpperInvariant(), 13, Vector2.zero, size - new Vector2(12f, 8f), TextAnchor.MiddleCenter, CaveUiTheme.PrimaryText);
            return button;
        }
        private static void Panel(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = size;
            Image image = go.GetComponent<Image>(); image.color = CaveUiTheme.Surface; CaveUiArt.ApplyCard(image, false, CaveUiTheme.BronzeLight);
        }
        private Text Label(string name, Transform parent, string value, int size, Vector2 pos, Vector2 dimensions, TextAnchor alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = dimensions;
            Text text = go.GetComponent<Text>(); text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = size; text.text = value; text.alignment = alignment; text.color = color; text.raycastTarget = false; return text;
        }
        private static Image Image(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = size;
            Image image = go.GetComponent<Image>(); image.sprite = sprite; image.color = Color.white; image.raycastTarget = false; return image;
        }
    }
}
