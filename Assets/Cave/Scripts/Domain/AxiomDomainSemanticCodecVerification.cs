using System;
using System.Collections.Generic;
using Cave.Axioms;
using Cave.Axioms.Elemental;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Focused deterministic coverage for the authoritative Axiom-to-Domain codec boundary.</summary>
    public static class AxiomDomainSemanticCodecVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyAllTypedMappings(out failure)
                && VerifySupportMatrix(out failure)
                && VerifySupportedRoundTripAndPreservedDynamics(LawPhenomenon.Heat, 1f, out failure)
                && VerifySupportedRoundTripAndPreservedDynamics(LawPhenomenon.Flow, 2f, out failure)
                && VerifySupportedRoundTripAndPreservedDynamics(LawPhenomenon.Compression, 1f, out failure)
                && VerifySupportedRoundTripAndPreservedDynamics(LawPhenomenon.Potential, 1f, out failure)
                && VerifyMassMultiplierAndBaseline(out failure)
                && VerifySupportedRoundTripAndPreservedDynamics(LawPhenomenon.Order, -1f, out failure)
                && VerifySupportedRoundTripAndPreservedDynamics(LawPhenomenon.Resonance, 1f, out failure)
                && VerifySupportedRoundTripAndPreservedDynamics(LawPhenomenon.Phase, 1f, out failure)
                && VerifyStaleSemanticSnapshot(out failure)
                && VerifyPatternContextsFailClosedAndStale(out failure);
        }

        private static bool VerifyAllTypedMappings(out string failure)
        {
            LawPhenomenon[] phenomena =
            {
                LawPhenomenon.Heat, LawPhenomenon.Flow, LawPhenomenon.Mass, LawPhenomenon.Compression,
                LawPhenomenon.Potential, LawPhenomenon.Resonance, LawPhenomenon.Phase, LawPhenomenon.Order
            };
            for (int index = 0; index < phenomena.Length; index++)
            {
                AxiomKind kind;
                LawPhenomenon roundTrip;
                if (!AxiomDomainSemanticCodec.TryMapPhenomenon(phenomena[index], out kind)
                    || !AxiomDomainSemanticCodec.TryMapKind(kind, out roundTrip)
                    || roundTrip != phenomena[index])
                {
                    failure = "Axiom/Domain typed mapping was incomplete or non-bijective for " + phenomena[index] + ".";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static bool VerifySupportMatrix(out string failure)
        {
            bool valid = AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Heat) == AxiomDomainSemanticCapability.ReadWrite
                && AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Flow) == AxiomDomainSemanticCapability.ReadWrite
                && AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Compression) == AxiomDomainSemanticCapability.ReadWrite
                && AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Potential) == AxiomDomainSemanticCapability.ReadWrite
                && AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Mass) == AxiomDomainSemanticCapability.ReadWrite
                && AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Order) == AxiomDomainSemanticCapability.ReadWrite
                && AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Resonance) == AxiomDomainSemanticCapability.ReadWrite
                && AxiomDomainSemanticCodec.GetCapability(LawPhenomenon.Phase) == AxiomDomainSemanticCapability.ReadWrite;
            failure = valid ? null : "Codec support matrix exposed an unproven semantic mapping.";
            return valid;
        }

        private static bool VerifySupportedRoundTripAndPreservedDynamics(
            LawPhenomenon phenomenon, float finalValue, out string failure)
        {
            GameObject actor = new GameObject("AxiomDomainSemanticCodecVerification");
            try
            {
                AxiomRuntimeState runtime = actor.AddComponent<AxiomRuntimeState>();
                AxiomKind kind;
                if (!AxiomDomainSemanticCodec.TryMapPhenomenon(phenomenon, out kind))
                {
                    failure = "Supported phenomenon did not map to an Axiom kind.";
                    return false;
                }

                if (phenomenon == LawPhenomenon.Resonance || phenomenon == LawPhenomenon.Phase)
                    AxiomPatternContextState.EnsureOn(actor).Establish(kind, "verification:" + phenomenon);

                runtime.ApplyDelta(kind, 1f, actor, actor, 1f);
                AxiomTrajectoryState beforeTrajectory;
                PhenomenonSemanticSnapshot before;
                AxiomDomainSemanticCodecRejection projectionRejection = AxiomDomainSemanticCodecRejection.None;
                if (!runtime.TryGetTrajectory(kind, 1.15f, out beforeTrajectory)
                    || !AxiomDomainSemanticCodec.TryProject(runtime, phenomenon, 1.15f, out before, out projectionRejection))
                {
                    failure = "Supported projection failed for " + phenomenon + ": " + projectionRejection + ".";
                    return false;
                }

                float roundTripState;
                AxiomDomainSemanticCodecRejection inverseRejection;
                if (!AxiomDomainSemanticCodec.TryResolveCommittedState(runtime, phenomenon, before.SemanticValue,
                        out roundTripState, out inverseRejection)
                    || !Near(roundTripState, beforeTrajectory.CurrentValue))
                {
                    failure = "Supported projection/inverse did not round-trip " + phenomenon + ".";
                    return false;
                }

                PhenomenonSemanticSnapshot final;
                PhenomenonSemanticResult semantic;
                if (!PhenomenonSemanticClassifier.TryCreateSnapshot(phenomenon, finalValue, out final, out semantic))
                {
                    failure = "Focused verifier could not build a valid final semantic value.";
                    return false;
                }

                AxiomRuntimeDomainTarget target = new AxiomRuntimeDomainTarget(runtime, phenomenon, 1.15f);
                Accessor accessor = new Accessor(new PhenomenonCarrierId("codec-" + phenomenon), phenomenon, target);
                DomainCommitEntry entry = new DomainCommitEntry(
                    new PhenomenonCarrierId("codec-" + phenomenon), phenomenon, before, final,
                    Read(new DomainTransitionSequenceId(1)));
                DomainCommitPlan plan = new DomainCommitPlan(null, Read(entry), DomainCommitPlanRejectionReason.None);
                DomainCommitResult result = new DomainRuntimeCommitter().Commit(plan, accessor);
                AxiomTrajectoryState afterTrajectory;
                bool valid = result.Succeeded
                    && runtime.TryGetTrajectory(kind, 1.15f, out afterTrajectory)
                    && Near(afterTrajectory.CurrentValue, finalValue)
                    && Near(afterTrajectory.ShortRate, beforeTrajectory.ShortRate)
                    && Near(afterTrajectory.ShortAcceleration, beforeTrajectory.ShortAcceleration);
                failure = valid ? null : "Domain codec commit did not preserve S/R/A semantics for " + phenomenon + ".";
                return valid;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static bool VerifyStaleSemanticSnapshot(out string failure)
        {
            GameObject actor = new GameObject("AxiomDomainSemanticCodecStaleVerification");
            try
            {
                AxiomRuntimeState runtime = actor.AddComponent<AxiomRuntimeState>();
                runtime.ApplyDelta(AxiomKind.Heat, 1f, actor, actor, 1f);
                PhenomenonSemanticSnapshot before;
                AxiomDomainSemanticCodecRejection rejected;
                if (!AxiomDomainSemanticCodec.TryProject(runtime, LawPhenomenon.Heat, 1.1f, out before, out rejected))
                {
                    failure = "Could not construct stale-check snapshot.";
                    return false;
                }

                runtime.ApplyDelta(AxiomKind.Heat, 1f, actor, actor, 1.2f);
                PhenomenonSemanticSnapshot final;
                PhenomenonSemanticResult semantic;
                PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Heat, 1f, out final, out semantic);
                PhenomenonCarrierId carrier = new PhenomenonCarrierId("stale-codec");
                DomainCommitEntry entry = new DomainCommitEntry(carrier, LawPhenomenon.Heat, before, final,
                    Read(new DomainTransitionSequenceId(1)));
                DomainCommitPlan plan = new DomainCommitPlan(null, Read(entry), DomainCommitPlanRejectionReason.None);
                DomainCommitResult result = new DomainRuntimeCommitter().Commit(plan,
                    new Accessor(carrier, LawPhenomenon.Heat, new AxiomRuntimeDomainTarget(runtime, LawPhenomenon.Heat, 1.2f)));
                bool valid = !result.Succeeded && result.RejectionReason == DomainCommitRejectionReason.RuntimeStateStale;
                failure = valid ? null : "Domain committer did not stale-check the canonical semantic projection.";
                return valid;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static bool VerifyMassMultiplierAndBaseline(out string failure)
        {
            GameObject actor = new GameObject("AxiomDomainSemanticCodecMassVerification");
            try
            {
                Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
                body.mass = 2f;
                AxiomRuntimeState runtime = actor.AddComponent<AxiomRuntimeState>();
                AxiomMassStateModel mass = AxiomMassStateModel.EnsureOn(actor);
                AxiomTrajectoryState beforeTrajectory;
                PhenomenonSemanticSnapshot before;
                AxiomDomainSemanticCodecRejection projectionRejection;
                if (mass == null || !mass.HasValidNaturalMassBaseline || !Near(mass.NaturalMassBaseline, 2f)
                    || !runtime.TryGetTrajectory(AxiomKind.Mass, 1f, out beforeTrajectory)
                    || !Near(beforeTrajectory.CurrentValue, 1f)
                    || !AxiomDomainSemanticCodec.TryProject(runtime, LawPhenomenon.Mass, 1f, out before, out projectionRejection)
                    || !Near(before.SemanticValue, 1f))
                {
                    failure = "Mass did not expose a stable natural baseline and multiplier-neutral S.";
                    return false;
                }

                PhenomenonSemanticSnapshot final;
                PhenomenonSemanticResult semantic;
                PhenomenonSemanticClassifier.TryCreateSnapshot(LawPhenomenon.Mass, 1.5f, out final, out semantic);
                PhenomenonCarrierId carrier = new PhenomenonCarrierId("codec-Mass");
                DomainCommitEntry entry = new DomainCommitEntry(carrier, LawPhenomenon.Mass, before, final,
                    Read(new DomainTransitionSequenceId(1)));
                DomainCommitResult result = new DomainRuntimeCommitter().Commit(
                    new DomainCommitPlan(null, Read(entry), DomainCommitPlanRejectionReason.None),
                    new Accessor(carrier, LawPhenomenon.Mass, new AxiomRuntimeDomainTarget(runtime, LawPhenomenon.Mass, 1f)));
                AxiomTrajectoryState afterTrajectory;
                bool valid = result.Succeeded
                    && runtime.TryGetTrajectory(AxiomKind.Mass, 1f, out afterTrajectory)
                    && Near(afterTrajectory.CurrentValue, 1.5f)
                    && Near(afterTrajectory.ShortRate, beforeTrajectory.ShortRate)
                    && Near(afterTrajectory.ShortAcceleration, beforeTrajectory.ShortAcceleration)
                    && Near(mass.NaturalMassBaseline, 2f)
                    && Near(mass.EffectiveMass(afterTrajectory.CurrentValue), 3f);
                failure = valid ? null : "Mass commit did not preserve multiplier semantics, baseline, or S/R/A.";
                return valid;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static bool VerifyPatternContextsFailClosedAndStale(out string failure)
        {
            GameObject actor = new GameObject("AxiomDomainSemanticCodecPatternVerification");
            try
            {
                AxiomRuntimeState runtime = actor.AddComponent<AxiomRuntimeState>();
                PhenomenonSemanticSnapshot absent;
                AxiomDomainSemanticCodecRejection reason;
                float state;
                if (AxiomDomainSemanticCodec.TryProject(runtime, LawPhenomenon.Resonance, 1f, out absent, out reason)
                    || reason != AxiomDomainSemanticCodecRejection.MissingPatternContext
                    || AxiomDomainSemanticCodec.TryResolveCommittedState(runtime, LawPhenomenon.Phase, 1f, out state, out reason)
                    || reason != AxiomDomainSemanticCodecRejection.MissingPatternContext)
                {
                    failure = "Pattern phenomena did not fail closed without a qualifying context.";
                    return false;
                }

                AxiomPatternContextState context = AxiomPatternContextState.EnsureOn(actor);
                context.Establish(AxiomKind.Resonance, "pattern-A");
                runtime.TrySetPatternStrength(AxiomKind.Resonance, 2f, 1f);
                PhenomenonSemanticSnapshot before;
                if (!AxiomDomainSemanticCodec.TryProject(runtime, LawPhenomenon.Resonance, 1f, out before, out reason))
                {
                    failure = "Could not project a qualified Resonance pattern.";
                    return false;
                }
                context.Establish(AxiomKind.Resonance, "pattern-B");
                PhenomenonSemanticSnapshot after;
                bool stale = AxiomDomainSemanticCodec.TryProject(runtime, LawPhenomenon.Resonance, 1f, out after, out reason)
                    && !DomainCommitSnapshot.Equal(before, after);
                failure = stale ? null : "Changing a Pattern identity did not invalidate the old semantic snapshot.";
                return stale;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static bool Near(float left, float right) { return Math.Abs(left - right) <= Tolerance; }
        private static IReadOnlyList<T> Read<T>(params T[] values) { return new List<T>(values).AsReadOnly(); }

        private sealed class Accessor : IDomainPhenomenonRuntimeAccessor
        {
            private readonly PhenomenonCarrierId id;
            private readonly LawPhenomenon phenomenon;
            private readonly IDomainPhenomenonRuntimeTarget target;

            public Accessor(PhenomenonCarrierId id, LawPhenomenon phenomenon, IDomainPhenomenonRuntimeTarget target)
            { this.id = id; this.phenomenon = phenomenon; this.target = target; }

            public bool TryResolve(PhenomenonCarrierId carrierId, LawPhenomenon requested,
                out IDomainPhenomenonRuntimeTarget resolved)
            {
                resolved = carrierId.Equals(id) && requested == phenomenon ? target : null;
                return resolved != null;
            }
        }
    }
}
