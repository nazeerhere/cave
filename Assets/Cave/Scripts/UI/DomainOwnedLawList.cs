using System.Collections.Generic;
using Cave.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Fixed-row, clipped presentation of the existing authored-law
    /// collection. Rows are pooled and do not introduce law selection state.</summary>
    public sealed class DomainOwnedLawList : MonoBehaviour
    {
        private readonly List<Text> rows = new List<Text>();
        private RectTransform content;
        private Text emptyText;
        private Font font;
        private const float RowHeight = 28f;

        public void Configure(RectTransform configuredContent, Text configuredEmptyText, Font configuredFont)
        { content = configuredContent; emptyText = configuredEmptyText; font = configuredFont; }

        public void Refresh(PlayerDomainLawCollection laws, bool hasSeed)
        {
            int count = hasSeed && laws != null ? laws.Laws.Count : 0;
            EnsureRows(count);
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(count == 0);
                emptyText.text = hasSeed ? "NO LAWS AUTHORED" : "Awaken a Domain Seed to begin preparation.";
            }
            for (int index = 0; index < rows.Count; index++)
            {
                bool active = index < count;
                rows[index].transform.parent.gameObject.SetActive(active);
                if (!active) continue;
                DomainLaw law = laws.Laws[index].Law;
                rows[index].text = law.Phenomenon.ToString().ToUpperInvariant() + "  •  "
                    + law.Expression.ToString().ToUpperInvariant() + "  •  "
                    + law.TerritoryPrinciple.ToString().ToUpperInvariant();
            }
            if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(1f, count * RowHeight));
        }

        private void EnsureRows(int count)
        {
            if (content == null) return;
            while (rows.Count < count)
            {
                int index = rows.Count;
                GameObject row = new GameObject("Owned Law Row " + index, typeof(RectTransform), typeof(Image));
                row.transform.SetParent(content, false);
                RectTransform rect = row.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -index * RowHeight); rect.sizeDelta = new Vector2(0f, RowHeight - 2f);
                Image image = row.GetComponent<Image>(); image.color = CaveUiTheme.SurfaceRaised; image.raycastTarget = false;
                GameObject label = new GameObject("Label", typeof(RectTransform), typeof(Text)); label.transform.SetParent(row.transform, false);
                Text text = label.GetComponent<Text>(); text.font = font; text.fontSize = 10; text.alignment = TextAnchor.MiddleLeft; text.color = CaveUiTheme.PrimaryText;
                RectTransform labelRect = text.rectTransform; labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = new Vector2(8f, 0f); labelRect.offsetMax = new Vector2(-4f, 0f);
                rows.Add(text);
            }
        }
    }
}
