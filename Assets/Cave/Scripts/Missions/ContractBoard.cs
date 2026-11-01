using Cave.InputSystem;
using Cave.Player;
using Cave.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.Missions
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class ContractBoardTerminal : MonoBehaviour
    {
        private PlayerHealth player;
        private TextMesh prompt;
        private ContractBoardHud hud;
        private bool attemptRunning;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            if (body == null) body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = Resources.Load<Sprite>("UI/HUD/Approved/HudCardRow");
            body.drawMode = body.sprite != null ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
            body.size = new Vector2(1.25f, 1.55f);
            body.color = new Color(.24f, .13f, .035f, .96f);
            body.sortingOrder = 4;

            GameObject label = new GameObject("Contract Board Prompt");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = Vector3.up * 1.45f;
            prompt = label.AddComponent<TextMesh>();
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.alignment = TextAlignment.Center;
            prompt.fontSize = 44;
            prompt.characterSize = .038f;
            prompt.color = new Color(.95f, .72f, .3f, 1f);
            prompt.text = "[" + GameInput.Bindings.GetBinding(GameAction.Interact).Primary
                .ToString().ToUpperInvariant() + "] CONTRACT BOARD";
            prompt.GetComponent<MeshRenderer>().sortingOrder = 30;
            prompt.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (player == null || hud != null || attemptRunning || !GameInput.InteractPressed) return;
            GameInput.ConsumeMenuInputForCurrentFrame();
            GameInput.SetGameplayInputEnabled(false);
            hud = ContractBoardHud.Create(this, player);
            prompt.gameObject.SetActive(false);
        }

        public bool TryStartAttempt(ContractDefinition definition, PlayerHealth currentPlayer, out string failure)
        {
            failure = null;
            if (attemptRunning || FindObjectOfType<RecoveryContractController>() != null)
            {
                failure = "A CONTRACT ATTEMPT IS ALREADY ACTIVE";
                return false;
            }
            if (definition == null || !definition.IsImplemented)
            {
                failure = "THIS CONTRACT IS NOT YET AVAILABLE";
                return false;
            }

            MissionRunContext context = MissionRunContext.Current;
            if (context == null)
            {
                failure = "MISSION CONTEXT IS UNAVAILABLE";
                return false;
            }

            long attemptId = context.BeginContract(definition, System.Environment.TickCount);
            if (!RecoveryContractController.TryCreate(this, definition, currentPlayer, context, attemptId))
            {
                context.EndContractAttempt();
                failure = "CONTRACT ACTIVITY COULD NOT START";
                return false;
            }

            attemptRunning = true;
            hud = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
            GameInput.EnableGameplayAfterInputRelease();
            return true;
        }

        public void Closed()
        {
            hud = null;
            GameInput.EnableGameplayAfterInputRelease();
            if (prompt != null && player != null && !attemptRunning) prompt.gameObject.SetActive(true);
        }

        public void AttemptEnded()
        {
            attemptRunning = false;
            if (prompt != null && player != null) prompt.gameObject.SetActive(true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerHealth found = other.GetComponentInParent<PlayerHealth>();
            if (found == null) return;
            player = found;
            PlayerContractRecords.Ensure(player.gameObject);
            if (hud == null && !attemptRunning) prompt.gameObject.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerHealth>() != player) return;
            player = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }
    }

    public sealed class ContractBoardHud : MonoBehaviour
    {
        private static readonly Color Panel = new Color(.014f, .03f, .055f, .985f);
        private static readonly Color Card = new Color(.025f, .055f, .075f, .985f);
        private static readonly Color Gold = new Color(.84f, .61f, .27f, 1f);
        private static readonly Color Cyan = new Color(.18f, .78f, 1f, 1f);
        private static readonly Color Muted = new Color(.52f, .58f, .63f, 1f);

        private ContractBoardTerminal owner;
        private PlayerHealth player;
        private PlayerContractRecords records;
        private ContractCatalog catalog;
        private ContractCard[] cards;
        private Button launchButton;
        private Text status;
        private int selectedIndex;
        private bool launched;

        public static ContractBoardHud Create(ContractBoardTerminal terminal, PlayerHealth currentPlayer)
        {
            GameObject root = new GameObject("Nexus Contract Board HUD");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 905;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>();
            ContractBoardHud hud = root.AddComponent<ContractBoardHud>();
            hud.owner = terminal;
            hud.player = currentPlayer;
            hud.records = PlayerContractRecords.Ensure(currentPlayer.gameObject);
            hud.Build();
            return hud;
        }

        private void Build()
        {
            catalog = ContractCatalog.LoadOrCreateDefaults();
            Image scrim = MissionSelectionHud.UiPanel(transform, "Dimmer", new Vector2(.5f, .5f),
                Vector2.zero, new Color(.003f, .008f, .018f, .72f));
            scrim.rectTransform.anchorMin = Vector2.zero;
            scrim.rectTransform.anchorMax = Vector2.one;
            scrim.rectTransform.offsetMin = Vector2.zero;
            scrim.rectTransform.offsetMax = Vector2.zero;

            Image frame = MissionSelectionHud.UiPanel(transform, "Contract Board Frame", new Vector2(.5f, .5f),
                new Vector2(1470f, 820f), Gold);
            CaveUiArt.ApplyCard(frame, false, Gold);
            Image interior = MissionSelectionHud.UiPanel(frame.transform, "Interior", new Vector2(.5f, .5f),
                new Vector2(1456f, 806f), Panel);
            Text title = MissionSelectionHud.UiText(interior.transform, "Title", new Vector2(0f, 360f),
                new Vector2(900f, 46f), 34);
            title.text = "NEXUS CONTRACT BOARD";
            title.fontStyle = FontStyle.Bold;
            Text subtitle = MissionSelectionHud.UiText(interior.transform, "Subtitle", new Vector2(0f, 327f),
                new Vector2(1050f, 26f), 15);
            subtitle.text = "REPEATABLE TRAINING • NO ENTRY FEE • BASE REWARD PAID ON SUCCESS";
            subtitle.color = Muted;

            ContractDefinition[] definitions = catalog.Contracts;
            cards = new ContractCard[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                int column = i % 2;
                int row = i / 2;
                Vector2 position = new Vector2(column == 0 ? -357f : 357f, 240f - row * 145f);
                cards[i] = new ContractCard(this, interior.transform, definitions[i], i, position);
            }

            launchButton = CreateButton(interior.transform, "Launch", new Vector2(-155f, -355f), LaunchSelected);
            CreateButton(interior.transform, "Close", new Vector2(155f, -355f), Close);
            status = MissionSelectionHud.UiText(interior.transform, "Status", new Vector2(0f, -310f),
                new Vector2(900f, 25f), 15);
            Text help = MissionSelectionHud.UiText(interior.transform, "Help", new Vector2(0f, -390f),
                new Vector2(1100f, 20f), 13);
            help.text = "ARROWS / WASD  SELECT     ENTER  LAUNCH     ESC  CLOSE";
            help.color = Muted;
            Refresh();
        }

        private void Update()
        {
            if (launched) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Select(Wrap(selectedIndex - 1));
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Select(Wrap(selectedIndex + 1));
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Select(Wrap(selectedIndex - 2));
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Select(Wrap(selectedIndex + 2));
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) LaunchSelected();
        }

        private void Select(int index)
        {
            selectedIndex = index;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].Refresh(i == selectedIndex, records.GetRecord(cards[i].Definition.Id));
            }

            ContractDefinition selected = cards[selectedIndex].Definition;
            launchButton.interactable = selected.IsImplemented;
            status.text = selected.IsImplemented
                ? "AVAILABLE AT EVERY LEVEL — " + selected.CoinReward + " COINS"
                : "UNAVAILABLE IN PHASE 2";
            status.color = selected.IsImplemented ? Cyan : Muted;
        }

        private void LaunchSelected()
        {
            if (launched) return;
            ContractDefinition definition = cards[selectedIndex].Definition;
            if (!definition.IsImplemented)
            {
                status.text = "THIS CONTRACT IS NOT YET AVAILABLE";
                return;
            }

            if (!owner.TryStartAttempt(definition, player, out string failure))
            {
                status.text = failure;
                status.color = new Color(1f, .45f, .35f, 1f);
                return;
            }

            launched = true;
            Destroy(gameObject);
        }

        private void Close()
        {
            if (launched) return;
            owner.Closed();
            Destroy(gameObject);
        }

        private int Wrap(int value)
        {
            int count = cards != null ? cards.Length : 0;
            return count == 0 ? 0 : (value % count + count) % count;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction action)
        {
            Image image = MissionSelectionHud.UiPanel(parent, label + " Button", new Vector2(.5f, .5f),
                new Vector2(270f, 52f), Gold);
            image.rectTransform.anchoredPosition = position;
            Button button = image.gameObject.AddComponent<Button>();
            button.onClick.AddListener(action);
            CaveUiArt.ApplyButton(button, false);
            Text text = MissionSelectionHud.UiText(image.transform, "Label", Vector2.zero,
                new Vector2(240f, 34f), 19);
            text.text = label.ToUpperInvariant();
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        private sealed class ContractCard
        {
            private readonly Image frame;
            private readonly Text title;
            private readonly Text objective;
            private readonly Text reward;
            private readonly Text record;
            private readonly Text availability;

            public ContractCard(ContractBoardHud hud, Transform parent, ContractDefinition definition,
                int index, Vector2 position)
            {
                Definition = definition;
                frame = MissionSelectionHud.UiPanel(parent, "Contract " + index, new Vector2(.5f, .5f),
                    new Vector2(680f, 132f), Gold);
                frame.rectTransform.anchoredPosition = position;
                Button button = frame.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => hud.Select(index));
                Image inside = MissionSelectionHud.UiPanel(frame.transform, "Interior", new Vector2(.5f, .5f),
                    new Vector2(670f, 122f), Card);
                title = MissionSelectionHud.UiText(inside.transform, "Title", new Vector2(-188f, 42f),
                    new Vector2(280f, 28f), 17);
                title.alignment = TextAnchor.MiddleLeft;
                title.fontStyle = FontStyle.Bold;
                objective = MissionSelectionHud.UiText(inside.transform, "Objective", new Vector2(-138f, 0f),
                    new Vector2(380f, 48f), 13);
                objective.alignment = TextAnchor.MiddleLeft;
                reward = MissionSelectionHud.UiText(inside.transform, "Reward", new Vector2(246f, 40f),
                    new Vector2(145f, 26f), 15);
                record = MissionSelectionHud.UiText(inside.transform, "Record", new Vector2(208f, -4f),
                    new Vector2(220f, 42f), 12);
                availability = MissionSelectionHud.UiText(inside.transform, "Availability", new Vector2(210f, -45f),
                    new Vector2(220f, 22f), 12);
                title.text = "TIER " + Roman(definition.Tier) + " — " + definition.DisplayName;
                objective.text = definition.Objective + "\nFOCUS: " + definition.TrainingFocus;
                reward.text = definition.CoinReward + " COINS";
            }

            public ContractDefinition Definition { get; }

            public void Refresh(bool selected, ContractCompletionRecord progress)
            {
                CaveUiArt.ApplyCard(frame, selected, selected ? Cyan : Gold);
                frame.color = selected ? Cyan : Gold;
                title.color = Definition.IsImplemented ? (selected ? Cyan : Gold) : Muted;
                objective.color = Definition.IsImplemented ? Color.white : Muted;
                reward.color = Definition.IsImplemented ? Gold : Muted;
                record.text = "BEST: " + (progress.BestRating == ContractRating.None
                    ? "—"
                    : progress.BestRating.ToString().ToUpperInvariant())
                    + "\nCOMPLETIONS: " + progress.CompletionCount;
                availability.text = Definition.IsImplemented ? "AVAILABLE" : "UNAVAILABLE";
                availability.color = Definition.IsImplemented ? new Color(.35f, 1f, .56f, 1f) : Muted;
            }

            private static string Roman(ContractTier tier)
            {
                switch (tier)
                {
                    case ContractTier.Adventurer: return "II";
                    case ContractTier.Veteran: return "III";
                    default: return "I";
                }
            }
        }
    }
}
