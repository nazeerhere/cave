using System.Collections.Generic;
using System.Collections;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class CurseAltar : MonoBehaviour
    {
        public enum MaterializationState
        {
            Ritual,
            Materializing,
            Active
        }

        private static readonly HashSet<CurseAltar> ActiveAltars = new HashSet<CurseAltar>();
        private static Sprite approvedAltarSprite;

        private PlayerCurseAltarController owner;
        private bool permanent;
        private float expiresAt;
        private Material runeMaterial;
        private LineRenderer rune;
        private TextMesh prompt;
        private TextMesh curseReadout;
        private SpriteRenderer altarRenderer;
        private CurseAltarZone zone;
        [SerializeField] private MaterializationState materializationState;

        [SerializeField, Min(0.5f)] private float zoneRadius = 4.5f;
        [SerializeField, Min(0.05f)] private float ritualDuration = 0.38f;
        [SerializeField, Min(0.1f)] private float materializationDuration = 1.1f;

        public bool IsPermanent => permanent;
        public CurseAltarZone Zone => zone;
        public MaterializationState CurrentMaterializationState => materializationState;

        public static CurseAltar Create(
            PlayerCurseAltarController controller,
            Vector2 position,
            bool isPermanent,
            float lifetime)
        {
            GameObject altarObject = new GameObject(
                isPermanent ? "Permanent Curse Altar" : "Summoned Curse Altar");
            altarObject.transform.position = position;
            CurseAltar altar = altarObject.AddComponent<CurseAltar>();
            altar.Initialize(controller, isPermanent, lifetime);
            return altar;
        }

        public static CurseAltar FindNearest(Vector2 position, float maximumDistance)
        {
            CurseAltar nearest = null;
            float bestDistanceSquared = maximumDistance * maximumDistance;
            foreach (CurseAltar altar in ActiveAltars)
            {
                if (altar == null || !altar.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distanceSquared = ((Vector2)altar.transform.position - position).sqrMagnitude;
                if (distanceSquared <= bestDistanceSquared)
                {
                    nearest = altar;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        public void ConsumeAfterBargain()
        {
            if (!permanent)
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            ActiveAltars.Add(this);
        }

        private void Initialize(PlayerCurseAltarController controller, bool isPermanent, float lifetime)
        {
            owner = controller;
            permanent = isPermanent;
            expiresAt = isPermanent ? float.PositiveInfinity : Time.time + Mathf.Max(1f, lifetime);

            List<PlayerCurseType> snapshot = new List<PlayerCurseType>(3);
            if (owner != null && owner.Curses != null)
            {
                foreach (PlayerCurseType curse in System.Enum.GetValues(typeof(PlayerCurseType)))
                {
                    if (owner.Curses.IsActive(curse) && snapshot.Count < 3)
                    {
                        snapshot.Add(curse);
                    }
                }
            }

            BuildPresentation(snapshot);
            zone.Configure(owner, snapshot, zoneRadius);
            StartCoroutine(Materialize());
        }

        private void Update()
        {
            if (!permanent && Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            if (rune != null && materializationState != MaterializationState.Active)
            {
                float pulse = 0.78f + Mathf.Sin(Time.time * 2.4f) * 0.16f;
                Color color = new Color(0.55f, 0.2f, 1f, pulse);
                rune.startColor = color;
                rune.endColor = color;
            }

            if (prompt != null && Camera.main != null)
            {
                float distance = Vector2.Distance(transform.position, owner.transform.position);
                prompt.gameObject.SetActive(distance <= owner.InteractionRange);
                prompt.text = "[" + FormatInteractKey() + "] "
                    + (owner != null ? owner.GetContextualInteractLabel() : "BARGAIN");
            }
        }

        private void BuildPresentation(IReadOnlyList<PlayerCurseType> snapshot)
        {
            BoxCollider2D footprint = gameObject.AddComponent<BoxCollider2D>();
            footprint.size = new Vector2(0.82f, 1.18f);
            footprint.offset = new Vector2(0f, 0.59f);

            zone = gameObject.AddComponent<CurseAltarZone>();

            GameObject visualObject = new GameObject("Approved Curse Altar Visual");
            visualObject.transform.SetParent(transform, false);
            altarRenderer = visualObject.AddComponent<SpriteRenderer>();
            altarRenderer.sprite = LoadApprovedAltarSprite();
            altarRenderer.sortingOrder = 10;
            altarRenderer.color = new Color(1f, 1f, 1f, 0.04f);

            runeMaterial = new Material(Shader.Find("Sprites/Default"));
            rune = gameObject.AddComponent<LineRenderer>();
            rune.material = runeMaterial;
            rune.useWorldSpace = false;
            rune.loop = true;
            rune.positionCount = 4;
            rune.startWidth = 0.09f;
            rune.endWidth = 0.09f;
            rune.sortingOrder = 12;
            rune.SetPosition(0, new Vector3(0f, 1.4f));
            rune.SetPosition(1, new Vector3(0.75f, 0.55f));
            rune.SetPosition(2, new Vector3(0f, 0f));
            rune.SetPosition(3, new Vector3(-0.75f, 0.55f));

            GameObject readoutObject = new GameObject("Powered Curses");
            readoutObject.transform.SetParent(transform, false);
            readoutObject.transform.localPosition = new Vector3(0f, 2.58f, 0f);
            readoutObject.transform.localScale = Vector3.one * 0.055f;
            curseReadout = readoutObject.AddComponent<TextMesh>();
            curseReadout.anchor = TextAnchor.MiddleCenter;
            curseReadout.alignment = TextAlignment.Center;
            curseReadout.fontSize = 24;
            curseReadout.characterSize = 0.12f;
            curseReadout.color = ResolveCurseColor(snapshot);
            curseReadout.text = FormatPoweredCurses(snapshot);
            curseReadout.GetComponent<MeshRenderer>().sortingOrder = 12;

            GameObject promptObject = new GameObject("Curse Altar Prompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = new Vector3(0f, 2.95f, 0f);
            promptObject.transform.localScale = Vector3.one * 0.08f;
            prompt = promptObject.AddComponent<TextMesh>();
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.alignment = TextAlignment.Center;
            prompt.fontSize = 28;
            prompt.characterSize = 0.12f;
            prompt.color = new Color(1f, 0.82f, 0.42f, 1f);
            prompt.GetComponent<MeshRenderer>().sortingOrder = 13;
        }

        private IEnumerator Materialize()
        {
            materializationState = MaterializationState.Ritual;
            yield return new WaitForSeconds(ritualDuration);

            materializationState = MaterializationState.Materializing;
            float startedAt = Time.time;
            while (Time.time < startedAt + materializationDuration)
            {
                float progress = Mathf.Clamp01((Time.time - startedAt) / materializationDuration);
                if (altarRenderer != null)
                {
                    altarRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.04f, 1f, progress));
                }

                if (rune != null)
                {
                    float width = Mathf.Lerp(0.06f, 0.13f, 1f - progress);
                    rune.startWidth = width;
                    rune.endWidth = width;
                }

                yield return null;
            }

            if (altarRenderer != null)
            {
                altarRenderer.color = Color.white;
            }

            materializationState = MaterializationState.Active;
            zone.SetZoneActive(true);
            StartCoroutine(PlayTerritoryPulse());
        }

        private IEnumerator PlayTerritoryPulse()
        {
            if (rune == null)
            {
                yield break;
            }

            float startedAt = Time.time;
            const float pulseDuration = 0.32f;
            while (Time.time < startedAt + pulseDuration)
            {
                float progress = Mathf.Clamp01((Time.time - startedAt) / pulseDuration);
                float scale = Mathf.Lerp(0.9f, zoneRadius, progress);
                rune.transform.localScale = Vector3.one * scale;
                Color color = Color.Lerp(new Color(1f, 0.85f, 0.25f, 1f), new Color(0.75f, 0.05f, 0.12f, 0f), progress);
                rune.startColor = color;
                rune.endColor = color;
                yield return null;
            }

            rune.transform.localScale = Vector3.one;
            rune.startWidth = 0.09f;
            rune.endWidth = 0.09f;
            rune.enabled = false;
        }

        private static Sprite LoadApprovedAltarSprite()
        {
            if (approvedAltarSprite == null)
            {
                approvedAltarSprite = Resources.Load<Sprite>("Player/CurseAltar/CurseAltarActive");
            }

            return approvedAltarSprite;
        }

        private static string FormatPoweredCurses(IReadOnlyList<PlayerCurseType> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                return "UNPOWERED ALTAR";
            }

            string[] names = new string[snapshot.Count];
            for (int index = 0; index < snapshot.Count; index++)
            {
                names[index] = snapshot[index].ToString().ToUpperInvariant();
            }

            return string.Join("  •  ", names);
        }

        private static Color ResolveCurseColor(IReadOnlyList<PlayerCurseType> snapshot)
        {
            if (snapshot != null && snapshot.Count > 0 && snapshot[0] == PlayerCurseType.CavesGlare)
            {
                return new Color(1f, 0.25f, 0.25f, 1f);
            }

            return new Color(0.8f, 0.35f, 1f, 1f);
        }

        private static string FormatInteractKey()
        {
            KeyCode key = GameInput.Bindings.GetBinding(GameAction.Interact).Primary;
            return key == KeyCode.None ? "UNBOUND" : key.ToString().ToUpperInvariant();
        }

        private void OnDisable()
        {
            ActiveAltars.Remove(this);
            owner?.NotifyAltarRemoved(this);
        }

        private void OnDestroy()
        {
            ActiveAltars.Remove(this);
            if (runeMaterial != null)
            {
                Destroy(runeMaterial);
            }
        }
    }
}
