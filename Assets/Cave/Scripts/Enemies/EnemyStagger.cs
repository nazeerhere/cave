using System;
using UnityEngine;

namespace Cave.Enemies
{
    public interface IEnemyInterruptible
    {
        void Interrupt();
    }

    [DisallowMultipleComponent]
    public sealed class EnemyStagger : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.9f)] private float staggerResistance;
        [SerializeField, Min(0f)] private float staggerImmunityDuration = 0.5f;
        [SerializeField] private bool interruptWindups = true;
        [SerializeField] private Color staggerFeedbackColor = new Color(1f, 0.82f, 0.25f, 0.9f);

        private EnemyController enemyController;
        private FlyingSwarmController flyingController;
        private float staggeredUntil;
        private float nextAllowedStaggerTime;

        public event Action<float> Staggered;
        public bool IsStaggered => Time.time < staggeredUntil;
        public bool CanAct => !IsStaggered;

        private void Awake()
        {
            enemyController = GetComponent<EnemyController>();
            flyingController = GetComponent<FlyingSwarmController>();
        }

        public bool TryStagger(float baseDuration)
        {
            if (baseDuration <= 0f || Time.time < nextAllowedStaggerTime)
            {
                return false;
            }

            float duration = baseDuration * (1f - Mathf.Clamp01(staggerResistance));
            if (duration <= 0.01f)
            {
                return false;
            }

            staggeredUntil = Mathf.Max(staggeredUntil, Time.time + duration);
            nextAllowedStaggerTime = staggeredUntil + staggerImmunityDuration;
            enemyController?.SuspendMovement(duration);
            flyingController?.SuspendMovement(duration);
            if (interruptWindups)
            {
                foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
                {
                    if (behaviour != this && behaviour is IEnemyInterruptible interruptible)
                    {
                        interruptible.Interrupt();
                    }
                }
            }

            Cave.Combat.AreaPulseEffect.Create(
                transform.position,
                0.55f,
                staggerFeedbackColor,
                0.18f);
            Staggered?.Invoke(duration);
            return true;
        }

        public void SetStaggerResistance(float resistance)
        {
            staggerResistance = Mathf.Clamp(resistance, 0f, 0.9f);
        }

        private void OnDisable()
        {
            staggeredUntil = 0f;
            nextAllowedStaggerTime = 0f;
        }
    }
}
