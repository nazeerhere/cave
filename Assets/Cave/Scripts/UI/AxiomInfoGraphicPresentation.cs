using Cave.Axioms.Vfx;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Static infographic presentation for the existing Cave Axioms reference page.</summary>
    public static class AxiomInfoGraphicPresentation
    {
        private static AxiomVfxCatalog catalog;

        public static void Build(Transform parent, Font font)
        {
            if (parent == null || parent.Find("Axiom Infographic") != null)
            {
                return;
            }

            DisableLegacyCard(parent, "RESONANCE Card");
            DisableLegacyCard(parent, "PHASE / IMAGINARY Card");
            DisableLegacyCard(parent, "CONTROL Card");

            catalog = Resources.Load<AxiomVfxCatalog>("Axioms/AxiomVfxCatalog");
            GameObject root = new GameObject("Axiom Infographic", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Set(root.GetComponent<RectTransform>(), new Vector2(0f, 8f), new Vector2(1020f, 500f));
            BuildResonance(root.transform, font);
            BuildPhase(root.transform, font);
            BuildControl(root.transform, font);
            BuildCombatLoop(root.transform, font);
        }

        private static void DisableLegacyCard(Transform parent, string name)
        {
            Transform legacy = parent.Find(name);
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false);
            }
        }

        private static void BuildResonance(Transform parent, Font font)
        {
            Image panel = Panel(parent, "Resonance Panel", new Vector2(-340f, 38f), new Vector2(322f, 422f), CaveUiTheme.BorderBright);
            Heading(panel.transform, font, "RESONANCE", "CADENCE PRESSURE AGAINST GUARD", CaveUiTheme.BorderBright);
            NodeRow(panel.transform, font, new[] { "REFERENCE", "R1", "R2", "R3", "BREAK" }, 92f, CaveUiTheme.BorderBright);
            Text endpoint = Label(panel.transform, font, "DESTABILIZED", 14, new Color(1f, 0.38f, 0.36f, 1f), TextAnchor.MiddleCenter);
            Set(endpoint.rectTransform, new Vector2(0f, 45f), new Vector2(268f, 28f));
            Body(panel.transform, font, "• Repeated well-timed contact builds cadence.\n• Resonance Break fractures Guard coherence.\n• Destabilized targets cannot Guard briefly; Parry remains possible.", -14f, 104f);
            Summary(panel.transform, font, "PRESSURE DEFENSE THROUGH RHYTHM, NOT RAW DAMAGE.", -166f, CaveUiTheme.BorderBright);
        }

        private static void BuildPhase(Transform parent, Font font)
        {
            Color phase = new Color(0.8f, 0.42f, 1f, 1f);
            Image panel = Panel(parent, "Phase Panel", new Vector2(0f, 38f), new Vector2(322f, 422f), phase);
            Heading(panel.transform, font, "PHASE / IMAGINARY", "LATENT PRESSURE AND COLLAPSE", phase);
            NodeRow(panel.transform, font, new[] { "OPENING", "STACKS", "COLLAPSE", "EFFECT" }, 92f, phase);
            Icon(panel.transform, catalog != null ? catalog.PhaseApply : default(AxiomVfxDefinition), new Vector2(-74f, 38f), 42f, phase);
            Icon(panel.transform, catalog != null ? catalog.PhaseCollapseStrong : default(AxiomVfxDefinition), new Vector2(74f, 38f), 42f, phase);
            Text parity = Label(panel.transform, font, "ODD  •  ACTIVE EFFECT\nEVEN  •  REALIGNED\nSTRONG ODD  •  BRIEF BURST\nWEAK-LONG ODD  •  LINGERING", 10, CaveUiTheme.PrimaryText, TextAnchor.MiddleLeft);
            Set(parity.rectTransform, new Vector2(0f, -32f), new Vector2(260f, 74f));
            Summary(panel.transform, font, "COMMITTED HEAVIES COLLAPSE LATENT PHASE.", -166f, phase);
        }

        private static void BuildControl(Transform parent, Font font)
        {
            Image panel = Panel(parent, "Control Panel", new Vector2(340f, 38f), new Vector2(322f, 422f), CaveUiTheme.Gold);
            Heading(panel.transform, font, "CONTROL", "TIMING SHAPES ELEMENTAL RESPONSE", CaveUiTheme.Gold);
            NodeRow(panel.transform, font, new[] { "PULSE", "STATE", "RATE", "ACCEL." }, 92f, CaveUiTheme.Gold);
            Text lockLabels = Label(panel.transform, font, "STATE LOCK   →   RATE LOCK   →   ACCELERATION LOCK", 10, CaveUiTheme.PrimaryText, TextAnchor.MiddleCenter);
            Set(lockLabels.rectTransform, new Vector2(0f, 48f), new Vector2(280f, 28f));
            ElementStrip(panel.transform, font);
            Body(panel.transform, font, "• Applications create activity over time.\n• Error cues describe timing misalignment.\n• Each lock makes control more stable.", -58f, 76f);
            Summary(panel.transform, font, "APPLICATION  →  TRAJECTORY  →  CONTROL", -166f, CaveUiTheme.Gold);
        }

        private static void BuildCombatLoop(Transform parent, Font font)
        {
            Image strip = Panel(parent, "Combat Loop", new Vector2(0f, -206f), new Vector2(1002f, 64f), CaveUiTheme.BronzeLight);
            string[] labels = { "APPLY PRESSURE", "BUILD RESONANCE / OPENINGS", "READ TIMING / CONTROL", "COLLAPSE PHASE / EXPLOIT" };
            for (int index = 0; index < labels.Length; index++)
            {
                float x = -350f + index * 235f;
                Text label = Label(strip.transform, font, labels[index], 10, index == 3 ? new Color(0.84f, 0.5f, 1f, 1f) : CaveUiTheme.PrimaryText, TextAnchor.MiddleCenter);
                Set(label.rectTransform, new Vector2(x, 0f), new Vector2(190f, 32f));
                if (index < labels.Length - 1)
                {
                    Text arrow = Label(strip.transform, font, "→", 21, CaveUiTheme.Gold, TextAnchor.MiddleCenter);
                    Set(arrow.rectTransform, new Vector2(x + 114f, 0f), new Vector2(32f, 32f));
                }
            }
        }

        private static void ElementStrip(Transform parent, Font font)
        {
            string[] names = { "HEAT", "ORDER", "FLOW", "MASS" };
            Color[] colors = { new Color(1f, 0.35f, 0.18f, 1f), CaveUiTheme.Gold, CaveUiTheme.BorderBright, new Color(0.67f, 0.39f, 0.92f, 1f) };
            for (int index = 0; index < names.Length; index++)
            {
                Text element = Label(parent, font, names[index], 10, colors[index], TextAnchor.MiddleCenter);
                Set(element.rectTransform, new Vector2(-105f + index * 70f, 8f), new Vector2(62f, 32f));
            }
        }

        private static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color accent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            Set(rect, position, size);
            Image image = go.GetComponent<Image>();
            image.color = CaveUiTheme.SurfaceInset;
            image.raycastTarget = false;
            CaveUiArt.ApplyCard(image, false, accent);
            return image;
        }

        private static void Heading(Transform parent, Font font, string title, string subtitle, Color color)
        {
            Text heading = Label(parent, font, title, 18, color, TextAnchor.MiddleCenter);
            Set(heading.rectTransform, new Vector2(0f, 169f), new Vector2(284f, 30f));
            Text deck = Label(parent, font, subtitle, 9, CaveUiTheme.SecondaryText, TextAnchor.MiddleCenter);
            Set(deck.rectTransform, new Vector2(0f, 139f), new Vector2(286f, 22f));
        }

        private static void NodeRow(Transform parent, Font font, string[] labels, float y, Color color)
        {
            float spacing = 250f / (labels.Length - 1);
            for (int index = 0; index < labels.Length; index++)
            {
                Text node = Label(parent, font, "◇\n" + labels[index], 9, color, TextAnchor.MiddleCenter);
                Set(node.rectTransform, new Vector2(-125f + index * spacing, y), new Vector2(62f, 48f));
                if (index < labels.Length - 1)
                {
                    Text arrow = Label(parent, font, "→", 14, CaveUiTheme.Gold, TextAnchor.MiddleCenter);
                    Set(arrow.rectTransform, new Vector2(-125f + (index + .5f) * spacing, y + 5f), new Vector2(22f, 22f));
                }
            }
        }

        private static void Body(Transform parent, Font font, string text, float y, float height)
        {
            Text body = Label(parent, font, text, 10, CaveUiTheme.PrimaryText, TextAnchor.UpperLeft);
            Set(body.rectTransform, new Vector2(0f, y), new Vector2(270f, height));
            body.fontStyle = FontStyle.Normal;
        }

        private static void Summary(Transform parent, Font font, string text, float y, Color color)
        {
            Text summary = Label(parent, font, text, 9, color, TextAnchor.MiddleCenter);
            Set(summary.rectTransform, new Vector2(0f, y), new Vector2(280f, 34f));
        }

        private static void Icon(Transform parent, AxiomVfxDefinition definition, Vector2 position, float size, Color fallback)
        {
            SpriteRenderer renderer = definition.Prefab != null ? definition.Prefab.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            GameObject go = new GameObject("Axiom Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = renderer.sprite;
            image.preserveAspect = true;
            image.color = fallback;
            image.raycastTarget = false;
            Set(image.rectTransform, position, new Vector2(size, size));
        }

        private static Text Label(Transform parent, Font font, string text, int size, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }

        private static void Set(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
