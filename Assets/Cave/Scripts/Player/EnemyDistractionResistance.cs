using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class EnemyDistractionResistance : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float remainingDuration;
        private float expiresAt;

        public bool IsResistant => Time.time < expiresAt;

        public void Apply(float duration)
        {
            expiresAt = Mathf.Max(expiresAt, Time.time + Mathf.Max(0f, duration));
        }

        private void Update()
        {
            remainingDuration = Mathf.Max(0f, expiresAt - Time.time);
        }

        private void OnDisable()
        {
            expiresAt = 0f;
            remainingDuration = 0f;
        }
    }
}
