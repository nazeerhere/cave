using System.Collections;
using UnityEngine;

namespace Cave.Hazards
{
    public sealed class BombSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BombProjectile bombPrefab;
        [SerializeField] private HazardWarning warningPrefab;
        [SerializeField] private Transform hazardContainer;
        [SerializeField] private Transform player;
        [SerializeField] private LayerMask environmentLayers;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float minimumSpawnInterval = 2.5f;
        [SerializeField, Min(0.1f)] private float maximumSpawnInterval = 4.5f;
        [SerializeField, Min(0f)] private float initialDelay = 1.5f;
        [SerializeField, Min(0.1f)] private float warningLeadTime = 1f;
        [SerializeField, Min(1)] private int maximumActiveBombs = 3;

        [Header("Spawn Area")]
        [SerializeField] private float minimumX = -10f;
        [SerializeField] private float maximumX = 10f;
        [SerializeField] private float spawnHeight = 6f;
        [SerializeField] private float fallbackGroundY = -1.45f;
        [SerializeField, Range(0f, 1f)] private float playerBiasChance = 0.45f;
        [SerializeField, Min(0f)] private float playerOffsetRange = 3f;
        [SerializeField, Min(0f)] private float minimumSpawnSeparation = 1.5f;

        private int activeBombSequences;
        private float lastSpawnX = float.PositiveInfinity;
        private float nextSpawnTime;

        public int ActiveBombSequences => activeBombSequences;

        private void OnEnable()
        {
            nextSpawnTime = Time.time + initialDelay;
        }

        private void Update()
        {
            if (Time.time < nextSpawnTime || activeBombSequences >= maximumActiveBombs)
            {
                return;
            }

            float spawnX = ChooseSpawnX();
            StartCoroutine(SpawnBombSequence(spawnX));
            nextSpawnTime = Time.time + Random.Range(minimumSpawnInterval, maximumSpawnInterval);
        }

        private IEnumerator SpawnBombSequence(float spawnX)
        {
            activeBombSequences++;
            lastSpawnX = spawnX;

            Vector2 warningPosition = FindWarningPosition(spawnX);
            HazardWarning warning = Instantiate(
                warningPrefab,
                warningPosition,
                Quaternion.identity,
                hazardContainer);

            yield return new WaitForSeconds(warningLeadTime);

            BombProjectile bomb = Instantiate(
                bombPrefab,
                new Vector3(spawnX, spawnHeight, 0f),
                Quaternion.identity,
                hazardContainer);

            bomb.Initialize(warning, HandleBombSequenceComplete);
        }

        private float ChooseSpawnX()
        {
            float candidate = Random.Range(minimumX, maximumX);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (player != null && Random.value < playerBiasChance)
                {
                    candidate = Mathf.Clamp(
                        player.position.x + Random.Range(-playerOffsetRange, playerOffsetRange),
                        minimumX,
                        maximumX);
                }
                else
                {
                    candidate = Random.Range(minimumX, maximumX);
                }

                if (float.IsPositiveInfinity(lastSpawnX)
                    || Mathf.Abs(candidate - lastSpawnX) >= minimumSpawnSeparation)
                {
                    return candidate;
                }
            }

            float distanceToMinimum = Mathf.Abs(lastSpawnX - minimumX);
            float distanceToMaximum = Mathf.Abs(lastSpawnX - maximumX);
            return distanceToMinimum > distanceToMaximum ? minimumX : maximumX;
        }

        private Vector2 FindWarningPosition(float spawnX)
        {
            Vector2 origin = new Vector2(spawnX, spawnHeight);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 30f, environmentLayers);
            float warningY = hit.collider != null ? hit.point.y + 0.03f : fallbackGroundY;
            return new Vector2(spawnX, warningY);
        }

        private void HandleBombSequenceComplete()
        {
            activeBombSequences = Mathf.Max(0, activeBombSequences - 1);
        }
    }
}
