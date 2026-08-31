using System.Collections;
using Cave.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Audio
{
    /// <summary>One persistent, fade-safe music source. Scene zones may request a state.</summary>
    [DisallowMultipleComponent]
    public sealed class CaveMusicController : MonoBehaviour
    {
        private const string LibraryResourceName = "CaveMusicLibrary";

        [SerializeField] private CaveMusicLibrary musicLibrary;
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.75f;
        [SerializeField, Min(0.1f)] private float combatCheckInterval = 0.5f;
        [SerializeField, Min(0.1f)] private float combatRange = 9f;
        [SerializeField, Min(1)] private int intenseHostileCount = 4;
        [SerializeField, Min(0f)] private float combatExitGracePeriod = 3f;
        [SerializeField] private CaveMusicState currentState;

        private static CaveMusicController instance;
        private AudioSource source;
        private Coroutine transition;
        private float nextCombatCheck;
        private float lastMeaningfulCombatTime = float.NegativeInfinity;
        private CaveMusicState? forcedState;

        public static CaveMusicController Instance => instance;
        public CaveMusicState CurrentState => currentState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject musicObject = new GameObject("[Cave] Music");
            DontDestroyOnLoad(musicObject);
            instance = musicObject.AddComponent<CaveMusicController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 64;
            musicLibrary = musicLibrary != null
                ? musicLibrary
                : Resources.Load<CaveMusicLibrary>(LibraryResourceName);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            RequestState(ResolveSceneDefault());
        }

        private void Update()
        {
            if (forcedState.HasValue || Time.time < nextCombatCheck)
            {
                return;
            }

            nextCombatCheck = Time.time + combatCheckInterval;
            Cave.Player.PlayerHealth player = FindObjectOfType<Cave.Player.PlayerHealth>();
            if (player == null || player.CurrentHealth <= 0)
            {
                return;
            }

            int engagedHostiles = HostileMobQuery.CountEngagedHostiles(
                player.transform.position,
                combatRange);
            if (engagedHostiles > 0)
            {
                lastMeaningfulCombatTime = Time.time;
                RequestState(engagedHostiles >= intenseHostileCount
                    ? CaveMusicState.IntenseCombat
                    : CaveMusicState.Combat);
                return;
            }

            if (Time.time - lastMeaningfulCombatTime >= combatExitGracePeriod)
            {
                RequestState(ResolveSceneDefault());
            }
        }

        public void RequestState(CaveMusicState requestedState)
        {
            if (currentState == requestedState && source != null && source.isPlaying)
            {
                return;
            }

            AudioClip nextClip = musicLibrary != null ? musicLibrary.GetClip(requestedState) : null;
            if (nextClip == null)
            {
                currentState = requestedState;
                return;
            }

            currentState = requestedState;
            if (transition != null)
            {
                StopCoroutine(transition);
            }

            transition = StartCoroutine(CrossFade(nextClip));
        }

        public void SetForcedState(CaveMusicState? requestedState)
        {
            forcedState = requestedState;
            RequestState(requestedState ?? ResolveSceneDefault());
        }

        private IEnumerator CrossFade(AudioClip nextClip)
        {
            float initialVolume = source.volume;
            float duration = Mathf.Max(0.05f, fadeDuration);
            for (float elapsed = 0f; elapsed < duration * 0.5f; elapsed += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(initialVolume, 0f, elapsed / (duration * 0.5f));
                yield return null;
            }

            source.clip = nextClip;
            source.Play();
            for (float elapsed = 0f; elapsed < duration * 0.5f; elapsed += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(0f, CaveAudioSettings.MusicVolume, elapsed / (duration * 0.5f));
                yield return null;
            }

            source.volume = CaveAudioSettings.MusicVolume;
            transition = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!forcedState.HasValue)
            {
                RequestState(ResolveSceneDefault());
            }
        }

        private static CaveMusicState ResolveSceneDefault()
        {
            string sceneName = SceneManager.GetActiveScene().name.ToLowerInvariant();
            if (sceneName.Contains("heart") || sceneName.Contains("final"))
            {
                return CaveMusicState.Heart;
            }

            return sceneName.Contains("start") || sceneName.Contains("breach")
                ? CaveMusicState.Starting
                : CaveMusicState.Exploration;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
