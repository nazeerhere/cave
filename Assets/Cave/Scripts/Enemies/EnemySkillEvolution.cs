using System;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    public enum EnemyEvolutionStage
    {
        Base,
        EvolutionOne,
        EvolutionTwo
    }

    public interface IEnemySkillEvolutionReceiver
    {
        void ApplyEvolution(EnemyEvolutionStage stage);
    }

    [DisallowMultipleComponent]
    public sealed class EnemySkillEvolution : MonoBehaviour
    {
        [Header("World Tier Thresholds")]
        [SerializeField, Min(0)] private int evolutionOneWorldTier = 3;
        [SerializeField, Min(0)] private int evolutionTwoWorldTier = 6;

        [Header("Current Evolution (Read Only)")]
        [SerializeField] private EnemyEvolutionStage currentStage;
        [SerializeField] private int evaluatedWorldTier;

        private WorldDifficultyManager difficultyManager;

        public event Action<EnemyEvolutionStage> EvolutionChanged;
        public EnemyEvolutionStage CurrentStage => currentStage;
        public int EvaluatedWorldTier => evaluatedWorldTier;

        private void Start()
        {
            if (difficultyManager == null)
            {
                Configure(FindObjectOfType<WorldDifficultyManager>());
            }
        }

        internal void Configure(WorldDifficultyManager manager)
        {
            Unsubscribe();
            difficultyManager = manager;
            Subscribe();
            Evaluate(true);
        }

        private void HandleDifficultyChanged()
        {
            Evaluate(false);
        }

        private void Evaluate(bool forceNotification)
        {
            int worldTier = difficultyManager != null ? difficultyManager.DifficultyTier : 0;
            int firstThreshold = Mathf.Max(0, evolutionOneWorldTier);
            int secondThreshold = Mathf.Max(firstThreshold, evolutionTwoWorldTier);
            EnemyEvolutionStage resolvedStage = worldTier >= secondThreshold
                ? EnemyEvolutionStage.EvolutionTwo
                : worldTier >= firstThreshold
                    ? EnemyEvolutionStage.EvolutionOne
                    : EnemyEvolutionStage.Base;

            bool changed = resolvedStage != currentStage || worldTier != evaluatedWorldTier;
            currentStage = resolvedStage;
            evaluatedWorldTier = worldTier;
            if (!changed && !forceNotification)
            {
                return;
            }

            foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != this && behaviour is IEnemySkillEvolutionReceiver receiver)
                {
                    receiver.ApplyEvolution(currentStage);
                }
            }

            if (changed)
            {
                EvolutionChanged?.Invoke(currentStage);
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Evaluate(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= HandleDifficultyChanged;
                difficultyManager.DifficultyChanged += HandleDifficultyChanged;
            }
        }

        private void Unsubscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= HandleDifficultyChanged;
            }
        }

        private void OnValidate()
        {
            evolutionTwoWorldTier = Mathf.Max(evolutionOneWorldTier, evolutionTwoWorldTier);
        }
    }
}
