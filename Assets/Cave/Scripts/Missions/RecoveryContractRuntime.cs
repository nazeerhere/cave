using System;
using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.Missions
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ContractRecoveryNode : MonoBehaviour
    {
        private PlayerHealth nearbyPlayer;
        private TextMesh prompt;
        private bool claimed;
        private string interactionName;

        public event Action<ContractRecoveryNode> Claimed;

        public void Configure(RecoveryContractObjectiveType type, int index)
        {
            interactionName = type == RecoveryContractObjectiveType.FloraSamples
                ? "HARVEST SAMPLE"
                : "RECOVER CACHE";
            gameObject.name = (type == RecoveryContractObjectiveType.FloraSamples
                ? "Contract Flora Sample "
                : "Contract Supply Cache ") + index;

            BoxCollider2D trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.25f, 1.7f);

            SpriteRenderer visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sprite = Resources.Load<Sprite>(type == RecoveryContractObjectiveType.FloraSamples
                ? "UI/Missions/ModeIcons/MissionMode_CorruptionPurge"
                : "UI/Missions/ModeIcons/MissionMode_Containment");
            visual.color = type == RecoveryContractObjectiveType.FloraSamples
                ? new Color(.35f, 1f, .58f, .95f)
                : new Color(1f, .72f, .24f, .95f);
            visual.sortingOrder = 20;
            transform.localScale = Vector3.one * .7f;

            GameObject label = new GameObject("Interaction Prompt");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = Vector3.up * 1.45f;
            prompt = label.AddComponent<TextMesh>();
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.alignment = TextAlignment.Center;
            prompt.fontSize = 42;
            prompt.characterSize = .035f;
            prompt.color = new Color(.92f, .8f, .48f, 1f);
            prompt.text = "[" + GameInput.Bindings.GetBinding(GameAction.Interact).Primary
                .ToString().ToUpperInvariant() + "] " + interactionName;
            prompt.GetComponent<MeshRenderer>().sortingOrder = 30;
            prompt.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (claimed || nearbyPlayer == null || !GameInput.InteractPressed) return;
            claimed = true;
            if (prompt != null) prompt.gameObject.SetActive(false);
            Claimed?.Invoke(this);
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (claimed) return;
            PlayerHealth found = other.GetComponentInParent<PlayerHealth>();
            if (found == null) return;
            nearbyPlayer = found;
            if (prompt != null) prompt.gameObject.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerHealth>() != nearbyPlayer) return;
            nearbyPlayer = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }
    }

    public sealed class RecoveryContractController : MissionControllerBase
    {
        private static readonly Vector3[] NodeOffsets =
        {
            new Vector3(-5f, .4f, 0f),
            new Vector3(5f, .4f, 0f),
            new Vector3(10f, .4f, 0f)
        };

        private ContractDefinition definition;
        private ContractBoardTerminal board;
        private PlayerHealth player;
        private PlayerContractRecords records;
        private ContractAttemptHud hud;
        private long attemptId;
        private float startedAt;
        private int collected;
        private int terminalFrame = -1;
        private bool terminal;

        public static bool TryCreate(
            ContractBoardTerminal owner,
            ContractDefinition contract,
            PlayerHealth currentPlayer,
            MissionRunContext context,
            long currentAttemptId)
        {
            if (owner == null || contract == null || currentPlayer == null || context == null
                || currentAttemptId == 0L || !contract.IsImplemented
                || FindObjectOfType<RecoveryContractController>() != null) return false;

            GameObject host = new GameObject("Recovery Contract - " + contract.DisplayName);
            RecoveryContractController controller = host.AddComponent<RecoveryContractController>();
            controller.definition = contract;
            controller.board = owner;
            controller.player = currentPlayer;
            controller.attemptId = currentAttemptId;
            controller.records = PlayerContractRecords.Ensure(currentPlayer.gameObject);
            controller.hud = ContractAttemptHud.Create();
            controller.hud.Bind(controller, context);
            controller.Initialize(context);
            return true;
        }

        public override void Initialize(MissionRunContext context)
        {
            base.Initialize(context);
            if (definition == null || player == null || records == null)
            {
                FailAttempt("Contract runtime could not initialize.");
                return;
            }

            startedAt = Time.unscaledTime;
            player.Died += HandlePlayerDied;
            int count = Mathf.Min(definition.RequiredCount, NodeOffsets.Length);
            for (int i = 0; i < count; i++)
            {
                GameObject nodeObject = new GameObject("Contract Objective " + (i + 1));
                nodeObject.transform.SetParent(transform, false);
                nodeObject.transform.position = player.transform.position + NodeOffsets[i];
                ContractRecoveryNode node = nodeObject.AddComponent<ContractRecoveryNode>();
                node.Configure(definition.ObjectiveType, i + 1);
                node.Claimed += HandleClaimed;
            }
            PublishProgress();
        }

        private void Update()
        {
            if (Context == null) return;
            if (!terminal && Context.State == MissionLifecycleState.Active
                && Input.GetKeyDown(KeyCode.Backspace))
            {
                FailAttempt("Contract abandoned. No contract penalty applied.");
                return;
            }

            if (terminal && Time.frameCount > terminalFrame && GameInput.InteractPressed)
            {
                FinishAttempt();
            }
        }

        private void HandleClaimed(ContractRecoveryNode node)
        {
            if (node != null) node.Claimed -= HandleClaimed;
            if (terminal || Context == null || Context.State != MissionLifecycleState.Active) return;
            collected++;
            if (collected < definition.RequiredCount)
            {
                PublishProgress();
                return;
            }

            Context.SetState(MissionLifecycleState.PrimaryObjectiveComplete);
            ContractRating rating = definition.EvaluateRating(Time.unscaledTime - startedAt);
            PlayerCurrency currency = player.GetComponent<PlayerCurrency>();
            if (!ContractRewardService.TrySettle(
                    Context, definition, attemptId, rating, currency, records, out int reward))
            {
                FailAttempt("Reward settlement was rejected; no coins were granted.");
                return;
            }

            terminal = true;
            terminalFrame = Time.frameCount;
            Context.SetState(MissionLifecycleState.Success);
            Publish("CONTRACT COMPLETE — " + rating.ToString().ToUpperInvariant(),
                "+" + reward + " COINS    INTERACT TO CLOSE");
        }

        private void PublishProgress()
        {
            string noun = definition.ObjectiveType == RecoveryContractObjectiveType.FloraSamples
                ? "Flora samples"
                : "Supply caches";
            Publish(definition.DisplayName, noun + ": " + collected + " / " + definition.RequiredCount
                + "    BACKSPACE  ABANDON");
        }

        private void HandlePlayerDied()
        {
            if (!terminal) FailAttempt("Attempt failed on death. No contract penalty applied.");
        }

        private void FailAttempt(string reason)
        {
            if (terminal) return;
            terminal = true;
            terminalFrame = Time.frameCount;
            if (Context != null) Context.SetState(MissionLifecycleState.Failure);
            Publish("CONTRACT FAILED", reason + "    INTERACT TO CLOSE");
            RemoveRemainingNodes();
        }

        private void FinishAttempt()
        {
            if (Context != null
                && Context.IsContractAttempt
                && Context.ActiveContractAttemptId == attemptId) Context.EndContractAttempt();
            if (board != null) board.AttemptEnded();
            Destroy(gameObject);
        }

        private void RemoveRemainingNodes()
        {
            ContractRecoveryNode[] nodes = GetComponentsInChildren<ContractRecoveryNode>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].Claimed -= HandleClaimed;
                Destroy(nodes[i].gameObject);
            }
        }

        private void OnDestroy()
        {
            if (player != null) player.Died -= HandlePlayerDied;
            RemoveRemainingNodes();
            if (hud != null) Destroy(hud.gameObject);
            if (Context != null
                && Context.IsContractAttempt
                && Context.ActiveContractAttemptId == attemptId) Context.EndContractAttempt();
        }
    }

    public sealed class ContractAttemptHud : MonoBehaviour
    {
        private Text text;
        private MissionControllerBase controller;
        private MissionRunContext context;

        public static ContractAttemptHud Create()
        {
            GameObject root = new GameObject("Contract Objective HUD");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 710;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            root.AddComponent<GraphicRaycaster>();
            ContractAttemptHud hud = root.AddComponent<ContractAttemptHud>();

            Image panel = MissionSelectionHud.UiPanel(root.transform, "Contract Objective Panel",
                new Vector2(.5f, 1f), new Vector2(720f, 92f), new Color(.015f, .035f, .065f, .94f));
            panel.rectTransform.pivot = new Vector2(.5f, 1f);
            panel.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            hud.text = MissionSelectionHud.UiText(panel.transform, "Objective", Vector2.zero,
                new Vector2(670f, 72f), 19);
            hud.text.fontStyle = FontStyle.Bold;
            return hud;
        }

        public void Bind(MissionControllerBase source, MissionRunContext run)
        {
            controller = source;
            context = run;
            controller.ObjectiveChanged += HandleObjective;
        }

        private void HandleObjective(string title, string detail)
        {
            if (text != null) text.text = title + "\n" + detail;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.ObjectiveChanged -= HandleObjective;
        }
    }
}
