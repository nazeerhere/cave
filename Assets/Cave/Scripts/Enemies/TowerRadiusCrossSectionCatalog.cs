using UnityEngine;

namespace Cave.Enemies
{
    public sealed class TowerRadiusCrossSectionCatalog : ScriptableObject
    {
        [SerializeField] private TowerRadiusCrossSectionVfx prefab;

        public TowerRadiusCrossSectionVfx Prefab => prefab;
    }
}
