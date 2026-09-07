using Cave.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Build-once, two-page presentation layered into the existing Ledger panel.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyLedgerPagesHud : MonoBehaviour
    {
        private const string IconRegistryResourceName = "MobStatusIconRegistry";

        private struct LedgerStatusEntry
        {
            public LedgerStatusEntry(MobStatusIconKind kind, string title, string description)
            {
                Kind = kind;
                Title = title;
                Description = description;
            }

            public MobStatusIconKind Kind;
            public string Title;
            public string Description;
        }

        private static readonly LedgerStatusEntry[] GlossaryEntries =
        {
            new LedgerStatusEntry(MobStatusIconKind.Burn, "BURNING", "Takes fire damage over time."),
            new LedgerStatusEntry(MobStatusIconKind.Slow, "SLOW", "Reduced movement pressure."),
            new LedgerStatusEntry(MobStatusIconKind.Poison, "POISONED", "Damage over time; leave poison zones."),
            new LedgerStatusEntry(MobStatusIconKind.Stagger, "STAGGER", "Temporarily disrupted."),
            new LedgerStatusEntry(MobStatusIconKind.Frenzied, "FRENZIED", "Altered, aggressive state."),
            new LedgerStatusEntry(MobStatusIconKind.TowerSuppression, "TOWER INTERFERENCE", "Suppresses recovery inside the tower field.")
        };

        private Text rosterPage;
        private GameObject symbolsPage;
        private Text pageIndicator;
        private Button previousButton;
        private Button nextButton;
        private MobStatusIconRegistry iconRegistry;
        private int currentPage;
        private bool rosterDirty = true;

        public void Configure(Text legacyBody)
        {
            if (rosterPage != null || legacyBody == null)
            {
                return;
            }

            legacyBody.gameObject.SetActive(false);
            rosterPage = CreatePage("Ledger Roster Page", legacyBody);
            iconRegistry = Resources.Load<MobStatusIconRegistry>(IconRegistryResourceName);
            symbolsPage = CreateSymbolsPage(legacyBody);

            pageIndicator = CreateText("Ledger Page Indicator", transform, legacyBody.font, 12);
            RectTransform indicatorRect = pageIndicator.rectTransform;
            indicatorRect.anchorMin = new Vector2(0.5f, 0f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0f);
            indicatorRect.pivot = new Vector2(0.5f, 0f);
            indicatorRect.anchoredPosition = new Vector2(0f, 50f);
            indicatorRect.sizeDelta = new Vector2(150f, 28f);
            pageIndicator.alignment = TextAnchor.MiddleCenter;
            pageIndicator.color = new Color(0.76f, 0.64f, 0.4f, 1f);

            previousButton = CreateButton("Previous Ledger Page", "<", -105f, legacyBody.font);
            nextButton = CreateButton("Next Ledger Page", ">", 105f, legacyBody.font);
            previousButton.onClick.AddListener(ShowPrevious);
            nextButton.onClick.AddListener(ShowNext);
            EnemyLedger.Changed -= MarkRosterDirty;
            EnemyLedger.Changed += MarkRosterDirty;
            ShowRoster();
        }

        public void ShowRoster()
        {
            currentPage = 0;
            RefreshVisiblePage();
        }

        private void ShowPrevious()
        {
            if (currentPage == 0)
            {
                return;
            }

            currentPage = 0;
            RefreshVisiblePage();
        }

        private void ShowNext()
        {
            if (currentPage == 1)
            {
                return;
            }

            currentPage = 1;
            RefreshVisiblePage();
        }

        private void RefreshVisiblePage()
        {
            if (rosterDirty && rosterPage != null)
            {
                rosterPage.text = EnemyLedger.BuildDetailedRosterPage();
                rosterDirty = false;
            }

            bool roster = currentPage == 0;
            if (rosterPage != null) rosterPage.gameObject.SetActive(roster);
            if (symbolsPage != null) symbolsPage.SetActive(!roster);
            if (pageIndicator != null)
            {
                pageIndicator.text = roster
                    ? "PAGE 1 / 2  •  ROSTER"
                    : "PAGE 2 / 2  •  SYMBOLS";
            }

            if (previousButton != null) previousButton.interactable = !roster;
            if (nextButton != null) nextButton.interactable = roster;
        }

        private static Text CreatePage(string name, Text source)
        {
            GameObject pageObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            pageObject.transform.SetParent(source.transform.parent, false);
            RectTransform rect = pageObject.GetComponent<RectTransform>();
            RectTransform sourceRect = source.rectTransform;
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.anchoredPosition = sourceRect.anchoredPosition;
            rect.sizeDelta = sourceRect.sizeDelta;
            Text page = pageObject.GetComponent<Text>();
            page.font = source.font;
            page.fontSize = 13;
            page.color = new Color(0.87f, 0.84f, 0.75f, 1f);
            page.alignment = TextAnchor.UpperLeft;
            page.horizontalOverflow = HorizontalWrapMode.Wrap;
            page.verticalOverflow = VerticalWrapMode.Truncate;
            page.supportRichText = true;
            page.raycastTarget = false;
            return page;
        }

        private GameObject CreateSymbolsPage(Text source)
        {
            GameObject pageObject = new GameObject("Ledger Symbols Page", typeof(RectTransform));
            pageObject.transform.SetParent(source.transform.parent, false);
            RectTransform pageRect = pageObject.GetComponent<RectTransform>();
            RectTransform sourceRect = source.rectTransform;
            pageRect.anchorMin = sourceRect.anchorMin;
            pageRect.anchorMax = sourceRect.anchorMax;
            pageRect.pivot = sourceRect.pivot;
            pageRect.anchoredPosition = sourceRect.anchoredPosition;
            pageRect.sizeDelta = sourceRect.sizeDelta;

            Text header = CreateText("Symbols Header", pageObject.transform, source.font, 13);
            header.text = "<b>SYMBOLS / MEANINGS</b>\n<i>Enemy signs, tactical shorthand, and field rules.</i>\n\n<b>STATUS EFFECTS</b>";
            header.color = new Color(0.87f, 0.84f, 0.75f, 1f);
            header.alignment = TextAnchor.UpperLeft;
            header.supportRichText = true;
            SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(490f, 80f));

            for (int index = 0; index < GlossaryEntries.Length; index++)
            {
                CreateGlossaryRow(pageObject.transform, source.font, GlossaryEntries[index], 82f + index * 32f);
            }

            Text tactics = CreateText("Tactical Shorthand", pageObject.transform, source.font, 11);
            tactics.text = "<b>TACTICAL SHORTHAND</b>\n"
                + "<color=#77c9ef>GB</color> = Guard Break; opens enemy defense.\n"
                + "<color=#d98eff>PP</color> = Perfect Parry; earns a stronger Heavy start.\n"
                + "<color=#e8b15c>LoS</color> = Line of Sight; walls block ranged pressure.\n"
                + "<color=#74d588>DASH</color> = reposition out of area attacks.\n"
                + "<color=#e7a75e>PUNISH</color> = attack after a committed recovery.\n"
                + "POISE = stability; mind stagger before committing.";
            tactics.color = new Color(0.87f, 0.84f, 0.75f, 1f);
            tactics.alignment = TextAnchor.UpperLeft;
            tactics.supportRichText = true;
            SetRect(tactics.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -280f), new Vector2(490f, 160f));
            return pageObject;
        }

        private void CreateGlossaryRow(Transform parent, Font font, LedgerStatusEntry entry, float y)
        {
            GameObject row = new GameObject("Status " + entry.Title, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            SetRect(rowRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(490f, 30f));

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(row.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = iconRegistry != null ? iconRegistry.GetIcon(entry.Kind) : null;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(24f, 24f));

            Text label = CreateText("Description", row.transform, font, 11);
            label.text = "<b>" + entry.Title + "</b>\n" + entry.Description;
            label.color = new Color(0.87f, 0.84f, 0.75f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(432f, 28f));
        }

        private static Text CreateText(string name, Transform parent, Font font, int size)
        {
            GameObject label = new GameObject(name, typeof(RectTransform), typeof(Text));
            label.transform.SetParent(parent, false);
            Text text = label.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0f, anchorMin.y == 0.5f ? 0.5f : 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private Button CreateButton(string name, string label, float x, Font font)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 50f);
            rect.sizeDelta = new Vector2(52f, 28f);
            buttonObject.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.14f, 0.95f);
            Text text = CreateText("Label", buttonObject.transform, font, 18);
            text.text = label;
            text.color = new Color(0.58f, 0.79f, 0.95f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return buttonObject.GetComponent<Button>();
        }

        private void MarkRosterDirty()
        {
            rosterDirty = true;
        }

        private void OnDestroy()
        {
            EnemyLedger.Changed -= MarkRosterDirty;
        }
    }
}
