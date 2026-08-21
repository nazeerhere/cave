using UnityEngine;

namespace Cave.World
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class DeathBoundary : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerRespawn playerRespawn = other.GetComponentInParent<PlayerRespawn>();
            if (playerRespawn != null)
            {
                playerRespawn.Respawn();
            }
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }
    }
}
