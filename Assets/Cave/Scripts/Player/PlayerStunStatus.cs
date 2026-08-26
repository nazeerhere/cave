using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerStunStatus : MonoBehaviour
    {
        [SerializeField] private Color stunColor = new Color(1f, 0.84f, 0.2f, 0.9f);
        [SerializeField, Min(0.1f)] private float feedbackRadius = 0.52f;
        [SerializeField, Min(0f)] private float currentStunRemaining;

        private PlayerController controller;
        private PlayerGuardBreak actionGate;
        private float stunnedUntil;

        public bool IsStunned => Time.time < stunnedUntil;
        public float CurrentStunRemaining => Mathf.Max(0f, stunnedUntil - Time.time);

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            actionGate = GetComponent<PlayerGuardBreak>();
        }

        private void Update()
        {
            currentStunRemaining = CurrentStunRemaining;
        }

        public void ApplyStun(float duration)
        {
            float resolvedDuration = Mathf.Max(0f, duration);
            if (resolvedDuration <= 0f)
            {
                return;
            }

            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + resolvedDuration);
            controller?.ApplyExternalControlLock(CurrentStunRemaining);
            actionGate?.ApplyExternalInterruption(CurrentStunRemaining);
            CombatShapeEffect.Create(
                transform.position + Vector3.up * 0.35f,
                CombatShape.Diamond,
                feedbackRadius,
                stunColor,
                Mathf.Min(CurrentStunRemaining, 0.35f));
        }

        public void ClearStun()
        {
            stunnedUntil = 0f;
            currentStunRemaining = 0f;
        }

        private void OnDisable()
        {
            ClearStun();
        }
    }
}
