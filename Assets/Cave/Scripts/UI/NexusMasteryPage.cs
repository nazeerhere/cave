using Cave.Axioms.Mastery;
using Cave.Combat;
using Cave.Domain;
using Cave.Player;
using Cave.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>Read-only Nexus presentation over the existing resource, Axiom, and Domain evidence owners.</summary>
    [DisallowMultipleComponent]
    public sealed class NexusMasteryPage : MonoBehaviour
    {
        private static readonly MasteryDomain[] Axioms =
        {
            MasteryDomain.Heat, MasteryDomain.Order, MasteryDomain.Flow, MasteryDomain.Mass,
            MasteryDomain.Phase, MasteryDomain.Resonance, MasteryDomain.StoneglassPrecision
        };
        private static readonly LawPhenomenon[] Phenomena =
        {
            LawPhenomenon.Heat, LawPhenomenon.Flow, LawPhenomenon.Mass, LawPhenomenon.Compression,
            LawPhenomenon.Potential, LawPhenomenon.Resonance, LawPhenomenon.Phase, LawPhenomenon.Order
        };

        private Font font;
        private PlayerResourceMastery resources;
        private PlayerHealth health;
        private SpinSwordAttack stamina;
        private PlayerMana mana;
        private AxiomMasteryState axiomMastery;
        private PlayerMasteryEvidenceRuntime evidence;
        private MasteryDomain selectedAxiom;
        private LawPhenomenon selectedPhenomenon;
        private Text resourceText;
        private Text axiomDetail;
        private Text domainDetail;
        private Text[] axiomLabels;
        private Text[] phenomenonLabels;

        public void Configure(GameObject player, Font uiFont)
        {
            font = uiFont;
            resources = player != null ? player.GetComponent<PlayerResourceMastery>() : null;
            health = player != null ? player.GetComponent<PlayerHealth>() : null;
            stamina = player != null ? player.GetComponent<SpinSwordAttack>() : null;
            mana = player != null ? player.GetComponent<PlayerMana>() : null;
            axiomMastery = player != null ? player.GetComponent<AxiomMasteryState>() : null;
            evidence = player != null ? player.GetComponent<PlayerMasteryEvidenceRuntime>() : null;
            Subscribe();
            Build();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            Unsubscribe();
            if (resources != null) resources.MasteryChanged += Refresh;
            if (axiomMastery != null) axiomMastery.MasteryChanged += HandleAxiomChanged;
            if (evidence != null) evidence.Changed += HandleEvidenceChanged;
        }
        private void Unsubscribe()
        {
            if (resources != null) resources.MasteryChanged -= Refresh;
            if (axiomMastery != null) axiomMastery.MasteryChanged -= HandleAxiomChanged;
            if (evidence != null) evidence.Changed -= HandleEvidenceChanged;
        }
        private void HandleAxiomChanged(MasteryDomain _, float __) => Refresh();
        private void HandleEvidenceChanged(PlayerMasteryEvidenceState _) => Refresh();

        private void Build()
        {
            if (resourceText != null) return;
            Image root = gameObject.AddComponent<Image>(); root.color = CaveUiTheme.SurfaceInset;
            Panel("Resource Mastery", transform, new Vector2(-410f, 8f), new Vector2(340f, 535f));
            Header("RESOURCE MASTERY", new Vector2(-410f, 250f), new Vector2(310f, 30f));
            resourceText = Label("Resource Values", transform, string.Empty, 13, new Vector2(-410f, 40f), new Vector2(300f, 390f), TextAnchor.UpperLeft, CaveUiTheme.PrimaryText);

            Panel("Axiom Mastery", transform, new Vector2(0f, 8f), new Vector2(430f, 535f));
            Header("AXIOM MASTERY", new Vector2(0f, 250f), new Vector2(400f, 30f));
            axiomLabels = new Text[Axioms.Length];
            for (int i = 0; i < Axioms.Length; i++)
            {
                int captured = i;
                Button button = Button("Axiom " + Axioms[i], transform, new Vector2(-130f + (i % 3) * 130f, 178f - (i / 3) * 86f), new Vector2(112f, 62f));
                button.onClick.AddListener(() => { selectedAxiom = Axioms[captured]; Refresh(); });
                axiomLabels[i] = button.GetComponentInChildren<Text>();
            }
            axiomDetail = Label("Axiom Detail", transform, string.Empty, 12, new Vector2(0f, -156f), new Vector2(380f, 120f), TextAnchor.UpperLeft, CaveUiTheme.PrimaryText);

            Panel("Domain Evidence", transform, new Vector2(410f, 8f), new Vector2(340f, 535f));
            Header("DOMAIN EVIDENCE", new Vector2(410f, 250f), new Vector2(310f, 30f));
            phenomenonLabels = new Text[Phenomena.Length];
            for (int i = 0; i < Phenomena.Length; i++)
            {
                int captured = i;
                Button button = Button("Phenomenon " + Phenomena[i], transform, new Vector2(340f + (i % 2) * 140f, 184f - (i / 2) * 49f), new Vector2(126f, 38f));
                button.onClick.AddListener(() => { selectedPhenomenon = Phenomena[captured]; Refresh(); });
                phenomenonLabels[i] = button.GetComponentInChildren<Text>();
            }
            domainDetail = Label("Domain Detail", transform, string.Empty, 12, new Vector2(410f, -151f), new Vector2(300f, 126f), TextAnchor.UpperLeft, CaveUiTheme.PrimaryText);
        }

        private void Refresh()
        {
            if (resourceText == null) return;
            resourceText.text = ResourceBlock();
            for (int i = 0; i < Axioms.Length; i++)
            {
                float value = axiomMastery != null ? axiomMastery.Get(Axioms[i]) : 0f;
                axiomLabels[i].text = Axioms[i].ToString().ToUpperInvariant() + "\n" + Mathf.RoundToInt(value * 100f) + "%";
                axiomLabels[i].color = Axioms[i] == selectedAxiom ? CaveUiTheme.BorderBright : CaveUiTheme.PrimaryText;
            }
            float selectedValue = axiomMastery != null ? axiomMastery.Get(selectedAxiom) : 0f;
            axiomDetail.text = selectedAxiom.ToString().ToUpperInvariant() + "\nMastery: " + Mathf.RoundToInt(selectedValue * 100f) + "%\n"
                + "Evidence: Ordinary Use • Quality • Correction • Regulation • Counterphase • Convergence";

            PlayerMasteryEvidenceState snapshot = evidence != null ? evidence.Snapshot : PlayerMasteryEvidenceState.Empty;
            PlayerMasteryPolicy policy = PlayerMasteryPolicy.Default;
            MasteryChannelReport report = snapshot.GetPhenomenonReport(selectedPhenomenon, policy);
            for (int i = 0; i < Phenomena.Length; i++)
            {
                MasteryChannelReport item = snapshot.GetPhenomenonReport(Phenomena[i], policy);
                phenomenonLabels[i].text = Phenomena[i].ToString().ToUpperInvariant() + "  " + (item.IsMastered ? "✓" : "");
                phenomenonLabels[i].color = Phenomena[i] == selectedPhenomenon ? CaveUiTheme.BorderBright : CaveUiTheme.PrimaryText;
            }
            domainDetail.text = selectedPhenomenon.ToString().ToUpperInvariant() + "\n"
                + Channel("STATE", report.StateEvidence, policy.StateThreshold, report.StateLock) + "\n"
                + Channel("RATE", report.RateEvidence, policy.RateThreshold, report.RateLock) + "\n"
                + Channel("ACCELERATION", report.AccelerationEvidence, policy.AccelerationThreshold, report.AccelerationLock) + "\n"
                + "LAW CONTROL: " + (report.IsMastered ? "ELIGIBLE" : "INCOMPLETE") + "\n"
                + "Run-local evidence";
        }

        private string ResourceBlock()
        {
            if (resources == null || health == null || stamina == null || mana == null)
                return "Resource mastery is unavailable until the player runtime is installed.";
            return "HEALTH GROWTH  +" + Percent(resources.HealthGrowthPercent) + "\n"
                + "Current maximum: " + health.MaxHealth + "\nBase maximum: " + resources.BaseMaximumHealth + "\n\n"
                + "STAMINA GROWTH  +" + Percent(resources.StaminaGrowthPercent) + "\n"
                + "Current maximum: " + stamina.MaximumStamina.ToString("0") + "\nBase maximum: " + resources.BaseMaximumStamina.ToString("0") + "\n\n"
                + "MANA GROWTH  +" + Percent(resources.ManaGrowthPercent) + "\n"
                + "Current maximum: " + mana.MaximumMana.ToString("0") + "\nBase maximum: " + resources.BaseMaximumMana.ToString("0");
        }

        private static string Percent(float value) => Mathf.RoundToInt(Mathf.Max(0f, value) * 100f) + "%";
        private static string Channel(string name, float actual, float required, bool complete) => name + "  " + actual.ToString("0.##") + " / " + required.ToString("0.##") + (complete ? "  UNLOCKED" : "  LOCKED");
        private void Header(string text, Vector2 pos, Vector2 size) { Text header = Label(text, transform, text, 16, pos, size, TextAnchor.MiddleCenter, CaveUiTheme.Gold); header.fontStyle = FontStyle.Bold; }
        private Button Button(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = size; go.GetComponent<Image>().color = CaveUiTheme.SurfaceRaised;
            Button button = go.GetComponent<Button>(); CaveUiArt.ApplyButton(button, false); Label("Label", go.transform, string.Empty, 10, Vector2.zero, size - new Vector2(6f, 4f), TextAnchor.MiddleCenter, CaveUiTheme.PrimaryText); return button;
        }
        private static void Panel(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = size; Image image = go.GetComponent<Image>(); image.color = CaveUiTheme.Surface; CaveUiArt.ApplyCard(image, false, CaveUiTheme.BronzeLight);
        }
        private Text Label(string name, Transform parent, string text, int size, Vector2 pos, Vector2 dimensions, TextAnchor alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = dimensions; Text label = go.GetComponent<Text>(); label.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"); label.fontSize = size; label.text = text; label.alignment = alignment; label.color = color; label.raycastTarget = false; return label;
        }
    }
}
