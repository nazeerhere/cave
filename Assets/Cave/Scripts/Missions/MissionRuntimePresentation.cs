using Cave.InputSystem;
using Cave.Player;
using Cave.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cave.Missions
{
    public sealed class MissionMapBootstrap : MonoBehaviour
    {
        private MissionControllerBase activeController;
        private void Start()
        {
            MissionRunContext context = MissionRunContext.Current;
            if (context == null || !context.HasMission || context.SelectedScene != gameObject.scene.name) return;
            switch (context.SelectedMode)
            {
                case CaveGameMode.CorruptionPurge: activeController = gameObject.AddComponent<CorruptionPurgeMissionController>(); break;
                case CaveGameMode.CorruptBounty: activeController = gameObject.AddComponent<CorruptBountyMissionController>(); break;
                case CaveGameMode.Containment: activeController = gameObject.AddComponent<ContainmentMissionController>(); break;
                default: activeController = gameObject.AddComponent<ClassicSweepMissionController>(); break;
            }
            MissionObjectiveHud hud = MissionObjectiveHud.Create();
            hud.Bind(activeController, context);
            activeController.Initialize(context);
        }
    }

    [RequireComponent(typeof(Collider2D))]
    public sealed class MissionTerminal : MonoBehaviour
    {
        private PlayerHealth player;
        private TextMesh prompt;
        private MissionSelectionHud hud;
        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            if (body == null) body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            body.drawMode = SpriteDrawMode.Sliced;
            body.size = new Vector2(.9f, 1.5f);
            body.color = new Color(.04f, .2f, .32f, .95f);
            body.sortingOrder = 4;
            GameObject label = new GameObject("Mission Prompt"); label.transform.SetParent(transform, false); label.transform.localPosition = Vector3.up * 1.4f;
            prompt = label.AddComponent<TextMesh>(); prompt.anchor = TextAnchor.MiddleCenter; prompt.alignment = TextAlignment.Center;
            prompt.fontSize = 46; prompt.characterSize = .04f; prompt.color = new Color(.3f, .85f, 1f, 1f);
            prompt.text = "[" + GameInput.Bindings.GetBinding(GameAction.Interact).Primary.ToString().ToUpperInvariant() + "] SELECT EXPEDITION";
            prompt.GetComponent<MeshRenderer>().sortingOrder = 30; prompt.gameObject.SetActive(false);
        }
        private void Update()
        {
            if (player != null && hud == null && GameInput.InteractPressed)
            {
                GameInput.ConsumeMenuInputForCurrentFrame(); GameInput.SetGameplayInputEnabled(false);
                hud = MissionSelectionHud.Create(this); prompt.gameObject.SetActive(false);
            }
        }
        public void Closed() { hud = null; GameInput.EnableGameplayAfterInputRelease(); if (prompt != null && player != null) prompt.gameObject.SetActive(true); }
        private void OnTriggerEnter2D(Collider2D other) { PlayerHealth found = other.GetComponentInParent<PlayerHealth>(); if (found != null) { player = found; if (hud == null) prompt.gameObject.SetActive(true); } }
        private void OnTriggerExit2D(Collider2D other) { if (other.GetComponentInParent<PlayerHealth>() == player) { player = null; if (prompt != null) prompt.gameObject.SetActive(false); } }
    }

    public sealed class MissionSelectionHud : MonoBehaviour
    {
        private MissionTerminal owner;
        private CaveMissionCatalog catalog;
        private Text modeText, mapText, summaryText, statusText;
        private int modeIndex, mapIndex;

        public static MissionSelectionHud Create(MissionTerminal owner)
        {
            GameObject root = new GameObject("Mission Selection HUD");
            Canvas canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();
            MissionSelectionHud hud = root.AddComponent<MissionSelectionHud>(); hud.owner = owner; hud.Build(); return hud;
        }

        private void Build()
        {
            catalog = Resources.Load<CaveMissionCatalog>("Missions/CaveMissionCatalog");
            if (catalog == null) catalog = CaveMissionCatalog.CreateRuntimeDefaults();
            Image backdrop = UiPanel(transform, "Panel", new Vector2(.5f, .5f), new Vector2(1040f, 620f), new Color(.015f, .025f, .055f, .97f));
            modeText = UiText(backdrop.transform, "Modes", new Vector2(0f, 190f), new Vector2(920f, 150f), 24);
            mapText = UiText(backdrop.transform, "Maps", new Vector2(0f, 35f), new Vector2(920f, 150f), 22);
            summaryText = UiText(backdrop.transform, "Summary", new Vector2(0f, -130f), new Vector2(920f, 150f), 20);
            statusText = UiText(backdrop.transform, "Status", new Vector2(0f, -255f), new Vector2(920f, 50f), 18);
            Refresh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { modeIndex = Wrap(modeIndex - 1, catalog.Modes.Length); Refresh(); }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { modeIndex = Wrap(modeIndex + 1, catalog.Modes.Length); Refresh(); }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { mapIndex = Wrap(mapIndex - 1, catalog.Maps.Length); Refresh(); }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { mapIndex = Wrap(mapIndex + 1, catalog.Maps.Length); Refresh(); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
        }

        private void Refresh()
        {
            CaveModeDefinition mode = catalog.Modes[modeIndex]; CaveMapDefinition map = catalog.Maps[mapIndex];
            string modes = "SELECT EXPEDITION\n";
            for (int i = 0; i < catalog.Modes.Length; i++) modes += (i == modeIndex ? "[ " : "  ") + catalog.Modes[i].displayName + (i == modeIndex ? " ]" : "  ") + (i + 1 < catalog.Modes.Length ? "    " : "");
            modeText.text = modes + "\n" + mode.description;
            string maps = "MAPS\n";
            for (int i = 0; i < catalog.Maps.Length; i++) maps += (i == mapIndex ? "> " : "  ") + catalog.Maps[i].displayName + (catalog.Maps[i].IsUnlocked ? "" : " [LOCKED]") + (i + 1 < catalog.Maps.Length ? "    " : "");
            mapText.text = maps;
            summaryText.text = "MODE: " + mode.displayName + "\nMAP: " + map.displayName + "\nOBJECTIVE: " + mode.objectiveSummary + "\n" + map.description;
            statusText.text = map.IsUnlocked ? "ENTER  CONFIRM     ESC  BACK" : map.LockReason;
        }

        private void Confirm()
        {
            CaveMapDefinition map = catalog.Maps[mapIndex];
            if (!map.IsUnlocked) { statusText.text = map.LockReason; return; }
            if (!Application.CanStreamedLevelBeLoaded(map.sceneName)) { statusText.text = "MAP IS NOT ENABLED IN BUILD SETTINGS"; return; }
            MissionRunContext.Current.Begin(catalog.Modes[modeIndex].mode, map, System.Environment.TickCount);
            if (!PlayerRunPersistence.PrepareTransition(map.sceneName, "MainEntrance"))
            {
                MissionRunContext.Current.ClearRuntime(true);
                statusText.text = "PLAYER RUN PERSISTENCE IS UNAVAILABLE";
                return;
            }
            GameInput.EnableGameplayAfterInputRelease();
            SceneManager.LoadSceneAsync(map.sceneName, LoadSceneMode.Single);
        }

        private void Close() { owner.Closed(); Destroy(gameObject); }
        private static int Wrap(int value, int count) { if (count <= 0) return 0; return (value % count + count) % count; }

        internal static Image UiPanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size;
            Image image = go.GetComponent<Image>(); image.color = color; return image;
        }
        internal static Text UiText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            Text text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.93f, .82f, .52f, 1f); return text;
        }
    }

    public sealed class MissionObjectiveHud : MonoBehaviour
    {
        private Text text; private MissionControllerBase controller; private MissionRunContext context;
        public static MissionObjectiveHud Create()
        {
            GameObject root = new GameObject("Mission Objective HUD"); Canvas canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 700;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            MissionObjectiveHud hud = root.AddComponent<MissionObjectiveHud>();
            hud.text = MissionSelectionHud.UiText(root.transform, "Objective", new Vector2(0f, 300f), new Vector2(720f, 80f), 20); return hud;
        }
        public void Bind(MissionControllerBase source, MissionRunContext run)
        {
            controller = source; context = run; controller.ObjectiveChanged += HandleObjective; context.StateChanged += HandleState;
        }
        private void HandleObjective(string title, string detail) { text.text = title + "\n" + detail; }
        private void HandleState(MissionLifecycleState state)
        {
            if (state == MissionLifecycleState.Success) text.text = "MISSION COMPLETE\nINTERACT  RETURN TO HUB";
            else if (state == MissionLifecycleState.Failure) text.text += "\nINTERACT  RETURN TO HUB";
            else if (state == MissionLifecycleState.Extraction) text.text = "REACH EXTRACTION";
        }
        private void Update() { if (context != null && (context.State == MissionLifecycleState.Success || context.State == MissionLifecycleState.Failure) && GameInput.InteractPressed) context.ReturnToHub(); }
        private void OnDestroy() { if (controller != null) controller.ObjectiveChanged -= HandleObjective; if (context != null) context.StateChanged -= HandleState; }
    }

    public static class MissionRuntimeInstaller
    {
        private static readonly string[] Maps = { "01_UpperCave", "02_Stronghold", "03_HollowDistricts", "04_DeepVeins", "05_HeartChamber" };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallCurrent() { Install(SceneManager.GetActiveScene(), LoadSceneMode.Single); SceneManager.sceneLoaded -= Install; SceneManager.sceneLoaded += Install; }
        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == MissionRunContext.HubScene)
            {
                MissionRunContext.Current?.ClearRuntime(true);
                LevelTransition legacyExit = Object.FindObjectOfType<LevelTransition>();
                if (legacyExit != null) legacyExit.enabled = false;
                if (Object.FindObjectOfType<MissionTerminal>() != null) return;
                GameObject terminal = new GameObject("Mission Expedition Terminal"); terminal.transform.position = new Vector3(0f, -1.2f, 0f);
                BoxCollider2D trigger = terminal.AddComponent<BoxCollider2D>(); trigger.size = new Vector2(2.2f, 2.4f); trigger.isTrigger = true;
                terminal.AddComponent<MissionTerminal>(); return;
            }
            for (int i = 0; i < Maps.Length; i++) if (scene.name == Maps[i]) { if (Object.FindObjectOfType<MissionMapBootstrap>() == null) new GameObject("Mission Map Bootstrap").AddComponent<MissionMapBootstrap>(); return; }
        }
    }
}
