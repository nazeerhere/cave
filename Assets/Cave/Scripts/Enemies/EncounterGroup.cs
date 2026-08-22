using System;
using Cave.Progression;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EncounterGroup : MonoBehaviour
    {
        [SerializeField] private StrategicCombatSettings settings;
        [SerializeField] private WorldDifficultyManager difficultyManager;
        [SerializeField] private EnemyArchetypeProfile[] members;

        [Header("Current Composition (Read Only)")]
        [SerializeField] private EnemyArchetype currentComposition;
        [SerializeField] private bool currentCompositionEligible = true;

        public event Action CompositionEligibilityChanged;

        public EnemyArchetype CurrentComposition => currentComposition;
        public bool CurrentCompositionEligible => currentCompositionEligible;

        private void Awake()
        {
            if (members == null || members.Length == 0)
            {
                members = GetComponentsInChildren<EnemyArchetypeProfile>(true);
            }

            Recalculate();
        }

        internal void ConfigureIfMissing(
            StrategicCombatSettings strategicSettings,
            WorldDifficultyManager worldDifficulty)
        {
            if (settings == null)
            {
                settings = strategicSettings;
            }

            if (difficultyManager == null)
            {
                difficultyManager = worldDifficulty;
            }

            Subscribe();
            Recalculate();
        }

        private void Recalculate()
        {
            bool previousEligibility = currentCompositionEligible;
            currentComposition = EnemyArchetype.None;
            if (members != null)
            {
                foreach (EnemyArchetypeProfile member in members)
                {
                    if (member != null)
                    {
                        currentComposition |= member.Archetypes;
                    }
                }
            }

            currentCompositionEligible = IsCompositionEligible(
                currentComposition,
                difficultyManager != null ? difficultyManager.DifficultyTier : 0);
            if (previousEligibility != currentCompositionEligible)
            {
                CompositionEligibilityChanged?.Invoke();
            }
        }

        public bool IsCompositionEligible(EnemyArchetype composition, int difficultyTier)
        {
            if (settings == null)
            {
                return true;
            }

            int distinctStrategies = CountFlags(composition);
            if (distinctStrategies > 1 && difficultyTier < settings.MixedEncounterMinimumTier)
            {
                return false;
            }

            return (!Includes(composition, EnemyArchetype.Melee)
                    || difficultyTier >= settings.MeleeMinimumTier)
                && (!Includes(composition, EnemyArchetype.Ranged)
                    || difficultyTier >= settings.RangedMinimumTier)
                && (!Includes(composition, EnemyArchetype.Support)
                    || difficultyTier >= settings.SupportMinimumTier)
                && (!Includes(composition, EnemyArchetype.Tank)
                    || difficultyTier >= settings.TankMinimumTier)
                && (!Includes(composition, EnemyArchetype.Swarm)
                    || difficultyTier >= settings.SwarmMinimumTier);
        }

        private static bool Includes(EnemyArchetype value, EnemyArchetype flag)
        {
            return (value & flag) != 0;
        }

        private static int CountFlags(EnemyArchetype value)
        {
            int count = 0;
            int bits = (int)value;
            while (bits != 0)
            {
                count += bits & 1;
                bits >>= 1;
            }

            return count;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= Recalculate;
            }
        }

        private void Subscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= Recalculate;
                difficultyManager.DifficultyChanged += Recalculate;
            }
        }
    }
}
