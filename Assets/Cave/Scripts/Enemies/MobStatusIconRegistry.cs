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
        Frenzied
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
    }
}
