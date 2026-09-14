using UnityEngine;

namespace Cave.Axioms.Vfx
{
    /// <summary>
    /// Serialized references for the approved Imaginary-only presentation frames.
    /// This remains separate from the broader Axiom catalog so the two approved
    /// families cannot be mistaken for generic Phase, halo, or ground effects.
    /// </summary>
    [CreateAssetMenu(menuName = "Cave/Axioms/Imaginary VFX Catalog", fileName = "AxiomImaginaryVfxCatalog")]
    public sealed class AxiomImaginaryVfxCatalog : ScriptableObject
    {
        [SerializeField] private GameObject shadowPrefab;
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private AxiomSpriteAnimation shadowSmear;
        [SerializeField] private AxiomSpriteAnimation impactRupture;

        public GameObject ShadowPrefab => shadowPrefab;
        public GameObject ImpactPrefab => impactPrefab;
        public AxiomSpriteAnimation ShadowSmear => shadowSmear;
        public AxiomSpriteAnimation ImpactRupture => impactRupture;

        public void Configure(
            GameObject shadowPrefabValue,
            GameObject impactPrefabValue,
            AxiomSpriteAnimation shadowAnimation,
            AxiomSpriteAnimation impactAnimation)
        {
            shadowPrefab = shadowPrefabValue;
            impactPrefab = impactPrefabValue;
            shadowSmear = shadowAnimation;
            impactRupture = impactAnimation;
        }
    }
}
