using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyRespawner : MonoBehaviour
    {
        [Header("Respawn")]
        [SerializeField] private bool respawnEnabled = true;
        [SerializeField, Min(0f)] private float respawnDelay = 5f;

        [Header("Player Safety")]
        [SerializeField, Min(0f)] private float playerSafetyRadius = 1.5f;
        [SerializeField, Min(0f)] private float additionalSafetyDelay = 1f;

        private Damageable damageable;
        private Rigidbody2D body;
        private EnemyController enemyController;
        private FlyingSwarmController flyingController;
        private WizardFlightMotor wizardFlightMotor;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Collider2D[] colliders;
        private bool[] colliderEnabledStates;
        private Renderer[] renderers;
        private bool[] rendererEnabledStates;
        private Animator[] animators;
        private bool respawnPending;
        private float runtimeRespawnDelay;
        private EnemyDifficultyScaler difficultyScaler;

        internal float RespawnDelay => runtimeRespawnDelay;
        public float BaseRespawnDelay => respawnDelay;
        internal float AdditionalSafetyDelay => additionalSafetyDelay;
        internal bool RespawnEnabled => respawnEnabled;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            body = GetComponent<Rigidbody2D>();
            enemyController = GetComponent<EnemyController>();
            flyingController = GetComponent<FlyingSwarmController>();
            wizardFlightMotor = GetComponent<WizardFlightMotor>();
            difficultyScaler = GetComponent<EnemyDifficultyScaler>();
            runtimeRespawnDelay = respawnDelay;

            originalParent = transform.parent;
            originalPosition = transform.position;
            originalRotation = transform.rotation;

            colliders = GetComponentsInChildren<Collider2D>(true);
            colliderEnabledStates = new bool[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                colliderEnabledStates[index] = colliders[index].enabled;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            rendererEnabledStates = new bool[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                rendererEnabledStates[index] = renderers[index].enabled;
            }

            animators = GetComponentsInChildren<Animator>(true);
        }

        private void OnEnable()
        {
            damageable.Died -= HandleDeath;
            damageable.Died += HandleDeath;
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.Died -= HandleDeath;
            }
        }

        private void HandleDeath()
        {
            EnemyCorruptionLifecycle corruption = GetComponent<EnemyCorruptionLifecycle>();
            if ((corruption != null && corruption.SuppressStandardRespawn)
                || !respawnEnabled
                || respawnPending)
            {
                return;
            }

            respawnPending = true;
            EnemyRespawnScheduler.Schedule(this);
        }

        internal bool IsPlayerInsideSafetyRadius()
        {
            if (playerSafetyRadius <= 0f)
            {
                return false;
            }

            float safetyRadiusSquared = playerSafetyRadius * playerSafetyRadius;
            foreach (PlayerHealth playerHealth in FindObjectsOfType<PlayerHealth>())
            {
                if ((playerHealth.transform.position - originalPosition).sqrMagnitude < safetyRadiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        internal void CompleteRespawn()
        {
            if (!respawnPending || !respawnEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                respawnPending = false;
                return;
            }

            if (transform.parent != originalParent)
            {
                transform.SetParent(originalParent, true);
            }

            transform.SetPositionAndRotation(originalPosition, originalRotation);

            if (body != null)
            {
                body.position = originalPosition;
                body.rotation = originalRotation.eulerAngles.z;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            RestoreEnabledStates();
            if (difficultyScaler == null)
            {
                difficultyScaler = GetComponent<EnemyDifficultyScaler>();
            }

            difficultyScaler?.ApplyForSpawn();
            damageable.RestoreToFullHealth();
            enemyController?.ResetForRespawn();
            flyingController?.ResetForRespawn();
            wizardFlightMotor?.ResetForRespawn();

            respawnPending = false;
            gameObject.SetActive(true);

            foreach (Animator animator in animators)
            {
                if (animator != null && animator.enabled)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
            }
        }

        internal void CancelPendingRespawn()
        {
            respawnPending = false;
        }

        public void SetRuntimeRespawnDelay(float delay)
        {
            runtimeRespawnDelay = Mathf.Max(0f, delay);
        }

        private void RestoreEnabledStates()
        {
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = colliderEnabledStates[index];
                }
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = rendererEnabledStates[index];
                }
            }
        }
    }
}
