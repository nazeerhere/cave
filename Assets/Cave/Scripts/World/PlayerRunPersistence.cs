using Cave.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerRunPersistence : MonoBehaviour
    {
        private static PlayerRunPersistence instance;
        private static string pendingSceneName;
        private static string pendingSpawnIdentifier;
        private static Vector3? destinationPlayerFallback;

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
        }

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            if (instance != null && instance != this)
            {
                CaptureDestinationFallback();
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static bool PrepareTransition(string sceneName, string spawnIdentifier)
        {
            if (instance == null || string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            pendingSceneName = sceneName;
            pendingSpawnIdentifier = spawnIdentifier;
            destinationPlayerFallback = null;
            return true;
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

            Vector3? destination = FindSpawnPoint(scene, pendingSpawnIdentifier);
            foreach (PlayerHealth candidate in FindObjectsOfType<PlayerHealth>(true))
            {
                if (candidate == null
                    || candidate == playerHealth
                    || candidate.gameObject.scene != scene)
                {
                    continue;
                }

                if (!destinationPlayerFallback.HasValue)
                {
                    destinationPlayerFallback = candidate.transform.position;
                }

                candidate.gameObject.SetActive(false);
                Destroy(candidate.gameObject);
            }

            if (!destination.HasValue)
            {
                destination = destinationPlayerFallback;
            }

            if (destination.HasValue)
            {
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
            }

            pendingSceneName = null;
            pendingSpawnIdentifier = null;
            destinationPlayerFallback = null;
        }

        private static Vector3? FindSpawnPoint(Scene scene, string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            foreach (LevelSpawnPoint spawnPoint in FindObjectsOfType<LevelSpawnPoint>(true))
            {
                if (spawnPoint.gameObject.scene == scene
                    && spawnPoint.SpawnIdentifier == identifier)
                {
                    return spawnPoint.transform.position;
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
