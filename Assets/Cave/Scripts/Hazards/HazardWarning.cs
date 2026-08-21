using UnityEngine;

namespace Cave.Hazards
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HazardWarning : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float warningSize = 1.4f;
        [SerializeField, Min(0f)] private float pulseAmount = 0.2f;
        [SerializeField, Min(0f)] private float pulseFrequency = 3f;

        private void Awake()
        {
            GetComponent<SpriteRenderer>().size = new Vector2(warningSize, warningSize * 0.25f);
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmount;
            transform.localScale = new Vector3(pulse, pulse, 1f);
        }
    }
}
