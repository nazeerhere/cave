using UnityEngine;

namespace Cave.World
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        public event System.Action Respawned;

        private Rigidbody2D body;
        private Vector2 spawnPosition;
        private bool runtimeInitialized;

        private void Awake()
        {
            InitializeRuntime();
        }

        /// <summary>
        /// Resolves the runtime references used by respawn without requiring a
        /// caller to invoke Unity lifecycle messages manually. Runtime Awake
        /// remains the normal initialization path.
        /// </summary>
        public void InitializeRuntime()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (!runtimeInitialized)
            {
                spawnPosition = transform.position;
                runtimeInitialized = true;
            }
        }

        public void Respawn()
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = spawnPosition;
            Respawned?.Invoke();
        }

        public void SetSpawnPosition(Vector2 newSpawnPosition)
        {
            spawnPosition = newSpawnPosition;
        }
    }
}
