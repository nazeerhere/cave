using System.Collections.Generic;
using Cave.Combat;

namespace Cave.Axioms.Elemental
{
    /// <summary>Pure semantic rule set shared by projectile and direct-hit bridges.</summary>
    public static class ElementalAxiomApplicationRules
    {
        public static bool TryResolveProjectile(
            SpecialMode firedMode,
            bool appliedPositiveDamage,
            bool isFrenzyBreak,
            out AxiomKind kind,
            out bool receiverIsTarget,
            out float amount)
        {
            return TryResolve(firedMode, appliedPositiveDamage, isFrenzyBreak, out kind, out receiverIsTarget, out amount);
        }

        public static bool TryResolveDirectPlayerHit(
            SpecialMode activeMode,
            bool appliedPositiveDamage,
            bool isFrenzyBreak,
            out AxiomKind kind,
            out bool receiverIsTarget,
            out float amount)
        {
            kind = default(AxiomKind);
            receiverIsTarget = false;
            amount = 0f;
            if (!appliedPositiveDamage)
            {
                return false;
            }

            if (activeMode == SpecialMode.Flight)
            {
                kind = AxiomKind.Flow;
            }
            else if (activeMode == SpecialMode.DamageBoost)
            {
                kind = AxiomKind.Mass;
            }
            else
            {
                return false;
            }

            amount = isFrenzyBreak ? 2f : 1f;
            return true;
        }

        private static bool TryResolve(
            SpecialMode mode,
            bool appliedPositiveDamage,
            bool isFrenzyBreak,
            out AxiomKind kind,
            out bool receiverIsTarget,
            out float amount)
        {
            kind = default(AxiomKind);
            receiverIsTarget = false;
            amount = 0f;
            if (!appliedPositiveDamage)
            {
                return false;
            }

            switch (mode)
            {
                case SpecialMode.BurnShot:
                    kind = AxiomKind.Heat;
                    receiverIsTarget = true;
                    break;
                case SpecialMode.SlowShot:
                    kind = AxiomKind.Order;
                    receiverIsTarget = true;
                    break;
                case SpecialMode.Flight:
                    kind = AxiomKind.Flow;
                    break;
                case SpecialMode.DamageBoost:
                    kind = AxiomKind.Mass;
                    break;
                default:
                    return false;
            }

            // Frenzy Break is a replacement application amount: +2 total, never +3.
            amount = isFrenzyBreak ? 2f : 1f;
            return true;
        }
    }

    /// <summary>
    /// Per-action duplicate guard. Existing combat hit sets decide physical hit
    /// eligibility; this receipt protects the semantic bridge if a source reports
    /// the same target/kind twice before its action ends.
    /// </summary>
    public sealed class ElementalAxiomApplicationReceipt
    {
        private readonly HashSet<int> claimedKeys = new HashSet<int>();

        public bool TryClaim(int physicalTargetInstanceId, AxiomKind kind)
        {
            unchecked
            {
                return claimedKeys.Add((physicalTargetInstanceId * 397) ^ (int)kind);
            }
        }

        public void Clear()
        {
            claimedKeys.Clear();
        }
    }
}
