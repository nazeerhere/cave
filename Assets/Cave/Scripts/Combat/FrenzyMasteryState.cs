using System;
using UnityEngine;

namespace Cave.Combat
{
    /// <summary>
    /// Session mastery for Frenzy.  It intentionally owns duration only: critical
    /// count, damage, elemental strength, and mana capacity stay elsewhere.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrenzyMasteryState : MonoBehaviour
    {
        [Header("Duration Tuning")]
        [SerializeField, Min(0f)] private float durationPerLevel = 0.25f;
        [SerializeField, Min(0f)] private float maximumDurationBonus = 2f;

        [Header("Session Progress (Read Only)")]
        [SerializeField, Min(0)] private int currentLevel;
        [SerializeField, Range(0f, 1f)] private float currentProgress;

        public event Action FrenzyMasteryChanged;

        public int CurrentLevel => currentLevel;
        public float CurrentProgress => currentProgress;
        public float DurationPerLevel => durationPerLevel;
        public float MaximumDurationBonus => maximumDurationBonus;
        public float DurationBonus => Mathf.Min(
            Mathf.Max(0f, maximumDurationBonus),
            Mathf.Max(0f, currentLevel) * Mathf.Max(0f, durationPerLevel));

        public void Configure(float configuredDurationPerLevel, float configuredMaximumDurationBonus)
        {
            durationPerLevel = Mathf.Max(0f, configuredDurationPerLevel);
            maximumDurationBonus = Mathf.Max(0f, configuredMaximumDurationBonus);
            currentProgress = Mathf.Clamp01(
                DurationBonus / Mathf.Max(0.0001f, maximumDurationBonus));
        }

        public static FrenzyMasteryState EnsureOn(GameObject owner)
        {
            return owner == null
                ? null
                : owner.GetComponent<FrenzyMasteryState>() ?? owner.AddComponent<FrenzyMasteryState>();
        }

        /// <summary>One qualified late-Frenzy result grants one bounded level.</summary>
        public bool TryGainQualifiedLevel()
        {
            if (DurationBonus >= maximumDurationBonus - 0.0001f || durationPerLevel <= 0f)
            {
                currentProgress = 1f;
                return false;
            }

            currentLevel++;
            currentProgress = Mathf.Clamp01(DurationBonus / Mathf.Max(0.0001f, maximumDurationBonus));
            FrenzyMasteryChanged?.Invoke();
            return true;
        }

        private void OnValidate()
        {
            durationPerLevel = Mathf.Max(0f, durationPerLevel);
            maximumDurationBonus = Mathf.Max(0f, maximumDurationBonus);
            currentLevel = Mathf.Max(0, currentLevel);
            currentProgress = Mathf.Clamp01(currentProgress);
        }
    }
}
