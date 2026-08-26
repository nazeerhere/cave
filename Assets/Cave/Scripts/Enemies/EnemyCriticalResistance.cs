using Cave.Progression;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyCriticalResistance : MonoBehaviour
    {
        [SerializeField] private WorldDifficultyManager difficultyManager;

        [Header("Current Life (Read Only)")]
        [SerializeField, Range(0f, 0.95f)] private float currentResistance;
        [SerializeField, Min(0)] private int rolledWorldTier;
        [SerializeField] private bool rolledForCurrentLife;

        public float CurrentResistance => currentResistance;
        public int RolledWorldTier => rolledWorldTier;

        internal void Configure(WorldDifficultyManager manager)
        {
            difficultyManager = manager;
        }

        public void RollForLife()
        {
            rolledWorldTier = difficultyManager != null ? difficultyManager.DifficultyTier : 0;
            ProgressionDifficultySettings settings = difficultyManager != null
                ? difficultyManager.Settings
                : null;
            Vector2 range = settings != null
                ? settings.GetCriticalResistanceRange(rolledWorldTier)
                : ProgressionDifficultySettings.GetDefaultCriticalResistanceRange(rolledWorldTier);
            float minimum = Mathf.Clamp(range.x, 0f, 0.95f);
            float maximum = Mathf.Clamp(range.y, minimum, 0.95f);
            currentResistance = Random.Range(minimum, maximum);
            rolledForCurrentLife = true;
        }

        private void OnEnable()
        {
            if (!rolledForCurrentLife)
            {
                RollForLife();
            }
        }

        private void OnDisable()
        {
            rolledForCurrentLife = false;
        }
    }
}
