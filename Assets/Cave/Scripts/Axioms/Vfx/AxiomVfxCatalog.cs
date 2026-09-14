using System;
using UnityEngine;

namespace Cave.Axioms.Vfx
{
    /// <summary>
    /// Serialized, reusable registry for the compact world-space Axiom effects.
    /// The editor builder creates the asset and its referenced prefabs from the
    /// approved Axiom sheet; runtime code only reads these cached references.
    /// </summary>
    [CreateAssetMenu(menuName = "Cave/Axioms/VFX Catalog", fileName = "AxiomVfxCatalog")]
    public sealed class AxiomVfxCatalog : ScriptableObject
    {
        [SerializeField] private AxiomVfxDefinition resonanceProgress;
        [SerializeField] private AxiomVfxDefinition resonanceBreak;
        [SerializeField] private AxiomVfxDefinition destabilized;
        [SerializeField] private AxiomVfxDefinition phaseApply;
        [SerializeField] private AxiomVfxDefinition phaseCollapseStrong;
        [SerializeField] private AxiomVfxDefinition phaseCollapseWeak;
        [SerializeField] private AxiomVfxDefinition phaseCollapseEven;
        [SerializeField] private AxiomVfxDefinition phaseVulnerability;
        [SerializeField] private AxiomVfxDefinition errorState;
        [SerializeField] private AxiomVfxDefinition errorRate;
        [SerializeField] private AxiomVfxDefinition errorAcceleration;
        [SerializeField] private AxiomVfxDefinition controlSuccess;
        [SerializeField] private AxiomVfxDefinition controlFailure;
        [SerializeField] private AxiomVfxDefinition counterphase;
        [SerializeField] private AxiomVfxDefinition heat;
        [SerializeField] private AxiomVfxDefinition order;
        [SerializeField] private AxiomVfxDefinition flow;
        [SerializeField] private AxiomVfxDefinition mass;

        public AxiomVfxDefinition ResonanceProgress => resonanceProgress;
        public AxiomVfxDefinition ResonanceBreak => resonanceBreak;
        public AxiomVfxDefinition Destabilized => destabilized;
        public AxiomVfxDefinition PhaseApply => phaseApply;
        public AxiomVfxDefinition PhaseCollapseStrong => phaseCollapseStrong;
        public AxiomVfxDefinition PhaseCollapseWeak => phaseCollapseWeak;
        public AxiomVfxDefinition PhaseCollapseEven => phaseCollapseEven;
        public AxiomVfxDefinition PhaseVulnerability => phaseVulnerability;
        public AxiomVfxDefinition ErrorState => errorState;
        public AxiomVfxDefinition ErrorRate => errorRate;
        public AxiomVfxDefinition ErrorAcceleration => errorAcceleration;
        public AxiomVfxDefinition ControlSuccess => controlSuccess;
        public AxiomVfxDefinition ControlFailure => controlFailure;
        public AxiomVfxDefinition Counterphase => counterphase;
        public AxiomVfxDefinition Heat => heat;
        public AxiomVfxDefinition Order => order;
        public AxiomVfxDefinition Flow => flow;
        public AxiomVfxDefinition Mass => mass;

        public void Configure(
            AxiomVfxDefinition resonanceProgressValue,
            AxiomVfxDefinition resonanceBreakValue,
            AxiomVfxDefinition destabilizedValue,
            AxiomVfxDefinition phaseApplyValue,
            AxiomVfxDefinition phaseCollapseStrongValue,
            AxiomVfxDefinition phaseCollapseWeakValue,
            AxiomVfxDefinition phaseCollapseEvenValue,
            AxiomVfxDefinition phaseVulnerabilityValue,
            AxiomVfxDefinition errorStateValue,
            AxiomVfxDefinition errorRateValue,
            AxiomVfxDefinition errorAccelerationValue,
            AxiomVfxDefinition controlSuccessValue,
            AxiomVfxDefinition controlFailureValue,
            AxiomVfxDefinition counterphaseValue,
            AxiomVfxDefinition heatValue,
            AxiomVfxDefinition orderValue,
            AxiomVfxDefinition flowValue,
            AxiomVfxDefinition massValue)
        {
            resonanceProgress = resonanceProgressValue;
            resonanceBreak = resonanceBreakValue;
            destabilized = destabilizedValue;
            phaseApply = phaseApplyValue;
            phaseCollapseStrong = phaseCollapseStrongValue;
            phaseCollapseWeak = phaseCollapseWeakValue;
            phaseCollapseEven = phaseCollapseEvenValue;
            phaseVulnerability = phaseVulnerabilityValue;
            errorState = errorStateValue;
            errorRate = errorRateValue;
            errorAcceleration = errorAccelerationValue;
            controlSuccess = controlSuccessValue;
            controlFailure = controlFailureValue;
            counterphase = counterphaseValue;
            heat = heatValue;
            order = orderValue;
            flow = flowValue;
            mass = massValue;
        }
    }

    [Serializable]
    public struct AxiomVfxDefinition
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0.01f)] private float lifetime;
        [SerializeField, Min(0.01f)] private float scale;
        [SerializeField] private Vector2 localOffset;
        [SerializeField, Min(0f)] private float cooldown;

        public AxiomVfxDefinition(GameObject prefabValue, float lifetimeValue, float scaleValue, Vector2 offsetValue, float cooldownValue)
        {
            prefab = prefabValue;
            lifetime = Mathf.Max(0.01f, lifetimeValue);
            scale = Mathf.Max(0.01f, scaleValue);
            localOffset = offsetValue;
            cooldown = Mathf.Max(0f, cooldownValue);
        }

        public GameObject Prefab => prefab;
        public float Lifetime => Mathf.Max(0.01f, lifetime);
        public float Scale => Mathf.Max(0.01f, scale);
        public Vector2 LocalOffset => localOffset;
        public float Cooldown => Mathf.Max(0f, cooldown);
        public bool IsValid => prefab != null;
    }
}
