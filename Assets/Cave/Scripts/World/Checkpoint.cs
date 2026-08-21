using UnityEngine;

namespace Cave.World
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private SpriteRenderer indicatorRenderer;
        [SerializeField] private Color inactiveColor = new Color(0.35f, 0.45f, 0.5f, 0.8f);
        [SerializeField] private Color activeColor = new Color(0.25f, 1f, 0.55f, 1f);

        public bool IsActive { get; private set; }
        public Vector2 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            SetActiveState(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerRespawn playerRespawn = other.GetComponentInParent<PlayerRespawn>();
            if (playerRespawn == null || IsActive)
            {
                return;
            }

            foreach (Checkpoint checkpoint in FindObjectsOfType<Checkpoint>(true))
            {
                checkpoint.SetActiveState(checkpoint == this);
            }

            playerRespawn.SetSpawnPosition(SpawnPosition);
            Debug.Log(name + " activated. Respawn position: " + SpawnPosition, this);
        }

        private void SetActiveState(bool active)
        {
            IsActive = active;
            if (indicatorRenderer != null)
            {
                indicatorRenderer.color = active ? activeColor : inactiveColor;
            }
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }
    }
}
