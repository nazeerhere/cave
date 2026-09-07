using Cave.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Build-once, two-page presentation layered into the existing Ledger panel.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyLedgerPagesHud : MonoBehaviour
    {
        private Text rosterPage;
        private Text symbolsPage;
        private Text pageIndicator;
        private Button previousButton;
        private Button nextButton;
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
            symbolsPage = CreatePage("Ledger Symbols Page", legacyBody);
            symbolsPage.text = EnemyLedger.BuildTacticalReferencePage();

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
            if (symbolsPage != null) symbolsPage.gameObject.SetActive(!roster);
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
