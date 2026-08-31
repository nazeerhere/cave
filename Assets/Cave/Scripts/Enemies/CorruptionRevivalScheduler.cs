using System.Collections;
using UnityEngine;

namespace Cave.Enemies
{
    internal sealed class CorruptionRevivalScheduler : MonoBehaviour
    {
        private static CorruptionRevivalScheduler instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
        }

        internal static void Schedule(EnemyCorruptionLifecycle target, float delay)
        {
            if (instance == null)
            {
                GameObject runner = new GameObject("[Cave] Corruption Revival Scheduler");
                DontDestroyOnLoad(runner);
                instance = runner.AddComponent<CorruptionRevivalScheduler>();
            }

            instance.StartCoroutine(Revive(target, delay));
        }

        private static IEnumerator Revive(EnemyCorruptionLifecycle target, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                yield return null;
            }

            target?.CompleteRevival();
        }
    }
}
