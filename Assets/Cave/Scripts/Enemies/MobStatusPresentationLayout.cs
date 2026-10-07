using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Pure layout and ordering policy shared by every world-space mob status
    /// cell. This remains presentation-only: it does not query or mutate
    /// gameplay state.
    /// </summary>
    public static class MobStatusPresentationLayout
    {
        private const int LegacyStatusOrderOffset = 100;

        public static int Compare(MobStatusIconKind left, MobStatusIconKind right)
        {
            return SortKey(left).CompareTo(SortKey(right));
        }

        public static void Sort(List<MobStatusPresentationEntry> entries)
        {
            if (entries == null || entries.Count < 2)
            {
                return;
            }

            entries.Sort(MobStatusPresentationEntryComparer.Instance);
        }

        /// <summary>
        /// Converts the existing bitmask/status-stack representation into the
        /// presentation contract without changing ownership of either value.
        /// Only existing production status identities are read here; future
        /// Axiom identities remain dormant until a gameplay owner supplies them.
        /// </summary>
        public static void PopulateLegacyEntries(
            int statusMask,
            int imaginaryStacks,
            List<MobStatusPresentationEntry> entries)
        {
            entries.Clear();
            for (int index = 0; index <= (int)MobStatusIconKind.Stoneglass; index++)
            {
                if ((statusMask & (1 << index)) == 0)
                {
                    continue;
                }

                MobStatusIconKind kind = (MobStatusIconKind)index;
                entries.Add(new MobStatusPresentationEntry(
                    kind,
                    kind == MobStatusIconKind.Imaginary ? imaginaryStacks : 0));
            }

            Sort(entries);
        }

        public static float ResolveArtworkExtent(float cellWidth, float cellHeight, float artworkPadding)
        {
            float smallestCellDimension = Mathf.Min(cellWidth, cellHeight);
            return Mathf.Max(0.0001f, smallestCellDimension - 2f * Mathf.Max(0f, artworkPadding));
        }

        public static float ResolveArtworkScale(
            float spriteBoundsWidth,
            float spriteBoundsHeight,
            float artworkExtent,
            float inheritedScale)
        {
            float spriteDimension = Mathf.Max(spriteBoundsWidth, spriteBoundsHeight);
            return artworkExtent / Mathf.Max(0.0001f, spriteDimension * Mathf.Max(0.0001f, inheritedScale));
        }

        public static float CellCenterX(int index, int cellCount, float cellWidth, float spacing)
        {
            float totalWidth = cellCount * cellWidth + Mathf.Max(0, cellCount - 1) * spacing;
            return -totalWidth * 0.5f + cellWidth * (index + 0.5f) + spacing * index;
        }

        public static bool IsAxiomPhenomenon(MobStatusIconKind kind)
        {
            switch (kind)
            {
                case MobStatusIconKind.AxiomHeat:
                case MobStatusIconKind.AxiomFlow:
                case MobStatusIconKind.AxiomMass:
                case MobStatusIconKind.AxiomCompression:
                case MobStatusIconKind.AxiomPotential:
                case MobStatusIconKind.AxiomResonance:
                case MobStatusIconKind.AxiomPhase:
                case MobStatusIconKind.AxiomOrder:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Heat and the legacy Burn cell communicate the same fire condition in
        /// the world-status row. Keep the richer Axiom cell when it is active.
        /// </summary>
        public static bool ShouldSuppressLegacyBurn(bool hasAxiomHeat)
        {
            return hasAxiomHeat;
        }

        private static int SortKey(MobStatusIconKind kind)
        {
            switch (kind)
            {
                case MobStatusIconKind.AxiomHeat: return 0;
                case MobStatusIconKind.AxiomFlow: return 1;
                case MobStatusIconKind.AxiomMass: return 2;
                case MobStatusIconKind.AxiomCompression: return 3;
                case MobStatusIconKind.AxiomPotential: return 4;
                case MobStatusIconKind.AxiomResonance: return 5;
                case MobStatusIconKind.AxiomPhase: return 6;
                case MobStatusIconKind.AxiomOrder: return 7;
                default: return LegacyStatusOrderOffset + (int)kind;
            }
        }

        private sealed class MobStatusPresentationEntryComparer : IComparer<MobStatusPresentationEntry>
        {
            public static readonly MobStatusPresentationEntryComparer Instance = new MobStatusPresentationEntryComparer();

            public int Compare(MobStatusPresentationEntry left, MobStatusPresentationEntry right)
            {
                return MobStatusPresentationLayout.Compare(left.Kind, right.Kind);
            }
        }
    }

    /// <summary>One status datum for the presentation layer; stack count is optional.</summary>
    public struct MobStatusPresentationEntry
    {
        public MobStatusPresentationEntry(MobStatusIconKind kind, int stackCount)
        {
            Kind = kind;
            StackCount = Mathf.Max(0, stackCount);
        }

        public MobStatusIconKind Kind { get; }
        public int StackCount { get; }
    }
}
