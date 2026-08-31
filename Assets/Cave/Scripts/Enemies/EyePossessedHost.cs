using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EyePossessedHost : MonoBehaviour
    {
        private Damageable host;
        private EnemyController movement;
        private PlayerHealth player;
        private PlayerAimDirection playerAim;
        private GameObject marker;
        private LineRenderer gazeLine;
        private float watcherRadius;
        private float memoryDuration;
        private float frenzyEndsAt;
        private float gazeEndsAt;
        private float gazeReadyAt;
        private bool initialized;

        public bool IsPossessed => initialized;

        public void Initialize(float healthBonusFraction, float radius, float memory, GameObject markerPrefab, Vector2 markerOffset)
        {
            if (initialized)
            {
                return;
            }

            host = GetComponent<Damageable>();
            if (host == null)
            {
                return;
            }
            movement = GetComponent<EnemyController>();
            watcherRadius = radius;
            memoryDuration = memory;
            int bonus = Mathf.Max(1, Mathf.CeilToInt(host.MaximumHealth * healthBonusFraction));
            host.SetRuntimeMaximumHealth(host.MaximumHealth + bonus, false);
            host.RestoreHealth(bonus);
            if (markerPrefab != null)
            {
                marker = Instantiate(markerPrefab, transform.position, Quaternion.identity);
                marker.transform.SetParent(transform, true);
                marker.transform.localPosition = markerOffset;
            }

            host.Died += Clear;
            CreateGazeLine();
            initialized = true;
            EnemyWorldStatusIndicators.EnsureOn(gameObject);
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (player == null)
            {
                player = FindObjectOfType<PlayerHealth>();
                playerAim = player != null ? player.GetComponent<PlayerAimDirection>() : null;
            }

            if (player != null && HasLineOfSight(player.transform.position))
            {
                EyeWatcherNetwork.Broadcast(this, player.transform.position, watcherRadius, memoryDuration);
                UpdateGaze();
            }

            if (movement != null && Time.time >= frenzyEndsAt)
            {
                movement.SetPossessionSpeedMultiplier(1f);
            }
        }

        private bool HasLineOfSight(Vector2 destination)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(transform.position, destination);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null
                    && hit.collider.GetComponentInParent<PlayerHealth>() == null
                    && hit.collider.GetComponentInParent<EnemyController>() == null
                    && !hit.collider.isTrigger)
                {
                    return false;
                }
            }

            return true;
        }

        private void Clear()
        {
            if (!initialized)
            {
                return;
            }

            initialized = false;
            EyeWatcherNetwork.Remove(this);
            movement?.SetPossessionSpeedMultiplier(1f);
            if (marker != null)
            {
                Destroy(marker);
                marker = null;
            }

            if (gazeLine != null)
            {
                Destroy(gazeLine.gameObject);
                gazeLine = null;
            }
        }

        private void UpdateGaze()
        {
            if (Time.time >= gazeReadyAt && Time.time >= gazeEndsAt)
            {
                gazeEndsAt = Time.time + 1.1f;
                gazeReadyAt = Time.time + 3f;
            }

            bool locking = Time.time < gazeEndsAt;
            if (gazeLine != null)
            {
                gazeLine.enabled = locking;
                if (locking)
                {
                    gazeLine.SetPosition(0, transform.position);
                    gazeLine.SetPosition(1, player.transform.position);
                }
            }

            if (locking && playerAim != null)
            {
                Vector2 towardHost = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;
                if (Vector2.Dot(playerAim.FacingDirection, towardHost) <= -0.2f)
                {
                    frenzyEndsAt = Time.time + 3.5f;
                    movement?.SetPossessionSpeedMultiplier(1.2f);
                    gazeEndsAt = Time.time;
                }
            }
        }

        private void CreateGazeLine()
        {
            GameObject line = new GameObject("Possessed Host Gaze");
            line.transform.SetParent(transform, false);
            gazeLine = line.AddComponent<LineRenderer>();
            gazeLine.material = new Material(Shader.Find("Sprites/Default"));
            gazeLine.positionCount = 2;
            gazeLine.startWidth = 0.03f;
            gazeLine.endWidth = 0.03f;
            gazeLine.startColor = new Color(0.72f, 0.22f, 1f, 0.8f);
            gazeLine.endColor = gazeLine.startColor;
            gazeLine.sortingOrder = 5;
            gazeLine.enabled = false;
        }

        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
    }
}
