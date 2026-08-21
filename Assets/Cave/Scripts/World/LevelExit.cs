using Cave.InputSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.World
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        [SerializeField] private string completionMessage = "LEVEL 01 COMPLETE";

        private bool isComplete;
        private float previousTimeScale = 1f;
        private GameObject completionUi;

        public bool IsComplete => isComplete;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isComplete || other.GetComponentInParent<PlayerRespawn>() == null)
            {
                return;
            }

            isComplete = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameInput.SetGameplayInputEnabled(false);
            GetComponent<BoxCollider2D>().enabled = false;
            ShowCompletionUi();
            Debug.Log(completionMessage, this);
        }

        private void ShowCompletionUi()
        {
            completionUi = new GameObject("Level Complete UI", typeof(RectTransform));

            Canvas canvas = completionUi.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = completionUi.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            completionUi.AddComponent<GraphicRaycaster>();

            GameObject panelObject = new GameObject("Completion Panel", typeof(RectTransform));
            panelObject.transform.SetParent(completionUi.transform, false);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(520f, 150f);

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.035f, 0.04f, 0.06f, 0.96f);

            GameObject textObject = new GameObject("Message", typeof(RectTransform));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = completionMessage;
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnDestroy()
        {
            if (!isComplete)
            {
                return;
            }

            if (completionUi != null)
            {
                Destroy(completionUi);
            }

            Time.timeScale = previousTimeScale;
            GameInput.EnableGameplayAfterInputRelease();
        }
    }
}
