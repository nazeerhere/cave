using System;
using Cave.Axioms;
using Cave.Axioms.Frequency;
using Cave.Axioms.Phase;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for Domain semantic regions.</summary>
    public static class PhenomenonSemanticsVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyRegionVocabulary(out failure)
                && VerifySignedRegions(out failure)
                && VerifyMagnitudeRegions(out failure)
                && VerifyMassRegions(out failure)
                && VerifyOutOfRangePolicy(out failure)
                && VerifyInvalidInputs(out failure)
                && VerifyAdaptersAreReadOnly(out failure)
                && VerifySpecializedPhaseRemainsIndependent(out failure)
                && VerifySpecializedResonanceRemainsIndependent(out failure);
        }

        private static bool VerifyRegionVocabulary(out string failure)
        {
            string[] names = Enum.GetNames(typeof(PhenomenonSemanticRegion));
            return Expect(names.Length == 3
                && Contains(names, "Low")
                && Contains(names, "Regular")
                && Contains(names, "High"),
                "Semantic region vocabulary did not contain exactly Low, Regular, and High.", out failure);
        }

        private static bool VerifySignedRegions(out string failure)
        {
            LawPhenomenon[] signed =
            {
                LawPhenomenon.Heat,
                LawPhenomenon.Compression,
                LawPhenomenon.Potential,
                LawPhenomenon.Order
            };

            for (int index = 0; index < signed.Length; index++)
            {
                LawPhenomenon phenomenon = signed[index];
                if (!HasRegion(phenomenon, -4f, PhenomenonSemanticRegion.Low)
                    || !HasRegion(phenomenon, -2f, PhenomenonSemanticRegion.Low)
                    || !HasRegion(phenomenon, -1f, PhenomenonSemanticRegion.Regular)
                    || !HasRegion(phenomenon, 0f, PhenomenonSemanticRegion.Regular)
                    || !HasRegion(phenomenon, 1f, PhenomenonSemanticRegion.Regular)
                    || !HasRegion(phenomenon, 2f, PhenomenonSemanticRegion.High)
                    || !HasRegion(phenomenon, 4f, PhenomenonSemanticRegion.High))
                {
                    failure = phenomenon + " did not preserve the signed starter regions.";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static bool VerifyMagnitudeRegions(out string failure)
        {
            bool flow = HasRegion(LawPhenomenon.Flow, 0f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Flow, 1f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Flow, 2f, PhenomenonSemanticRegion.Regular)
                && HasRegion(LawPhenomenon.Flow, 3f, PhenomenonSemanticRegion.High)
                && HasRegion(LawPhenomenon.Flow, 4f, PhenomenonSemanticRegion.High);
            bool resonance = HasRegion(LawPhenomenon.Resonance, 0f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Resonance, 1f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Resonance, 2f, PhenomenonSemanticRegion.Regular)
                && HasRegion(LawPhenomenon.Resonance, 3f, PhenomenonSemanticRegion.High)
                && HasRegion(LawPhenomenon.Resonance, 4f, PhenomenonSemanticRegion.High);
            bool phase = HasRegion(LawPhenomenon.Phase, 0f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Phase, 1f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Phase, 2f, PhenomenonSemanticRegion.Regular)
                && HasRegion(LawPhenomenon.Phase, 3f, PhenomenonSemanticRegion.Regular)
                && HasRegion(LawPhenomenon.Phase, 4f, PhenomenonSemanticRegion.High)
                && HasRegion(LawPhenomenon.Phase, 5f, PhenomenonSemanticRegion.High);
            return Expect(flow && resonance && phase,
                "Flow, Resonance, or Phase starter magnitude regions were incorrect.", out failure);
        }

        private static bool VerifyMassRegions(out string failure)
        {
            PhenomenonSemanticSnapshot low;
            PhenomenonSemanticResult lowResult;
            PhenomenonSemanticSnapshot regular;
            PhenomenonSemanticResult regularResult;
            PhenomenonSemanticSnapshot high;
            PhenomenonSemanticResult highResult;
            bool lowCreated = PhenomenonSemanticAdapters.TryFromMassRelativeToBaseline(.75f, 1f, out low, out lowResult);
            bool regularCreated = PhenomenonSemanticAdapters.TryFromMassRelativeToBaseline(1f, 1f, out regular, out regularResult);
            bool highCreated = PhenomenonSemanticAdapters.TryFromMassRelativeToBaseline(1.25f, 1f, out high, out highResult);
            return Expect(lowCreated && lowResult.IsValid && low.Region == PhenomenonSemanticRegion.Low
                && regularCreated && regularResult.IsValid && regular.Region == PhenomenonSemanticRegion.Regular
                && highCreated && highResult.IsValid && high.Region == PhenomenonSemanticRegion.High,
                "Mass baseline-ratio regions were incorrect.", out failure);
        }

        private static bool VerifyOutOfRangePolicy(out string failure)
        {
            return Expect(HasRegion(LawPhenomenon.Heat, -10f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Heat, 10f, PhenomenonSemanticRegion.High)
                && HasRegion(LawPhenomenon.Mass, .1f, PhenomenonSemanticRegion.Low)
                && HasRegion(LawPhenomenon.Mass, 2f, PhenomenonSemanticRegion.High),
                "Out-of-range semantic values did not remain in their outer region.", out failure);
        }

        private static bool VerifyInvalidInputs(out string failure)
        {
            PhenomenonSemanticSnapshot invalidPhenomenon;
            PhenomenonSemanticResult invalidPhenomenonResult;
            bool invalidPhenomenonCreated = PhenomenonSemanticClassifier.TryCreateSnapshot(
                (LawPhenomenon)999,
                0f,
                out invalidPhenomenon,
                out invalidPhenomenonResult);
            PhenomenonSemanticSnapshot invalidValue;
            PhenomenonSemanticResult invalidValueResult;
            bool invalidValueCreated = PhenomenonSemanticClassifier.TryCreateSnapshot(
                LawPhenomenon.Heat,
                float.NaN,
                out invalidValue,
                out invalidValueResult);
            PhenomenonSemanticSnapshot invalidMass;
            PhenomenonSemanticResult invalidMassResult;
            bool invalidMassCreated = PhenomenonSemanticAdapters.TryFromMassRelativeToBaseline(
                1f,
                0f,
                out invalidMass,
                out invalidMassResult);
            return Expect(!invalidPhenomenonCreated
                && invalidPhenomenon == null
                && invalidPhenomenonResult.RejectionReason == PhenomenonSemanticRejectionReason.InvalidPhenomenon
                && !invalidValueCreated
                && invalidValue == null
                && invalidValueResult.RejectionReason == PhenomenonSemanticRejectionReason.InvalidSemanticValue
                && !invalidMassCreated
                && invalidMass == null
                && invalidMassResult.RejectionReason == PhenomenonSemanticRejectionReason.InvalidNaturalMassBaseline,
                "Invalid phenomenon, value, or Mass baseline did not fail deterministically.", out failure);
        }

        private static bool VerifyAdaptersAreReadOnly(out string failure)
        {
            AxiomTrajectoryState source = new AxiomTrajectoryState(
                AxiomKind.Heat, 2f, 3f, 4f, 5f, true, true, true,
                AxiomTrajectoryDirection.Rising, AxiomRateIntensity.Fast,
                AxiomCurvature.Accelerating, 6f);
            float valueBefore = source.CurrentValue;
            AxiomTrajectoryDirection directionBefore = source.Direction;
            PhenomenonSemanticSnapshot snapshot;
            PhenomenonSemanticResult result;
            bool created = PhenomenonSemanticAdapters.TryFromAxiomTrajectory(source, out snapshot, out result);
            return Expect(created
                && result.IsValid
                && snapshot.Region == PhenomenonSemanticRegion.High
                && source.CurrentValue == valueBefore
                && source.Direction == directionBefore
                && typeof(PhenomenonSemanticRegion) != typeof(AxiomRateIntensity),
                "Semantic classification mutated trajectory data or reused a rate-label type.", out failure);
        }

        private static bool VerifySpecializedPhaseRemainsIndependent(out string failure)
        {
            PhenomenonSemanticSnapshot snapshot;
            PhenomenonSemanticResult result;
            bool adapted = PhenomenonSemanticAdapters.TryFromPhaseLatentStacks(4, out snapshot, out result);
            PhaseExposureDefinition exposure = PhaseCombatRules.ResolveExposure(4, 1.35f, 1.18f, 2.5f, 4f, .6f);
            return Expect(adapted
                && result.IsValid
                && snapshot.Region == PhenomenonSemanticRegion.High
                && exposure.EffectClass == PhaseEffectClass.None,
                "Phase semantic region incorrectly replaced existing parity-based collapse behavior.", out failure);
        }

        private static bool VerifySpecializedResonanceRemainsIndependent(out string failure)
        {
            GuardResonanceSettings settings = new GuardResonanceSettings(.8f, .15f, 2.5f, 4);
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            tracker.RegisterContact(.8f, settings);
            tracker.RegisterContact(1.6f, settings);
            tracker.RegisterContact(2.4f, settings);
            GuardResonanceContactResult breakResult = tracker.RegisterContact(3.2f, settings);
            PhenomenonSemanticSnapshot snapshot;
            PhenomenonSemanticResult result;
            bool adapted = PhenomenonSemanticAdapters.TryFromResonanceProgress(
                breakResult.Progress,
                out snapshot,
                out result);
            return Expect(adapted
                && result.IsValid
                && snapshot.Region == PhenomenonSemanticRegion.High
                && breakResult.Broke,
                "Resonance semantic region incorrectly replaced the existing Break state.", out failure);
        }

        private static bool HasRegion(
            LawPhenomenon phenomenon,
            float value,
            PhenomenonSemanticRegion expected)
        {
            PhenomenonSemanticRegion actual;
            PhenomenonSemanticResult result;
            return PhenomenonSemanticClassifier.TryClassify(phenomenon, value, out actual, out result)
                && result.IsValid
                && actual == expected;
        }

        private static bool Contains(string[] values, string expected)
        {
            return Array.IndexOf(values, expected) >= 0;
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }
    }
}
