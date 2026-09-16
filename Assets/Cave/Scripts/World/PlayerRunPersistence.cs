using Cave.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.World
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerRunPersistence : MonoBehaviour
    {
        private static PlayerRunPersistence instance;
        private static string pendingSceneName;
        private static string pendingSpawnIdentifier;
        private static Vector3? destinationPlayerFallback;
        private static bool transitionInFlight;

        private PlayerHealth playerHealth;

        public static PlayerHealth CurrentPlayerHealth => instance != null
            ? instance.playerHealth
            : null;
        public static Transform CurrentPlayerTransform => instance != null
            ? instance.transform
            : null;
        public bool IsAuthoritative => instance == this;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            pendingSceneName = null;
            pendingSpawnIdentifier = null;
            destinationPlayerFallback = null;
            transitionInFlight = false;
        }

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            if (instance != null && instance != this)
            {
                Debug.Log("[Cave][Mission] Destination Player Awake entered.", this);
                CaptureDestinationFallback();
                Debug.Log("[Cave][Mission] Suppressing destination authored Player in favor of the persistent Player.", this);
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[Cave][Mission] Persistent Player Awake entered.", this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static bool PrepareTransition(string sceneName, string spawnIdentifier)
        {
            if (instance == null || string.IsNullOrWhiteSpace(sceneName) || transitionInFlight)
            {
                return false;
            }

            transitionInFlight = true;
            pendingSceneName = sceneName;
            pendingSpawnIdentifier = spawnIdentifier;
            destinationPlayerFallback = null;
            return true;
        }

        public static void CancelPendingTransition()
        {
            pendingSceneName = null;
            pendingSpawnIdentifier = null;
            destinationPlayerFallback = null;
            transitionInFlight = false;
        }

        private void CaptureDestinationFallback()
        {
            if (!string.IsNullOrEmpty(pendingSceneName)
                && gameObject.scene.name == pendingSceneName
                && !destinationPlayerFallback.HasValue)
            {
                destinationPlayerFallback = transform.position;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != pendingSceneName)
            {
                return;
            }

            Debug.Log("[Cave][Mission] PlayerRunPersistence sceneLoaded callback entered for " + scene.name + ".", this);

            Vector3? destination = FindSpawnPoint(scene, pendingSpawnIdentifier);
            if (!destination.HasValue)
            {
                destination = destinationPlayerFallback;
            }

            if (destination.HasValue)
            {
                Debug.Log("[Cave][Mission] Spawn located for " + scene.name + ".", this);
                Rigidbody2D body = GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.velocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    body.position = destination.Value;
                }
                else
                {
                    transform.position = destination.Value;
                }

                GetComponent<PlayerRespawn>()?.SetSpawnPosition(destination.Value);
                Debug.Log("[Cave][Mission] Persistent Player positioned for " + scene.name
                    + " at " + destination.Value + ".", this);
            }
            else
            {
                Debug.LogError("[Cave][Mission] No authored or fallback destination exists for "
                    + scene.name + ".", this);
            }

            pendingSceneName = null;
            pendingSpawnIdentifier = null;
            destinationPlayerFallback = null;
            transitionInFlight = false;
        }

        private static Vector3? FindSpawnPoint(Scene scene, string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                LevelSpawnPoint[] spawnPoints = roots[rootIndex].GetComponentsInChildren<LevelSpawnPoint>(true);
                for (int spawnIndex = 0; spawnIndex < spawnPoints.Length; spawnIndex++)
                {
                    LevelSpawnPoint spawnPoint = spawnPoints[spawnIndex];
                    if (spawnPoint.SpawnIdentifier == identifier)
                    {
                        return spawnPoint.transform.position;
                    }
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }
    }
}
