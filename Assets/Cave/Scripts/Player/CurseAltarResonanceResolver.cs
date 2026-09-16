using System;
using System.Collections.Generic;

namespace Cave.Player
{
    public enum CurseAltarResonanceTier
    {
        None,
        Pair,
        Triple
    }

    /// <summary>
    /// Stable gameplay identifiers for the deliberately authored two-curse
    /// interactions.  Triple snapshots intentionally resolve to None here:
    /// their future rules must be authored separately rather than inheriting
    /// any pair behaviour.
    /// </summary>
    public enum CurseAltarPairResonance
    {
        None,
        Paranoia,
        BloodMoney,
        ShatteredMadness,
        Riot,
        Bounty,
        FaultAnalysis,
        Execution,
        BreakAndTake,
        Harvest,
        Momentum
    }

    /// <summary>
    /// Immutable description of one altar's normalized curse snapshot.  The key
    /// is a bitmask, not a display string, so ordering can never produce a
    /// different resonance result.
    /// </summary>
    public readonly struct CurseAltarResonance
    {
        internal CurseAltarResonance(
            int combinationKey,
            int curseCount,
            CurseAltarResonanceTier tier,
            string debugKey,
            string resonanceId,
            CurseAltarPairResonance pairResonance)
        {
            CombinationKey = combinationKey;
            CurseCount = curseCount;
            Tier = tier;
            DebugKey = debugKey;
            ResonanceId = resonanceId;
            PairResonance = pairResonance;
        }

        public int CombinationKey { get; }
        public int CurseCount { get; }
        public CurseAltarResonanceTier Tier { get; }
        public string DebugKey { get; }
        public string ResonanceId { get; }
        public CurseAltarPairResonance PairResonance { get; }

        public bool IsImplemented => Tier == CurseAltarResonanceTier.Pair
            && PairResonance != CurseAltarPairResonance.None;
    }

    /// <summary>
    /// Single authority for future altar pair/triple resonance lookup.  Base
    /// effects are intentionally not represented here; they remain independent
    /// and always stack from the snapshot in <see cref="CurseAltarZone"/>.
    /// </summary>
    public static class CurseAltarResonanceResolver
    {
        private const int MaximumSupportedCurses = 3;

        public static CurseAltarResonance Resolve(IReadOnlyList<PlayerCurseType> curses)
        {
            int combinationKey = 0;
            int count = 0;
            for (int index = 0; curses != null && index < curses.Count; index++)
            {
                int flag = 1 << (int)curses[index];
                if ((combinationKey & flag) != 0)
                {
                    continue;
                }

                combinationKey |= flag;
                count++;
                if (count == MaximumSupportedCurses)
                {
                    break;
                }
            }

            CurseAltarResonanceTier tier = count == 2
                ? CurseAltarResonanceTier.Pair
                : count == 3
                    ? CurseAltarResonanceTier.Triple
                    : CurseAltarResonanceTier.None;
            string debugKey = BuildDebugKey(combinationKey);
            CurseAltarPairResonance pairResonance = tier == CurseAltarResonanceTier.Pair
                ? ResolvePair(combinationKey)
                : CurseAltarPairResonance.None;
            string resonanceId = pairResonance != CurseAltarPairResonance.None
                ? ResolvePairId(pairResonance)
                : tier == CurseAltarResonanceTier.None
                    ? string.Empty
                    : "triple." + debugKey.ToLowerInvariant().Replace("|", ".");

            return new CurseAltarResonance(
                combinationKey,
                count,
                tier,
                debugKey,
                resonanceId,
                pairResonance);
        }

        private static CurseAltarPairResonance ResolvePair(int key)
        {
            switch (key)
            {
                case (1 << (int)PlayerCurseType.Insanity) | (1 << (int)PlayerCurseType.Detective):
                    return CurseAltarPairResonance.Paranoia;
                case (1 << (int)PlayerCurseType.Insanity) | (1 << (int)PlayerCurseType.Avarice):
                    return CurseAltarPairResonance.BloodMoney;
                case (1 << (int)PlayerCurseType.Insanity) | (1 << (int)PlayerCurseType.Stoneglass):
                    return CurseAltarPairResonance.ShatteredMadness;
                case (1 << (int)PlayerCurseType.Insanity) | (1 << (int)PlayerCurseType.CavesGlare):
                    return CurseAltarPairResonance.Riot;
                case (1 << (int)PlayerCurseType.Detective) | (1 << (int)PlayerCurseType.Avarice):
                    return CurseAltarPairResonance.Bounty;
                case (1 << (int)PlayerCurseType.Detective) | (1 << (int)PlayerCurseType.Stoneglass):
                    return CurseAltarPairResonance.FaultAnalysis;
                case (1 << (int)PlayerCurseType.Detective) | (1 << (int)PlayerCurseType.CavesGlare):
                    return CurseAltarPairResonance.Execution;
                case (1 << (int)PlayerCurseType.Avarice) | (1 << (int)PlayerCurseType.Stoneglass):
                    return CurseAltarPairResonance.BreakAndTake;
                case (1 << (int)PlayerCurseType.Avarice) | (1 << (int)PlayerCurseType.CavesGlare):
                    return CurseAltarPairResonance.Harvest;
                case (1 << (int)PlayerCurseType.Stoneglass) | (1 << (int)PlayerCurseType.CavesGlare):
                    return CurseAltarPairResonance.Momentum;
                default:
                    return CurseAltarPairResonance.None;
            }
        }

        private static string ResolvePairId(CurseAltarPairResonance pair)
        {
            switch (pair)
            {
                case CurseAltarPairResonance.Paranoia: return "pair.insanity.detective";
                case CurseAltarPairResonance.BloodMoney: return "pair.insanity.avarice";
                case CurseAltarPairResonance.ShatteredMadness: return "pair.insanity.stoneglass";
                case CurseAltarPairResonance.Riot: return "pair.insanity.cavesglare";
                case CurseAltarPairResonance.Bounty: return "pair.detective.avarice";
                case CurseAltarPairResonance.FaultAnalysis: return "pair.detective.stoneglass";
                case CurseAltarPairResonance.Execution: return "pair.detective.cavesglare";
                case CurseAltarPairResonance.BreakAndTake: return "pair.avarice.stoneglass";
                case CurseAltarPairResonance.Harvest: return "pair.avarice.cavesglare";
                case CurseAltarPairResonance.Momentum: return "pair.stoneglass.cavesglare";
                default: return string.Empty;
            }
        }

        private static string BuildDebugKey(int combinationKey)
        {
            List<string> names = new List<string>(MaximumSupportedCurses);
            foreach (PlayerCurseType curse in Enum.GetValues(typeof(PlayerCurseType)))
            {
                if ((combinationKey & (1 << (int)curse)) != 0)
                {
                    names.Add(curse.ToString());
                }
            }

            return string.Join("|", names);
        }
    }
}
