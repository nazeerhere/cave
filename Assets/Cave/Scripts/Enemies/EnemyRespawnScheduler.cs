using System.Collections;
using UnityEngine;

namespace Cave.Enemies
{
    internal sealed class EnemyRespawnScheduler : MonoBehaviour
    {
        private static EnemyRespawnScheduler instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
        }

        internal static void Schedule(EnemyRespawner enemyRespawner)
        {
            EnsureInstance().StartCoroutine(RespawnAfterDelay(enemyRespawner));
        }

        private static EnemyRespawnScheduler EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject schedulerObject = new GameObject("[Cave] Enemy Respawn Scheduler");
            DontDestroyOnLoad(schedulerObject);
            instance = schedulerObject.AddComponent<EnemyRespawnScheduler>();
            return instance;
        }

        private static IEnumerator RespawnAfterDelay(EnemyRespawner enemyRespawner)
        {
            float spawnPressure = Cave.Player.PlayerCurseController.Active != null
                ? Cave.Player.PlayerCurseController.Active.AvariceSpawnPressureMultiplier
                : 1f;
            float adjustedDelay = enemyRespawner.RespawnDelay / Mathf.Max(1f, spawnPressure);
            if (adjustedDelay > 0f)
            {
                yield return new WaitForSeconds(adjustedDelay);
            }
            else
            {
                yield return null;
            }

            while (enemyRespawner != null
                && enemyRespawner.RespawnEnabled
                && enemyRespawner.IsPlayerInsideSafetyRadius())
            {
                if (enemyRespawner.AdditionalSafetyDelay > 0f)
                {
                    yield return new WaitForSeconds(enemyRespawner.AdditionalSafetyDelay);
                }
                else
                {
                    yield return null;
                }
            }

            if (enemyRespawner == null)
            {
                yield break;
            }

            if (!enemyRespawner.RespawnEnabled)
            {
                enemyRespawner.CancelPendingRespawn();
                yield break;
            }

            enemyRespawner.CompleteRespawn();
        }
    }
}
