using System;
using Cave.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace Cave.Enemies
{
    [Serializable]
    public sealed class BossPhaseDefinition
    {
        [SerializeField, Range(0f, 1f)] private float enterAtHealthFraction = 1f;
        [SerializeField] private EnemyArchetype addedStrategies;
        [SerializeField] private UnityEvent onPhaseEntered;

        public float EnterAtHealthFraction => enterAtHealthFraction;
        public EnemyArchetype AddedStrategies => addedStrategies;
        public UnityEvent OnPhaseEntered => onPhaseEntered;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class BossPhaseController : MonoBehaviour
    {
        [SerializeField] private BossPhaseDefinition[] phases;
        [SerializeField] private int currentPhase = -1;

        private Damageable damageable;
        private EnemyArchetypeProfile archetypeProfile;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            archetypeProfile = GetComponent<EnemyArchetypeProfile>();
        }

        private void Start()
        {
            EvaluatePhase();
        }

        private void Update()
        {
            EvaluatePhase();
        }

        private void EvaluatePhase()
        {
            if (phases == null || phases.Length == 0 || damageable.MaximumHealth <= 0)
            {
                return;
            }

            float healthFraction = damageable.CurrentHealth / (float)damageable.MaximumHealth;
            int nextPhase = currentPhase;
            for (int index = 0; index < phases.Length; index++)
            {
                BossPhaseDefinition phase = phases[index];
                if (phase != null && healthFraction <= phase.EnterAtHealthFraction)
                {
                    nextPhase = Mathf.Max(nextPhase, index);
                }
            }

            while (currentPhase < nextPhase)
            {
                currentPhase++;
                BossPhaseDefinition entered = phases[currentPhase];
                archetypeProfile.AddRuntimeArchetype(entered.AddedStrategies);
                entered.OnPhaseEntered?.Invoke();
            }
        }
    }
}
