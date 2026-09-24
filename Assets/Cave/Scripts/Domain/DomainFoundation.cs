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

    /// <summary>
    /// Legacy serialized placeholder vocabulary for a future Domain's power
    /// source. Do not use this enum for new Laws; <see cref="LawExpression"/>
    /// is the authoritative Law vocabulary. Values remain unchanged here to
    /// preserve existing serialized data until an explicit migration is made.
    /// </summary>
    public enum PowerExpression
    {
        Projectiles,
        Frenzy,
        Hybrid
    }

    /// <summary>
    /// Legacy serialized placeholder vocabulary for future Domain spatial data.
    /// Do not use this enum for new Laws; <see cref="LawTerritoryPrinciple"/>
    /// is the authoritative Law vocabulary. Values remain unchanged here to
    /// preserve existing serialized data until an explicit migration is made.
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
    /// Legacy data-only placeholder for earlier Domain UI/configuration work.
    /// Its multi-phenomenon list is not a Law and must not be used as a new
    /// runtime Law source. It remains intact because these serialized fields
    /// may be referenced by existing assets; a later migration may adapt it
    /// explicitly to a collection of <see cref="DomainLaw"/> values.
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
