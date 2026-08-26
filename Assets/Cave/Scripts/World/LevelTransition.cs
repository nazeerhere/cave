using System.Collections;
using Cave.InputSystem;
using Cave.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelTransition : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Sprint5_Hazards";
        [SerializeField] private string destinationSpawnIdentifier = "MainEntrance";
        [SerializeField] private bool requireInteraction = true;
        [SerializeField] private bool showInteractionPrompt = true;
        [SerializeField] private Vector2 promptOffset = new Vector2(0f, 1.7f);

        private PlayerHealth playerInside;
        private TextMesh interactionPrompt;
        private bool isLoading;

        public void Configure(
            string sceneName,
            string spawnIdentifier,
            bool interactionRequired = true)
        {
            destinationScene = sceneName;
            destinationSpawnIdentifier = spawnIdentifier;
            requireInteraction = interactionRequired;
        }

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
            if (showInteractionPrompt)
            {
                CreatePrompt();
            }
        }

        private void Update()
        {
            if (isLoading || playerInside == null)
            {
                return;
            }

            if (!requireInteraction || GameInput.InteractPressed)
            {
                StartCoroutine(LoadDestination());
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null)
            {
                return;
            }

            playerInside = player;
            if (interactionPrompt != null)
            {
                interactionPrompt.gameObject.SetActive(requireInteraction);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || player != playerInside)
            {
                return;
            }

            playerInside = null;
            if (interactionPrompt != null)
            {
                interactionPrompt.gameObject.SetActive(false);
            }
        }

        private IEnumerator LoadDestination()
        {
            isLoading = true;
            if (interactionPrompt != null)
            {
                interactionPrompt.gameObject.SetActive(false);
            }

            if (!Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                Debug.LogError(
                    "Level transition destination is not in Build Settings: " + destinationScene,
                    this);
                isLoading = false;
                yield break;
            }

            if (!PlayerRunPersistence.PrepareTransition(
                    destinationScene,
                    destinationSpawnIdentifier))
            {
                Debug.LogError("No authoritative PlayerRunPersistence is available.", this);
                isLoading = false;
                yield break;
            }

            yield return SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        }

        private void CreatePrompt()
        {
            GameObject promptObject = new GameObject("Enter Cave Prompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptOffset;
            interactionPrompt = promptObject.AddComponent<TextMesh>();
            KeyBinding binding = GameInput.Bindings.GetBinding(GameAction.Interact);
            string bindingLabel = binding.Primary != KeyCode.None
                ? binding.Primary.ToString().ToUpperInvariant()
                : "INTERACT";
            interactionPrompt.text = bindingLabel + "  ENTER CAVE";
            interactionPrompt.anchor = TextAnchor.MiddleCenter;
            interactionPrompt.alignment = TextAlignment.Center;
            interactionPrompt.fontSize = 48;
            interactionPrompt.characterSize = 0.045f;
            interactionPrompt.color = new Color(0.95f, 0.8f, 0.45f, 1f);
            interactionPrompt.GetComponent<MeshRenderer>().sortingOrder = 20;
            interactionPrompt.gameObject.SetActive(false);
        }
    }
}
