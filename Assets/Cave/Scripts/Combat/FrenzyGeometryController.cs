using UnityEngine;

namespace Cave.Combat
{
    [DisallowMultipleComponent]
    public sealed class FrenzyGeometryController : MonoBehaviour
    {
        private PlayerCombatFlow flow;

        public static FrenzyGeometryController EnsureOn(GameObject player)
        {
            return player == null
                ? null
                : player.GetComponent<FrenzyGeometryController>()
                    ?? player.AddComponent<FrenzyGeometryController>();
        }

        private void Awake()
        {
            flow = GetComponent<PlayerCombatFlow>();
        }

        private void LateUpdate()
        {
            bool reveal = flow != null
                && flow.IsFrenzyActive
                && flow.FrenzyMasteryLevel >= 3;
            var structures = FrenzyGeometryStructure.ActiveStructures;
            for (int index = structures.Count - 1; index >= 0; index--)
            {
                FrenzyGeometryStructure structure = structures[index];
                if (structure != null)
                {
                    structure.SetRevealed(reveal);
                }
            }
        }

        private void OnDisable()
        {
            var structures = FrenzyGeometryStructure.ActiveStructures;
            for (int index = structures.Count - 1; index >= 0; index--)
            {
                if (structures[index] != null) structures[index].SetRevealed(false);
            }
        }
    }
}
