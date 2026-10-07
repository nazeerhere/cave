using System;
using UnityEngine;

namespace Cave.Enemies
{
    public enum MobStatusIconKind
    {
        Poison,
        Burn,
        Slow,
        PinRoot,
        Stagger,
        TowerSuppression,
        Freeze,
        StrengthBuff,
        Overheal,
        Regeneration,
        WatcherMark,
        GazeLock,
        Possessed,
        ElementallyBuffed,
        Frenzied,
        Imaginary,
        Stoneglass,

        // Reserved presentation identities for the canonical Axiom order. These
        // do not activate, apply, or otherwise alter any gameplay state here.
        // Appending them preserves every existing serialized enum value.
        AxiomHeat,
        AxiomFlow,
        AxiomMass,
        AxiomCompression,
        AxiomPotential,
        AxiomResonance,
        AxiomPhase,
        AxiomOrder
    }

    [Serializable]
    public struct MobStatusIconEntry
    {
        public MobStatusIconKind Kind;
        public Sprite Icon;
    }

    [CreateAssetMenu(menuName = "Cave/UI/Mob Status Icon Registry", fileName = "MobStatusIconRegistry")]
    public sealed class MobStatusIconRegistry : ScriptableObject
    {
        [SerializeField] private MobStatusIconEntry[] icons;

        public Sprite GetIcon(MobStatusIconKind kind)
        {
            if (icons == null)
            {
                return null;
            }

            foreach (MobStatusIconEntry entry in icons)
            {
                if (entry.Kind == kind)
                {
                    return entry.Icon;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void SetIconForEditor(MobStatusIconKind kind, Sprite icon)
        {
            int existingIndex = -1;
            if (icons != null)
            {
                for (int index = 0; index < icons.Length; index++)
                {
                    if (icons[index].Kind == kind)
                    {
                        existingIndex = index;
                        break;
                    }
                }
            }

            if (existingIndex >= 0)
            {
                icons[existingIndex].Icon = icon;
                return;
            }

            int existingCount = icons != null ? icons.Length : 0;
            MobStatusIconEntry[] expanded = new MobStatusIconEntry[existingCount + 1];
            for (int index = 0; index < existingCount; index++)
            {
                expanded[index] = icons[index];
            }

            expanded[existingCount] = new MobStatusIconEntry { Kind = kind, Icon = icon };
            icons = expanded;
        }
#endif
    }
}
