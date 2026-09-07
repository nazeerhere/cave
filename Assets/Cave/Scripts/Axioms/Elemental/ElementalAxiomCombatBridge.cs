using Cave.Combat;
using Cave.Axioms.Control;
using Cave.Axioms.Mastery;
using Cave.Player;
using UnityEngine;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// The sole elemental Axiom combat boundary. Calls occur only after an existing
    /// attack path has resolved positive direct damage.
    /// </summary>
    public static class ElementalAxiomCombatBridge
    {
        public static void TryApplyProjectileHit(
            Damageable target,
            SpecialMode firedMode,
            int appliedDamage,
            bool isFrenzyBreak,
            DamageContext context,
            float timestamp,
            ElementalAxiomApplicationReceipt receipt)
        {
            AxiomKind kind;
            bool receiverIsTarget;
            float amount;
            if (!ElementalAxiomApplicationRules.TryResolveProjectile(
                firedMode,
                appliedDamage > 0,
                isFrenzyBreak,
                out kind,
                out receiverIsTarget,
                out amount))
            {
                return;
            }

            Apply(target, context.Source, kind, receiverIsTarget, amount, context, timestamp, receipt);
        }

        public static void TryApplyPlayerModeDirectHit(
            GameObject player,
            Damageable target,
            int appliedDamage,
            bool isFrenzyBreak,
            DamageContext context,
            float timestamp,
            ElementalAxiomApplicationReceipt receipt)
        {
            if (player == null)
            {
                return;
            }

            PlayerSpecialMode mode = player.GetComponent<PlayerSpecialMode>();
            SpecialMode activeMode = mode != null ? mode.CurrentMode : default(SpecialMode);
            AxiomKind kind;
            bool receiverIsTarget;
            float amount;
            if (!ElementalAxiomApplicationRules.TryResolveDirectPlayerHit(
                activeMode,
                appliedDamage > 0,
                isFrenzyBreak,
                out kind,
                out receiverIsTarget,
                out amount))
            {
                return;
            }

            Apply(target, player, kind, receiverIsTarget, amount, context, timestamp, receipt);
        }

        private static void Apply(
            Damageable target,
            GameObject source,
            AxiomKind kind,
            bool receiverIsTarget,
            float amount,
            DamageContext context,
            float timestamp,
            ElementalAxiomApplicationReceipt receipt)
        {
            if (target == null || amount <= 0f)
            {
                return;
            }

            // Flow and Mass are player-owned. A projectile without a valid player
            // source cannot silently assign either effect to an arbitrary target.
            GameObject receiver = receiverIsTarget ? target.gameObject : source;
            if (receiver == null)
            {
                return;
            }

            if (receipt != null && !receipt.TryClaim(target.gameObject.GetInstanceID(), kind))
            {
                return;
            }

            AxiomRuntimeState runtime = receiver.GetComponent<AxiomRuntimeState>();
            if (runtime == null)
            {
                runtime = receiver.AddComponent<AxiomRuntimeState>();
            }

            AxiomDynamicsState dynamics = AxiomDynamicsState.EnsureOn(receiver);
            AxiomControlState control = AxiomControlState.EnsureOn(receiver);
            control.ResolveExpired(runtime, timestamp);
            // A new positive application is only a correction when it opposes an
            // opportunity's signed error. It never supplies a stack by itself.
            AxiomControlOpportunity opportunity;
            if (control.TryGetActiveOpportunity(kind, timestamp, out opportunity))
            {
                control.TryIntervene(
                    runtime,
                    dynamics,
                    kind,
                    amount,
                    1f,
                    timestamp,
                    source,
                    target.gameObject);
            }

            runtime.ApplyDelta(kind, amount, source, target.gameObject, timestamp);
            AxiomTrajectoryState trajectory;
            if (!runtime.TryGetTrajectory(kind, out trajectory))
            {
                return;
            }

            dynamics.ApplySuccessfulInput(
                kind,
                trajectory.CurrentValue,
                amount,
                timestamp);
            control.EvaluateAndRefresh(runtime, kind, timestamp, source, target.gameObject);
            MasteryDomain domain;
            if (source != null && AxiomMasteryState.TryDomain(kind, out domain))
            {
                AxiomMasteryState.EnsureOn(source).Record(new MasteryEvidence(domain, MasteryEvidenceKind.OrdinaryUse, 1f, amount, timestamp));
            }
        }
    }
}
