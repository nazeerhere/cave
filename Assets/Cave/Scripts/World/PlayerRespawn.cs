using UnityEngine;

namespace Cave.World
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        public event System.Action Respawned;

        private Rigidbody2D body;
        private Vector2 spawnPosition;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spawnPosition = transform.position;
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
