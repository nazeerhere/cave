using Cave.InputSystem;
using Cave.Player;
using Cave.UI;
using Cave.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cave.Missions
{
    public sealed class MissionSelectionHud : MonoBehaviour
    {
        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;
        private static readonly Color ScrimColor = new Color(0.005f, 0.01f, 0.025f, 0.67f);
        private static readonly Color PanelColor = new Color(0.018f, 0.04f, 0.075f, 0.985f);
        private static readonly Color CardColor = new Color(0.025f, 0.07f, 0.105f, 0.985f);
        private static readonly Color CardInnerColor = new Color(0.012f, 0.026f, 0.05f, 0.96f);
        private static readonly Color Bronze = new Color(0.79f, 0.58f, 0.28f, 1f);
        private static readonly Color Cyan = new Color(0.12f, 0.78f, 1f, 1f);
        private static readonly Color Muted = new Color(0.56f, 0.62f, 0.68f, 1f);
        private static int nextTransitionId;

        private MissionTerminal owner;
        private CaveMissionCatalog catalog;
        private ModeCardView[] modeCards;
        private MapCardView[] mapCards;
        private Image detailThumbnail;
        private Text detailMapName;
        private Text detailDescription;
        private Text detailMode;
        private Text detailObjective;
        private Text statusText;
        private Button confirmButton;
        private int modeIndex, mapIndex;
        private bool isTransitioning;
        private AsyncOperation transitionOperation;
        private int transitionId;

        public static MissionSelectionHud Create(MissionTerminal owner)
        {
            GameObject root = new GameObject("Mission Selection HUD");
            Canvas canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>();
            MissionSelectionHud hud = root.AddComponent<MissionSelectionHud>(); hud.owner = owner; hud.Build(); return hud;
        }

        private void Build()
        {
            catalog = Resources.Load<CaveMissionCatalog>("Missions/CaveMissionCatalog");
            if (catalog == null) catalog = CaveMissionCatalog.CreateRuntimeDefaults();

            CreateFullscreenPanel(transform, "Screen Dimmer", ScrimColor);
            Transform header = CreateFramedPanel(transform, "Expedition Header", new Vector2(0f, 365f), new Vector2(960f, 86f), PanelColor, Bronze);
            Text headerTitle = UiText(header, "Title", new Vector2(0f, 13f), new Vector2(850f, 42f), 34);
            headerTitle.text = "SELECT EXPEDITION";
            headerTitle.fontStyle = FontStyle.Bold;
            Text headerSubtitle = UiText(header, "Subtitle", new Vector2(0f, -22f), new Vector2(850f, 26f), 16);
            headerSubtitle.text = "CHOOSE YOUR PATH INTO THE DEPTHS";
            headerSubtitle.color = Muted;

            Transform main = CreateFramedPanel(transform, "Expedition Main Frame", new Vector2(0f, -40f), new Vector2(1480f, 770f), PanelColor, Bronze);
            CreateSectionLabel(main, "Mode Section Label", "1. GAME MODE SELECTION", 290f);
            CreateSectionLabel(main, "Map Section Label", "2. MAP SELECTION", 94f);

            BuildModeCards(main);
            BuildMapCards(main);
            BuildDetailPanel(main);
            BuildActionRow(main);
            Text hints = UiText(main, "Navigation Hints", new Vector2(0f, -373f), new Vector2(1260f, 20f), 15);
            hints.text = "A / D or LEFT / RIGHT  CHANGE MODE     W / S or UP / DOWN  CHANGE MAP     ENTER  CONFIRM     ESC  BACK";
            hints.color = Muted;
            Refresh();
        }

        private void BuildModeCards(Transform parent)
        {
            CaveModeDefinition[] modes = catalog.Modes;
            int count = modes != null ? modes.Length : 0;
            modeCards = new ModeCardView[count];
            for (int index = 0; index < count; index++)
            {
                float x = GetRowPosition(index, count, 352f);
                modeCards[index] = new ModeCardView(this, parent, modes[index], index, new Vector2(x, 205f));
            }
        }

        private void BuildMapCards(Transform parent)
        {
            CaveMapDefinition[] maps = catalog.Maps;
            int count = maps != null ? maps.Length : 0;
            mapCards = new MapCardView[count];
            for (int index = 0; index < count; index++)
            {
                float x = GetRowPosition(index, count, 278f);
                mapCards[index] = new MapCardView(this, parent, maps[index], index, new Vector2(x, 3f));
            }
        }

        private void BuildDetailPanel(Transform parent)
        {
            Transform detail = CreateFramedPanel(parent, "Selected Map Information", new Vector2(0f, -168f), new Vector2(1320f, 158f), CardColor, Bronze);
            detailThumbnail = CreateThumbnail(detail, "Selected Map Thumbnail", new Vector2(-540f, 0f), new Vector2(190f, 116f), null, false);
            detailMapName = UiText(detail, "Map Name", new Vector2(-292f, 43f), new Vector2(290f, 30f), 22);
            detailMapName.alignment = TextAnchor.MiddleLeft;
            detailMapName.fontStyle = FontStyle.Bold;
            detailDescription = UiText(detail, "Map Description", new Vector2(-275f, -5f), new Vector2(350f, 58f), 16);
            detailDescription.alignment = TextAnchor.UpperLeft;
            detailDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailMode = UiText(detail, "Mode Summary", new Vector2(282f, 34f), new Vector2(385f, 28f), 16);
            detailMode.alignment = TextAnchor.MiddleLeft;
            detailObjective = UiText(detail, "Objective Summary", new Vector2(282f, -15f), new Vector2(385f, 64f), 15);
            detailObjective.alignment = TextAnchor.UpperLeft;
            detailObjective.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void BuildActionRow(Transform parent)
        {
            confirmButton = CreateActionButton(parent, "Confirm Button", "CONFIRM", new Vector2(-154f, -300f), Confirm);
            CreateActionButton(parent, "Back Button", "BACK", new Vector2(154f, -300f), Close);
            statusText = UiText(parent, "Selection Status", new Vector2(0f, -345f), new Vector2(700f, 20f), 14);
        }

        private void Update()
        {
            if (isTransitioning)
            {
                if (transitionOperation != null) return;

                // A load that cannot produce an operation must leave the selector usable.
                isTransitioning = false;
                PlayerRunPersistence.CancelPendingTransition();
                MissionRunContext.Current?.ClearRuntime(true);
                statusText.text = "MAP COULD NOT BE LOADED";
                if (confirmButton != null) confirmButton.interactable = true;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { SelectMode(Wrap(modeIndex - 1, catalog.Modes.Length)); }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { SelectMode(Wrap(modeIndex + 1, catalog.Modes.Length)); }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { SelectMap(Wrap(mapIndex - 1, catalog.Maps.Length)); }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { SelectMap(Wrap(mapIndex + 1, catalog.Maps.Length)); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
        }

        private void SelectMode(int index)
        {
            modeIndex = index;
            Refresh();
        }

        private void SelectMap(int index)
        {
            mapIndex = index;
            Refresh();
        }

        private void Refresh()
        {
            CaveModeDefinition mode = catalog.Modes[modeIndex]; CaveMapDefinition map = catalog.Maps[mapIndex];
            for (int index = 0; index < modeCards.Length; index++) modeCards[index].SetSelected(index == modeIndex);
            for (int index = 0; index < mapCards.Length; index++) mapCards[index].SetSelected(index == mapIndex);
            detailMapName.text = map.displayName;
            detailDescription.text = map.description;
            detailMode.text = "MODE: " + mode.displayName;
            detailObjective.text = "OBJECTIVE\n" + mode.objectiveSummary;
            SetThumbnail(detailThumbnail, map.thumbnail, map.IsUnlocked);
            statusText.text = map.IsUnlocked ? "READY TO DEPLOY" : map.LockReason;
            statusText.color = map.IsUnlocked ? Cyan : new Color(1f, .48f, .34f, 1f);
            if (confirmButton != null) confirmButton.interactable = map.IsUnlocked;
        }

        private void Confirm()
        {
            if (isTransitioning) return;
            Debug.Log("[Cave][Mission] Confirm entered.", this);
            CaveMapDefinition map = catalog.Maps[mapIndex];
            if (!map.IsUnlocked) { statusText.text = map.LockReason; return; }
            if (!Application.CanStreamedLevelBeLoaded(map.sceneName)) { statusText.text = "MAP IS NOT ENABLED IN BUILD SETTINGS"; return; }
            MissionRunContext context = MissionRunContext.Current;
            if (context == null) { statusText.text = "MISSION CONTEXT IS UNAVAILABLE"; return; }

            context.Begin(catalog.Modes[modeIndex].mode, map, System.Environment.TickCount);
            if (!PlayerRunPersistence.PrepareTransition(map.sceneName, "MainEntrance"))
            {
                context.ClearRuntime(true);
                statusText.text = "PLAYER RUN PERSISTENCE IS UNAVAILABLE";
                return;
            }

            isTransitioning = true;
            transitionId = ++nextTransitionId;
            if (confirmButton != null) confirmButton.interactable = false;
            statusText.text = "LOADING " + map.displayName.ToUpperInvariant() + "...";
            statusText.color = Cyan;
            Debug.Log("[Cave][Mission] Confirm transaction " + transitionId
                + " prepared for " + map.sceneName + " at MainEntrance.", this);

            try
            {
                transitionOperation = SceneManager.LoadSceneAsync(map.sceneName, LoadSceneMode.Single);
                Debug.Log("[Cave][Mission] Confirm transaction " + transitionId
                    + " issued one LoadSceneAsync request for " + map.sceneName + ".", this);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                PlayerRunPersistence.CancelPendingTransition();
                isTransitioning = false;
                context.ClearRuntime(true);
                statusText.text = "MAP COULD NOT BE LOADED";
                if (confirmButton != null) confirmButton.interactable = true;
                return;
            }

            if (transitionOperation == null)
            {
                PlayerRunPersistence.CancelPendingTransition();
                isTransitioning = false;
                context.ClearRuntime(true);
                statusText.text = "MAP COULD NOT BE LOADED";
                if (confirmButton != null) confirmButton.interactable = true;
                return;
            }

            GameInput.EnableGameplayAfterInputRelease();
        }

        private void Close()
        {
            if (isTransitioning) return;
            owner.Closed();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (isTransitioning)
            {
                Debug.Log("[Cave][Mission] Mission selector is unloading with the old scene.", this);
            }
        }
        private static int Wrap(int value, int count) { if (count <= 0) return 0; return (value % count + count) % count; }

        private static float GetRowPosition(int index, int count, float spacing)
        {
            return count <= 1 ? 0f : (index - (count - 1) * .5f) * spacing;
        }

        private static void CreateSectionLabel(Transform parent, string name, string value, float y)
        {
            Text label = UiText(parent, name, new Vector2(-630f, y), new Vector2(430f, 32f), 21);
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Bold;
            label.text = value;
            label.color = Bronze;
            Image line = UiPanel(parent, name + " Divider", new Vector2(.5f, .5f), new Vector2(760f, 2f), new Color(Bronze.r, Bronze.g, Bronze.b, .6f));
            line.rectTransform.anchoredPosition = new Vector2(190f, y);
        }

        private static Transform CreateFramedPanel(Transform parent, string name, Vector2 position, Vector2 size, Color innerColor, Color accent)
        {
            Image outer = UiPanel(parent, name, new Vector2(.5f, .5f), size, accent);
            outer.rectTransform.anchoredPosition = position;
            Sprite frame = CaveUiArt.GetSprite("Ui_MainFrame");
            if (frame != null)
            {
                outer.sprite = frame;
                outer.type = Image.Type.Sliced;
                outer.color = Color.white;
            }
            Image inner = UiPanel(outer.transform, "Interior", new Vector2(.5f, .5f), size - new Vector2(14f, 14f), innerColor);
            inner.raycastTarget = false;
            return outer.transform;
        }

        private static Image CreateFullscreenPanel(Transform parent, string name, Color color)
        {
            Image panel = UiPanel(parent, name, new Vector2(.5f, .5f), Vector2.zero, color);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.raycastTarget = true;
            return panel;
        }

        private static Image CreateThumbnail(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, bool unlocked)
        {
            Image frame = UiPanel(parent, name, new Vector2(.5f, .5f), size + new Vector2(8f, 8f), Bronze);
            frame.rectTransform.anchoredPosition = position;
            frame.raycastTarget = false;
            Image image = UiPanel(frame.transform, "Image", new Vector2(.5f, .5f), size, CardInnerColor);
            image.raycastTarget = false;
            SetThumbnail(image, sprite, unlocked);
            return image;
        }

        private static void SetThumbnail(Image image, Sprite sprite, bool unlocked)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = image.sprite != null ? Image.Type.Simple : Image.Type.Sliced;
            image.preserveAspect = image.sprite != null;
            image.color = sprite != null ? (unlocked ? Color.white : new Color(.28f, .3f, .34f, 1f))
                : (unlocked ? new Color(.07f, .22f, .31f, 1f) : new Color(.035f, .045f, .06f, 1f));
        }

        private static Button CreateActionButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            Image image = UiPanel(parent, name, new Vector2(.5f, .5f), new Vector2(270f, 54f), Bronze);
            image.rectTransform.anchoredPosition = position;
            Button button = image.gameObject.AddComponent<Button>();
            button.onClick.AddListener(action);
            CaveUiArt.ApplyButton(button, false);
            Text text = UiText(image.transform, "Label", Vector2.zero, new Vector2(240f, 36f), 20);
            text.text = label;
            text.fontStyle = FontStyle.Bold;
            text.color = label == "CONFIRM" ? Cyan : Bronze;
            return button;
        }

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
            text.color = new Color(.93f, .82f, .52f, 1f); text.raycastTarget = false; return text;
        }

        private sealed class ModeCardView
        {
            private readonly Image frame;
            private readonly Text title;
            private readonly Text description;
            private readonly Image thumbnail;

            public ModeCardView(MissionSelectionHud hud, Transform parent, CaveModeDefinition definition, int index, Vector2 position)
            {
                Transform root = CreateFramedPanel(parent, "Mode Card " + index, position, new Vector2(328f, 132f), CardColor, Bronze);
                frame = root.GetComponent<Image>();
                Button button = root.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => hud.SelectMode(index));
                thumbnail = CreateThumbnail(root, "Thumbnail", new Vector2(-116f, 0f), new Vector2(72f, 76f), definition != null ? definition.icon : null, true);
                title = UiText(root, "Mode Name", new Vector2(62f, 31f), new Vector2(184f, 32f), 17);
                title.alignment = TextAnchor.MiddleLeft;
                title.fontStyle = FontStyle.Bold;
                description = UiText(root, "Mode Description", new Vector2(62f, -14f), new Vector2(184f, 58f), 13);
                description.alignment = TextAnchor.UpperLeft;
                description.horizontalOverflow = HorizontalWrapMode.Wrap;
                title.text = definition != null ? definition.displayName : "UNAVAILABLE";
                description.text = definition != null ? definition.description : string.Empty;
            }

            public void SetSelected(bool selected)
            {
                CaveUiArt.ApplyCard(frame, selected, selected ? Cyan : Bronze);
                frame.color = selected ? Cyan : Bronze;
                title.color = selected ? Cyan : Bronze;
                description.color = selected ? Color.white : Muted;
            }
        }

        private sealed class MapCardView
        {
            private readonly Image frame;
            private readonly Text title;
            private readonly Text lockText;
            private readonly Image thumbnail;
            private readonly CaveMapDefinition definition;

            public MapCardView(MissionSelectionHud hud, Transform parent, CaveMapDefinition map, int index, Vector2 position)
            {
                definition = map;
                Transform root = CreateFramedPanel(parent, "Map Card " + index, position, new Vector2(254f, 140f), CardColor, Bronze);
                frame = root.GetComponent<Image>();
                Button button = root.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => hud.SelectMap(index));
                thumbnail = CreateThumbnail(root, "Thumbnail", new Vector2(0f, 21f), new Vector2(224f, 67f), map != null ? map.thumbnail : null, map != null && map.IsUnlocked);
                title = UiText(root, "Map Name", new Vector2(0f, -31f), new Vector2(230f, 26f), 15);
                title.fontStyle = FontStyle.Bold;
                lockText = UiText(root, "Lock State", new Vector2(0f, -55f), new Vector2(230f, 22f), 12);
                title.text = map != null ? map.displayName : "UNAVAILABLE";
            }

            public void SetSelected(bool selected)
            {
                bool unlocked = definition != null && definition.IsUnlocked;
                CaveUiArt.ApplyCard(frame, selected, selected ? Cyan : Bronze);
                frame.color = selected ? Cyan : Bronze;
                title.color = unlocked ? (selected ? Cyan : Bronze) : Muted;
                lockText.text = unlocked ? string.Empty : "LOCKED  " + (definition != null ? definition.LockReason : "UNAVAILABLE");
                lockText.color = unlocked ? Color.clear : new Color(1f, .48f, .34f, 1f);
                SetThumbnail(thumbnail, definition != null ? definition.thumbnail : null, unlocked);
            }
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
            Debug.Log("[Cave][Mission] MissionRuntimeInstaller entered for " + scene.name + ".");
            if (scene.name == MissionRunContext.HubScene)
            {
                MissionRunContext.Current?.ClearRuntime(true);
                LevelTransition legacyExit = Object.FindObjectOfType<LevelTransition>();
                if (legacyExit != null) legacyExit.enabled = false;
                if (Object.FindObjectOfType<MissionTerminal>() != null)
                {
                    Debug.Log("[Cave][Mission] Using authored expedition terminal.");
                    return;
                }
                GameObject terminal = new GameObject("Mission Expedition Terminal"); terminal.transform.position = new Vector3(0f, -1.2f, 0f);
                BoxCollider2D trigger = terminal.AddComponent<BoxCollider2D>(); trigger.size = new Vector2(2.2f, 2.4f); trigger.isTrigger = true;
                terminal.AddComponent<MissionTerminal>();
                Debug.LogWarning("[Cave][Mission] No authored expedition terminal was found; created the runtime fallback.");
                return;
            }
            for (int i = 0; i < Maps.Length; i++)
            {
                if (scene.name != Maps[i]) continue;
                if (Object.FindObjectOfType<MissionMapBootstrap>() == null)
                {
                    new GameObject("Mission Map Bootstrap").AddComponent<MissionMapBootstrap>();
                    Debug.LogWarning("[Cave][Mission] No authored MissionMapBootstrap was found; created the runtime fallback.");
                }
                else Debug.Log("[Cave][Mission] Found authored MissionMapBootstrap.");
                return;
            }
        }
    }
}
