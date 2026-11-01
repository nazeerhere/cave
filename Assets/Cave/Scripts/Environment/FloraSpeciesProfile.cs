using UnityEngine;

namespace Cave.Environmental
{
    [CreateAssetMenu(fileName = "FloraSpecies", menuName = "Cave/Environment/Flora Species Profile")]
    public sealed class FloraSpeciesProfile : ScriptableObject
    {
        [SerializeField] private CaveFloraSpecies species;
        [SerializeField, Min(.01f)] private float biologicalVitality = 3f;
        [SerializeField, Min(.01f)] private float coreIntegrity = 2f;
        [SerializeField, Min(0f)] private float metabolism = 1f;
        [SerializeField, Min(0)] private int maximumPropagationChildren = 2;
        [SerializeField, Min(0f)] private float propagationRadius = 4f;
        [SerializeField] private EnvironmentalMaterialProfile tissueMaterial;

        public CaveFloraSpecies Species => species;
        public float BiologicalVitality => biologicalVitality;
        public float CoreIntegrity => coreIntegrity;
        public float Metabolism => metabolism;
        public int MaximumPropagationChildren => maximumPropagationChildren;
        public float PropagationRadius => propagationRadius;
        public EnvironmentalMaterialProfile TissueMaterial => tissueMaterial;
    }
}
