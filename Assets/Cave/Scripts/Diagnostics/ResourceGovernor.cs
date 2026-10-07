using System;
using UnityEngine;

namespace Cave.Diagnostics
{
    public enum ResourceGovernorMode { Normal = 0, Degraded = 1, Critical = 2 }
    public enum ResourceGovernorControl { Auto = 0, ForceNormal = 1, ForceDegraded = 2, ForceCritical = 3 }
    public enum RuntimeQosPriority { P0CriticalReadability = 0, P1StrongFeedback = 1, P2Presentation = 2, P3Cosmetic = 3 }
    public enum RuntimeQosLevel { Full = 0, Reduced = 1, Minimal = 2, Denied = 3 }

    public readonly struct RuntimeQosRequest
    {
        public RuntimeQosRequest(RuntimeWorkCategory category, RuntimeQosPriority priority, float estimatedCost, float duration, bool requiredGameplay = false)
        { Category = category; Priority = priority; EstimatedCost = Mathf.Max(0f, estimatedCost); Duration = Mathf.Max(0f, duration); RequiredGameplay = requiredGameplay; }
        public RuntimeWorkCategory Category { get; } public RuntimeQosPriority Priority { get; }
        public float EstimatedCost { get; } public float Duration { get; } public bool RequiredGameplay { get; }
    }

    public readonly struct ResourceGovernorSnapshot
    {
        public ResourceGovernorSnapshot(ResourceGovernorMode mode, float frameMilliseconds, float framesPerSecond, bool forced)
        { Mode = mode; FrameMilliseconds = frameMilliseconds; FramesPerSecond = framesPerSecond; IsForced = forced; }
        public ResourceGovernorMode Mode { get; } public float FrameMilliseconds { get; } public float FramesPerSecond { get; } public bool IsForced { get; }
    }

    /// <summary>Pure hysteretic policy. It cannot alter resolver or combat inputs.</summary>
    public sealed class ResourceGovernorPolicy
    {
        public float DegradedEntryMilliseconds { get; set; } = 22f;
        public float CriticalEntryMilliseconds { get; set; } = 33f;
        public float DegradedRecoveryMilliseconds { get; set; } = 18f;
        public float CriticalRecoveryMilliseconds { get; set; } = 27f;
        public float SustainedPressureSeconds { get; set; } = .75f;
        public float SustainedRecoverySeconds { get; set; } = 1.5f;
        public ResourceGovernorMode Mode { get; private set; } = ResourceGovernorMode.Normal;
        private float pressureSince = -1f;
        private float recoverySince = -1f;

        public ResourceGovernorMode Evaluate(float now, float frameMilliseconds)
        {
            bool pressure = Mode == ResourceGovernorMode.Normal
                ? frameMilliseconds >= DegradedEntryMilliseconds
                : Mode == ResourceGovernorMode.Degraded
                    ? frameMilliseconds >= CriticalEntryMilliseconds
                    : false;
            bool recovery = Mode == ResourceGovernorMode.Critical
                ? frameMilliseconds <= CriticalRecoveryMilliseconds
                : Mode == ResourceGovernorMode.Degraded && frameMilliseconds <= DegradedRecoveryMilliseconds;
            if (pressure)
            {
                pressureSince = pressureSince < 0f ? now : pressureSince; recoverySince = -1f;
                if (now - pressureSince >= SustainedPressureSeconds)
                {
                    Mode = Mode == ResourceGovernorMode.Normal ? ResourceGovernorMode.Degraded : ResourceGovernorMode.Critical;
                    pressureSince = -1f;
                }
            }
            else if (recovery)
            {
                recoverySince = recoverySince < 0f ? now : recoverySince; pressureSince = -1f;
                if (now - recoverySince >= SustainedRecoverySeconds)
                {
                    Mode = Mode == ResourceGovernorMode.Critical ? ResourceGovernorMode.Degraded : ResourceGovernorMode.Normal;
                    recoverySince = -1f;
                }
            }
            else { pressureSince = -1f; recoverySince = -1f; }
            return Mode;
        }

        public RuntimeQosLevel Decide(RuntimeQosRequest request)
        {
            if (request.RequiredGameplay || request.Priority == RuntimeQosPriority.P0CriticalReadability) return RuntimeQosLevel.Full;
            if (Mode == ResourceGovernorMode.Normal) return RuntimeQosLevel.Full;
            if (Mode == ResourceGovernorMode.Degraded)
            {
                if (request.Priority == RuntimeQosPriority.P1StrongFeedback) return RuntimeQosLevel.Full;
                return request.Priority == RuntimeQosPriority.P2Presentation ? RuntimeQosLevel.Reduced : RuntimeQosLevel.Minimal;
            }
            if (request.Priority == RuntimeQosPriority.P1StrongFeedback) return RuntimeQosLevel.Reduced;
            return request.Priority == RuntimeQosPriority.P2Presentation ? RuntimeQosLevel.Minimal : RuntimeQosLevel.Denied;
        }
    }

