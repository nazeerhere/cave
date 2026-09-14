using Cave.Enemies;
using Cave.Axioms.Vfx;
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
            new LedgerStatusEntry(MobStatusIconKind.Regeneration, "REGENERATION", "Restores health over time."),
            new LedgerStatusEntry(MobStatusIconKind.Overheal, "OVERHEAL", "Health exceeds its ordinary limit."),
            new LedgerStatusEntry(MobStatusIconKind.Possessed, "POSSESSED", "The Cave has altered the host."),
            new LedgerStatusEntry(MobStatusIconKind.ElementallyBuffed, "ELEMENTALLY BUFFED", "An elemental effect empowers this enemy."),
            new LedgerStatusEntry(MobStatusIconKind.WatcherMark, "STUDIED", "A Detective is recording your actions."),
            new LedgerStatusEntry(MobStatusIconKind.GazeLock, "GAZE LOCK", "The Watcher has fixed its attention on you."),
            new LedgerStatusEntry(MobStatusIconKind.TowerSuppression, "TOWER INTERFERENCE", "Suppresses recovery inside the tower field."),
            new LedgerStatusEntry(MobStatusIconKind.Imaginary, "IMAGINARY / PHASE", "Latent Phase potential; committed attacks can collapse it."),
            new LedgerStatusEntry(MobStatusIconKind.Stoneglass, "STONEGLASS", "Crystalline fracture, precision, and reconstruction.")
        };

        // These entries deliberately use the same MobStatusIconKind values as
        // EnemyTeamBuffState and EnemyWorldStatusIndicators.  The Ledger is an
        // explanation of the live world-state icon, not a second icon catalog.
        private static readonly LedgerStatusEntry[] CorruptedTeamEntries =
        {
            new LedgerStatusEntry(MobStatusIconKind.Stagger, "TYRANT'S PRESENCE", "Resists knockback; recovers from stagger faster."),
            new LedgerStatusEntry(MobStatusIconKind.Frenzied, "BLOOD FRENZY", "Attacks and recovers from attacks faster."),
            new LedgerStatusEntry(MobStatusIconKind.StrengthBuff, "COMMANDER'S MARK", "Moves faster."),
            new LedgerStatusEntry(MobStatusIconKind.Stagger, "ANCIENT DISCIPLINE", "Resists knockback and disruption."),
            new LedgerStatusEntry(MobStatusIconKind.ElementallyBuffed, "ARCANE RESONANCE", "Caster abilities resolve faster."),
            new LedgerStatusEntry(MobStatusIconKind.Regeneration, "DEATH COVENANT", "Recovers health when a nearby ally dies."),
            new LedgerStatusEntry(MobStatusIconKind.GazeLock, "UNNATURAL INSIGHT", "Reacts and repositions more effectively."),
            new LedgerStatusEntry(MobStatusIconKind.TowerSuppression, "TACTICAL CALCULATION", "Repositions more efficiently."),
            new LedgerStatusEntry(MobStatusIconKind.PinRoot, "FIELD COMMUNION", "Resists displacement and field control.")
        };

        private const string AxiomReferenceText =
            "<b>DEFENSE / RHYTHM</b>\n"
            + "GUARD — Sustained defense.\n"
            + "PERFECT PARRY — A precisely timed defense.\n"
            + "RESONANCE — Correctly timed hits synchronize with Guard.\n"
            + "RESONANCE BREAK — Guard coherence has fractured.\n"
            + "DESTABILIZED — Guard is unavailable; Parry remains possible.\n"
            + "COUNTERPHASE — Opposing Phase effects cancel.\n\n"
            + "<b>CAVE AXIOMS</b>\n"
            + "HEAT — Tracks accumulated thermal response and dissipation.\n"
            + "ORDER — Tracks constraint and resistance to changing state.\n"
            + "FLOW — Tracks coherent movement and divergence.\n"
            + "MASS — Tracks force expression and inertia.\n"
            + "IMAGINARY / PHASE — Tracks latent Phase potential and reality-state vulnerability.\n"
            + "STONEGLASS — Tracks crystalline precision, fracture, and reconstruction.\n\n"
            + "<b>AXIOM CONTROL</b>\n"
            + "STATE ERROR — Spatial or state alignment is incorrect.\n"
            + "RATE ERROR — Application rhythm has drifted.\n"
            + "ACCELERATION ERROR — The changing rate is discontinuous.\n"
            + "STATE LOCK — Holds a stable state region.\n"
            + "RATE LOCK — Holds a stable application rate.\n"
            + "ACCELERATION LOCK — Holds a stable changing rate.";

        private GameObject rosterPage;
        private GameObject symbolsPage;
        private Text pageIndicator;
        private Button previousButton;
        private Button nextButton;
        private MobStatusIconRegistry iconRegistry;
        private AxiomVfxCatalog axiomVfxCatalog;
        private Font presentationFont;
        private int currentPage;
        private bool rosterDirty = true;

        public void Configure(Text legacyBody)
        {
            if (rosterPage != null || legacyBody == null)
            {
                return;
            }

            legacyBody.gameObject.SetActive(false);
            presentationFont = legacyBody.font;
            rosterPage = CreateRosterPage(legacyBody);
            iconRegistry = Resources.Load<MobStatusIconRegistry>(IconRegistryResourceName);
            axiomVfxCatalog = Resources.Load<AxiomVfxCatalog>("Axioms/AxiomVfxCatalog");
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
                RebuildRosterPage();
                rosterDirty = false;
            }

            bool roster = currentPage == 0;
            if (rosterPage != null) rosterPage.SetActive(roster);
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

        private GameObject CreateRosterPage(Text source)
        {
            GameObject page = new GameObject("Ledger Roster Page", typeof(RectTransform));
            page.transform.SetParent(source.transform.parent, false);
            CopyRect(source.rectTransform, page.GetComponent<RectTransform>());
            return page;
        }

        private void RebuildRosterPage()
        {
            for (int index = rosterPage.transform.childCount - 1; index >= 0; index--)
            {
                Object.Destroy(rosterPage.transform.GetChild(index).gameObject);
            }

            Text title = CreateText("Roster Heading", rosterPage.transform, presentationFont, 15);
            title.text = "ENEMY LEDGER / ROSTER";
            title.fontStyle = FontStyle.Bold;
            title.color = CaveUiTheme.Gold;
            title.alignment = TextAnchor.MiddleLeft;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(900f, 28f));

            EnemyLedger.RosterEntry[] entries = EnemyLedger.GetRosterEntries();
            Font font = title.font;
            for (int index = 0; index < entries.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                CreateRosterCard(
                    rosterPage.transform,
                    font,
                    entries[index],
                    EnemyLedger.IsEncountered(entries[index].EnemyId),
                    new Vector2(6f + column * 454f, -42f - row * 112f));
            }
        }

        private static void CreateRosterCard(Transform parent, Font font, EnemyLedger.RosterEntry entry, bool discovered, Vector2 position)
        {
            GameObject card = new GameObject("Roster " + entry.EnemyId, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(442f, 102f));
            Image image = card.GetComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            CaveUiArt.ApplyCard(image, discovered, discovered ? CaveUiTheme.BorderBright : CaveUiTheme.IronLight);

            Text portrait = CreateText("Portrait Crest", card.transform, font, 22);
            portrait.text = discovered ? "◇" : "?";
            portrait.color = discovered ? CaveUiTheme.BorderBright : CaveUiTheme.IronLight;
            portrait.alignment = TextAnchor.MiddleCenter;
            SetRect(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(52f, 52f));

            Text heading = CreateText("Name", card.transform, font, 12);
            heading.text = discovered ? entry.Name : "???";
            heading.fontStyle = FontStyle.Bold;
            heading.color = discovered ? CaveUiTheme.Gold : CaveUiTheme.SecondaryText;
            heading.alignment = TextAnchor.MiddleLeft;
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(68f, -18f), new Vector2(158f, 22f));

            Text archetype = CreateText("Archetype", card.transform, font, 9);
            archetype.text = discovered ? entry.Archetype : "UNDISCOVERED";
            archetype.color = CaveUiTheme.BorderBright;
            archetype.alignment = TextAnchor.MiddleLeft;
            SetRect(archetype.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(68f, -39f), new Vector2(158f, 18f));

            Text details = CreateText("Details", card.transform, font, 9);
            details.text = discovered ? entry.Description : "Encounter this enemy to reveal its role and counterplay.";
            details.color = CaveUiTheme.PrimaryText;
            details.alignment = TextAnchor.UpperLeft;
            details.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(details.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(68f, 10f), new Vector2(158f, 42f));

            Text counter = CreateText("Counterplay", card.transform, font, 9);
            counter.text = discovered ? "COUNTERPLAY\n" + entry.Counterplay : "";
            counter.color = CaveUiTheme.SecondaryText;
            counter.alignment = TextAnchor.UpperLeft;
            counter.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(counter.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(236f, 10f), new Vector2(194f, 70f));
        }

        private GameObject CreateSymbolsPage(Text source)
        {
            GameObject pageObject = new GameObject("Ledger Symbols Page", typeof(RectTransform));
            pageObject.transform.SetParent(source.transform.parent, false);
            RectTransform pageRect = pageObject.GetComponent<RectTransform>();
            CopyRect(source.rectTransform, pageRect);
            CreateCorruptedTeamEffectsSection(pageObject.transform, source.font);
            CreateReferenceSection(pageObject.transform, source.font, "STATUS / DEFENSE", "Burning • Chilled • Poisoned • Stagger\nFrenzied • Regeneration • Guard • Perfect Parry\nResonance • Break • Destabilized • Counterphase", new Vector2(-318f, -105f), new Vector2(308f, 144f), CaveUiTheme.BorderBright);
            CreateReferenceSection(pageObject.transform, source.font, "CAVE AXIOMS / CONTROL", "Heat • Order • Flow • Mass\nImaginary / Phase • Stoneglass\nState, Rate, and Acceleration locks", new Vector2(0f, -105f), new Vector2(308f, 144f), CaveUiTheme.Gold);
            CreateReferenceSection(pageObject.transform, source.font, "MARKERS / SHORTHAND", "Elite • Summoner • Armored • Enraged • Stealth\nGB Guard Break • PP Perfect Parry\nLoS Line of Sight • Poise stability", new Vector2(318f, -105f), new Vector2(308f, 144f), CaveUiTheme.BronzeLight);
            return pageObject;
        }

        private void CreateCorruptedTeamEffectsSection(Transform parent, Font font)
        {
            GameObject section = new GameObject("Corrupted Team Effects Section", typeof(RectTransform), typeof(Image));
            section.transform.SetParent(parent, false);
            RectTransform rect = section.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 126f);
            rect.sizeDelta = new Vector2(960f, 204f);
            Image image = section.GetComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            CaveUiArt.ApplyCard(image, false, CaveUiTheme.Mana);

            Text heading = CreateText("Heading", section.transform, font, 13);
            heading.text = "CORRUPTED TEAM EFFECTS";
            heading.fontStyle = FontStyle.Bold;
            heading.color = CaveUiTheme.Mana;
            heading.alignment = TextAnchor.MiddleLeft;
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -20f), new Vector2(928f, 24f));

            for (int index = 0; index < CorruptedTeamEntries.Length; index++)
            {
                int column = index % 3;
                int row = index / 3;
                CreateCorruptedTeamEffectRow(
                    section.transform,
                    font,
                    CorruptedTeamEntries[index],
                    new Vector2(14f + column * 314f, -47f - row * 48f));
            }
        }

        private void CreateCorruptedTeamEffectRow(Transform parent, Font font, LedgerStatusEntry entry, Vector2 position)
        {
            GameObject row = new GameObject("Team Effect " + entry.Title, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetRect(row.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(292f, 40f));

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(row.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = iconRegistry != null ? iconRegistry.GetIcon(entry.Kind) : null;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(15f, 0f), new Vector2(28f, 28f));

            Text label = CreateText("Description", row.transform, font, 8);
            label.text = "<b>" + entry.Title + "</b>\n" + entry.Description;
            label.color = CaveUiTheme.PrimaryText;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(250f, 40f));
        }

        private static void CreateReferenceSection(Transform parent, Font font, string title, string body, Vector2 position, Vector2 size, Color accent)
        {
            GameObject section = new GameObject(title + " Section", typeof(RectTransform), typeof(Image));
            section.transform.SetParent(parent, false);
            RectTransform rect = section.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = section.GetComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            CaveUiArt.ApplyCard(image, false, accent);

            Text heading = CreateText("Heading", section.transform, font, 13);
            heading.text = title;
            heading.fontStyle = FontStyle.Bold;
            heading.color = accent;
            heading.alignment = TextAnchor.MiddleLeft;
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -24f), new Vector2(size.x - 32f, 28f));
            Text content = CreateText("Entries", section.transform, font, 11);
            content.text = body;
            content.color = CaveUiTheme.PrimaryText;
            content.alignment = TextAnchor.UpperLeft;
            content.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(content.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -54f), new Vector2(size.x - 32f, size.y - 62f));
        }

        private void CreateAxiomSymbols(Transform parent)
        {
            if (axiomVfxCatalog == null)
            {
                return;
            }

            CreateAxiomSymbol(parent, "Resonance Symbol", axiomVfxCatalog.ResonanceProgress, 56f);
            CreateAxiomSymbol(parent, "Resonance Break Symbol", axiomVfxCatalog.ResonanceBreak, 96f);
            CreateAxiomSymbol(parent, "Destabilized Symbol", axiomVfxCatalog.Destabilized, 116f);
            CreateAxiomSymbol(parent, "Counterphase Symbol", axiomVfxCatalog.Counterphase, 136f);
            CreateAxiomSymbol(parent, "Heat Symbol", axiomVfxCatalog.Heat, 218f);
            CreateAxiomSymbol(parent, "Order Symbol", axiomVfxCatalog.Order, 238f);
            CreateAxiomSymbol(parent, "Flow Symbol", axiomVfxCatalog.Flow, 258f);
            CreateAxiomSymbol(parent, "Mass Symbol", axiomVfxCatalog.Mass, 278f);
            CreateAxiomSymbol(parent, "Phase Symbol", axiomVfxCatalog.PhaseApply, 298f);
            CreateAxiomSymbol(parent, "Stoneglass Symbol", axiomVfxCatalog.PhaseCollapseStrong, 318f);
            CreateAxiomSymbol(parent, "Error Symbol", axiomVfxCatalog.ErrorState, 390f);
            CreateAxiomSymbol(parent, "Lock Symbol", axiomVfxCatalog.ControlSuccess, 450f);
        }

        private static void CreateAxiomSymbol(Transform parent, string name, AxiomVfxDefinition definition, float y)
        {
            GameObject prefab = definition.Prefab;
            SpriteRenderer source = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
            if (source == null || source.sprite == null)
            {
                return;
            }

            GameObject iconObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = source.sprite;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(474f, -y), new Vector2(18f, 18f));
        }

        private void CreateGlossaryRow(Transform parent, Font font, LedgerStatusEntry entry, float y)
        {
            GameObject row = new GameObject("Status " + entry.Title, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            SetRect(rowRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(232f, 27f));

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(row.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = iconRegistry != null ? iconRegistry.GetIcon(entry.Kind) : null;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(24f, 24f));

            Text label = CreateText("Description", row.transform, font, 10);
            label.text = "<b>" + entry.Title + "</b>\n" + entry.Description;
            label.color = new Color(0.87f, 0.84f, 0.75f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(178f, 26f));
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

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
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
