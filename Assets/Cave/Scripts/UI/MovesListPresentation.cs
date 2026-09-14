using Cave.InputSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Visual-only, build-once composition for the existing Moves pages and Axioms reference page.</summary>
    [DisallowMultipleComponent]
    public sealed class MovesListPresentation : MonoBehaviour
    {
        public const int PageCount = 4;
        private GameObject[] pages;

        public void Configure(Font font, Text[] legacyText)
        {
            if (pages != null)
            {
                return;
            }

            for (int index = 0; legacyText != null && index < legacyText.Length; index++)
            {
                if (legacyText[index] != null)
                {
                    legacyText[index].gameObject.SetActive(false);
                }
            }

            Disable("Moves Left Surface");
            Disable("Moves Right Surface");
            pages = new[]
            {
                transform.Find("Moves Basic Page")?.gameObject,
                transform.Find("Moves Follow-ups Page")?.gameObject,
                transform.Find("Moves Skills Page")?.gameObject,
                transform.Find("Moves Axioms Page")?.gameObject
            };

            if (pages[0] == null)
            {
                pages[0] = CreatePage("Moves Basic Page", font);
                BuildBasicPage(pages[0].transform, font);
            }

            if (pages[1] == null)
            {
                pages[1] = CreatePage("Moves Follow-ups Page", font);
                BuildFollowUpsPage(pages[1].transform, font);
            }

            if (pages[2] == null)
            {
                pages[2] = CreatePage("Moves Skills Page", font);
                BuildSkillsPage(pages[2].transform, font);
            }

            if (pages[3] == null)
            {
                pages[3] = CreatePage("Moves Axioms Page", font);
            }
            ShowPage(0);
        }

        /// <summary>
        /// Adds the pre-existing infographic as the fourth, cached Moves page. The
        /// infographic owns no gameplay state; this keeps one presentation path rather
        /// than recreating its content in a separate pause-menu panel.
        /// </summary>
        public void EnsureAxiomPresentation(Font font)
        {
            if (pages == null || pages.Length < PageCount)
            {
                return;
            }

            AxiomInfoGraphicPresentation.Build(pages[3].transform, font);
            RectTransform infographic = pages[3].transform.Find("Axiom Infographic") as RectTransform;
            if (infographic != null)
            {
                infographic.anchoredPosition = Vector2.zero;
                infographic.localScale = Vector3.one * 0.82f;
            }
        }

        /// <summary>Rehomes an older runtime Axioms panel without duplicating its infographic.</summary>
        public void AdoptLegacyAxiomPresentation(Transform legacyPanel, Font font)
        {
            if (pages == null || pages.Length < PageCount)
            {
                return;
            }

            Transform infographic = legacyPanel != null ? legacyPanel.Find("Axiom Infographic") : null;
            if (infographic == null)
            {
                EnsureAxiomPresentation(font);
                return;
            }

            infographic.SetParent(pages[3].transform, false);
            RectTransform rect = infographic as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one * 0.82f;
            }
        }

        public void ShowPage(int page)
        {
            if (pages == null)
            {
                return;
            }

            for (int index = 0; index < pages.Length; index++)
            {
                pages[index].SetActive(index == page);
            }
        }

        private GameObject CreatePage(string name, Font font)
        {
            GameObject page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(transform, false);
            RectTransform rect = page.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(850f, 520f);
            return page;
        }

        private void BuildBasicPage(Transform parent, Font font)
        {
            CreateSection(parent, font, "BASIC MOVES", 220f);
            CreateMoveRow(parent, font, "MOVE", "Move", Binding(GameAction.MoveLeft) + " / " + Binding(GameAction.MoveRight), "Travel through the cave.", 178f, CaveUiTheme.BorderBright);
            CreateMoveRow(parent, font, "JUMP", "Jump", Binding(GameAction.Jump), "Clear hazards and reach ledges.", 118f, CaveUiTheme.BorderBright);
            CreateMoveRow(parent, font, "DASH", "Dash", Binding(GameAction.Dash), "Reposition through pressure.", 58f, CaveUiTheme.BorderBright);
            CreateSection(parent, font, "COMBAT", -18f);
            CreateMoveRow(parent, font, "SPIN", "Spin", Binding(GameAction.BasicAttack), "Hold for a sustained sword spin.", -60f, CaveUiTheme.Gold);
            CreateMoveRow(parent, font, "HEAVY", "Heavy", Binding(GameAction.ChargedAttack), "Hold and release a committed Heavy; charge tiers matter.", -120f, CaveUiTheme.Gold);
            CreateMoveRow(parent, font, "GUARD / PARRY", "Guard", Binding(GameAction.Parry), "Hold to Guard. Precise timing creates a Perfect Parry.", -180f, CaveUiTheme.Gold);
            CreateMoveRow(parent, font, "GUARD BREAK", "GuardBreak", Binding(GameAction.GuardBreak), "Break defense or counter a committed foe.", -240f, CaveUiTheme.Gold);
        }

        private void BuildFollowUpsPage(Transform parent, Font font)
        {
            CreateSection(parent, font, "FOLLOW-UPS / CHAINS", 220f);
            CreateChainRow(parent, font, "SPIN", "Spin", "BASH", "Bash", "Spin then Guard Break", 166f, CaveUiTheme.BorderBright);
            CreateChainRow(parent, font, "PARRY", "Guard", "HEAVY T1", "Heavy", "Normal Parry starts Heavy at Tier 1", 106f, CaveUiTheme.BorderBright);
            CreateChainRow(parent, font, "PERFECT PARRY", "Guard", "HEAVY T2", "Heavy", "Perfect Parry starts Heavy at Tier 2", 46f, CaveUiTheme.Mana);
            CreateChainRow(parent, font, "GUARD BREAK", "GuardBreak", "HEAVY T1", "Heavy", "Successful break opens a Heavy follow-up", -14f, CaveUiTheme.Gold);
            CreateChainRow(parent, font, "HEAVY / GB", "Heavy", "CROSS STEP", "CrossStep", "Dash after commitment to reposition", -74f, CaveUiTheme.BorderBright);
            CreateBraceCard(parent, font);
        }

        private void BuildSkillsPage(Transform parent, Font font)
        {
            CreateCompactCard(parent, font, "SKILLS", "SLOW SHOT\nFrost response and ranged pressure.\n\nBURN SHOT\nFire response.\n\nFLIGHT\nWind movement and Ground Smash.", new Vector2(-276f, 56f), new Vector2(250f, 350f), CaveUiTheme.BorderBright);
            CreateCompactCard(parent, font, "RESOURCES", "MANA\nPowers projectiles and skills.\n\nSTAMINA\nPowers Spin, Heavy, movement, and Ground Smash.\n\n" + Binding(GameAction.FireProjectile) + "  Fire the current mode projectile.", new Vector2(0f, 56f), new Vector2(250f, 350f), CaveUiTheme.Mana);
            CreateCompactCard(parent, font, "SPECIAL RULES", "FRENZY BREAK\n" + Binding(GameAction.Interact) + " tap prepares Physical; hold infuses Mana.\n\nUNBLOCKABLE ≠ UNPARRYABLE\nSome attacks bypass Guard but can still be perfectly parried.", new Vector2(276f, 56f), new Vector2(250f, 350f), CaveUiTheme.Gold);
        }

        private static void CreateSection(Transform parent, Font font, string title, float y)
        {
            Text text = Label(parent, "Section " + title, font, title, 15, CaveUiTheme.Gold, TextAnchor.MiddleLeft);
            Set(text.rectTransform, new Vector2(-404f, y), new Vector2(808f, 28f));
            GameObject dividerObject = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            dividerObject.transform.SetParent(parent, false);
            Image divider = dividerObject.GetComponent<Image>();
            divider.sprite = CaveUiArt.GetSprite("Ui_Divider");
            divider.type = Image.Type.Sliced;
            divider.color = new Color(1f, 1f, 1f, 0.72f);
            divider.raycastTarget = false;
            Set(divider.rectTransform, new Vector2(0f, y), new Vector2(808f, 28f));
            dividerObject.transform.SetAsFirstSibling();
        }

        private static void CreateMoveRow(Transform parent, Font font, string title, string iconName, string input, string description, float y, Color accent)
        {
            Image row = Panel(parent, "Move " + title, new Vector2(0f, y), new Vector2(808f, 52f), accent);
            CreateSquareIcon(row.transform, iconName, new Vector2(-377f, 0f), accent);
            Text name = Label(row.transform, "Name", font, title, 13, CaveUiTheme.PrimaryText, TextAnchor.MiddleLeft);
            Set(name.rectTransform, new Vector2(-332f, 0f), new Vector2(130f, 34f));
            Text key = Label(row.transform, "Input", font, input, 11, CaveUiTheme.BorderBright, TextAnchor.MiddleCenter);
            Set(key.rectTransform, new Vector2(-184f, 0f), new Vector2(110f, 28f));
            Image keyBacking = key.gameObject.AddComponent<Image>();
            CaveUiArt.ApplyCard(keyBacking, true, CaveUiTheme.BorderBright);
            key.rectTransform.SetAsLastSibling();
            Text body = Label(row.transform, "Description", font, description, 11, CaveUiTheme.SecondaryText, TextAnchor.MiddleLeft);
            Set(body.rectTransform, new Vector2(8f, 0f), new Vector2(374f, 36f));
        }

        private static void CreateChainRow(Transform parent, Font font, string start, string startIcon, string result, string resultIcon, string description, float y, Color accent)
        {
            Image row = Panel(parent, "Chain " + start, new Vector2(0f, y), new Vector2(808f, 50f), accent);
            CreateSquareIcon(row.transform, startIcon, new Vector2(-365f, 0f), accent);
            CreateSquareIcon(row.transform, resultIcon, new Vector2(-118f, 0f), accent);
            Text left = Label(row.transform, "Start", font, start, 12, CaveUiTheme.PrimaryText, TextAnchor.MiddleCenter);
            Set(left.rectTransform, new Vector2(-268f, 0f), new Vector2(126f, 30f));
            Text arrow = Label(row.transform, "Arrow", font, "→", 22, accent, TextAnchor.MiddleCenter);
            Set(arrow.rectTransform, new Vector2(-180f, 0f), new Vector2(36f, 32f));
            Text right = Label(row.transform, "Result", font, result, 12, accent, TextAnchor.MiddleCenter);
            Set(right.rectTransform, new Vector2(-54f, 0f), new Vector2(122f, 30f));
            Text body = Label(row.transform, "Description", font, description, 10, CaveUiTheme.SecondaryText, TextAnchor.MiddleLeft);
            Set(body.rectTransform, new Vector2(145f, 0f), new Vector2(246f, 34f));
        }

        private static void CreateBraceCard(Transform parent, Font font)
        {
            CreateCompactCard(parent, font, "BRACE", "GUARD + GB after the Parry window enters Brace.\n\nQUICK — mobile and cheaper next exit.\nFULL — restores Stamina and can Deflect.\nDEEP — immobile; restores Mana slowly.", new Vector2(0f, -192f), new Vector2(808f, 116f), CaveUiTheme.Mana);
        }

        private static void CreateCompactCard(Transform parent, Font font, string title, string body, Vector2 position, Vector2 size, Color accent)
        {
            Image panel = Panel(parent, title + " Card", position, size, accent);
            Text heading = Label(panel.transform, "Heading", font, title, 14, accent, TextAnchor.MiddleLeft);
            Set(heading.rectTransform, new Vector2(-size.x * 0.5f + 18f, size.y * 0.5f - 25f), new Vector2(size.x - 36f, 28f));
            Text text = Label(panel.transform, "Body", font, body, 11, CaveUiTheme.PrimaryText, TextAnchor.UpperLeft);
            Set(text.rectTransform, new Vector2(-size.x * 0.5f + 18f, size.y * 0.5f - 62f), new Vector2(size.x - 36f, size.y - 72f));
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color accent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            CaveUiArt.ApplyCard(image, false, accent);
            return image;
        }

        private static void CreateSquareIcon(Transform parent, string spriteName, Vector2 position, Color accent)
        {
            GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = CaveUiArt.GetSprite(spriteName);
            image.preserveAspect = true;
            image.color = accent;
            image.raycastTarget = false;
            Set(go.GetComponent<RectTransform>(), position, new Vector2(32f, 32f));
        }

        private static Text Label(Transform parent, string name, Font font, string value, int size, Color color, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private void Disable(string childName)
        {
            Transform target = transform.Find(childName);
            if (target != null) target.gameObject.SetActive(false);
        }

        private static void Set(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static string Binding(GameAction action)
        {
            return "[" + SettingsMenuController.FormatBinding(action) + "]";
        }
    }
}