    /// <summary>Optional scene component. With no instance, policy remains Normal and gameplay is unchanged.</summary>
    [DisallowMultipleComponent]
    public sealed class ResourceGovernor : MonoBehaviour
    {
        [SerializeField] private ResourceGovernorControl control = ResourceGovernorControl.Auto;
        [SerializeField, Min(1f)] private float degradedEntryMilliseconds = 22f;
        [SerializeField, Min(1f)] private float criticalEntryMilliseconds = 33f;
        [SerializeField, Min(.05f)] private float sustainedPressureSeconds = .75f;
        [SerializeField, Min(.05f)] private float sustainedRecoverySeconds = 1.5f;
        private readonly ResourceGovernorPolicy policy = new ResourceGovernorPolicy();
        private float frameMilliseconds;
        public static ResourceGovernor Active { get; private set; }
        public ResourceGovernorMode Mode { get; private set; } = ResourceGovernorMode.Normal;
        public ResourceGovernorControl Control
        {
            get => control;
            set
            {
                control = value;
                if (control != ResourceGovernorControl.Auto)
                {
                    Mode = ForcedMode(control, Mode);
                }
            }
        }
        public ResourceGovernorSnapshot Snapshot => new ResourceGovernorSnapshot(Mode, frameMilliseconds, frameMilliseconds <= 0f ? 0f : 1000f / frameMilliseconds, control != ResourceGovernorControl.Auto);

        private void OnEnable() { Active = this; }
        private void OnDisable() { if (Active == this) Active = null; }
        private void Update()
        {
            frameMilliseconds = Mathf.Max(0f, Time.unscaledDeltaTime * 1000f);
            policy.DegradedEntryMilliseconds = degradedEntryMilliseconds; policy.CriticalEntryMilliseconds = Mathf.Max(degradedEntryMilliseconds, criticalEntryMilliseconds);
            policy.SustainedPressureSeconds = sustainedPressureSeconds; policy.SustainedRecoverySeconds = sustainedRecoverySeconds;
            Mode = ForcedMode(control, policy.Evaluate(Time.unscaledTime, frameMilliseconds));
        }
        public RuntimeQosLevel Decide(RuntimeQosRequest request)
        {
            if (control == ResourceGovernorControl.Auto) return policy.Decide(request);
            return DecideForced(ForcedMode(control, Mode), request);
        }
        public float NonUrgentCtcIntervalMultiplier => Mode == ResourceGovernorMode.Critical ? 2f : Mode == ResourceGovernorMode.Degraded ? 1.4f : 1f;
        public static RuntimeQosLevel DecideCurrent(RuntimeQosRequest request) => Active == null ? RuntimeQosLevel.Full : Active.Decide(request);
        public static float CurrentNonUrgentCtcIntervalMultiplier => Active == null ? 1f : Active.NonUrgentCtcIntervalMultiplier;
        private static ResourceGovernorMode ForcedMode(ResourceGovernorControl value, ResourceGovernorMode automatic)
        { return value == ResourceGovernorControl.ForceNormal ? ResourceGovernorMode.Normal : value == ResourceGovernorControl.ForceDegraded ? ResourceGovernorMode.Degraded : value == ResourceGovernorControl.ForceCritical ? ResourceGovernorMode.Critical : automatic; }
        private static RuntimeQosLevel DecideForced(ResourceGovernorMode mode, RuntimeQosRequest request)
        {
            if (request.RequiredGameplay || request.Priority == RuntimeQosPriority.P0CriticalReadability) return RuntimeQosLevel.Full;
            if (mode == ResourceGovernorMode.Normal) return RuntimeQosLevel.Full;
            if (mode == ResourceGovernorMode.Degraded)
                return request.Priority == RuntimeQosPriority.P1StrongFeedback ? RuntimeQosLevel.Full
                    : request.Priority == RuntimeQosPriority.P2Presentation ? RuntimeQosLevel.Reduced : RuntimeQosLevel.Minimal;
            return request.Priority == RuntimeQosPriority.P1StrongFeedback ? RuntimeQosLevel.Reduced
                : request.Priority == RuntimeQosPriority.P2Presentation ? RuntimeQosLevel.Minimal : RuntimeQosLevel.Denied;
        }
    }
}
