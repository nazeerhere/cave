using System;
using System.Collections.Generic;
using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Represents the permanent prerequisite for personal Domain construction.
    /// Acquisition is owned by <see cref="DomainProgression"/>; this value does
    /// not persist any current-run configuration or mastery.
    /// </summary>
    [Serializable]
    public struct DomainSeed
    {
        [SerializeField] private bool acquired;

        public DomainSeed(bool isAcquired)
        {
            acquired = isAcquired;
        }

        public bool IsAcquired => acquired;
    }

    /// <summary>Future source of a player's personal Domain power.</summary>
    public enum PowerExpression
    {
        Projectiles,
        Frenzy,
        Hybrid
    }

    /// <summary>
    /// Inert, centralized vocabulary for how future Domain authority occupies
    /// space. These are not Derived Laws and have no simulation in this pass.
    /// </summary>
    public enum TerritoryPrinciple
    {
        Propagation,
        Accumulation,
        Synchronization,
        Localization,
        Conversion
    }

    /// <summary>
    /// UI-ready capacity readout for future Domain construction. Stability and
    /// over-complexity behaviour deliberately remain unimplemented.
    /// </summary>
    [Serializable]
    public sealed class DomainComplexity
    {
        [SerializeField, Min(0)] private int capacity;
        [SerializeField, Min(0)] private int committed;

        public DomainComplexity(int maximumCapacity = 0, int currentCommitted = 0)
        {
            capacity = Mathf.Max(0, maximumCapacity);
            committed = Mathf.Clamp(currentCommitted, 0, capacity);
        }

        public int Capacity => capacity;
        public int Committed => committed;
        public int Available => Mathf.Max(0, capacity - committed);
    }

    /// <summary>
    /// Data-only outline of a future personal Domain. It is intentionally not
    /// persisted: a Domain Seed and current-run mastery are the prerequisites,
    /// while future construction choices remain run-local.
    /// </summary>
    [Serializable]
    public sealed class DomainConfiguration
    {
        [SerializeField] private PowerExpression powerExpression;
        [SerializeField] private TerritoryPrinciple territoryPrinciple;
        [SerializeField] private DomainComplexity complexity = new DomainComplexity();
        [SerializeField] private List<MasteryDomain> phenomena = new List<MasteryDomain>();

        public PowerExpression PowerExpression => powerExpression;
        public TerritoryPrinciple TerritoryPrinciple => territoryPrinciple;
        public DomainComplexity Complexity => complexity;
        public IReadOnlyList<MasteryDomain> Phenomena => phenomena;
    }

    /// <summary>
    /// Runtime seam reserved for the future personal Domain simulation. This
    /// class intentionally creates no effects, colliders, or authority yet.
    /// </summary>
    public sealed class DomainRuntime
    {
        public DomainRuntime(DomainConfiguration configuration)
        {
            Configuration = configuration ?? new DomainConfiguration();
        }

        public DomainConfiguration Configuration { get; }
        public bool IsSeeded => DomainProgression.HasDomainSeed;
    }
}
