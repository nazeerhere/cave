using UnityEngine;

namespace Cave.Missions
{
    public sealed class CorruptionSourceSocket : MonoBehaviour
    {
        [SerializeField] private CorruptionSource sourcePrefab;

        public CorruptionSource Spawn()
        {
            CorruptionSource source = sourcePrefab != null
                ? Instantiate(sourcePrefab, transform.position, Quaternion.identity, transform)
                : new GameObject("Corruption Source").AddComponent<CorruptionSource>();
            if (sourcePrefab == null) source.transform.position = transform.position;
            return source;
        }
    }
}
