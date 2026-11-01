using System;
using UnityEngine;

namespace Cave.Projectiles
{
    /// <summary>Selectable standard-fire presentations. Heavy remains outside this selection.</summary>
    public enum FirePattern
    {
        Precision,
        Automatic,
        Burst,
        Volley,
        SpoolUp
    }

    /// <summary>
    /// Inspector-tunable standard-fire data. A zero cadence, mana cost, or
    /// speed inherits the launcher value so Automatic preserves legacy scenes.
    /// </summary>
    [Serializable]
    public struct FirePatternProfile
    {
        [Min(0f)] public float cadence;
        [Min(0f)] public float minimumCadence;
        [Min(0f)] public float spoolDuration;
        [Min(1)] public int projectileCount;
        [Min(0f)] public float burstSpacing;
        [Min(0f)] public float recovery;
        [Range(0f, 180f)] public float spreadAngle;
        [Min(0f)] public float projectileSpeed;
        [Min(0f)] public float manaCostPerProjectile;
        [Min(0f)] public float damageMultiplier;
        [Min(0f)] public float intrinsicImpulse;

        public FirePatternProfile(
            float cadence, float minimumCadence, float spoolDuration,
            int projectileCount, float burstSpacing, float recovery,
            float spreadAngle, float projectileSpeed, float manaCostPerProjectile,
            float damageMultiplier, float intrinsicImpulse)
        {
            this.cadence = Mathf.Max(0f, cadence);
            this.minimumCadence = Mathf.Max(0f, minimumCadence);
            this.spoolDuration = Mathf.Max(0f, spoolDuration);
            this.projectileCount = Mathf.Max(1, projectileCount);
            this.burstSpacing = Mathf.Max(0f, burstSpacing);
            this.recovery = Mathf.Max(0f, recovery);
            this.spreadAngle = Mathf.Clamp(spreadAngle, 0f, 180f);
            this.projectileSpeed = Mathf.Max(0f, projectileSpeed);
            this.manaCostPerProjectile = Mathf.Max(0f, manaCostPerProjectile);
            this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
            this.intrinsicImpulse = Mathf.Max(0f, intrinsicImpulse);
        }

        public int ProjectileCount => Mathf.Max(1, projectileCount);
        public float DamageMultiplier => damageMultiplier <= 0f ? 1f : damageMultiplier;
    }

    /// <summary>
    /// Player-owned selection and profile source for ordinary projectiles.
    /// It intentionally owns no input, Heavy state, mana, or projectile state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFirePatternState : MonoBehaviour
    {
        private const string SelectionKey = "Cave.FirePattern.Selection.v1";

        [Header("Selection")]
        [SerializeField] private FirePattern selectedPattern = FirePattern.Automatic;

        [Header("Profiles — provisional tuning")]
        [SerializeField] private FirePatternProfile precision = new FirePatternProfile(
            .62f, .62f, 0f, 1, 0f, .62f, 0f, 20f, 12f, 1.55f, 8f);
        // Zero cadence / mana / speed means inherit the pre-existing launcher fields.
        [SerializeField] private FirePatternProfile automatic = new FirePatternProfile(
            0f, 0f, 0f, 1, 0f, 0f, 0f, 0f, 0f, 1f, 4f);
        [SerializeField] private FirePatternProfile burst = new FirePatternProfile(
            .6f, .6f, 0f, 3, .09f, .6f, 0f, 16f, 5f, .8f, 4.5f);
        [SerializeField] private FirePatternProfile volley = new FirePatternProfile(
            .52f, .52f, 0f, 3, 0f, .52f, 22f, 14f, 4f, .75f, 3.5f);
        [SerializeField] private FirePatternProfile spoolUp = new FirePatternProfile(
            .42f, .1f, 1.35f, 1, 0f, 0f, 0f, 17f, 6f, .9f, 4.75f);

        public event Action<FirePattern> SelectionChanged;
        public FirePattern SelectedPattern => selectedPattern;

        public static PlayerFirePatternState EnsureOn(GameObject owner)
        {
            if (owner == null) return null;
            PlayerFirePatternState state = owner.GetComponent<PlayerFirePatternState>();
            return state != null ? state : owner.AddComponent<PlayerFirePatternState>();
        }

        private void Awake()
        {
            selectedPattern = (FirePattern)Mathf.Clamp(
                PlayerPrefs.GetInt(SelectionKey, (int)selectedPattern),
                (int)FirePattern.Precision,
                (int)FirePattern.SpoolUp);
        }

        public bool Select(FirePattern pattern)
        {
            if (pattern < FirePattern.Precision || pattern > FirePattern.SpoolUp)
            {
                return false;
            }

            if (selectedPattern == pattern) return true;
            selectedPattern = pattern;
            PlayerPrefs.SetInt(SelectionKey, (int)selectedPattern);
            PlayerPrefs.Save();
            SelectionChanged?.Invoke(selectedPattern);
            return true;
        }

        public FirePatternProfile ProfileFor(FirePattern pattern)
        {
            switch (pattern)
            {
                case FirePattern.Precision: return precision;
                case FirePattern.Burst: return burst;
                case FirePattern.Volley: return volley;
                case FirePattern.SpoolUp: return spoolUp;
                default: return automatic;
            }
        }

        /// <summary>Explicit new-game seam; death and ordinary run reset never call this.</summary>
        public void ResetForNewGame()
        {
            PlayerPrefs.DeleteKey(SelectionKey);
            selectedPattern = FirePattern.Automatic;
            PlayerPrefs.Save();
            SelectionChanged?.Invoke(selectedPattern);
        }
    }
}
