using Cave.InputSystem;
using Cave.Player;
using UnityEngine;

namespace Cave.Missions
{
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
            body.sprite = Resources.Load<Sprite>("UI/HUD/Approved/HudCardRow");
            body.drawMode = body.sprite != null ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
            body.size = new Vector2(.9f, 1.5f);
            body.color = new Color(.04f, .2f, .32f, .95f);
            body.sortingOrder = 4;
            GameObject label = new GameObject("Mission Prompt");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = Vector3.up * 1.4f;
            prompt = label.AddComponent<TextMesh>();
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.alignment = TextAlignment.Center;
            prompt.fontSize = 46;
            prompt.characterSize = .04f;
            prompt.color = new Color(.3f, .85f, 1f, 1f);
            prompt.text = "[" + GameInput.Bindings.GetBinding(GameAction.Interact).Primary.ToString().ToUpperInvariant() + "] SELECT EXPEDITION";
            prompt.GetComponent<MeshRenderer>().sortingOrder = 30;
            prompt.gameObject.SetActive(false);
            Debug.Log("[Cave][Mission] Authored MissionTerminal Awake.", this);
        }

        private void Update()
        {
            if (player == null || hud != null || !GameInput.InteractPressed) return;
            GameInput.ConsumeMenuInputForCurrentFrame();
            GameInput.SetGameplayInputEnabled(false);
            hud = MissionSelectionHud.Create(this);
            prompt.gameObject.SetActive(false);
            Debug.Log("[Cave][Mission] Expedition selector opened from the authored terminal.", this);
        }

        public void Closed()
        {
            hud = null;
            GameInput.EnableGameplayAfterInputRelease();
            if (prompt != null && player != null) prompt.gameObject.SetActive(true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerHealth found = other.GetComponentInParent<PlayerHealth>();
            if (found == null) return;
            player = found;
            if (hud == null) prompt.gameObject.SetActive(true);
            Debug.Log("[Cave][Mission] Player entered the authored expedition terminal.", this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerHealth>() != player) return;
            player = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }
    }
}
